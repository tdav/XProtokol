# XPacketRpc — Design Spec

**Date:** 2026-05-08
**Status:** Approved (brainstorming complete, awaiting implementation plan)
**Authors:** Иван (user) + Claude
**Source repo:** `C:\Works_Test\XProtokol`

---

## 1. Цель и контекст

### 1.1. Что делаем

Создаём `XPacketRpc` — бинарный sourcegen-сериализатор для RPC-сообщений, идейно наследующий
проект `XProtocol`, но реализованный clean-room. Назначение: использование в RabbitMQ-RPC-pipeline
(перед `BasicPublish` / после `BasicDeliverEventArgs.Body`) с минимальным runtime-overhead'ом.

### 1.2. Входной контракт (фиксирован пользователем)

```csharp
public interface IRpcSerializer
{
    string ContentType { get; }
    byte[] Serialize<T>(T value);
    T? Deserialize<T>(ReadOnlyMemory<byte> payload);
}
```

### 1.3. Зачем не использовать существующий `XProtocol`

Текущий `XProtocol`:
- Поддерживает только value-типы (`Marshal.StructureToPtr`).
- Лимит 255 байт на поле и 255 полей в кадре.
- Host-endian (зависит от архитектуры).
- Reflection-based в hot-path.
- Возвращает `XPacket`, не `byte[]`.

Этого недостаточно для RPC-DTO с строками, коллекциями, nested-объектами, record'ами.

### 1.4. Сводка ключевых решений (из brainstorming)

| # | Решение | Описание |
|---|---|---|
| Q1 | B+C+D | Эволюция wire + новый формат + sourcegen без атрибутов |
| Q2 | D | Полный набор типов: primitives + collections + nested + dictionaries |
| Q3 | C | Sourcegen через анализ call-sites; без marker-интерфейсов и атрибутов |
| Q4 | D | Schema-driven, без tag-bytes, чистая последовательность значений + nullability bitmap |
| Q5 | C+D | Properties + fields + ctor-binding для record/immutable |
| Q6 | A | Bare payload, без header/trailer/magic |
| Q7 | C+cache | Hash-stable порядок (FNV-1a), cached `FieldOrder[]` per DTO |
| Q8 | B | Отдельный clean-room проект, без зависимости от `XProtocol` |
| Q9 | A | Fail-fast при отсутствии generated-кода (`MissingSerializerException`) |
| Q10 | B | `IRpcSerializer` facade + low-level static `XPRpc.Write/Read` |
| Q11 | E | Бенчмарки vs MessagePack + MemoryPack + System.Text.Json + protobuf-net + Bond |
| Q12 | — | Имя `XPacketRpc`, ContentType `application/x-xprotocol-rpc`, LE, AOT — best-effort |
| Q13 | C | 8 DTO для бенчмарков |

---

## 2. Архитектура

### 2.1. Принципы

- **Schema-driven** wire-формат (без tag-bytes). Generator знает layout — payload минимален.
- **Source-generated Read/Write** на этапе компиляции. Без рефлексии в hot-path.
- **Fail-fast** при отсутствии generated-кода для T — `MissingSerializerException`.
- **Bare payload** — без magic/header/trailer. Тип DTO идентифицируется внешне (AMQP `type`-header).
- **Little-endian**, фиксированный.
- **Стабильный порядок полей** — FNV-1a от имени поля, tiebreak — ordinal compare.
- **Layered API:** низкоуровневый `XPRpc.Write/Read` (zero-alloc) + facade `XPacketRpcSerializer : IRpcSerializer`.

### 2.2. Высокоуровневые компоненты

1. **`XPacketRpc`** (runtime, net10.0):
   - `IRpcSerializer` — входящий контракт пользователя.
   - `XPacketRpcSerializer : IRpcSerializer` — facade, использует pooled buffer + копирует в `byte[]`.
   - `XPRpc` — статический низкоуровневый API.
   - `XPRpcReader` — `ref struct` для чтения из `ReadOnlySpan<byte>`.
   - `MissingSerializerException`, `RpcSerializationException`.
   - `Internal/Writers` — public-static helper'ы для write-side (вызываются из generated-кода).
   - `Internal/PooledBufferWriter` — `IBufferWriter<byte>` поверх `ArrayPool<byte>.Shared`.
   - `Internal/Fnv1a` — runtime-helper (для тестов и совместимости с generator'ом).

2. **`XPacketRpc.Generators`** (sourcegen, netstandard2.0):
   - `IIncrementalGenerator` (Roslyn 4.11+).
   - Сканирует call-sites: `XPRpc.Write<T>`, `XPRpc.Read<T>`, `XPRpc.Touch<T>`,
     `IRpcSerializer.Serialize<T>`, `IRpcSerializer.Deserialize<T>`.
   - Транзитивно обходит nested/element/key/value-типы.
   - Эмитит per-DTO `internal static class __XPRpcGen_<Type>` с `Write`/`Read`/`FieldOrder`.
   - Эмитит per-assembly `__XPRpcRegistry` с `[ModuleInitializer]`.

3. **`XPacketRpc.Benchmarks`** (executable, net10.0):
   - BenchmarkDotNet-runner.
   - 8 DTO × 6 serializer'ов (включая нас как baseline).

### 2.3. Поток данных

**Serialize:**

```
user → IRpcSerializer.Serialize<T>(value)
     → XPacketRpcSerializer
         → PooledBufferWriter (ArrayPool<byte>.Shared)
         → XPRpc.Write<T>(value, writer)
             → __XPRpcGen_<T>.Write(value, writer)        ← generated
                 → primitive-writers + nested-writers
         → buffer.WrittenSpan.ToArray()                    ← single allocation
         → return buffer to pool
     → byte[] caller
```

**Deserialize:**

```
user → IRpcSerializer.Deserialize<T>(payload)
     → XPacketRpcSerializer
         → XPRpc.Read<T>(payload.Span)
             → XPRpcReader r = new(span)
             → __XPRpcGen_<T>.Read(ref r)                  ← generated
                 → primitive-readers + nested-readers + ctor-binding/setter
         → return T?
```

---

## 3. Wire-формат

### 3.1. Базовые блоки

- **Endianness:** little-endian, фиксирован.
- **Varint:** LEB128 unsigned. До 5 байт для `uint32`. Используется для всех длин (string, byte[], collection, dictionary).
- **Nullability bitmap:** `ceil(N/8)` байт, где N — число nullable-полей в типе. Бит `i` = 1 → поле под индексом `i` (в hash-сортированном порядке) равно null. Если N = 0, bitmap отсутствует.

### 3.2. Layout DTO

```
[NullBitmap: ceil(NullableFieldCount/8) bytes]   ← omit if NullableFieldCount == 0
[Field[0] if not-null]
[Field[1] if not-null]
...
[Field[N-1] if not-null]
```

Порядок полей — hash-stable: `(Fnv1a32(name), name)` ascending.

Nullability определяется по annotation на этапе компиляции:

| Тип в коде | Wire-поведение |
|---|---|
| `string` | required (NRE при null → `RpcSerializationException`) |
| `string?` | в bitmap |
| `int` | required |
| `int?` (`Nullable<int>`) | в bitmap |
| `Foo` (reference) | required |
| `Foo?` (reference) | в bitmap |
| `List<T>?` | в bitmap |

### 3.3. Wire по типам

| Тип | Байты на проводе |
|---|---|
| `bool` | 1 байт (0/1) |
| `byte` / `sbyte` | 1 байт |
| `short` / `ushort` | 2 байта LE |
| `int` / `uint` | 4 байта LE |
| `long` / `ulong` | 8 байт LE |
| `float` | 4 байта LE (`BitConverter.SingleToInt32Bits`) |
| `double` | 8 байт LE |
| `decimal` | 16 байт (4 × int32 LE по `decimal.GetBits`) |
| `Guid` | 16 байт (`Guid.TryWriteBytes(span, bigEndian: false)`) |
| `DateTime` | 8 байт ticks (`Int64`) + 1 байт `DateTimeKind` |
| `DateTimeOffset` | 8 байт ticks + 2 байта offset-minutes (`Int16`) |
| `TimeSpan` | 8 байт ticks |
| `enum` | underlying type (LE) |
| `string` | varint(byteLength) + UTF-8 bytes |
| `byte[]` | varint(length) + raw bytes |
| `T[]` / `List<T>` | varint(count) + [optional element-bitmap] + elements |
| `Dictionary<K,V>` | varint(count) + [optional value-bitmap] + (K, V) pairs |
| nested DTO | recursive (его собственный bitmap + поля) |

Element-bitmap в коллекциях: только если annotation элемента — nullable
(`List<string?>`, `int?[]`). Иначе элементы идут плотно.

### 3.4. Пример

```csharp
public sealed class FooDto
{
    public int Id { get; init; }
    public string Name { get; init; }
    public string? Comment { get; init; }
    public List<int> Scores { get; init; }
}
```

Hash-порядок (после сортировки): `[Comment, Id, Name, Scores]`. Nullable-полей: 1 (`Comment`).

Wire для `new FooDto { Id = 7, Name = "Bob", Comment = null, Scores = [1, 2] }`:

```
[bitmap = 0b00000001]                              1 byte:  bit0=1 (Comment null)
[no Comment value]
[Id = 0x07 0x00 0x00 0x00]                         4 bytes
[Name: varint(3) = 0x03, "Bob" = 0x42 0x6F 0x62]   4 bytes
[Scores: varint(2) = 0x02, 0x01000000 0x02000000]  9 bytes
```

Итого: **18 байт**. Без header/trailer/tag-bytes.

---

## 4. Source generator

### 4.1. Базовое

- `IIncrementalGenerator` (Roslyn 4.11+).
- Target framework: `netstandard2.0`.
- Активируется в потребителе через `<ProjectReference ... OutputItemType="Analyzer" ReferenceOutputAssembly="false" />`.

### 4.2. Discovery — сканирование call-sites

| Call-site | Источник T |
|---|---|
| `XPRpc.Write<T>(value, writer)` | generic argument |
| `XPRpc.Read<T>(span)` | generic argument |
| `XPRpc.Touch<T>()` | generic argument (явный prime для startup) |
| `IRpcSerializer.Serialize<T>(value)` | generic argument |
| `IRpcSerializer.Deserialize<T>(payload)` | generic argument |

Для каждого вызова берётся closed type symbol через `IMethodSymbol.TypeArguments`.
Если T остаётся открытым (type-parameter caller'а) — диагностика `XPRPC001` и пропуск.

### 4.3. Транзитивное замыкание

Для каждого discovered T рекурсивно обходим:

- public instance fields (не const).
- public instance properties (declared in type, init/setter — не обязателен на этом шаге).

Для каждого собранного типа:

- built-in (primitives/string/Guid/etc.) — ok, не углубляемся.
- коллекция — углубляемся в `T[]`/`List<T>`.element / `Dictionary<K,V>`.key + value.
- nested DTO — рекурсия.

Cycle-protection: множество посещённых типов.

Open-generic типы (`Foo<T>` где T не закрыт) → диагностика `XPRPC002`.

### 4.4. Хеш — FNV-1a 32-bit

Канонический алгоритм (используется и в generator'е, и в runtime helper'е):

```csharp
static uint Fnv1a(string s)
{
    const uint offset = 2166136261u;
    const uint prime = 16777619u;
    uint h = offset;
    for (int i = 0; i < s.Length; i++) { h ^= s[i]; h *= prime; }
    return h;
}
```

Sort: `OrderBy(name => (Fnv1a(name), name), Comparer<(uint,string)>.Default)`.

### 4.5. Ctor-binding strategy

1. Если у типа есть public parameterless constructor → instantiate, set members через setter/init.
2. Иначе — найти public constructor с **максимальным числом параметров**, имена которых
   (case-insensitive) полностью являются подмножеством имён собранных members. Параметры
   биндятся по имени; conflict resolved by ordinal name match.
3. Если ctor покрывает не все members:
   - оставшиеся members с public setter/init → присваиваются после `new`;
   - оставшиеся members **без** setter/init → диагностика `XPRPC003` (immutable, но не входит в ctor).
4. Если ни parameterless, ни покрывающий ctor нет → диагностика `XPRPC003`.

Примеры:
- `record class Foo(int X, string Name)` — primary-ctor покрывает все.
- `record class Foo(int X) { public string? Comment { get; init; } }` — ctor для X, init для Comment.

### 4.6. Структура generated-кода (per DTO)

```csharp
// <auto-generated/>
using System;
using System.Buffers;
using XPacketRpc;
using XPacketRpc.Internal;

namespace XPacketRpc.Generated;

internal static class __XPRpcGen_FooDto
{
    internal static readonly string[] FieldOrder =
        new[] { "Comment", "Id", "Name", "Scores" };

    internal static void Write(global::FooDto value, IBufferWriter<byte> w)
    {
        // build nullability bitmap (1 байт для 1-4 nullable полей)
        byte bitmap = 0;
        if (value.Comment is null) bitmap |= 0b0000_0001;
        var span = w.GetSpan(1);
        span[0] = bitmap;
        w.Advance(1);

        if (value.Comment is not null) Writers.WriteString(value.Comment, w);
        Writers.WriteInt32LE(value.Id, w);
        if (value.Name is null) Writers.ThrowNullRequired("Name");
        Writers.WriteString(value.Name, w);
        if (value.Scores is null) Writers.ThrowNullRequired("Scores");
        Writers.WriteListInt32(value.Scores, w);
    }

    internal static FooDto Read(ref XPRpcReader r)
    {
        byte bitmap = r.ReadByte();
        bool commentIsNull = (bitmap & 0b0000_0001) != 0;

        string? comment = commentIsNull ? null : r.ReadString();
        int id = r.ReadInt32();
        string name = r.ReadString();
        var scores = r.ReadListInt32();

        return new FooDto { Id = id, Name = name, Comment = comment, Scores = scores };
        // Если ctor-binding нужен:
        //   return new FooDto(id, name) { Comment = comment, Scores = scores };
    }
}
```

### 4.7. Registry / module-init

Один файл-агрегатор на сборку (имя namespace включает короткое имя сборки для уникальности):

```csharp
namespace XPacketRpc.Generated.MyServiceAssembly;

internal static class __XPRpcRegistry
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Init()
    {
        XPRpc.Register<global::FooDto>(__XPRpcGen_FooDto.Write, __XPRpcGen_FooDto.Read);
        XPRpc.Register<global::BarDto>(__XPRpcGen_BarDto.Write, __XPRpcGen_BarDto.Read);
        // ... per discovered type
    }
}
```

`XPRpc.Register<T>(write, read)` кладёт в `ConcurrentDictionary<Type, (object writer, object reader)>`.

### 4.8. Диагностики

| ID | Severity | Сообщение |
|---|---|---|
| `XPRPC001` | Warning | Open-generic call-site: T cannot be resolved at compile-time. Add `XPRpc.Touch<ConcreteType>()` in startup. |
| `XPRPC002` | Error | Open-generic type `Foo<T>` reached in transitive closure. Sourcegen requires closed types. |
| `XPRPC003` | Error | Cannot construct `T`: no parameterless constructor and no constructor with parameters matching property names. |
| `XPRPC004` | Error | Field type `X` of `T.Y` is unsupported (e.g. `object`, polymorphic abstract, jagged 2D array). |
| `XPRPC005` | Error | Field name collision after FNV-1a + ordinal tiebreak. (defensive — крайне маловероятно) |
| `XPRPC006` | Warning | Type `T` has no fields/properties — empty wire payload. |

---

## 5. Public API

### 5.1. Namespaces

```
XPacketRpc                  ← публичный runtime API
XPacketRpc.Internal         ← public-видимые helper'ы для generated-кода
XPacketRpc.Generated        ← internal, эмитит generator
XPacketRpc.Generators       ← отдельная сборка, sourcegen
```

### 5.2. `IRpcSerializer` (контракт пользователя)

```csharp
namespace XPacketRpc;

public interface IRpcSerializer
{
    string ContentType { get; }
    byte[] Serialize<T>(T value);
    T? Deserialize<T>(ReadOnlyMemory<byte> payload);
}
```

### 5.3. `XPacketRpcSerializer` — фасад

```csharp
namespace XPacketRpc;

public sealed class XPacketRpcSerializer : IRpcSerializer
{
    public const string XPacketRpcContentType = "application/x-xprotocol-rpc";

    public string ContentType => XPacketRpcContentType;

    public byte[] Serialize<T>(T value)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));

        using var buffer = new PooledBufferWriter(ArrayPool<byte>.Shared, initialCapacity: 256);
        XPRpc.Write(value, buffer);
        return buffer.WrittenSpan.ToArray();
    }

    public T? Deserialize<T>(ReadOnlyMemory<byte> payload)
        => XPRpc.Read<T>(payload.Span);
}
```

### 5.4. `XPRpc` — низкоуровневый API

```csharp
namespace XPacketRpc;

public static class XPRpc
{
    public static void Write<T>(T value, IBufferWriter<byte> writer);
    public static T? Read<T>(ReadOnlySpan<byte> source);

    /// <summary>No-op. Существует только чтобы generator увидел T в call-site analysis.</summary>
    public static void Touch<T>();

    /// <summary>
    /// Public для использования из generated module-initializer'ов в произвольных consumer-сборках.
    /// Не вызывайте напрямую — generator делает это автоматически.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void Register<T>(WriteDelegate<T> write, ReadDelegate<T> read);

    public delegate void WriteDelegate<T>(T value, IBufferWriter<byte> writer);
    public delegate T ReadDelegate<T>(ref XPRpcReader reader);
}
```

### 5.5. `XPRpcReader` — ref struct

```csharp
namespace XPacketRpc;

public ref struct XPRpcReader
{
    private ReadOnlySpan<byte> source;
    private int position;

    public XPRpcReader(ReadOnlySpan<byte> source) { ... }
    public int Position => position;
    public int Remaining => source.Length - position;

    public byte ReadByte();
    public short ReadInt16();
    public int ReadInt32();
    public long ReadInt64();
    public float ReadSingle();
    public double ReadDouble();
    public decimal ReadDecimal();
    public uint ReadVarUInt32();
    public string ReadString();
    public byte[] ReadBytes();
    public Guid ReadGuid();
    public DateTime ReadDateTime();
    public DateTimeOffset ReadDateTimeOffset();
    public TimeSpan ReadTimeSpan();
    public List<int> ReadListInt32();
    public List<T> ReadList<T>(Func<XPRpcReader, T> elem);
    // ... остальные
}
```

Public — потому что generator'у нужно эмитить вызовы из произвольных consumer-сборок.

### 5.6. `Internal/Writers` — write-side helper'ы

```csharp
namespace XPacketRpc.Internal;

public static class Writers
{
    public static void WriteByte(byte v, IBufferWriter<byte> w);
    public static void WriteInt16LE(short v, IBufferWriter<byte> w);
    public static void WriteInt32LE(int v, IBufferWriter<byte> w);
    public static void WriteInt64LE(long v, IBufferWriter<byte> w);
    public static void WriteSingleLE(float v, IBufferWriter<byte> w);
    public static void WriteDoubleLE(double v, IBufferWriter<byte> w);
    public static void WriteDecimalLE(decimal v, IBufferWriter<byte> w);
    public static void WriteVarUInt32(uint v, IBufferWriter<byte> w);
    public static void WriteString(string v, IBufferWriter<byte> w);
    public static void WriteBytes(byte[] v, IBufferWriter<byte> w);
    public static void WriteGuid(Guid v, IBufferWriter<byte> w);
    public static void WriteDateTime(DateTime v, IBufferWriter<byte> w);
    public static void WriteDateTimeOffset(DateTimeOffset v, IBufferWriter<byte> w);
    public static void WriteTimeSpan(TimeSpan v, IBufferWriter<byte> w);

    [DoesNotReturn]
    public static void ThrowNullRequired(string fieldName);
}
```

### 5.7. Исключения

```csharp
namespace XPacketRpc;

public sealed class MissingSerializerException : Exception
{
    public Type MissingType { get; }
    public MissingSerializerException(Type t)
        : base($"No generated serializer for type '{t.FullName}'. " +
               $"Add a closed-generic call-site (e.g. XPRpc.Touch<{t.Name}>()) so the source generator can emit code.")
    { MissingType = t; }
}

public sealed class RpcSerializationException : Exception
{
    public RpcSerializationException(string message) : base(message) { }
    public RpcSerializationException(string message, Exception inner) : base(message, inner) { }
}
```

### 5.8. Использование

```csharp
// startup
services.AddSingleton<IRpcSerializer, XPacketRpcSerializer>();

// для типов, которые резолвятся через MakeGenericMethod в runtime — явный prime:
public static class RpcMessageRegistry
{
    public static void Prime()
    {
        XPRpc.Touch<OrderRequest>();
        XPRpc.Touch<OrderResponse>();
        XPRpc.Touch<UserProfile>();
    }
}
// вызов: RpcMessageRegistry.Prime() в startup до первого Deserialize.

// hot-path — publish
byte[] payload = serializer.Serialize(new OrderRequest { ... });

// hot-path — receive
OrderResponse? response = serializer.Deserialize<OrderResponse>(deliveryArgs.Body);
```

---

## 6. Структура проектов

### 6.1. Layout

Добавляются в существующий `TCPProtocol.sln`. Старые проекты не трогаются.

```
C:\Works_Test\XProtokol\
├── TCPProtocol.sln                               (обновить — добавить ссылки)
├── XProtocol\, XProtocol.Tests\, …               (legacy, untouched)
│
├── XPacketRpc\                                   ← runtime
│   ├── XPacketRpc.csproj
│   ├── IRpcSerializer.cs
│   ├── XPacketRpcSerializer.cs
│   ├── XPRpc.cs
│   ├── XPRpcReader.cs
│   ├── MissingSerializerException.cs
│   ├── RpcSerializationException.cs
│   └── Internal\
│       ├── Writers.cs
│       ├── PooledBufferWriter.cs
│       └── Fnv1a.cs
│
├── XPacketRpc.Generators\                        ← sourcegen (netstandard2.0)
│   ├── XPacketRpc.Generators.csproj
│   ├── XPacketRpcGenerator.cs
│   ├── Discovery\
│   │   ├── CallSiteCollector.cs
│   │   └── TypeWalker.cs
│   ├── Emit\
│   │   ├── WriteEmitter.cs
│   │   ├── ReadEmitter.cs
│   │   ├── RegistryEmitter.cs
│   │   └── Templates.cs
│   └── Diagnostics\
│       └── Descriptors.cs
│
└── XPacketRpc.Benchmarks\                        ← BDN runner (net10.0)
    ├── XPacketRpc.Benchmarks.csproj
    ├── Program.cs
    ├── Dtos\           (8 DTO + helper-классы для каждого serializer'а)
    └── Benchmarks\
        ├── SerializeBenchmarks.cs
        ├── DeserializeBenchmarks.cs
        └── RoundtripBenchmarks.cs
```

### 6.2. `XPacketRpc.csproj` (runtime)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>XPacketRpc</RootNamespace>
  </PropertyGroup>
</Project>
```

Без зависимостей от `XProtocol.csproj`. Без зависимости от `XPacketRpc.Generators` (генератор активируется в потребителях).

### 6.3. `XPacketRpc.Generators.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
    <IsRoslynComponent>true</IsRoslynComponent>
    <RootNamespace>XPacketRpc.Generators</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.11.0" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.11.0" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

### 6.4. `XPacketRpc.Benchmarks.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <ServerGarbageCollection>true</ServerGarbageCollection>
    <ConcurrentGarbageCollection>true</ConcurrentGarbageCollection>
    <RootNamespace>XPacketRpc.Benchmarks</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\XPacketRpc\XPacketRpc.csproj" />
    <ProjectReference Include="..\XPacketRpc.Generators\XPacketRpc.Generators.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="BenchmarkDotNet" Version="0.14.0" />
    <PackageReference Include="MessagePack" Version="3.1.4" />
    <PackageReference Include="MemoryPack" Version="1.21.4" />
    <PackageReference Include="protobuf-net" Version="3.2.30" />
    <PackageReference Include="Bond.CSharp" Version="13.0.1" />
    <!-- System.Text.Json — встроен в net10.0 -->
  </ItemGroup>
</Project>
```

---

## 7. Бенчмарки

### 7.1. DTO для бенчмарков (8 штук)

Каждый DTO имеет одинаковый shape (имена полей + типы) во всех вариантах для каждого serializer'а
(некоторые библиотеки требуют свой набор атрибутов / partial / runtime-регистрации).

| # | DTO | Поля | Размер payload (приблиз.) | Стресс-тест чего |
|---|---|---|---|---|
| 1 | `Vector3` | 3 × `float` | ~12 B | минимальный overhead |
| 2 | `LogEntry` | `DateTimeOffset`, `byte`, `string`, `Guid`, `Guid` | ~80 B | mixed primitives + Guid + DateTime |
| 3 | `OrderRequest(N)` | `Guid`, `int`, `List<OrderItem>` (N=5, 50) | ~250 B / ~2.5 KB | коллекции + nested |
| 4 | `UserProfile` | `Guid`, `string`, `Address` (nested), `string[]` | ~150 B | nested + string[] |
| 5 | `ChunkPayload(K)` | `Guid`, `byte[]` (K=16 KB, 64 KB) | ~16 KB / ~64 KB | большой блоб |
| 6 | `BigDictionary(N)` | `Dictionary<string, int>` (N=100, 1000) | ~2 KB / ~20 KB | dictionary throughput |
| 7 | `DeepNested` | 5 уровней по 3 поля | ~100 B | recursion overhead |
| 8 | `RecordRequest` | `record class(Guid, int, string, DateTimeOffset, decimal) + string?` | ~70 B | ctor-binding |

`OrderItem` и `Address` — вспомогательные nested DTO.

### 7.2. Конкуренты

| Serializer | Маркировка DTO | Заметка |
|---|---|---|
| **XPacketRpc** | без атрибутов, sourcegen | baseline |
| **MessagePack-CSharp 3.x** | `ContractlessStandardResolver.Instance` | без атрибутов |
| **MemoryPack 1.21** | `[MemoryPackable]` + `partial` | копия DTO с атрибутами |
| **System.Text.Json** | без атрибутов | reflection-based; отдельный прогон с `JsonSerializerContext` (sourcegen) |
| **protobuf-net 3.x** | `RuntimeTypeModel.Default.Add(type, false)` + явное добавление полей | без атрибутов, runtime-модель |
| **Bond.CSharp 13** | `.bond` schema → codegen | отдельные классы. **Risk:** setup может быть тяжёлым; план B — выкинуть |

### 7.3. BDN-конфигурация

```csharp
public class BenchConfig : ManualConfig
{
    public BenchConfig()
    {
        AddJob(Job.Default
            .WithRuntime(CoreRuntime.Core100)
            .WithGcServer(true)
            .WithGcConcurrent(true)
            .WithWarmupCount(3)
            .WithIterationCount(10));

        AddDiagnoser(MemoryDiagnoser.Default);
        AddExporter(MarkdownExporter.GitHub, CsvExporter.Default);
        AddColumn(StatisticColumn.AllStatistics);
        AddColumnProvider(DefaultColumnProviders.Instance);
    }
}
```

### 7.4. Метрики

- **Mean** (ns / μs) — время операции.
- **Allocated** (B) — managed memory / op.
- **Gen0/Gen1/Gen2** — GC-pressure.
- **Ratio** — относительно `XPacketRpc`-baseline.
- **Wire-size** (B) — отдельная не-BDN таблица: размер payload каждого serializer'а на каждом DTO.

### 7.5. Точки достоверности

- `[GlobalSetup]` каждого benchmark-класса делает roundtrip каждого serializer'а и
  `Assert.Equal(input, deserialized)`. Если упадёт — bench не запускается.
- Все `DtoFactory.Create(...)` — seeded random; payload идентичны между прогонами.

### 7.6. Запуск

```bash
dotnet run -c Release --project XPacketRpc.Benchmarks -- --filter '*Serialize*'
dotnet run -c Release --project XPacketRpc.Benchmarks -- --filter '*'
```

Output: `BenchmarkDotNet.Artifacts/results/*.md` + CSV → копируются в `docs/benchmarks/` для трекинга в git.

---

## 8. Тесты — open question

По правилу пользователя из `~/.claude/CLAUDE.md`: тесты добавляются только после явного подтверждения.

**Предлагаемый объём для `XPacketRpc.Tests/` (TUnit 1.43.x):**

- **Roundtrip-тесты** — для каждого DTO из бенчмарков: `Serialize → Deserialize` даёт эквивалент.
- **Wire-format-тесты** — фиксируют точное байтовое представление известных DTO (regression-guard).
- **Nullability-тесты** — null в required-поле бросает `RpcSerializationException`; null в nullable корректно.
- **Edge-cases:** empty `string`/`byte[]`/коллекция, varint > 1 байт, Unicode (BMP + supplementary),
  decimal со знаком, граничные `DateTime`/`DateTimeOffset`, enum с `byte`/`long` underlying,
  record с primary ctor + дополнительными init-properties.
- **Generator-тесты** — snapshot-тесты эмита (через `Verify` или ручные string-asserts).
- **Diagnostic-тесты** — каждый из `XPRPC001..006` срабатывает на синтетическом входе.
- **Hash-stability-тест** — FNV-1a от фиксированного списка имён даёт фиксированные значения.

Решение пользователя по объёму — отдельный шаг **после** одобрения этого spec'а.

---

## 9. Риски и known limitations

| # | Риск | Решение / mitigation |
|---|---|---|
| R1 | Bond.CSharp setup может быть тяжёлым | если за первый день не интегрируется — выкидываем |
| R2 | Cross-assembly call-site discovery — generator видит только текущую сборку | каждая сборка-потребитель reference'ит `XPacketRpc.Generators` как Analyzer самостоятельно |
| R3 | Конфликт двух module-initializer'ов из разных сборок | namespace registry'я включает короткое имя сборки: `XPacketRpc.Generated.<AssemblyShortName>` |
| R4 | `dynamic` в BDN добавляет overhead | альтернатива — generic-instance per type. Решим после первого прогона по noise-level |
| R5 | FNV-1a реализация в generator'е и runtime может разойтись | один canonical алгоритм, hash-stability-тест |
| R6 | Пользователь забудет `XPRpc.Touch<T>()` для runtime-резолвинга | warning `XPRPC001` на каждом open-generic call-site + раздел в README |
| R7 | AOT-готовность — Q12.4 = b. Sourcegen-путь де-факто AOT-clean, но не тестируется | в spec фиксируем: best-effort; reflection используется только в `ConcurrentDictionary` lookup, без `MakeGenericMethod` |
| R8 | Версионирование wire-формата отсутствует (Q6 = A) | known limitation. Любое изменение схемы DTO ломает старые payload'ы. Будущее решение — отдельный envelope-режим или AMQP `type`-header с версией. Не входит в scope |

## 10. Out of scope (первая итерация)

- Полиморфизм / `object`-поля / abstract base + KnownType.
- Наследование DTO (private fields parent'а не сериализуются).
- Версионирование wire-формата.
- Шифрование (XProtocol AES) — RPC канал шифруется TLS на уровне AMQP-broker'а.
- NuGet-пакетирование с auto-attach analyzer'а.
- AOT-`PublishAot`-валидация.
- Streaming (PipeWriter/PipeReader API).

---

## 11. Definition of Done (первая итерация)

- [ ] Создан `XPacketRpc/` runtime-проект, собирается.
- [ ] Создан `XPacketRpc.Generators/` sourcegen-проект, собирается.
- [ ] `XPacketRpc.Benchmarks/` собирается, **roundtrip-проверка** во всех `[GlobalSetup]` проходит для всех 8 DTO × 6 serializer'ов.
- [ ] BDN-прогон даёт результаты для `Serialize`/`Deserialize`/`Roundtrip`.
- [ ] Wire-size таблица сгенерирована.
- [ ] (Опционально, после подтверждения) тестовый проект с покрытием из §8.
- [ ] Spec и реализация закоммичены в `master`.
