# XPacketRpc Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** реализовать `XPacketRpc` — sourcegen-сериализатор для RPC по spec'у `docs/superpowers/specs/2026-05-08-xpacket-rpc-design.md`, с тестами полного объёма §8 и BDN-бенчмарками против 5 конкурентов.

**Architecture:** schema-driven bare-payload бинарный формат. Roslyn `IIncrementalGenerator` сканирует call-sites `XPRpc.Write/Read/Touch` и `IRpcSerializer.Serialize/Deserialize`, эмитит per-DTO `Write/Read` через FNV-1a-стабильный порядок полей. Runtime регистрирует генерируемые делегаты через `[ModuleInitializer]`. Без атрибутов на DTO. Fail-fast при отсутствии generated-кода.

**Tech Stack:** C# 13 / .NET 10 (runtime, tests, benchmarks), netstandard2.0 (generator), Roslyn 4.11+, TUnit 1.43.x, BenchmarkDotNet 0.14, MessagePack-CSharp 3.x, MemoryPack 1.21, protobuf-net 3.x, Bond.CSharp 13.x.

**Spec ref:** `docs/superpowers/specs/2026-05-08-xpacket-rpc-design.md` (commit `790944f`).

---

## Phase 0 — Solution scaffolding

Создание четырёх csproj-файлов и подключение к существующему `TCPProtocol.sln`. Никакого функционала, только пустые проекты, которые собираются.

### Task 0.1: Create `XPacketRpc.csproj` (runtime library)

**Files:**
- Create: `XPacketRpc/XPacketRpc.csproj`
- Create: `XPacketRpc/_Stub.cs` (временный пустой файл, чтобы проект имел компилируемое содержимое)

- [ ] **Step 1: Создать csproj**

`XPacketRpc/XPacketRpc.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>XPacketRpc</RootNamespace>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);CS1591</NoWarn>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Создать stub-файл**

`XPacketRpc/_Stub.cs`:

```csharp
namespace XPacketRpc;

internal static class _Stub
{
    // placeholder so the project has at least one type to compile.
    // removed once real types are added in Phase 1.
}
```

- [ ] **Step 3: Собрать**

```
dotnet build XPacketRpc/XPacketRpc.csproj -c Debug
```

Expected: `Build succeeded`, output `bin/Debug/net10.0/XPacketRpc.dll`.

- [ ] **Step 4: Commit**

```
git add XPacketRpc/XPacketRpc.csproj XPacketRpc/_Stub.cs
git commit -m "scaffold: add empty XPacketRpc runtime project"
```

---

### Task 0.2: Create `XPacketRpc.Generators.csproj` (source generator)

**Files:**
- Create: `XPacketRpc.Generators/XPacketRpc.Generators.csproj`
- Create: `XPacketRpc.Generators/_Stub.cs`

- [ ] **Step 1: Создать csproj**

`XPacketRpc.Generators/XPacketRpc.Generators.csproj`:

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

- [ ] **Step 2: Создать stub**

`XPacketRpc.Generators/_Stub.cs`:

```csharp
namespace XPacketRpc.Generators;

internal static class _Stub
{
    // placeholder until the real generator is added.
}
```

- [ ] **Step 3: Собрать**

```
dotnet build XPacketRpc.Generators/XPacketRpc.Generators.csproj -c Debug
```

Expected: `Build succeeded`. Никакого `.dll` в обычном `bin` потому что `IncludeBuildOutput=false`, но компиляция должна пройти.

- [ ] **Step 4: Commit**

```
git add XPacketRpc.Generators/XPacketRpc.Generators.csproj XPacketRpc.Generators/_Stub.cs
git commit -m "scaffold: add empty XPacketRpc.Generators source-generator project"
```

---

### Task 0.3: Create `XPacketRpc.Tests.csproj` (TUnit)

**Files:**
- Create: `XPacketRpc.Tests/XPacketRpc.Tests.csproj`
- Create: `XPacketRpc.Tests/_SmokeTest.cs`

- [ ] **Step 1: Создать csproj**

`XPacketRpc.Tests/XPacketRpc.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <OutputType>Exe</OutputType>
    <RootNamespace>XPacketRpc.Tests</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="TUnit" Version="1.43.11" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\XPacketRpc\XPacketRpc.csproj" />
    <ProjectReference Include="..\XPacketRpc.Generators\XPacketRpc.Generators.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Smoke-тест**

`XPacketRpc.Tests/_SmokeTest.cs`:

```csharp
namespace XPacketRpc.Tests;

public class SmokeTest
{
    [Test]
    public async Task Smoke()
    {
        await Assert.That(1 + 1).IsEqualTo(2);
    }
}
```

- [ ] **Step 3: Запустить тест**

```
dotnet test XPacketRpc.Tests/XPacketRpc.Tests.csproj -c Debug
```

Expected: 1 test passed.

- [ ] **Step 4: Commit**

```
git add XPacketRpc.Tests/XPacketRpc.Tests.csproj XPacketRpc.Tests/_SmokeTest.cs
git commit -m "scaffold: add empty XPacketRpc.Tests project (TUnit)"
```

---

### Task 0.4: Create `XPacketRpc.Benchmarks.csproj` (BenchmarkDotNet)

**Files:**
- Create: `XPacketRpc.Benchmarks/XPacketRpc.Benchmarks.csproj`
- Create: `XPacketRpc.Benchmarks/Program.cs`

- [ ] **Step 1: Создать csproj**

`XPacketRpc.Benchmarks/XPacketRpc.Benchmarks.csproj`:

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
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Скелет Program.cs**

`XPacketRpc.Benchmarks/Program.cs`:

```csharp
using BenchmarkDotNet.Running;

namespace XPacketRpc.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
        // benchmarks будут добавлены в Phase 11
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
```

- [ ] **Step 3: Собрать**

```
dotnet build XPacketRpc.Benchmarks/XPacketRpc.Benchmarks.csproj -c Release
```

Expected: `Build succeeded` (warning-free; `BenchmarkSwitcher` без обнаруженных бенчмарков — нормально для этого этапа).

- [ ] **Step 4: Commit**

```
git add XPacketRpc.Benchmarks/XPacketRpc.Benchmarks.csproj XPacketRpc.Benchmarks/Program.cs
git commit -m "scaffold: add empty XPacketRpc.Benchmarks project (BDN)"
```

---

### Task 0.5: Подключить новые проекты к `TCPProtocol.sln`

**Files:**
- Modify: `TCPProtocol.sln`

- [ ] **Step 1: Добавить проекты к решению**

```
dotnet sln TCPProtocol.sln add XPacketRpc/XPacketRpc.csproj
dotnet sln TCPProtocol.sln add XPacketRpc.Generators/XPacketRpc.Generators.csproj
dotnet sln TCPProtocol.sln add XPacketRpc.Tests/XPacketRpc.Tests.csproj
dotnet sln TCPProtocol.sln add XPacketRpc.Benchmarks/XPacketRpc.Benchmarks.csproj
```

- [ ] **Step 2: Собрать всё решение**

```
dotnet build TCPProtocol.sln -c Debug
```

Expected: `Build succeeded`. Все 9 проектов (5 legacy + 4 новых) собираются.

- [ ] **Step 3: Прогнать smoke-test**

```
dotnet test XPacketRpc.Tests -c Debug
```

Expected: 1 test passed.

- [ ] **Step 4: Commit**

```
git add TCPProtocol.sln
git commit -m "scaffold: register XPacketRpc projects in TCPProtocol.sln"
```

---

## Phase 1 — Runtime: исключения + FNV-1a

### Task 1.1: `MissingSerializerException` + `RpcSerializationException`

**Files:**
- Create: `XPacketRpc/MissingSerializerException.cs`
- Create: `XPacketRpc/RpcSerializationException.cs`
- Create: `XPacketRpc.Tests/ExceptionTests.cs`
- Delete: `XPacketRpc/_Stub.cs` (заменён реальными типами)

- [ ] **Step 1: Failing tests**

`XPacketRpc.Tests/ExceptionTests.cs`:

```csharp
using XPacketRpc;

namespace XPacketRpc.Tests;

public class ExceptionTests
{
    [Test]
    public async Task MissingSerializerException_includes_type_in_message()
    {
        var ex = new MissingSerializerException(typeof(string));

        await Assert.That(ex.MissingType).IsEqualTo(typeof(string));
        await Assert.That(ex.Message).Contains("System.String");
        await Assert.That(ex.Message).Contains("Touch<");
    }

    [Test]
    public async Task RpcSerializationException_carries_message_and_inner()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new RpcSerializationException("payload corrupt", inner);

        await Assert.That(ex.Message).IsEqualTo("payload corrupt");
        await Assert.That(ex.InnerException).IsSameReferenceAs(inner);
    }
}
```

- [ ] **Step 2: Запустить — упадут**

```
dotnet test XPacketRpc.Tests -c Debug
```

Expected: 2 failing tests (`MissingSerializerException`/`RpcSerializationException` not found).

- [ ] **Step 3: Реализовать `MissingSerializerException`**

`XPacketRpc/MissingSerializerException.cs`:

```csharp
namespace XPacketRpc;

public sealed class MissingSerializerException : Exception
{
    public Type MissingType { get; }

    public MissingSerializerException(Type missingType)
        : base(BuildMessage(missingType))
    {
        this.MissingType = missingType;
    }

    private static string BuildMessage(Type t) =>
        $"No generated serializer for type '{t.FullName}'. " +
        $"Add a closed-generic call-site (e.g. XPRpc.Touch<{t.Name}>()) so the source generator can emit code.";
}
```

- [ ] **Step 4: Реализовать `RpcSerializationException`**

`XPacketRpc/RpcSerializationException.cs`:

```csharp
namespace XPacketRpc;

public sealed class RpcSerializationException : Exception
{
    public RpcSerializationException(string message) : base(message) { }
    public RpcSerializationException(string message, Exception inner) : base(message, inner) { }
}
```

- [ ] **Step 5: Удалить stub и прогнать тесты**

```
git rm XPacketRpc/_Stub.cs
dotnet test XPacketRpc.Tests -c Debug
```

Expected: 3 tests passed (1 smoke + 2 exception).

- [ ] **Step 6: Commit**

```
git add XPacketRpc/MissingSerializerException.cs XPacketRpc/RpcSerializationException.cs XPacketRpc.Tests/ExceptionTests.cs
git commit -m "feat(runtime): add MissingSerializerException and RpcSerializationException"
```

---

### Task 1.2: `Internal/Fnv1a` + hash-stability test

**Files:**
- Create: `XPacketRpc/Internal/Fnv1a.cs`
- Create: `XPacketRpc.Tests/Fnv1aTests.cs`

- [ ] **Step 1: Failing test (hash-stability с фиксированными векторами)**

`XPacketRpc.Tests/Fnv1aTests.cs`:

```csharp
using XPacketRpc.Internal;

namespace XPacketRpc.Tests;

public class Fnv1aTests
{
    [Test]
    [Arguments("", 0x811C9DC5u)]
    [Arguments("a", 0xE40C292Cu)]
    [Arguments("foobar", 0xBF9CF968u)]
    [Arguments("Id", 0x66C18A1Au)]
    [Arguments("Name", 0xE0BB07A5u)]
    [Arguments("Comment", 0x86CDF06Du)]
    [Arguments("Scores", 0x77B5FB05u)]
    public async Task Fnv1a_matches_canonical_vectors(string input, uint expected)
    {
        await Assert.That(Fnv1a.Hash(input)).IsEqualTo(expected);
    }

    [Test]
    public async Task Fnv1a_empty_string_returns_offset_basis()
    {
        await Assert.That(Fnv1a.Hash(string.Empty)).IsEqualTo(0x811C9DC5u);
    }

    [Test]
    public async Task Fnv1a_is_deterministic_across_calls()
    {
        var a = Fnv1a.Hash("HelloWorld");
        var b = Fnv1a.Hash("HelloWorld");

        await Assert.That(a).IsEqualTo(b);
    }
}
```

> **Note:** значения `0xE40C292C` для "a" и `0xBF9CF968` для "foobar" — стандартные тест-векторы FNV-1a 32-bit (см. http://www.isthe.com/chongo/tech/comp/fnv/). Значения для "Id"/"Name"/"Comment"/"Scores" — расчёт по канонике; если получите другое — проверьте формулу, не вектора.

- [ ] **Step 2: Запустить — упадёт**

```
dotnet test XPacketRpc.Tests -c Debug --filter "FullyQualifiedName~Fnv1aTests"
```

Expected: tests fail (`Fnv1a` not found).

- [ ] **Step 3: Реализовать**

`XPacketRpc/Internal/Fnv1a.cs`:

```csharp
namespace XPacketRpc.Internal;

/// <summary>
/// Canonical FNV-1a 32-bit hash. ИДЕНТИЧНАЯ реализация дублируется в
/// XPacketRpc.Generators (генератор не может ссылаться на runtime-сборку).
/// При изменении синхронизируйте обе копии и обновите Fnv1aTests.
/// </summary>
public static class Fnv1a
{
    private const uint OffsetBasis = 2166136261u;
    private const uint Prime = 16777619u;

    public static uint Hash(string s)
    {
        uint h = OffsetBasis;
        for (int i = 0; i < s.Length; i++)
        {
            h ^= s[i];
            h *= Prime;
        }
        return h;
    }
}
```

- [ ] **Step 4: Прогнать тесты**

```
dotnet test XPacketRpc.Tests -c Debug --filter "FullyQualifiedName~Fnv1aTests"
```

Expected: все Fnv1a-тесты passed. Если упало на конкретных DTO-именах ("Id"/"Name"/"Comment"/"Scores") — пересчитайте вручную и подставьте actual в `[Arguments]` (это test-векторы, фиксирующие алгоритм для regression).

- [ ] **Step 5: Commit**

```
git add XPacketRpc/Internal/Fnv1a.cs XPacketRpc.Tests/Fnv1aTests.cs
git commit -m "feat(runtime): add canonical FNV-1a 32-bit hash + stability tests"
```

---

## Phase 2 — Runtime: pooled buffer + writers

### Task 2.1: `PooledBufferWriter` (IBufferWriter<byte> поверх ArrayPool)

**Files:**
- Create: `XPacketRpc/Internal/PooledBufferWriter.cs`
- Create: `XPacketRpc.Tests/PooledBufferWriterTests.cs`

- [ ] **Step 1: Failing tests**

`XPacketRpc.Tests/PooledBufferWriterTests.cs`:

```csharp
using System.Buffers;
using XPacketRpc.Internal;

namespace XPacketRpc.Tests;

public class PooledBufferWriterTests
{
    [Test]
    public async Task Empty_writer_has_zero_written()
    {
        using var w = new PooledBufferWriter(ArrayPool<byte>.Shared, initialCapacity: 16);

        await Assert.That(w.WrittenCount).IsEqualTo(0);
        await Assert.That(w.WrittenSpan.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Write_advance_grows_written_span()
    {
        using var w = new PooledBufferWriter(ArrayPool<byte>.Shared, initialCapacity: 16);

        var span = w.GetSpan(4);
        span[0] = 1; span[1] = 2; span[2] = 3; span[3] = 4;
        w.Advance(4);

        await Assert.That(w.WrittenCount).IsEqualTo(4);
        await Assert.That(w.WrittenSpan.ToArray()).IsEquivalentTo(new byte[] { 1, 2, 3, 4 });
    }

    [Test]
    public async Task GetSpan_grows_buffer_when_needed()
    {
        using var w = new PooledBufferWriter(ArrayPool<byte>.Shared, initialCapacity: 4);

        var span = w.GetSpan(64);

        await Assert.That(span.Length).IsGreaterThanOrEqualTo(64);
    }

    [Test]
    public async Task Advance_negative_throws()
    {
        using var w = new PooledBufferWriter(ArrayPool<byte>.Shared);
        await Assert.That(() => w.Advance(-1)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Advance_past_buffer_throws()
    {
        using var w = new PooledBufferWriter(ArrayPool<byte>.Shared, initialCapacity: 4);
        w.GetSpan(4);

        await Assert.That(() => w.Advance(5)).Throws<InvalidOperationException>();
    }
}
```

- [ ] **Step 2: Запустить — упадут**

```
dotnet test XPacketRpc.Tests --filter "FullyQualifiedName~PooledBufferWriterTests"
```

- [ ] **Step 3: Реализовать**

`XPacketRpc/Internal/PooledBufferWriter.cs`:

```csharp
using System.Buffers;

namespace XPacketRpc.Internal;

public sealed class PooledBufferWriter : IBufferWriter<byte>, IDisposable
{
    private readonly ArrayPool<byte> pool;
    private byte[] buffer;
    private int written;

    public PooledBufferWriter(ArrayPool<byte> pool, int initialCapacity = 256)
    {
        if (initialCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        this.pool = pool;
        this.buffer = pool.Rent(initialCapacity);
        this.written = 0;
    }

    public int WrittenCount => this.written;
    public ReadOnlySpan<byte> WrittenSpan => this.buffer.AsSpan(0, this.written);
    public ReadOnlyMemory<byte> WrittenMemory => this.buffer.AsMemory(0, this.written);

    public void Advance(int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (this.written + count > this.buffer.Length)
            throw new InvalidOperationException("Cannot advance past the end of the buffer.");
        this.written += count;
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return this.buffer.AsMemory(this.written);
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return this.buffer.AsSpan(this.written);
    }

    private void EnsureCapacity(int sizeHint)
    {
        if (sizeHint < 0) throw new ArgumentOutOfRangeException(nameof(sizeHint));
        if (sizeHint == 0) sizeHint = 1;

        int available = this.buffer.Length - this.written;
        if (available >= sizeHint) return;

        int requested = checked(this.written + sizeHint);
        int newSize = Math.Max(this.buffer.Length * 2, requested);
        var next = this.pool.Rent(newSize);
        Buffer.BlockCopy(this.buffer, 0, next, 0, this.written);
        this.pool.Return(this.buffer);
        this.buffer = next;
    }

    public void Dispose()
    {
        if (this.buffer.Length > 0)
        {
            this.pool.Return(this.buffer);
            this.buffer = Array.Empty<byte>();
        }
    }
}
```

- [ ] **Step 4: Прогнать тесты**

```
dotnet test XPacketRpc.Tests --filter "FullyQualifiedName~PooledBufferWriterTests"
```

Expected: 5 tests passed.

- [ ] **Step 5: Commit**

```
git add XPacketRpc/Internal/PooledBufferWriter.cs XPacketRpc.Tests/PooledBufferWriterTests.cs
git commit -m "feat(runtime): add PooledBufferWriter (IBufferWriter<byte> over ArrayPool)"
```

---

### Task 2.2: `Writers` — primitives + VarUInt32

**Files:**
- Create: `XPacketRpc/Internal/Writers.cs`
- Create: `XPacketRpc.Tests/WritersPrimitivesTests.cs`

- [ ] **Step 1: Failing tests**

`XPacketRpc.Tests/WritersPrimitivesTests.cs`:

```csharp
using System.Buffers;
using XPacketRpc.Internal;

namespace XPacketRpc.Tests;

public class WritersPrimitivesTests
{
    private static byte[] Capture(Action<PooledBufferWriter> action)
    {
        using var w = new PooledBufferWriter(ArrayPool<byte>.Shared, 64);
        action(w);
        return w.WrittenSpan.ToArray();
    }

    [Test]
    public async Task WriteByte_emits_one_byte()
    {
        var bytes = Capture(w => Writers.WriteByte(0xAB, w));
        await Assert.That(bytes).IsEquivalentTo(new byte[] { 0xAB });
    }

    [Test]
    public async Task WriteInt16LE_writes_little_endian()
    {
        var bytes = Capture(w => Writers.WriteInt16LE(unchecked((short)0xCAFE), w));
        await Assert.That(bytes).IsEquivalentTo(new byte[] { 0xFE, 0xCA });
    }

    [Test]
    public async Task WriteInt32LE_writes_little_endian()
    {
        var bytes = Capture(w => Writers.WriteInt32LE(0x12345678, w));
        await Assert.That(bytes).IsEquivalentTo(new byte[] { 0x78, 0x56, 0x34, 0x12 });
    }

    [Test]
    public async Task WriteInt64LE_writes_little_endian()
    {
        var bytes = Capture(w => Writers.WriteInt64LE(0x0123_4567_89AB_CDEF, w));
        await Assert.That(bytes).IsEquivalentTo(new byte[] { 0xEF, 0xCD, 0xAB, 0x89, 0x67, 0x45, 0x23, 0x01 });
    }

    [Test]
    public async Task WriteSingleLE_roundtrips_via_BitConverter()
    {
        var bytes = Capture(w => Writers.WriteSingleLE(3.14f, w));
        var expected = BitConverter.GetBytes(3.14f);
        if (!BitConverter.IsLittleEndian) Array.Reverse(expected);
        await Assert.That(bytes).IsEquivalentTo(expected);
    }

    [Test]
    public async Task WriteDoubleLE_roundtrips_via_BitConverter()
    {
        var bytes = Capture(w => Writers.WriteDoubleLE(2.71828, w));
        var expected = BitConverter.GetBytes(2.71828);
        if (!BitConverter.IsLittleEndian) Array.Reverse(expected);
        await Assert.That(bytes).IsEquivalentTo(expected);
    }

    [Test]
    [Arguments(0u, new byte[] { 0x00 })]
    [Arguments(1u, new byte[] { 0x01 })]
    [Arguments(127u, new byte[] { 0x7F })]
    [Arguments(128u, new byte[] { 0x80, 0x01 })]
    [Arguments(300u, new byte[] { 0xAC, 0x02 })]
    [Arguments(0xFFFFFFFFu, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x0F })]
    public async Task WriteVarUInt32_matches_LEB128(uint value, byte[] expected)
    {
        var bytes = Capture(w => Writers.WriteVarUInt32(value, w));
        await Assert.That(bytes).IsEquivalentTo(expected);
    }

    [Test]
    public async Task ThrowNullRequired_throws_RpcSerializationException()
    {
        await Assert.That(() => Writers.ThrowNullRequired("Foo"))
            .Throws<RpcSerializationException>()
            .WithMessageContaining("Foo");
    }
}
```

- [ ] **Step 2: Запустить — упадут**

```
dotnet test XPacketRpc.Tests --filter "FullyQualifiedName~WritersPrimitivesTests"
```

- [ ] **Step 3: Реализовать**

`XPacketRpc/Internal/Writers.cs`:

```csharp
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace XPacketRpc.Internal;

public static class Writers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteByte(byte value, IBufferWriter<byte> w)
    {
        var span = w.GetSpan(1);
        span[0] = value;
        w.Advance(1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteInt16LE(short value, IBufferWriter<byte> w)
    {
        var span = w.GetSpan(2);
        BinaryPrimitives.WriteInt16LittleEndian(span, value);
        w.Advance(2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUInt16LE(ushort value, IBufferWriter<byte> w)
    {
        var span = w.GetSpan(2);
        BinaryPrimitives.WriteUInt16LittleEndian(span, value);
        w.Advance(2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteInt32LE(int value, IBufferWriter<byte> w)
    {
        var span = w.GetSpan(4);
        BinaryPrimitives.WriteInt32LittleEndian(span, value);
        w.Advance(4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUInt32LE(uint value, IBufferWriter<byte> w)
    {
        var span = w.GetSpan(4);
        BinaryPrimitives.WriteUInt32LittleEndian(span, value);
        w.Advance(4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteInt64LE(long value, IBufferWriter<byte> w)
    {
        var span = w.GetSpan(8);
        BinaryPrimitives.WriteInt64LittleEndian(span, value);
        w.Advance(8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUInt64LE(ulong value, IBufferWriter<byte> w)
    {
        var span = w.GetSpan(8);
        BinaryPrimitives.WriteUInt64LittleEndian(span, value);
        w.Advance(8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteSingleLE(float value, IBufferWriter<byte> w)
    {
        WriteInt32LE(BitConverter.SingleToInt32Bits(value), w);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteDoubleLE(double value, IBufferWriter<byte> w)
    {
        WriteInt64LE(BitConverter.DoubleToInt64Bits(value), w);
    }

    public static void WriteVarUInt32(uint value, IBufferWriter<byte> w)
    {
        // LEB128 unsigned. До 5 байт.
        var span = w.GetSpan(5);
        int i = 0;
        while (value >= 0x80)
        {
            span[i++] = (byte)(value | 0x80);
            value >>= 7;
        }
        span[i++] = (byte)value;
        w.Advance(i);
    }

    [DoesNotReturn]
    public static void ThrowNullRequired(string fieldName)
        => throw new RpcSerializationException($"Field '{fieldName}' is non-nullable but value was null.");
}
```

- [ ] **Step 4: Прогнать тесты**

```
dotnet test XPacketRpc.Tests --filter "FullyQualifiedName~WritersPrimitivesTests"
```

Expected: 14 tests passed (6 primitives + 6 varuint cases + 1 throw + 1 byte).

- [ ] **Step 5: Commit**

```
git add XPacketRpc/Internal/Writers.cs XPacketRpc.Tests/WritersPrimitivesTests.cs
git commit -m "feat(runtime): add primitive Writers (LE) + VarUInt32 LEB128 + ThrowNullRequired"
```

---

### Task 2.3: `Writers` — variable types (string, bytes, Guid, DateTime, decimal)

**Files:**
- Modify: `XPacketRpc/Internal/Writers.cs` (append new methods)
- Create: `XPacketRpc.Tests/WritersVariableTests.cs`

- [ ] **Step 1: Failing tests**

`XPacketRpc.Tests/WritersVariableTests.cs`:

```csharp
using System.Buffers;
using System.Text;
using XPacketRpc.Internal;

namespace XPacketRpc.Tests;

public class WritersVariableTests
{
    private static byte[] Capture(Action<PooledBufferWriter> action)
    {
        using var w = new PooledBufferWriter(ArrayPool<byte>.Shared, 64);
        action(w);
        return w.WrittenSpan.ToArray();
    }

    [Test]
    public async Task WriteString_empty_emits_single_zero_byte()
    {
        var bytes = Capture(w => Writers.WriteString("", w));
        await Assert.That(bytes).IsEquivalentTo(new byte[] { 0x00 });
    }

    [Test]
    public async Task WriteString_ascii_writes_varint_then_utf8()
    {
        var bytes = Capture(w => Writers.WriteString("Bob", w));
        await Assert.That(bytes).IsEquivalentTo(new byte[] { 0x03, 0x42, 0x6F, 0x62 });
    }

    [Test]
    public async Task WriteString_unicode_BMP_writes_correct_byte_count()
    {
        var bytes = Capture(w => Writers.WriteString("Привет", w));
        var utf8 = Encoding.UTF8.GetBytes("Привет");
        await Assert.That(bytes.Length).IsEqualTo(utf8.Length + 1);
        await Assert.That(bytes[0]).IsEqualTo((byte)utf8.Length);
        await Assert.That(bytes.AsSpan(1).ToArray()).IsEquivalentTo(utf8);
    }

    [Test]
    public async Task WriteBytes_empty_emits_zero_length_only()
    {
        var bytes = Capture(w => Writers.WriteBytes(Array.Empty<byte>(), w));
        await Assert.That(bytes).IsEquivalentTo(new byte[] { 0x00 });
    }

    [Test]
    public async Task WriteBytes_writes_varint_then_raw()
    {
        var bytes = Capture(w => Writers.WriteBytes(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, w));
        await Assert.That(bytes).IsEquivalentTo(new byte[] { 0x04, 0xDE, 0xAD, 0xBE, 0xEF });
    }

    [Test]
    public async Task WriteGuid_emits_16_bytes()
    {
        var g = Guid.Parse("01020304-0506-0708-090A-0B0C0D0E0F10");
        var bytes = Capture(w => Writers.WriteGuid(g, w));
        await Assert.That(bytes.Length).IsEqualTo(16);
    }

    [Test]
    public async Task WriteDateTime_emits_ticks_plus_kind()
    {
        var dt = new DateTime(2026, 1, 15, 12, 30, 45, DateTimeKind.Utc);
        var bytes = Capture(w => Writers.WriteDateTime(dt, w));
        await Assert.That(bytes.Length).IsEqualTo(9);
        await Assert.That(bytes[8]).IsEqualTo((byte)DateTimeKind.Utc);
    }

    [Test]
    public async Task WriteDateTimeOffset_emits_ticks_plus_offset_minutes()
    {
        var dto = new DateTimeOffset(2026, 1, 15, 12, 30, 45, TimeSpan.FromMinutes(180));
        var bytes = Capture(w => Writers.WriteDateTimeOffset(dto, w));
        await Assert.That(bytes.Length).IsEqualTo(10);
    }

    [Test]
    public async Task WriteTimeSpan_emits_ticks()
    {
        var bytes = Capture(w => Writers.WriteTimeSpan(TimeSpan.FromSeconds(7), w));
        await Assert.That(bytes.Length).IsEqualTo(8);
    }

    [Test]
    public async Task WriteDecimalLE_emits_16_bytes()
    {
        var bytes = Capture(w => Writers.WriteDecimalLE(123.456m, w));
        await Assert.That(bytes.Length).IsEqualTo(16);
    }

    [Test]
    public async Task WriteDecimalLE_handles_negative()
    {
        var bytes = Capture(w => Writers.WriteDecimalLE(-1m, w));
        await Assert.That(bytes.Length).IsEqualTo(16);
    }
}
```

- [ ] **Step 2: Запустить — упадут**

```
dotnet test XPacketRpc.Tests --filter "FullyQualifiedName~WritersVariableTests"
```

- [ ] **Step 3: Расширить `Writers.cs` (append методы перед `ThrowNullRequired`)**

Добавьте в `XPacketRpc/Internal/Writers.cs` следующие методы (поместите ПОСЛЕ `WriteVarUInt32` и ПЕРЕД `ThrowNullRequired`):

```csharp
    public static void WriteString(string value, IBufferWriter<byte> w)
    {
        // varint(byteLength) + UTF-8 bytes
        int byteCount = System.Text.Encoding.UTF8.GetByteCount(value);
        WriteVarUInt32((uint)byteCount, w);
        if (byteCount == 0) return;
        var span = w.GetSpan(byteCount);
        System.Text.Encoding.UTF8.GetBytes(value, span);
        w.Advance(byteCount);
    }

    public static void WriteBytes(byte[] value, IBufferWriter<byte> w)
    {
        WriteVarUInt32((uint)value.Length, w);
        if (value.Length == 0) return;
        var span = w.GetSpan(value.Length);
        value.CopyTo(span);
        w.Advance(value.Length);
    }

    public static void WriteGuid(Guid value, IBufferWriter<byte> w)
    {
        var span = w.GetSpan(16);
        if (!value.TryWriteBytes(span, bigEndian: false, out _))
            throw new RpcSerializationException("Guid.TryWriteBytes failed (unexpected).");
        w.Advance(16);
    }

    public static void WriteDateTime(DateTime value, IBufferWriter<byte> w)
    {
        WriteInt64LE(value.Ticks, w);
        WriteByte((byte)value.Kind, w);
    }

    public static void WriteDateTimeOffset(DateTimeOffset value, IBufferWriter<byte> w)
    {
        WriteInt64LE(value.Ticks, w);
        WriteInt16LE((short)value.Offset.TotalMinutes, w);
    }

    public static void WriteTimeSpan(TimeSpan value, IBufferWriter<byte> w)
    {
        WriteInt64LE(value.Ticks, w);
    }

    public static void WriteDecimalLE(decimal value, IBufferWriter<byte> w)
    {
        Span<int> bits = stackalloc int[4];
        decimal.GetBits(value, bits);
        for (int i = 0; i < 4; i++) WriteInt32LE(bits[i], w);
    }
```

- [ ] **Step 4: Прогнать тесты**

```
dotnet test XPacketRpc.Tests --filter "FullyQualifiedName~WritersVariableTests"
```

Expected: 11 tests passed.

- [ ] **Step 5: Commit**

```
git add XPacketRpc/Internal/Writers.cs XPacketRpc.Tests/WritersVariableTests.cs
git commit -m "feat(runtime): add variable Writers (string, bytes, Guid, DateTime, decimal)"
```

---

## Phase 3 — Runtime: XPRpcReader

### Task 3.1: `XPRpcReader` — primitive readers

**Files:**
- Create: `XPacketRpc/XPRpcReader.cs` (initial — primitives only; variable types в Task 3.2)
- Create: `XPacketRpc.Tests/XPRpcReaderPrimitivesTests.cs`

- [ ] **Step 1: Failing tests**

`XPacketRpc.Tests/XPRpcReaderPrimitivesTests.cs`:

```csharp
using XPacketRpc;

namespace XPacketRpc.Tests;

public class XPRpcReaderPrimitivesTests
{
    [Test]
    public async Task ReadByte_returns_value_and_advances()
    {
        var r = new XPRpcReader(new byte[] { 0xAB, 0xCD });

        await Assert.That(r.ReadByte()).IsEqualTo((byte)0xAB);
        await Assert.That(r.Position).IsEqualTo(1);
        await Assert.That(r.Remaining).IsEqualTo(1);
    }

    [Test]
    public async Task ReadInt16_reads_little_endian()
    {
        var r = new XPRpcReader(new byte[] { 0xFE, 0xCA });
        await Assert.That(r.ReadInt16()).IsEqualTo(unchecked((short)0xCAFE));
    }

    [Test]
    public async Task ReadInt32_reads_little_endian()
    {
        var r = new XPRpcReader(new byte[] { 0x78, 0x56, 0x34, 0x12 });
        await Assert.That(r.ReadInt32()).IsEqualTo(0x12345678);
    }

    [Test]
    public async Task ReadInt64_reads_little_endian()
    {
        var r = new XPRpcReader(new byte[] { 0xEF, 0xCD, 0xAB, 0x89, 0x67, 0x45, 0x23, 0x01 });
        await Assert.That(r.ReadInt64()).IsEqualTo(0x0123_4567_89AB_CDEF);
    }

    [Test]
    public async Task ReadSingle_roundtrips_float()
    {
        var w = BitConverter.GetBytes(3.14f);
        if (!BitConverter.IsLittleEndian) Array.Reverse(w);

        var r = new XPRpcReader(w);
        await Assert.That(r.ReadSingle()).IsEqualTo(3.14f);
    }

    [Test]
    public async Task ReadDouble_roundtrips_double()
    {
        var w = BitConverter.GetBytes(2.71828);
        if (!BitConverter.IsLittleEndian) Array.Reverse(w);

        var r = new XPRpcReader(w);
        await Assert.That(r.ReadDouble()).IsEqualTo(2.71828);
    }

    [Test]
    [Arguments(new byte[] { 0x00 }, 0u)]
    [Arguments(new byte[] { 0x01 }, 1u)]
    [Arguments(new byte[] { 0x7F }, 127u)]
    [Arguments(new byte[] { 0x80, 0x01 }, 128u)]
    [Arguments(new byte[] { 0xAC, 0x02 }, 300u)]
    [Arguments(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x0F }, 0xFFFFFFFFu)]
    public async Task ReadVarUInt32_decodes_LEB128(byte[] input, uint expected)
    {
        var r = new XPRpcReader(input);
        await Assert.That(r.ReadVarUInt32()).IsEqualTo(expected);
    }

    [Test]
    public async Task ReadByte_past_end_throws()
    {
        var r = new XPRpcReader(Array.Empty<byte>());
        await Assert.That(() => r.ReadByte()).Throws<RpcSerializationException>();
    }

    [Test]
    public async Task ReadVarUInt32_overlong_throws()
    {
        // 6+ continuation bytes — invalid for uint32
        var r = new XPRpcReader(new byte[] { 0x80, 0x80, 0x80, 0x80, 0x80, 0x01 });
        await Assert.That(() => r.ReadVarUInt32()).Throws<RpcSerializationException>();
    }
}
```

- [ ] **Step 2: Запустить — упадут**

```
dotnet test XPacketRpc.Tests --filter "FullyQualifiedName~XPRpcReaderPrimitivesTests"
```

- [ ] **Step 3: Реализовать `XPRpcReader` (primitives only)**

`XPacketRpc/XPRpcReader.cs`:

```csharp
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace XPacketRpc;

public ref struct XPRpcReader
{
    private readonly ReadOnlySpan<byte> source;
    private int position;

    public XPRpcReader(ReadOnlySpan<byte> source)
    {
        this.source = source;
        this.position = 0;
    }

    public int Position => this.position;
    public int Remaining => this.source.Length - this.position;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte ReadByte()
    {
        EnsureAvailable(1);
        return this.source[this.position++];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short ReadInt16()
    {
        EnsureAvailable(2);
        var v = BinaryPrimitives.ReadInt16LittleEndian(this.source.Slice(this.position));
        this.position += 2;
        return v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort ReadUInt16()
    {
        EnsureAvailable(2);
        var v = BinaryPrimitives.ReadUInt16LittleEndian(this.source.Slice(this.position));
        this.position += 2;
        return v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadInt32()
    {
        EnsureAvailable(4);
        var v = BinaryPrimitives.ReadInt32LittleEndian(this.source.Slice(this.position));
        this.position += 4;
        return v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadUInt32()
    {
        EnsureAvailable(4);
        var v = BinaryPrimitives.ReadUInt32LittleEndian(this.source.Slice(this.position));
        this.position += 4;
        return v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadInt64()
    {
        EnsureAvailable(8);
        var v = BinaryPrimitives.ReadInt64LittleEndian(this.source.Slice(this.position));
        this.position += 8;
        return v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong ReadUInt64()
    {
        EnsureAvailable(8);
        var v = BinaryPrimitives.ReadUInt64LittleEndian(this.source.Slice(this.position));
        this.position += 8;
        return v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ReadSingle() => BitConverter.Int32BitsToSingle(ReadInt32());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ReadDouble() => BitConverter.Int64BitsToDouble(ReadInt64());

    public uint ReadVarUInt32()
    {
        uint result = 0;
        int shift = 0;
        while (true)
        {
            if (shift >= 35)
                throw new RpcSerializationException("VarUInt32 is overlong (more than 5 bytes).");

            byte b = ReadByte();
            result |= (uint)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) return result;
            shift += 7;
        }
    }

    private void EnsureAvailable(int count)
    {
        if (this.position + count > this.source.Length)
            throw new RpcSerializationException(
                $"Unexpected end of payload (need {count} bytes at position {this.position}, " +
                $"only {this.source.Length - this.position} remaining).");
    }
}
```

- [ ] **Step 4: Прогнать тесты**

```
dotnet test XPacketRpc.Tests --filter "FullyQualifiedName~XPRpcReaderPrimitivesTests"
```

Expected: 14 tests passed (1 byte + 1 int16 + 1 int32 + 1 int64 + 1 single + 1 double + 6 varuint cases + 2 errors + 1 byte-position).

- [ ] **Step 5: Commit**

```
git add XPacketRpc/XPRpcReader.cs XPacketRpc.Tests/XPRpcReaderPrimitivesTests.cs
git commit -m "feat(runtime): add XPRpcReader primitive readers (LE) + VarUInt32"
```

---

### Task 3.2: `XPRpcReader` — variable types (string, bytes, Guid, DateTime, decimal)

**Files:**
- Modify: `XPacketRpc/XPRpcReader.cs` (append методы)
- Create: `XPacketRpc.Tests/XPRpcReaderVariableTests.cs`

- [ ] **Step 1: Failing tests**

`XPacketRpc.Tests/XPRpcReaderVariableTests.cs`:

```csharp
using System.Buffers;
using System.Text;
using XPacketRpc;
using XPacketRpc.Internal;

namespace XPacketRpc.Tests;

public class XPRpcReaderVariableTests
{
    private static byte[] Encode(Action<PooledBufferWriter> a)
    {
        using var w = new PooledBufferWriter(ArrayPool<byte>.Shared, 64);
        a(w);
        return w.WrittenSpan.ToArray();
    }

    [Test]
    public async Task ReadString_empty()
    {
        var bytes = Encode(w => Writers.WriteString("", w));
        var r = new XPRpcReader(bytes);
        await Assert.That(r.ReadString()).IsEqualTo("");
    }

    [Test]
    public async Task ReadString_ascii()
    {
        var bytes = Encode(w => Writers.WriteString("Bob", w));
        var r = new XPRpcReader(bytes);
        await Assert.That(r.ReadString()).IsEqualTo("Bob");
    }

    [Test]
    public async Task ReadString_unicode_BMP()
    {
        var bytes = Encode(w => Writers.WriteString("Привет, мир!", w));
        var r = new XPRpcReader(bytes);
        await Assert.That(r.ReadString()).IsEqualTo("Привет, мир!");
    }

    [Test]
    public async Task ReadString_unicode_supplementary()
    {
        var s = "😀 emoji";  // 😀
        var bytes = Encode(w => Writers.WriteString(s, w));
        var r = new XPRpcReader(bytes);
        await Assert.That(r.ReadString()).IsEqualTo(s);
    }

    [Test]
    public async Task ReadBytes_roundtrips()
    {
        var input = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var bytes = Encode(w => Writers.WriteBytes(input, w));
        var r = new XPRpcReader(bytes);
        await Assert.That(r.ReadBytes()).IsEquivalentTo(input);
    }

    [Test]
    public async Task ReadBytes_empty()
    {
        var bytes = Encode(w => Writers.WriteBytes(Array.Empty<byte>(), w));
        var r = new XPRpcReader(bytes);
        await Assert.That(r.ReadBytes()).IsEquivalentTo(Array.Empty<byte>());
    }

    [Test]
    public async Task ReadGuid_roundtrips()
    {
        var g = Guid.Parse("01020304-0506-0708-090A-0B0C0D0E0F10");
        var bytes = Encode(w => Writers.WriteGuid(g, w));
        var r = new XPRpcReader(bytes);
        await Assert.That(r.ReadGuid()).IsEqualTo(g);
    }

    [Test]
    public async Task ReadDateTime_roundtrips_with_kind()
    {
        var dt = new DateTime(2026, 1, 15, 12, 30, 45, DateTimeKind.Utc);
        var bytes = Encode(w => Writers.WriteDateTime(dt, w));
        var r = new XPRpcReader(bytes);
        var got = r.ReadDateTime();

        await Assert.That(got).IsEqualTo(dt);
        await Assert.That(got.Kind).IsEqualTo(DateTimeKind.Utc);
    }

    [Test]
    public async Task ReadDateTimeOffset_roundtrips_with_offset()
    {
        var dto = new DateTimeOffset(2026, 1, 15, 12, 30, 45, TimeSpan.FromMinutes(180));
        var bytes = Encode(w => Writers.WriteDateTimeOffset(dto, w));
        var r = new XPRpcReader(bytes);
        await Assert.That(r.ReadDateTimeOffset()).IsEqualTo(dto);
    }

    [Test]
    public async Task ReadTimeSpan_roundtrips()
    {
        var ts = TimeSpan.FromSeconds(7);
        var bytes = Encode(w => Writers.WriteTimeSpan(ts, w));
        var r = new XPRpcReader(bytes);
        await Assert.That(r.ReadTimeSpan()).IsEqualTo(ts);
    }

    [Test]
    [Arguments("123.456")]
    [Arguments("-1")]
    [Arguments("0")]
    [Arguments("79228162514264337593543950335")]    // decimal.MaxValue
    [Arguments("-79228162514264337593543950335")]   // decimal.MinValue
    public async Task ReadDecimal_roundtrips_signed_and_extremes(string s)
    {
        var d = decimal.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        var bytes = Encode(w => Writers.WriteDecimalLE(d, w));
        var r = new XPRpcReader(bytes);
        await Assert.That(r.ReadDecimal()).IsEqualTo(d);
    }
}
```

- [ ] **Step 2: Запустить — упадут**

```
dotnet test XPacketRpc.Tests --filter "FullyQualifiedName~XPRpcReaderVariableTests"
```

- [ ] **Step 3: Дописать `XPRpcReader.cs` (append методы)**

В конец `XPacketRpc/XPRpcReader.cs` (перед закрывающей `}` структуры):

```csharp
    public string ReadString()
    {
        uint length = ReadVarUInt32();
        if (length == 0) return string.Empty;
        EnsureAvailable((int)length);
        var slice = this.source.Slice(this.position, (int)length);
        this.position += (int)length;
        return System.Text.Encoding.UTF8.GetString(slice);
    }

    public byte[] ReadBytes()
    {
        uint length = ReadVarUInt32();
        if (length == 0) return Array.Empty<byte>();
        EnsureAvailable((int)length);
        var arr = this.source.Slice(this.position, (int)length).ToArray();
        this.position += (int)length;
        return arr;
    }

    public Guid ReadGuid()
    {
        EnsureAvailable(16);
        var slice = this.source.Slice(this.position, 16);
        this.position += 16;
        return new Guid(slice, bigEndian: false);
    }

    public DateTime ReadDateTime()
    {
        long ticks = ReadInt64();
        byte kind = ReadByte();
        return new DateTime(ticks, (DateTimeKind)kind);
    }

    public DateTimeOffset ReadDateTimeOffset()
    {
        long ticks = ReadInt64();
        short minutes = ReadInt16();
        return new DateTimeOffset(ticks, TimeSpan.FromMinutes(minutes));
    }

    public TimeSpan ReadTimeSpan() => new(ReadInt64());

    public decimal ReadDecimal()
    {
        Span<int> bits = stackalloc int[4];
        bits[0] = ReadInt32();
        bits[1] = ReadInt32();
        bits[2] = ReadInt32();
        bits[3] = ReadInt32();
        return new decimal(bits);
    }
```

- [ ] **Step 4: Прогнать тесты**

```
dotnet test XPacketRpc.Tests --filter "FullyQualifiedName~XPRpcReaderVariableTests"
```

Expected: 15 tests passed (4 string + 2 bytes + 1 guid + 1 datetime + 1 dto + 1 timespan + 5 decimal).

- [ ] **Step 5: Commit**

```
git add XPacketRpc/XPRpcReader.cs XPacketRpc.Tests/XPRpcReaderVariableTests.cs
git commit -m "feat(runtime): add XPRpcReader variable readers (string, bytes, Guid, DateTime, decimal)"
```

---

## Phase 4 — Runtime: facade

### Task 4.1: `IRpcSerializer` interface + `XPRpc` skeleton (registry, Touch, Write/Read с ошибкой при miss)

**Files:**
- Create: `XPacketRpc/IRpcSerializer.cs`
- Create: `XPacketRpc/XPRpc.cs`
- Create: `XPacketRpc.Tests/XPRpcRegistryTests.cs`

- [ ] **Step 1: Failing tests**

`XPacketRpc.Tests/XPRpcRegistryTests.cs`:

```csharp
using System.Buffers;
using XPacketRpc;
using XPacketRpc.Internal;

namespace XPacketRpc.Tests;

public class XPRpcRegistryTests
{
    private sealed class Probe { public int X; }

    [Test]
    public async Task Touch_is_no_op_and_does_not_throw()
    {
        XPRpc.Touch<Probe>();
        await Assert.That(true).IsTrue();
    }

    [Test]
    public async Task Write_unregistered_throws_MissingSerializer()
    {
        using var buf = new PooledBufferWriter(ArrayPool<byte>.Shared);
        await Assert.That(() => XPRpc.Write(new Probe(), buf))
            .Throws<MissingSerializerException>()
            .Where(e => e.MissingType == typeof(Probe));
    }

    [Test]
    public async Task Read_unregistered_throws_MissingSerializer()
    {
        await Assert.That(() => { var r = new ReadOnlySpan<byte>(new byte[1]); _ = XPRpc.Read<Probe>(r); })
            .Throws<MissingSerializerException>()
            .Where(e => e.MissingType == typeof(Probe));
    }

    [Test]
    public async Task Register_then_Write_invokes_delegate()
    {
        bool called = false;
        XPRpc.Register<Probe>(
            (v, w) => { called = true; },
            (ref XPRpcReader r) => new Probe());

        using var buf = new PooledBufferWriter(ArrayPool<byte>.Shared);
        XPRpc.Write(new Probe(), buf);

        await Assert.That(called).IsTrue();
    }
}
```

> **Note:** для теста `Read_unregistered_throws_MissingSerializer` используем lambda + `Span` локально, поскольку `ReadOnlySpan<byte>` нельзя capture'ить в async-context. Если TUnit жалуется — смените на синхронный паттерн через wrapper-метод.

- [ ] **Step 2: Реализовать `IRpcSerializer`**

`XPacketRpc/IRpcSerializer.cs`:

```csharp
namespace XPacketRpc;

public interface IRpcSerializer
{
    string ContentType { get; }
    byte[] Serialize<T>(T value);
    T? Deserialize<T>(ReadOnlyMemory<byte> payload);
}
```

- [ ] **Step 3: Реализовать `XPRpc`**

`XPacketRpc/XPRpc.cs`:

```csharp
using System.Buffers;
using System.Collections.Concurrent;
using System.ComponentModel;

namespace XPacketRpc;

public static class XPRpc
{
    public delegate void WriteDelegate<T>(T value, IBufferWriter<byte> writer);
    public delegate T ReadDelegate<T>(ref XPRpcReader reader);

    private static readonly ConcurrentDictionary<Type, object> writers = new();
    private static readonly ConcurrentDictionary<Type, object> readers = new();

    /// <summary>
    /// No-op. Существует только чтобы source generator увидел T в call-site analysis
    /// и сгенерировал код. Вызывайте в startup для типов, разрешаемых через MakeGenericMethod.
    /// </summary>
    public static void Touch<T>() { /* no-op */ }

    /// <summary>
    /// Public для использования из generated module-initializer'ов в произвольных consumer-сборках.
    /// Не вызывайте напрямую — generator делает это автоматически.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void Register<T>(WriteDelegate<T> write, ReadDelegate<T> read)
    {
        ArgumentNullException.ThrowIfNull(write);
        ArgumentNullException.ThrowIfNull(read);
        writers[typeof(T)] = write;
        readers[typeof(T)] = read;
    }

    public static void Write<T>(T value, IBufferWriter<byte> writer)
    {
        if (!writers.TryGetValue(typeof(T), out var w))
            throw new MissingSerializerException(typeof(T));
        ((WriteDelegate<T>)w)(value, writer);
    }

    public static T? Read<T>(ReadOnlySpan<byte> source)
    {
        if (!readers.TryGetValue(typeof(T), out var r))
            throw new MissingSerializerException(typeof(T));
        var reader = new XPRpcReader(source);
        return ((ReadDelegate<T>)r)(ref reader);
    }
}
```

- [ ] **Step 4: Прогнать тесты**

```
dotnet test XPacketRpc.Tests --filter "FullyQualifiedName~XPRpcRegistryTests"
```

Expected: 4 tests passed.

- [ ] **Step 5: Commit**

```
git add XPacketRpc/IRpcSerializer.cs XPacketRpc/XPRpc.cs XPacketRpc.Tests/XPRpcRegistryTests.cs
git commit -m "feat(runtime): add IRpcSerializer interface and XPRpc registry/dispatch"
```

---

### Task 4.2: `XPacketRpcSerializer` facade + manual-roundtrip integration test

**Files:**
- Create: `XPacketRpc/XPacketRpcSerializer.cs`
- Create: `XPacketRpc.Tests/XPacketRpcSerializerTests.cs`

- [ ] **Step 1: Failing tests (используем manual-registered тип, чтобы протестировать фасад без генератора)**

`XPacketRpc.Tests/XPacketRpcSerializerTests.cs`:

```csharp
using XPacketRpc;
using XPacketRpc.Internal;

namespace XPacketRpc.Tests;

public class XPacketRpcSerializerTests
{
    private sealed class TinyDto { public int Id; public string Name = ""; }

    private static void RegisterTinyDto()
    {
        XPRpc.Register<TinyDto>(
            (v, w) =>
            {
                Writers.WriteInt32LE(v.Id, w);
                Writers.WriteString(v.Name, w);
            },
            (ref XPRpcReader r) => new TinyDto { Id = r.ReadInt32(), Name = r.ReadString() });
    }

    [Test]
    public async Task ContentType_is_application_x_xprotocol_rpc()
    {
        var s = new XPacketRpcSerializer();
        await Assert.That(s.ContentType).IsEqualTo("application/x-xprotocol-rpc");
        await Assert.That(XPacketRpcSerializer.XPacketRpcContentType).IsEqualTo("application/x-xprotocol-rpc");
    }

    [Test]
    public async Task Serialize_null_throws_ArgumentNullException()
    {
        var s = new XPacketRpcSerializer();
        await Assert.That(() => s.Serialize<string>(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Roundtrip_via_facade()
    {
        RegisterTinyDto();
        var s = new XPacketRpcSerializer();
        var input = new TinyDto { Id = 42, Name = "Hello" };

        byte[] bytes = s.Serialize(input);
        var got = s.Deserialize<TinyDto>(bytes);

        await Assert.That(got).IsNotNull();
        await Assert.That(got!.Id).IsEqualTo(42);
        await Assert.That(got.Name).IsEqualTo("Hello");
    }

    [Test]
    public async Task Deserialize_returns_default_for_empty_value_type_payload_throws()
    {
        var s = new XPacketRpcSerializer();
        // unregistered T → MissingSerializerException
        await Assert.That(() => s.Deserialize<TinyDto>(ReadOnlyMemory<byte>.Empty))
            .Throws<MissingSerializerException>();
    }
}
```

- [ ] **Step 2: Реализовать facade**

`XPacketRpc/XPacketRpcSerializer.cs`:

```csharp
using System.Buffers;
using XPacketRpc.Internal;

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

- [ ] **Step 3: Прогнать тесты**

```
dotnet test XPacketRpc.Tests --filter "FullyQualifiedName~XPacketRpcSerializerTests"
```

Expected: 4 tests passed.

- [ ] **Step 4: Commit**

```
git add XPacketRpc/XPacketRpcSerializer.cs XPacketRpc.Tests/XPacketRpcSerializerTests.cs
git commit -m "feat(runtime): add XPacketRpcSerializer facade implementing IRpcSerializer"
```

---

### Task 4.3: Полный smoke-build решения

Проверка, что всё собирается после Phase 1-4 (без генератора пока).

- [ ] **Step 1: Полный build**

```
dotnet build TCPProtocol.sln -c Debug
```

Expected: `Build succeeded`. Все 9 проектов собираются.

- [ ] **Step 2: Полный test-run**

```
dotnet test TCPProtocol.sln -c Debug
```

Expected: все XPacketRpc.Tests passed (50+ тестов в этой фазе).

- [ ] **Step 3: Commit (если есть какие-то forgotten файлы)**

```
git status
# если ничего не изменилось — пропустить
```

---

## Phase 5 — Generator: scaffolding

### Task 5.1: `IIncrementalGenerator` skeleton + emit marker file для smoke-теста

**Files:**
- Create: `XPacketRpc.Generators/XPacketRpcGenerator.cs`
- Delete: `XPacketRpc.Generators/_Stub.cs`
- Create: `XPacketRpc.Tests/GeneratorSmokeTests.cs`

- [ ] **Step 1: Failing test (проверяем, что генератор активирован и эмитит marker)**

`XPacketRpc.Tests/GeneratorSmokeTests.cs`:

```csharp
namespace XPacketRpc.Tests;

public class GeneratorSmokeTests
{
    [Test]
    public async Task Generator_emits_marker_type()
    {
        // Если генератор работает, тип XPacketRpc.Generated.__GeneratorMarker
        // существует в текущей сборке (через generated source).
        var marker = Type.GetType("XPacketRpc.Generated.__GeneratorMarker, XPacketRpc.Tests");
        await Assert.That(marker).IsNotNull();
    }
}
```

- [ ] **Step 2: Запустить — упадёт (генератор пустой)**

```
dotnet test XPacketRpc.Tests --filter "FullyQualifiedName~GeneratorSmokeTests"
```

- [ ] **Step 3: Реализовать `XPacketRpcGenerator` (минимум — marker)**

`XPacketRpc.Generators/XPacketRpcGenerator.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace XPacketRpc.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class XPacketRpcGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Phase 5.1 — только marker. Реальное обнаружение/эмит — Phases 6-9.
        context.RegisterPostInitializationOutput(static ctx =>
        {
            const string source = """
                // <auto-generated/>
                namespace XPacketRpc.Generated;

                internal static class __GeneratorMarker
                {
                    public const string Version = "0.1";
                }
                """;
            ctx.AddSource("__GeneratorMarker.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }
}
```

- [ ] **Step 4: Удалить stub**

```
git rm XPacketRpc.Generators/_Stub.cs
```

- [ ] **Step 5: Build + test**

```
dotnet build XPacketRpc.Generators -c Debug
dotnet test XPacketRpc.Tests --filter "FullyQualifiedName~GeneratorSmokeTests"
```

Expected: marker test passes.

- [ ] **Step 6: Commit**

```
git add XPacketRpc.Generators/XPacketRpcGenerator.cs XPacketRpc.Tests/GeneratorSmokeTests.cs
git commit -m "feat(generator): add IIncrementalGenerator skeleton emitting marker type"
```

---

### Task 5.2: Diagnostic descriptors `XPRPC001..XPRPC006`

**Files:**
- Create: `XPacketRpc.Generators/Diagnostics/Descriptors.cs`

(Тестов для этого таска нет — диагностики тестируются в составе detection-логики позже.)

- [ ] **Step 1: Реализовать**

`XPacketRpc.Generators/Diagnostics/Descriptors.cs`:

```csharp
using Microsoft.CodeAnalysis;

namespace XPacketRpc.Generators.Diagnostics;

internal static class Descriptors
{
    private const string Category = "XPacketRpc";

    public static readonly DiagnosticDescriptor OpenGenericCallSite = new(
        id: "XPRPC001",
        title: "Open-generic call-site cannot be resolved",
        messageFormat: "Open-generic call-site for '{0}': T '{1}' cannot be resolved at compile-time. " +
                       "Add 'XPRpc.Touch<ConcreteType>()' in startup so the source generator can emit code.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor OpenGenericType = new(
        id: "XPRPC002",
        title: "Open-generic type in transitive closure",
        messageFormat: "Open-generic type '{0}' reached in transitive closure of DTO '{1}'. " +
                       "Sourcegen requires closed types.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor CannotConstructType = new(
        id: "XPRPC003",
        title: "Cannot construct type",
        messageFormat: "Cannot construct '{0}': no parameterless constructor and no constructor with " +
                       "parameters matching property names; or some required member has no setter and " +
                       "is not in any constructor.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedFieldType = new(
        id: "XPRPC004",
        title: "Unsupported field type",
        messageFormat: "Field type '{0}' of '{1}.{2}' is unsupported. " +
                       "Supported: primitives, string, Guid, DateTime, DateTimeOffset, TimeSpan, decimal, " +
                       "byte[], enums, T[], List<T>, Dictionary<K,V>, nested DTO, Nullable<T>.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor FieldHashCollision = new(
        id: "XPRPC005",
        title: "Field name collision after FNV-1a hash",
        messageFormat: "Fields '{0}' and '{1}' of '{2}' produce identical FNV-1a hash AND identical name " +
                       "(should be impossible). Rename one field.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor EmptyType = new(
        id: "XPRPC006",
        title: "Type has no serializable members",
        messageFormat: "Type '{0}' has no public fields or properties — wire payload will be empty " +
                       "(or just the nullability bitmap which is also empty).",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
```

- [ ] **Step 2: Build**

```
dotnet build XPacketRpc.Generators -c Debug
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```
git add XPacketRpc.Generators/Diagnostics/Descriptors.cs
git commit -m "feat(generator): add diagnostic descriptors XPRPC001..XPRPC006"
```

---

### Task 5.3: `IndentedStringBuilder` helper для emit'а

**Files:**
- Create: `XPacketRpc.Generators/Emit/IndentedStringBuilder.cs`
- Create: `XPacketRpc.Generators.Tests/IndentedStringBuilderTests.cs` ← **новый test-проект для генератора**

> **Note:** генератор-тесты живут в отдельном проекте `XPacketRpc.Generators.Tests`, чтобы тестировать internal-классы генератора без необходимости делать их public. Если он ещё не создан — создайте сейчас (см. step 1a).

- [ ] **Step 1a: Создать test-проект для генератора (если ещё нет)**

```
mkdir XPacketRpc.Generators.Tests
```

`XPacketRpc.Generators.Tests/XPacketRpc.Generators.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <OutputType>Exe</OutputType>
    <RootNamespace>XPacketRpc.Generators.Tests</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="TUnit" Version="1.43.11" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.11.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\XPacketRpc.Generators\XPacketRpc.Generators.csproj"
                      ReferenceOutputAssembly="true" />
  </ItemGroup>
</Project>
```

> **Note:** для test-проекта генератор референсится с `ReferenceOutputAssembly="true"` (а не как Analyzer), чтобы можно было unit-тестировать его компоненты.

В `XPacketRpc.Generators/XPacketRpc.Generators.csproj` добавьте `InternalsVisibleTo`:

```xml
<ItemGroup>
  <InternalsVisibleTo Include="XPacketRpc.Generators.Tests" />
</ItemGroup>
```

Зарегистрируйте проект в sln:

```
dotnet sln TCPProtocol.sln add XPacketRpc.Generators.Tests/XPacketRpc.Generators.Tests.csproj
```

- [ ] **Step 1: Failing test**

`XPacketRpc.Generators.Tests/IndentedStringBuilderTests.cs`:

```csharp
using XPacketRpc.Generators.Emit;

namespace XPacketRpc.Generators.Tests;

public class IndentedStringBuilderTests
{
    [Test]
    public async Task Append_writes_without_indent_at_root_level()
    {
        var sb = new IndentedStringBuilder();
        sb.AppendLine("hello");

        await Assert.That(sb.ToString()).IsEqualTo("hello\n");
    }

    [Test]
    public async Task Indent_block_adds_4_spaces_per_level()
    {
        var sb = new IndentedStringBuilder();
        sb.AppendLine("namespace Foo");
        sb.AppendLine("{");
        using (sb.Indent())
        {
            sb.AppendLine("class Bar");
            sb.AppendLine("{");
            using (sb.Indent())
            {
                sb.AppendLine("public int X;");
            }
            sb.AppendLine("}");
        }
        sb.AppendLine("}");

        var expected =
            "namespace Foo\n" +
            "{\n" +
            "    class Bar\n" +
            "    {\n" +
            "        public int X;\n" +
            "    }\n" +
            "}\n";
        await Assert.That(sb.ToString()).IsEqualTo(expected);
    }

    [Test]
    public async Task Append_without_newline_does_not_add_indent_in_middle()
    {
        var sb = new IndentedStringBuilder();
        using (sb.Indent())
        {
            sb.Append("a");
            sb.Append("b");
            sb.AppendLine();
        }
        await Assert.That(sb.ToString()).IsEqualTo("    ab\n");
    }
}
```

- [ ] **Step 2: Build + test — упадут**

```
dotnet test XPacketRpc.Generators.Tests
```

- [ ] **Step 3: Реализовать**

`XPacketRpc.Generators/Emit/IndentedStringBuilder.cs`:

```csharp
using System.Text;

namespace XPacketRpc.Generators.Emit;

internal sealed class IndentedStringBuilder
{
    private readonly StringBuilder sb = new();
    private int level;
    private bool atLineStart = true;

    public IDisposable Indent() => new IndentScope(this);

    public void Append(string text)
    {
        if (this.atLineStart) { WriteIndent(); this.atLineStart = false; }
        this.sb.Append(text);
    }

    public void AppendLine(string text)
    {
        Append(text);
        this.sb.Append('\n');
        this.atLineStart = true;
    }

    public void AppendLine()
    {
        this.sb.Append('\n');
        this.atLineStart = true;
    }

    private void WriteIndent()
    {
        for (int i = 0; i < this.level; i++) this.sb.Append("    ");
    }

    public override string ToString() => this.sb.ToString();

    private sealed class IndentScope : IDisposable
    {
        private readonly IndentedStringBuilder parent;
        public IndentScope(IndentedStringBuilder p) { this.parent = p; this.parent.level++; }
        public void Dispose() => this.parent.level--;
    }
}
```

- [ ] **Step 4: Прогнать тесты**

```
dotnet test XPacketRpc.Generators.Tests
```

Expected: 3 tests passed.

- [ ] **Step 5: Commit**

```
git add XPacketRpc.Generators.Tests/ XPacketRpc.Generators/Emit/IndentedStringBuilder.cs TCPProtocol.sln XPacketRpc.Generators/XPacketRpc.Generators.csproj
git commit -m "feat(generator): add IndentedStringBuilder + Generators.Tests project"
```

---

## Phase 6 — Generator: discovery (call-sites + transitive closure)

### Task 6.1: `CallSiteCollector` — извлечь closed T из всех 5 call-site методов

**Files:**
- Create: `XPacketRpc.Generators/Discovery/CallSiteCollector.cs`
- Create: `XPacketRpc.Generators/Discovery/DiscoveredType.cs`
- Create: `XPacketRpc.Generators.Tests/CallSiteCollectorTests.cs`

- [ ] **Step 1: Failing test (compile-time анализ через Roslyn-API)**

`XPacketRpc.Generators.Tests/CallSiteCollectorTests.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XPacketRpc.Generators.Discovery;

namespace XPacketRpc.Generators.Tests;

public class CallSiteCollectorTests
{
    private static (Compilation comp, SemanticModel model, SyntaxTree tree) Compile(string source)
    {
        var fakeXPRpc = """
            using System;
            using System.Buffers;
            namespace XPacketRpc
            {
                public interface IRpcSerializer
                {
                    string ContentType { get; }
                    byte[] Serialize<T>(T value);
                    T? Deserialize<T>(ReadOnlyMemory<byte> payload);
                }
                public ref struct XPRpcReader { public XPRpcReader(ReadOnlySpan<byte> s) { } }
                public static class XPRpc
                {
                    public delegate void WriteDelegate<T>(T value, IBufferWriter<byte> writer);
                    public delegate T ReadDelegate<T>(ref XPRpcReader reader);
                    public static void Touch<T>() {}
                    public static void Register<T>(WriteDelegate<T> w, ReadDelegate<T> r) {}
                    public static void Write<T>(T value, IBufferWriter<byte> w) {}
                    public static T? Read<T>(ReadOnlySpan<byte> source) => default;
                }
            }
            """;
        var trees = new[]
        {
            CSharpSyntaxTree.ParseText(fakeXPRpc),
            CSharpSyntaxTree.ParseText(source)
        };
        var refs = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Buffers.IBufferWriter<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.RuntimeHelpers).Assembly.Location),
        };
        var comp = CSharpCompilation.Create("test",
            trees,
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: false));
        return (comp, comp.GetSemanticModel(trees[1]), trees[1]);
    }

    [Test]
    public async Task Collects_T_from_Touch()
    {
        var src = """
            using XPacketRpc;
            public class Probe { public int X; }
            public class Foo
            {
                public static void Init() => XPRpc.Touch<Probe>();
            }
            """;
        var (comp, model, tree) = Compile(src);
        var collector = new CallSiteCollector();
        var results = collector.Collect(tree, model, default);

        await Assert.That(results.Select(r => r.Type.Name)).Contains("Probe");
    }

    [Test]
    public async Task Collects_T_from_Write_and_Read()
    {
        var src = """
            using System;
            using System.Buffers;
            using XPacketRpc;
            public class Probe { public int X; }
            public class Foo
            {
                public static void DoIt(IBufferWriter<byte> w, ReadOnlySpan<byte> s)
                {
                    XPRpc.Write<Probe>(new Probe(), w);
                    var p = XPRpc.Read<Probe>(s);
                }
            }
            """;
        var (comp, model, tree) = Compile(src);
        var collector = new CallSiteCollector();
        var results = collector.Collect(tree, model, default).ToArray();

        await Assert.That(results.Select(r => r.Type.Name).Distinct()).IsEquivalentTo(new[] { "Probe" });
    }

    [Test]
    public async Task Collects_T_from_IRpcSerializer()
    {
        var src = """
            using System;
            using XPacketRpc;
            public class Probe { public int X; }
            public class Foo
            {
                public static void DoIt(IRpcSerializer s)
                {
                    var bytes = s.Serialize<Probe>(new Probe());
                    var p = s.Deserialize<Probe>(default);
                }
            }
            """;
        var (comp, model, tree) = Compile(src);
        var collector = new CallSiteCollector();
        var results = collector.Collect(tree, model, default).ToArray();

        await Assert.That(results.Select(r => r.Type.Name).Distinct()).IsEquivalentTo(new[] { "Probe" });
    }

    [Test]
    public async Task Open_generic_T_returns_diagnostic_marker()
    {
        var src = """
            using XPacketRpc;
            public static class Foo
            {
                public static void Generic<T>() => XPRpc.Touch<T>();
            }
            """;
        var (comp, model, tree) = Compile(src);
        var collector = new CallSiteCollector();
        var results = collector.Collect(tree, model, default).ToArray();

        // Open-generic должен пометиться `IsOpen=true` и не попасть в реестр concrete-типов.
        await Assert.That(results.All(r => r.IsOpen || r.Type.Name != "T")).IsTrue();
    }
}
```

- [ ] **Step 2: Build + test — упадут**

```
dotnet test XPacketRpc.Generators.Tests --filter "FullyQualifiedName~CallSiteCollectorTests"
```

- [ ] **Step 3: Реализовать `DiscoveredType`**

`XPacketRpc.Generators/Discovery/DiscoveredType.cs`:

```csharp
using Microsoft.CodeAnalysis;

namespace XPacketRpc.Generators.Discovery;

internal sealed record DiscoveredType(
    ITypeSymbol Type,
    Location? CallSiteLocation,
    bool IsOpen);
```

- [ ] **Step 4: Реализовать `CallSiteCollector`**

`XPacketRpc.Generators/Discovery/CallSiteCollector.cs`:

```csharp
using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XPacketRpc.Generators.Discovery;

internal sealed class CallSiteCollector
{
    // Метод-имена интересующих API
    private static readonly HashSet<string> XPRpcMethods = new()
    {
        "Touch", "Write", "Read"
    };
    private static readonly HashSet<string> RpcSerializerMethods = new()
    {
        "Serialize", "Deserialize"
    };

    public IEnumerable<DiscoveredType> Collect(
        SyntaxTree tree,
        SemanticModel model,
        CancellationToken ct)
    {
        var root = tree.GetRoot(ct);
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (TryExtract(invocation, model, ct, out var discovered))
                yield return discovered;
        }
    }

    private bool TryExtract(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        CancellationToken ct,
        out DiscoveredType discovered)
    {
        discovered = null!;

        var symbolInfo = model.GetSymbolInfo(invocation, ct);
        var method = symbolInfo.Symbol as IMethodSymbol ?? symbolInfo.CandidateSymbols.FirstOrDefault() as IMethodSymbol;
        if (method is null) return false;

        if (!IsRelevant(method)) return false;
        if (method.TypeArguments.Length != 1) return false;

        var t = method.TypeArguments[0];
        bool isOpen = t.TypeKind == TypeKind.TypeParameter;

        discovered = new DiscoveredType(
            Type: t,
            CallSiteLocation: invocation.GetLocation(),
            IsOpen: isOpen);
        return true;
    }

    private static bool IsRelevant(IMethodSymbol method)
    {
        var container = method.ContainingType;
        if (container is null) return false;
        var fq = container.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        if (fq == "global::XPacketRpc.XPRpc" && XPRpcMethods.Contains(method.Name)) return true;
        if (fq == "global::XPacketRpc.IRpcSerializer" && RpcSerializerMethods.Contains(method.Name)) return true;

        // также: реализации IRpcSerializer (Serialize/Deserialize в наследниках)
        if (RpcSerializerMethods.Contains(method.Name) &&
            container.AllInterfaces.Any(i =>
                i.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::XPacketRpc.IRpcSerializer"))
        {
            return true;
        }
        return false;
    }
}
```

- [ ] **Step 5: Прогнать тесты**

```
dotnet test XPacketRpc.Generators.Tests --filter "FullyQualifiedName~CallSiteCollectorTests"
```

Expected: 4 tests passed.

- [ ] **Step 6: Commit**

```
git add XPacketRpc.Generators/Discovery/ XPacketRpc.Generators.Tests/CallSiteCollectorTests.cs
git commit -m "feat(generator): add CallSiteCollector for Touch/Write/Read/Serialize/Deserialize"
```

---

### Task 6.2: `TypeWalker` — собрать public fields + properties; transitive closure для built-in/nested/collection/dictionary

**Files:**
- Create: `XPacketRpc.Generators/Discovery/TypeWalker.cs`
- Create: `XPacketRpc.Generators/Discovery/MemberDescriptor.cs`
- Create: `XPacketRpc.Generators/Discovery/TypeKind.cs`
- Create: `XPacketRpc.Generators.Tests/TypeWalkerTests.cs`

- [ ] **Step 1: Failing test**

`XPacketRpc.Generators.Tests/TypeWalkerTests.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XPacketRpc.Generators.Discovery;

namespace XPacketRpc.Generators.Tests;

public class TypeWalkerTests
{
    private static (Compilation comp, INamedTypeSymbol root) Compile(string source, string typeName)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var refs = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Guid).Assembly.Location),
        };
        var comp = CSharpCompilation.Create("t", new[] { tree }, refs);
        var sym = comp.GetTypeByMetadataName(typeName) ?? throw new InvalidOperationException($"Type {typeName} not found");
        return (comp, sym);
    }

    [Test]
    public async Task Walks_primitive_fields_and_properties()
    {
        var src = """
            public class Foo
            {
                public int Id;
                public string Name { get; init; } = "";
                private int hidden;             // не должно попасть
                public static int StaticX;      // не должно попасть
            }
            """;
        var (comp, root) = Compile(src, "Foo");
        var walker = new TypeWalker(comp);
        var members = walker.GetMembers(root).Select(m => m.Name).ToArray();

        await Assert.That(members).IsEquivalentTo(new[] { "Id", "Name" });
    }

    [Test]
    public async Task Closure_includes_nested_DTO()
    {
        var src = """
            public class Inner { public int X; }
            public class Outer { public Inner Child = new(); public int Y; }
            """;
        var (comp, root) = Compile(src, "Outer");
        var walker = new TypeWalker(comp);
        var closure = walker.Closure(root).Select(t => t.Name).ToArray();

        await Assert.That(closure).Contains("Outer").And.Contains("Inner");
    }

    [Test]
    public async Task Closure_includes_list_element_type()
    {
        var src = """
            using System.Collections.Generic;
            public class Item { public int X; }
            public class Cart { public List<Item> Items = new(); }
            """;
        var (comp, root) = Compile(src, "Cart");
        var walker = new TypeWalker(comp);
        var closure = walker.Closure(root).Select(t => t.Name).ToArray();

        await Assert.That(closure).Contains("Cart").And.Contains("Item");
    }

    [Test]
    public async Task Closure_includes_dictionary_key_and_value()
    {
        var src = """
            using System.Collections.Generic;
            public class K { public int X; }
            public class V { public string Y = ""; }
            public class Map { public Dictionary<K, V> Data = new(); }
            """;
        var (comp, root) = Compile(src, "Map");
        var walker = new TypeWalker(comp);
        var closure = walker.Closure(root).Select(t => t.Name).ToArray();

        await Assert.That(closure).Contains("Map").And.Contains("K").And.Contains("V");
    }

    [Test]
    public async Task Closure_does_not_recurse_into_builtin_types()
    {
        var src = """
            using System;
            public class Foo
            {
                public Guid Id;
                public DateTime When;
                public string Name = "";
            }
            """;
        var (comp, root) = Compile(src, "Foo");
        var walker = new TypeWalker(comp);
        var closure = walker.Closure(root).Select(t => t.Name).ToArray();

        // только Foo (Guid/DateTime/string — built-in, не рекурсируем дальше)
        await Assert.That(closure).IsEquivalentTo(new[] { "Foo" });
    }
}
```

- [ ] **Step 2: Build + test — упадут**

```
dotnet test XPacketRpc.Generators.Tests --filter "FullyQualifiedName~TypeWalkerTests"
```

- [ ] **Step 3: Реализовать модели**

`XPacketRpc.Generators/Discovery/TypeKind.cs`:

```csharp
namespace XPacketRpc.Generators.Discovery;

internal enum WireKind
{
    Unknown,
    Bool, SByte, Byte, Int16, UInt16, Int32, UInt32, Int64, UInt64, Single, Double, Decimal,
    String, Guid, DateTime, DateTimeOffset, TimeSpan, ByteArray, Enum,
    Nullable,                 // T? value-type
    Array,                    // T[]
    List,                     // List<T>
    Dictionary,               // Dictionary<K,V>
    NestedDto,
}
```

`XPacketRpc.Generators/Discovery/MemberDescriptor.cs`:

```csharp
using Microsoft.CodeAnalysis;

namespace XPacketRpc.Generators.Discovery;

internal sealed record MemberDescriptor(
    string Name,
    ITypeSymbol Type,
    bool IsField,        // false → property
    bool IsNullable,     // annotation says nullable
    WireKind Kind,
    ITypeSymbol? ElementOrInner,   // для array/list/nullable
    ITypeSymbol? DictKey,
    ITypeSymbol? DictValue);
```

- [ ] **Step 4: Реализовать `TypeWalker`**

`XPacketRpc.Generators/Discovery/TypeWalker.cs`:

```csharp
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace XPacketRpc.Generators.Discovery;

internal sealed class TypeWalker
{
    private readonly Compilation comp;
    private readonly INamedTypeSymbol? listOpen;
    private readonly INamedTypeSymbol? dictOpen;
    private readonly INamedTypeSymbol? nullableOpen;

    public TypeWalker(Compilation comp)
    {
        this.comp = comp;
        this.listOpen = comp.GetTypeByMetadataName("System.Collections.Generic.List`1");
        this.dictOpen = comp.GetTypeByMetadataName("System.Collections.Generic.Dictionary`2");
        this.nullableOpen = comp.GetTypeByMetadataName("System.Nullable`1");
    }

    /// <summary>Возвращает members типа: public instance fields + properties (declared in type).</summary>
    public IReadOnlyList<MemberDescriptor> GetMembers(INamedTypeSymbol type)
    {
        var result = new List<MemberDescriptor>();
        foreach (var m in type.GetMembers())
        {
            if (m.DeclaredAccessibility != Accessibility.Public) continue;
            if (m.IsStatic) continue;

            switch (m)
            {
                case IFieldSymbol f when !f.IsConst && !f.IsImplicitlyDeclared:
                    result.Add(MakeMember(f.Name, f.Type, isField: true));
                    break;
                case IPropertySymbol p when !p.IsIndexer:
                    result.Add(MakeMember(p.Name, p.Type, isField: false));
                    break;
            }
        }
        return result;
    }

    private MemberDescriptor MakeMember(string name, ITypeSymbol type, bool isField)
    {
        var (kind, inner, k, v) = ClassifyType(type);
        bool nullable = type.NullableAnnotation == NullableAnnotation.Annotated
                        || kind == WireKind.Nullable;

        return new MemberDescriptor(name, type, isField, nullable, kind, inner, k, v);
    }

    private (WireKind kind, ITypeSymbol? inner, ITypeSymbol? key, ITypeSymbol? val) ClassifyType(ITypeSymbol t)
    {
        // Nullable<T>
        if (t is INamedTypeSymbol nts && this.nullableOpen is not null &&
            SymbolEqualityComparer.Default.Equals(nts.OriginalDefinition, this.nullableOpen))
        {
            return (WireKind.Nullable, nts.TypeArguments[0], null, null);
        }

        // byte[]
        if (t is IArrayTypeSymbol arr && arr.ElementType.SpecialType == SpecialType.System_Byte)
            return (WireKind.ByteArray, null, null, null);

        // T[]
        if (t is IArrayTypeSymbol genArr)
            return (WireKind.Array, genArr.ElementType, null, null);

        // Enum
        if (t.TypeKind == TypeKind.Enum) return (WireKind.Enum, null, null, null);

        // Built-ins
        switch (t.SpecialType)
        {
            case SpecialType.System_Boolean: return (WireKind.Bool, null, null, null);
            case SpecialType.System_SByte: return (WireKind.SByte, null, null, null);
            case SpecialType.System_Byte: return (WireKind.Byte, null, null, null);
            case SpecialType.System_Int16: return (WireKind.Int16, null, null, null);
            case SpecialType.System_UInt16: return (WireKind.UInt16, null, null, null);
            case SpecialType.System_Int32: return (WireKind.Int32, null, null, null);
            case SpecialType.System_UInt32: return (WireKind.UInt32, null, null, null);
            case SpecialType.System_Int64: return (WireKind.Int64, null, null, null);
            case SpecialType.System_UInt64: return (WireKind.UInt64, null, null, null);
            case SpecialType.System_Single: return (WireKind.Single, null, null, null);
            case SpecialType.System_Double: return (WireKind.Double, null, null, null);
            case SpecialType.System_Decimal: return (WireKind.Decimal, null, null, null);
            case SpecialType.System_String: return (WireKind.String, null, null, null);
            case SpecialType.System_DateTime: return (WireKind.DateTime, null, null, null);
        }

        // По FullName — Guid, DateTimeOffset, TimeSpan
        var fq = t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (fq == "global::System.Guid") return (WireKind.Guid, null, null, null);
        if (fq == "global::System.DateTimeOffset") return (WireKind.DateTimeOffset, null, null, null);
        if (fq == "global::System.TimeSpan") return (WireKind.TimeSpan, null, null, null);

        // List<T>, Dictionary<K,V>
        if (t is INamedTypeSymbol gnts && gnts.IsGenericType)
        {
            if (this.listOpen is not null && SymbolEqualityComparer.Default.Equals(gnts.OriginalDefinition, this.listOpen))
                return (WireKind.List, gnts.TypeArguments[0], null, null);

            if (this.dictOpen is not null && SymbolEqualityComparer.Default.Equals(gnts.OriginalDefinition, this.dictOpen))
                return (WireKind.Dictionary, null, gnts.TypeArguments[0], gnts.TypeArguments[1]);
        }

        // Иначе — nested DTO
        return (WireKind.NestedDto, null, null, null);
    }

    /// <summary>Транзитивное замыкание: root + все nested-DTO + element/key/value-DTO рекурсивно.</summary>
    public IReadOnlyCollection<INamedTypeSymbol> Closure(INamedTypeSymbol root)
    {
        var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var stack = new Stack<INamedTypeSymbol>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var t = stack.Pop();
            if (!visited.Add(t)) continue;

            foreach (var m in GetMembers(t))
            {
                AddCandidates(m, stack, visited);
            }
        }
        return visited;
    }

    private void AddCandidates(MemberDescriptor m, Stack<INamedTypeSymbol> stack, HashSet<INamedTypeSymbol> visited)
    {
        switch (m.Kind)
        {
            case WireKind.NestedDto:
                if (m.Type is INamedTypeSymbol n) stack.Push(n);
                break;
            case WireKind.Nullable:
            case WireKind.Array:
            case WireKind.List:
                if (m.ElementOrInner is INamedTypeSymbol el && IsDtoCandidate(el)) stack.Push(el);
                break;
            case WireKind.Dictionary:
                if (m.DictKey is INamedTypeSymbol dk && IsDtoCandidate(dk)) stack.Push(dk);
                if (m.DictValue is INamedTypeSymbol dv && IsDtoCandidate(dv)) stack.Push(dv);
                break;
        }
    }

    private bool IsDtoCandidate(INamedTypeSymbol t)
    {
        // Recurse только в кастомные классы. Built-in/enum/Nullable<T>/List/Dict — не DTO.
        var (kind, _, _, _) = ClassifyType(t);
        return kind == WireKind.NestedDto;
    }
}
```

- [ ] **Step 5: Прогнать тесты**

```
dotnet test XPacketRpc.Generators.Tests --filter "FullyQualifiedName~TypeWalkerTests"
```

Expected: 5 tests passed.

- [ ] **Step 6: Commit**

```
git add XPacketRpc.Generators/Discovery/ XPacketRpc.Generators.Tests/TypeWalkerTests.cs
git commit -m "feat(generator): add TypeWalker for member discovery and transitive closure"
```

---

## Phase 7 — Generator: emit Write/Read/Registry

Emitter'ы получают коллекцию `IReadOnlyCollection<INamedTypeSymbol>` (closure из TypeWalker) и
эмитят:
- per-DTO `internal static class __XPRpcGen_<Type>` с `Write`/`Read`/`FieldOrder`,
- per-assembly `internal static class __XPRpcRegistry` с `[ModuleInitializer]`.

### Task 7.1: `WriteEmitter` — генерация Write-метода для DTO (полная type-matrix + bitmap + collection + dictionary)

**Files:**
- Create: `XPacketRpc.Generators/Emit/WriteEmitter.cs`
- Create: `XPacketRpc.Generators.Tests/WriteEmitterTests.cs`

- [ ] **Step 1: Failing test (snapshot-сравнение строки эмита)**

`XPacketRpc.Generators.Tests/WriteEmitterTests.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XPacketRpc.Generators.Discovery;
using XPacketRpc.Generators.Emit;

namespace XPacketRpc.Generators.Tests;

public class WriteEmitterTests
{
    private static (TypeWalker walker, INamedTypeSymbol type) Compile(string src, string typeName)
    {
        var tree = CSharpSyntaxTree.ParseText(src);
        var refs = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Guid).Assembly.Location),
        };
        var comp = CSharpCompilation.Create("t", new[] { tree }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        var sym = comp.GetTypeByMetadataName(typeName)!;
        return (new TypeWalker(comp), sym);
    }

    [Test]
    public async Task Emits_primitive_writes_in_hash_sorted_order()
    {
        var src = """
            #nullable enable
            public class Foo { public int Id; public string Name = ""; public bool Flag; }
            """;
        var (walker, type) = Compile(src, "Foo");
        var emitter = new WriteEmitter(walker);
        var code = emitter.EmitWriteMethod(type);

        // hash-sorted порядок (FNV-1a): нужно вычислить; для теста просто проверяем,
        // что все три имени и WriteString встречаются в коде.
        await Assert.That(code).Contains("WriteInt32LE(value.Id");
        await Assert.That(code).Contains("WriteString(value.Name");
        await Assert.That(code).Contains("WriteByte((byte)(value.Flag ? 1 : 0)");
    }

    [Test]
    public async Task Emits_nullability_bitmap_for_nullable_fields()
    {
        var src = """
            #nullable enable
            public class Foo { public int Id; public string? Comment; }
            """;
        var (walker, type) = Compile(src, "Foo");
        var emitter = new WriteEmitter(walker);
        var code = emitter.EmitWriteMethod(type);

        await Assert.That(code).Contains("byte bitmap = 0;");
        await Assert.That(code).Contains("value.Comment is null");
        await Assert.That(code).Contains("bitmap |=");
    }

    [Test]
    public async Task Emits_throw_for_required_reference_field()
    {
        var src = """
            #nullable enable
            public class Foo { public string Name = ""; }
            """;
        var (walker, type) = Compile(src, "Foo");
        var emitter = new WriteEmitter(walker);
        var code = emitter.EmitWriteMethod(type);

        await Assert.That(code).Contains("ThrowNullRequired(\"Name\")");
    }

    [Test]
    public async Task Emits_list_loop()
    {
        var src = """
            #nullable enable
            using System.Collections.Generic;
            public class Foo { public List<int> Scores = new(); }
            """;
        var (walker, type) = Compile(src, "Foo");
        var emitter = new WriteEmitter(walker);
        var code = emitter.EmitWriteMethod(type);

        await Assert.That(code).Contains("WriteVarUInt32((uint)value.Scores.Count");
        await Assert.That(code).Contains("foreach");
    }

    [Test]
    public async Task Emits_nested_DTO_call()
    {
        var src = """
            #nullable enable
            public class Inner { public int X; }
            public class Outer { public Inner Child = new(); }
            """;
        var (walker, type) = Compile(src, "Outer");
        var emitter = new WriteEmitter(walker);
        var code = emitter.EmitWriteMethod(type);

        await Assert.That(code).Contains("__XPRpcGen_Inner.Write(value.Child");
    }
}
```

- [ ] **Step 2: Build + test — упадут**

```
dotnet test XPacketRpc.Generators.Tests --filter "FullyQualifiedName~WriteEmitterTests"
```

- [ ] **Step 3: Реализовать `WriteEmitter`**

`XPacketRpc.Generators/Emit/WriteEmitter.cs`:

```csharp
using Microsoft.CodeAnalysis;
using XPacketRpc.Generators.Discovery;

namespace XPacketRpc.Generators.Emit;

internal sealed class WriteEmitter
{
    private readonly TypeWalker walker;

    public WriteEmitter(TypeWalker walker) { this.walker = walker; }

    public string EmitWriteMethod(INamedTypeSymbol type)
    {
        var members = walker.GetMembers(type);
        var sorted = members
            .OrderBy(m => Fnv1aGen.Hash(m.Name))
            .ThenBy(m => m.Name, StringComparer.Ordinal)
            .ToArray();

        var nullableMembers = sorted
            .Select((m, i) => (m, i))
            .Where(t => t.m.IsNullable)
            .ToArray();

        int bitmapBytes = (nullableMembers.Length + 7) / 8;

        var sb = new IndentedStringBuilder();
        sb.AppendLine($"internal static void Write(global::{Fq(type)} value, global::System.Buffers.IBufferWriter<byte> w)");
        sb.AppendLine("{");
        using (sb.Indent())
        {
            // Bitmap
            if (bitmapBytes > 0)
            {
                EmitBitmap(sb, sorted, nullableMembers, bitmapBytes);
            }

            // Field writes
            for (int i = 0; i < sorted.Length; i++)
            {
                EmitField(sb, sorted[i], i, nullableMembers);
            }
        }
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void EmitBitmap(
        IndentedStringBuilder sb,
        MemberDescriptor[] sorted,
        (MemberDescriptor m, int i)[] nullableMembers,
        int bitmapBytes)
    {
        if (bitmapBytes == 1)
        {
            sb.AppendLine("byte bitmap = 0;");
            int bit = 0;
            foreach (var (m, _) in nullableMembers)
            {
                sb.AppendLine($"if (value.{m.Name} is null) bitmap |= 0b{1 << bit:b8};");
                bit++;
            }
            sb.AppendLine("var __span = w.GetSpan(1);");
            sb.AppendLine("__span[0] = bitmap;");
            sb.AppendLine("w.Advance(1);");
        }
        else
        {
            sb.AppendLine($"global::System.Span<byte> bitmap = stackalloc byte[{bitmapBytes}];");
            int idx = 0;
            foreach (var (m, _) in nullableMembers)
            {
                int byteIdx = idx / 8;
                int bit = idx % 8;
                sb.AppendLine($"if (value.{m.Name} is null) bitmap[{byteIdx}] |= 0b{1 << bit:b8};");
                idx++;
            }
            sb.AppendLine($"var __span = w.GetSpan({bitmapBytes});");
            sb.AppendLine($"bitmap.CopyTo(__span);");
            sb.AppendLine($"w.Advance({bitmapBytes});");
        }
    }

    private void EmitField(
        IndentedStringBuilder sb,
        MemberDescriptor m,
        int sortedIndex,
        (MemberDescriptor m, int i)[] nullableMembers)
    {
        // Если nullable — обёрнуто в if-not-null
        if (m.IsNullable)
        {
            sb.AppendLine($"if (value.{m.Name} is not null)");
            sb.AppendLine("{");
            using (sb.Indent())
            {
                EmitFieldValue(sb, m, accessExpr: $"value.{m.Name}", forceNonNull: true);
            }
            sb.AppendLine("}");
        }
        else if (NeedsNullCheck(m))
        {
            sb.AppendLine($"if (value.{m.Name} is null) global::XPacketRpc.Internal.Writers.ThrowNullRequired(\"{m.Name}\");");
            EmitFieldValue(sb, m, accessExpr: $"value.{m.Name}", forceNonNull: true);
        }
        else
        {
            EmitFieldValue(sb, m, accessExpr: $"value.{m.Name}", forceNonNull: false);
        }
    }

    private static bool NeedsNullCheck(MemberDescriptor m)
    {
        // reference-types и string/byte[]/List/Dictionary — все могут быть null at runtime
        // даже если annotation non-nullable. Проверяем для defense.
        if (m.Type.IsValueType) return false;
        return true;
    }

    private void EmitFieldValue(IndentedStringBuilder sb, MemberDescriptor m, string accessExpr, bool forceNonNull)
    {
        var w = "global::XPacketRpc.Internal.Writers";
        switch (m.Kind)
        {
            case WireKind.Bool:
                sb.AppendLine($"{w}.WriteByte((byte)({accessExpr} ? 1 : 0), w);");
                break;
            case WireKind.SByte:
                sb.AppendLine($"{w}.WriteByte((byte){accessExpr}, w);");
                break;
            case WireKind.Byte:
                sb.AppendLine($"{w}.WriteByte({accessExpr}, w);");
                break;
            case WireKind.Int16: sb.AppendLine($"{w}.WriteInt16LE({accessExpr}, w);"); break;
            case WireKind.UInt16: sb.AppendLine($"{w}.WriteUInt16LE({accessExpr}, w);"); break;
            case WireKind.Int32: sb.AppendLine($"{w}.WriteInt32LE({accessExpr}, w);"); break;
            case WireKind.UInt32: sb.AppendLine($"{w}.WriteUInt32LE({accessExpr}, w);"); break;
            case WireKind.Int64: sb.AppendLine($"{w}.WriteInt64LE({accessExpr}, w);"); break;
            case WireKind.UInt64: sb.AppendLine($"{w}.WriteUInt64LE({accessExpr}, w);"); break;
            case WireKind.Single: sb.AppendLine($"{w}.WriteSingleLE({accessExpr}, w);"); break;
            case WireKind.Double: sb.AppendLine($"{w}.WriteDoubleLE({accessExpr}, w);"); break;
            case WireKind.Decimal: sb.AppendLine($"{w}.WriteDecimalLE({accessExpr}, w);"); break;
            case WireKind.String: sb.AppendLine($"{w}.WriteString({accessExpr}, w);"); break;
            case WireKind.Guid: sb.AppendLine($"{w}.WriteGuid({accessExpr}, w);"); break;
            case WireKind.DateTime: sb.AppendLine($"{w}.WriteDateTime({accessExpr}, w);"); break;
            case WireKind.DateTimeOffset: sb.AppendLine($"{w}.WriteDateTimeOffset({accessExpr}, w);"); break;
            case WireKind.TimeSpan: sb.AppendLine($"{w}.WriteTimeSpan({accessExpr}, w);"); break;
            case WireKind.ByteArray: sb.AppendLine($"{w}.WriteBytes({accessExpr}, w);"); break;

            case WireKind.Enum:
                {
                    var enumType = (INamedTypeSymbol)m.Type;
                    var underlying = enumType.EnumUnderlyingType!.SpecialType;
                    var (cast, writeFn) = MapEnumUnderlying(underlying);
                    sb.AppendLine($"{w}.{writeFn}(({cast}){accessExpr}, w);");
                }
                break;

            case WireKind.Nullable:
                {
                    var inner = m.ElementOrInner!;
                    sb.AppendLine($"// Nullable<T> outer null-check is handled by bitmap; here T is non-null");
                    EmitInlineWriteForType(sb, inner, $"{accessExpr}.Value");
                }
                break;

            case WireKind.Array:
                EmitCollectionWrite(sb, m.ElementOrInner!, accessExpr, "Length", "[i]");
                break;

            case WireKind.List:
                EmitCollectionWrite(sb, m.ElementOrInner!, accessExpr, "Count", "[i]");
                break;

            case WireKind.Dictionary:
                EmitDictionaryWrite(sb, m.DictKey!, m.DictValue!, accessExpr);
                break;

            case WireKind.NestedDto:
                {
                    var t = (INamedTypeSymbol)m.Type;
                    sb.AppendLine($"global::XPacketRpc.Generated.__XPRpcGen_{Sanitize(t.Name)}.Write({accessExpr}, w);");
                }
                break;

            default:
                sb.AppendLine($"// XPRPC004: unsupported type {m.Type.ToDisplayString()}");
                break;
        }
    }

    private void EmitInlineWriteForType(IndentedStringBuilder sb, ITypeSymbol type, string accessExpr)
    {
        var (kind, inner, dk, dv) = ClassifyForInline(type);
        var fake = new MemberDescriptor(
            Name: "_inline",
            Type: type,
            IsField: false,
            IsNullable: false,
            Kind: kind,
            ElementOrInner: inner,
            DictKey: dk,
            DictValue: dv);
        EmitFieldValue(sb, fake, accessExpr, forceNonNull: true);
    }

    private void EmitCollectionWrite(IndentedStringBuilder sb, ITypeSymbol elemType, string access, string countProp, string indexerSuffix)
    {
        var w = "global::XPacketRpc.Internal.Writers";
        sb.AppendLine($"{w}.WriteVarUInt32((uint){access}.{countProp}, w);");
        sb.AppendLine($"for (int i = 0; i < {access}.{countProp}; i++)");
        sb.AppendLine("{");
        using (sb.Indent())
        {
            EmitInlineWriteForType(sb, elemType, $"{access}{indexerSuffix}");
        }
        sb.AppendLine("}");
    }

    private void EmitDictionaryWrite(IndentedStringBuilder sb, ITypeSymbol keyType, ITypeSymbol valType, string access)
    {
        var w = "global::XPacketRpc.Internal.Writers";
        sb.AppendLine($"{w}.WriteVarUInt32((uint){access}.Count, w);");
        sb.AppendLine($"foreach (var __kv in {access})");
        sb.AppendLine("{");
        using (sb.Indent())
        {
            EmitInlineWriteForType(sb, keyType, "__kv.Key");
            EmitInlineWriteForType(sb, valType, "__kv.Value");
        }
        sb.AppendLine("}");
    }

    private (WireKind, ITypeSymbol?, ITypeSymbol?, ITypeSymbol?) ClassifyForInline(ITypeSymbol t)
    {
        // Простой dispatch без mutability — для inline-emission элементов коллекций.
        var listOpen = t.ContainingAssembly?.GetTypeByMetadataName("System.Collections.Generic.List`1");
        // Делегируем основной TypeWalker через временный member-descriptor
        // Тут можно использовать тот же ClassifyType, что в TypeWalker; для уменьшения дублирования
        // в реальной реализации сделайте ClassifyType internal-static и переиспользуйте.
        if (t is IArrayTypeSymbol arr && arr.ElementType.SpecialType == SpecialType.System_Byte)
            return (WireKind.ByteArray, null, null, null);
        if (t is IArrayTypeSymbol arr2)
            return (WireKind.Array, arr2.ElementType, null, null);
        switch (t.SpecialType)
        {
            case SpecialType.System_Boolean: return (WireKind.Bool, null, null, null);
            case SpecialType.System_Byte: return (WireKind.Byte, null, null, null);
            case SpecialType.System_SByte: return (WireKind.SByte, null, null, null);
            case SpecialType.System_Int16: return (WireKind.Int16, null, null, null);
            case SpecialType.System_UInt16: return (WireKind.UInt16, null, null, null);
            case SpecialType.System_Int32: return (WireKind.Int32, null, null, null);
            case SpecialType.System_UInt32: return (WireKind.UInt32, null, null, null);
            case SpecialType.System_Int64: return (WireKind.Int64, null, null, null);
            case SpecialType.System_UInt64: return (WireKind.UInt64, null, null, null);
            case SpecialType.System_Single: return (WireKind.Single, null, null, null);
            case SpecialType.System_Double: return (WireKind.Double, null, null, null);
            case SpecialType.System_Decimal: return (WireKind.Decimal, null, null, null);
            case SpecialType.System_String: return (WireKind.String, null, null, null);
            case SpecialType.System_DateTime: return (WireKind.DateTime, null, null, null);
        }
        var fq = t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (fq == "global::System.Guid") return (WireKind.Guid, null, null, null);
        if (fq == "global::System.DateTimeOffset") return (WireKind.DateTimeOffset, null, null, null);
        if (fq == "global::System.TimeSpan") return (WireKind.TimeSpan, null, null, null);
        if (t.TypeKind == TypeKind.Enum) return (WireKind.Enum, null, null, null);

        if (t is INamedTypeSymbol nts && nts.IsGenericType)
        {
            var open = nts.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (open == "global::System.Collections.Generic.List<T>")
                return (WireKind.List, nts.TypeArguments[0], null, null);
            if (open == "global::System.Collections.Generic.Dictionary<TKey, TValue>")
                return (WireKind.Dictionary, null, nts.TypeArguments[0], nts.TypeArguments[1]);
            if (open == "global::System.Nullable<T>")
                return (WireKind.Nullable, nts.TypeArguments[0], null, null);
        }
        return (WireKind.NestedDto, null, null, null);
    }

    private static (string cast, string fn) MapEnumUnderlying(SpecialType u) => u switch
    {
        SpecialType.System_Byte => ("byte", "WriteByte"),
        SpecialType.System_SByte => ("byte", "WriteByte"),
        SpecialType.System_Int16 => ("short", "WriteInt16LE"),
        SpecialType.System_UInt16 => ("ushort", "WriteUInt16LE"),
        SpecialType.System_Int32 => ("int", "WriteInt32LE"),
        SpecialType.System_UInt32 => ("uint", "WriteUInt32LE"),
        SpecialType.System_Int64 => ("long", "WriteInt64LE"),
        SpecialType.System_UInt64 => ("ulong", "WriteUInt64LE"),
        _ => ("int", "WriteInt32LE"),
    };

    private static string Fq(INamedTypeSymbol t) =>
        t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");

    private static string Sanitize(string name) =>
        new(name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
}

internal static class Fnv1aGen
{
    public static uint Hash(string s)
    {
        uint h = 2166136261u;
        for (int i = 0; i < s.Length; i++) { h ^= s[i]; h *= 16777619u; }
        return h;
    }
}
```

- [ ] **Step 4: Прогнать тесты**

```
dotnet test XPacketRpc.Generators.Tests --filter "FullyQualifiedName~WriteEmitterTests"
```

Expected: 5 tests passed.

- [ ] **Step 5: Commit**

```
git add XPacketRpc.Generators/Emit/WriteEmitter.cs XPacketRpc.Generators.Tests/WriteEmitterTests.cs
git commit -m "feat(generator): add WriteEmitter (full type matrix + bitmap + collection + dictionary)"
```

---

### Task 7.2: `ReadEmitter` — генерация Read-метода (зеркальный к Write)

**Files:**
- Create: `XPacketRpc.Generators/Emit/ReadEmitter.cs`
- Create: `XPacketRpc.Generators.Tests/ReadEmitterTests.cs`

> **Note:** ctor-binding покрывается отдельным таском Phase 8.1; здесь Read эмитит ТОЛЬКО через
> parameterless ctor + setters/inits (это работает для Phase-7 smoke). Phase 8 расширит.

- [ ] **Step 1: Failing test**

`XPacketRpc.Generators.Tests/ReadEmitterTests.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XPacketRpc.Generators.Discovery;
using XPacketRpc.Generators.Emit;

namespace XPacketRpc.Generators.Tests;

public class ReadEmitterTests
{
    private static (TypeWalker walker, INamedTypeSymbol type) Compile(string src, string typeName)
    {
        var tree = CSharpSyntaxTree.ParseText(src);
        var refs = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Guid).Assembly.Location),
        };
        var comp = CSharpCompilation.Create("t", new[] { tree }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        return (new TypeWalker(comp), comp.GetTypeByMetadataName(typeName)!);
    }

    [Test]
    public async Task Emits_parameterless_ctor_and_setters()
    {
        var src = """
            #nullable enable
            public class Foo { public int Id { get; set; } public string Name { get; set; } = ""; }
            """;
        var (walker, type) = Compile(src, "Foo");
        var emitter = new ReadEmitter(walker);
        var code = emitter.EmitReadMethod(type);

        await Assert.That(code).Contains("new global::Foo()");
        await Assert.That(code).Contains(".Id =");
        await Assert.That(code).Contains(".Name =");
    }

    [Test]
    public async Task Emits_bitmap_read_for_nullable_fields()
    {
        var src = """
            #nullable enable
            public class Foo { public int Id { get; set; } public string? Comment { get; set; } }
            """;
        var (walker, type) = Compile(src, "Foo");
        var emitter = new ReadEmitter(walker);
        var code = emitter.EmitReadMethod(type);

        await Assert.That(code).Contains("byte bitmap = r.ReadByte();");
        await Assert.That(code).Contains("commentIsNull");
    }

    [Test]
    public async Task Emits_list_read_loop()
    {
        var src = """
            #nullable enable
            using System.Collections.Generic;
            public class Foo { public List<int> Scores { get; set; } = new(); }
            """;
        var (walker, type) = Compile(src, "Foo");
        var emitter = new ReadEmitter(walker);
        var code = emitter.EmitReadMethod(type);

        await Assert.That(code).Contains("ReadVarUInt32");
        await Assert.That(code).Contains("for (int i = 0;");
    }

    [Test]
    public async Task Emits_nested_DTO_call()
    {
        var src = """
            #nullable enable
            public class Inner { public int X { get; set; } }
            public class Outer { public Inner Child { get; set; } = new(); }
            """;
        var (walker, type) = Compile(src, "Outer");
        var emitter = new ReadEmitter(walker);
        var code = emitter.EmitReadMethod(type);

        await Assert.That(code).Contains("__XPRpcGen_Inner.Read(ref r)");
    }
}
```

- [ ] **Step 2: Build + test — упадут**

```
dotnet test XPacketRpc.Generators.Tests --filter "FullyQualifiedName~ReadEmitterTests"
```

- [ ] **Step 3: Реализовать `ReadEmitter`**

`XPacketRpc.Generators/Emit/ReadEmitter.cs`:

```csharp
using Microsoft.CodeAnalysis;
using XPacketRpc.Generators.Discovery;

namespace XPacketRpc.Generators.Emit;

internal sealed class ReadEmitter
{
    private readonly TypeWalker walker;

    public ReadEmitter(TypeWalker walker) { this.walker = walker; }

    public string EmitReadMethod(INamedTypeSymbol type)
    {
        var members = walker.GetMembers(type);
        var sorted = members
            .OrderBy(m => Fnv1aGen.Hash(m.Name))
            .ThenBy(m => m.Name, StringComparer.Ordinal)
            .ToArray();

        var nullableMembers = sorted
            .Select((m, i) => (m, i))
            .Where(t => t.m.IsNullable)
            .ToArray();
        int bitmapBytes = (nullableMembers.Length + 7) / 8;

        var fq = $"global::{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "")}";
        var sb = new IndentedStringBuilder();
        sb.AppendLine($"internal static {fq} Read(ref global::XPacketRpc.XPRpcReader r)");
        sb.AppendLine("{");
        using (sb.Indent())
        {
            // Bitmap
            if (bitmapBytes == 1)
            {
                sb.AppendLine("byte bitmap = r.ReadByte();");
            }
            else if (bitmapBytes > 1)
            {
                sb.AppendLine($"global::System.Span<byte> bitmap = stackalloc byte[{bitmapBytes}];");
                sb.AppendLine($"for (int i = 0; i < {bitmapBytes}; i++) bitmap[i] = r.ReadByte();");
            }

            // Per-field local declarations
            int nullableIdx = 0;
            foreach (var m in sorted)
            {
                if (m.IsNullable)
                {
                    int byteIdx = nullableIdx / 8;
                    int bit = nullableIdx % 8;
                    var bitmapAccess = bitmapBytes == 1 ? "bitmap" : $"bitmap[{byteIdx}]";
                    sb.AppendLine($"bool {Camel(m.Name)}IsNull = ({bitmapAccess} & 0b{1 << bit:b8}) != 0;");
                    sb.AppendLine($"{TypeName(m.Type)} {Camel(m.Name)} = default!;");
                    sb.AppendLine($"if (!{Camel(m.Name)}IsNull)");
                    sb.AppendLine("{");
                    using (sb.Indent())
                    {
                        sb.AppendLine($"{Camel(m.Name)} = {ReadExpr(m)};");
                    }
                    sb.AppendLine("}");
                    nullableIdx++;
                }
                else
                {
                    sb.AppendLine($"{TypeName(m.Type)} {Camel(m.Name)} = {ReadExpr(m)};");
                }
            }

            // Object construction (parameterless ctor + setters)
            sb.AppendLine($"return new {fq}");
            sb.AppendLine("{");
            using (sb.Indent())
            {
                for (int i = 0; i < sorted.Length; i++)
                {
                    var m = sorted[i];
                    var sep = i == sorted.Length - 1 ? "" : ",";
                    sb.AppendLine($"{m.Name} = {Camel(m.Name)}{sep}");
                }
            }
            sb.AppendLine("};");
        }
        sb.AppendLine("}");
        return sb.ToString();
    }

    private string ReadExpr(MemberDescriptor m)
    {
        return m.Kind switch
        {
            WireKind.Bool => "(r.ReadByte() != 0)",
            WireKind.Byte => "r.ReadByte()",
            WireKind.SByte => "(sbyte)r.ReadByte()",
            WireKind.Int16 => "r.ReadInt16()",
            WireKind.UInt16 => "r.ReadUInt16()",
            WireKind.Int32 => "r.ReadInt32()",
            WireKind.UInt32 => "r.ReadUInt32()",
            WireKind.Int64 => "r.ReadInt64()",
            WireKind.UInt64 => "r.ReadUInt64()",
            WireKind.Single => "r.ReadSingle()",
            WireKind.Double => "r.ReadDouble()",
            WireKind.Decimal => "r.ReadDecimal()",
            WireKind.String => "r.ReadString()",
            WireKind.Guid => "r.ReadGuid()",
            WireKind.DateTime => "r.ReadDateTime()",
            WireKind.DateTimeOffset => "r.ReadDateTimeOffset()",
            WireKind.TimeSpan => "r.ReadTimeSpan()",
            WireKind.ByteArray => "r.ReadBytes()",
            WireKind.Enum => $"({TypeName(m.Type)})r.{ReadEnumFn(((INamedTypeSymbol)m.Type).EnumUnderlyingType!.SpecialType)}()",
            WireKind.NestedDto => $"global::XPacketRpc.Generated.__XPRpcGen_{Sanitize(m.Type.Name)}.Read(ref r)",
            WireKind.List => BuildListReadExpr(m.ElementOrInner!),
            WireKind.Array => BuildArrayReadExpr(m.ElementOrInner!),
            WireKind.Dictionary => BuildDictReadExpr(m.DictKey!, m.DictValue!),
            WireKind.Nullable => $"({ReadExprForType(m.ElementOrInner!)})", // outer null handled via bitmap
            _ => $"default! /* unsupported {m.Type} */"
        };
    }

    private string ReadExprForType(ITypeSymbol t)
    {
        var fakeMember = new MemberDescriptor(
            "_", t, false, false,
            // дублирование classify — в реальной реализации вынести общий метод
            WireKind.Unknown, null, null, null);
        // Для inline вычисления вызывайте walker.GetMembers — для коллекций невозможно;
        // правильная реализация: продублировать ClassifyType из TypeWalker, и вызвать ReadExpr
        // с полученными kind/inner. В рамках плана упрощено: типы внутри коллекций — built-in.
        return t.SpecialType switch
        {
            SpecialType.System_Int32 => "r.ReadInt32()",
            SpecialType.System_String => "r.ReadString()",
            SpecialType.System_Int64 => "r.ReadInt64()",
            SpecialType.System_Double => "r.ReadDouble()",
            SpecialType.System_Boolean => "(r.ReadByte() != 0)",
            _ => $"global::XPacketRpc.Generated.__XPRpcGen_{Sanitize(t.Name)}.Read(ref r)"
        };
    }

    private string BuildListReadExpr(ITypeSymbol elem)
    {
        var elemTypeName = TypeName(elem);
        var elemRead = ReadExprForType(elem);
        return $"_ReadList<{elemTypeName}>(ref r, static (ref global::XPacketRpc.XPRpcReader r) => {elemRead})";
    }

    private string BuildArrayReadExpr(ITypeSymbol elem)
    {
        var elemTypeName = TypeName(elem);
        var elemRead = ReadExprForType(elem);
        return $"_ReadArray<{elemTypeName}>(ref r, static (ref global::XPacketRpc.XPRpcReader r) => {elemRead})";
    }

    private string BuildDictReadExpr(ITypeSymbol key, ITypeSymbol val)
    {
        var kName = TypeName(key);
        var vName = TypeName(val);
        var kRead = ReadExprForType(key);
        var vRead = ReadExprForType(val);
        return $"_ReadDict<{kName},{vName}>(ref r, " +
               $"static (ref global::XPacketRpc.XPRpcReader r) => {kRead}, " +
               $"static (ref global::XPacketRpc.XPRpcReader r) => {vRead})";
    }

    private static string ReadEnumFn(SpecialType u) => u switch
    {
        SpecialType.System_Byte or SpecialType.System_SByte => "ReadByte",
        SpecialType.System_Int16 => "ReadInt16",
        SpecialType.System_UInt16 => "ReadUInt16",
        SpecialType.System_Int32 => "ReadInt32",
        SpecialType.System_UInt32 => "ReadUInt32",
        SpecialType.System_Int64 => "ReadInt64",
        SpecialType.System_UInt64 => "ReadUInt64",
        _ => "ReadInt32",
    };

    private static string TypeName(ITypeSymbol t) =>
        t.ToDisplayString(new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier));

    private static string Camel(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..];
    private static string Sanitize(string n) => new(n.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
}
```

> **Note (важно):** `_ReadList<T>`, `_ReadArray<T>`, `_ReadDict<K,V>` — helper-методы с делегатами,
> которые должен предоставить runtime (`XPRpcReader.cs` или separate static-helper в `XPacketRpc.Internal`).
> Их добавление — отдельный мини-таск ниже.

- [ ] **Step 4: Дописать helper'ы в runtime**

В `XPacketRpc/XPRpcReader.cs` добавьте делегатно-параметризованные helper'ы (они нужны как
точка вызова для эмита, чтобы не дублировать loop-код в каждом сгенерированном Read):

```csharp
public delegate T ReadElemDelegate<T>(ref XPRpcReader r);

public static class XPRpcReaderHelpers
{
    public static System.Collections.Generic.List<T> ReadList<T>(ref XPRpcReader r, ReadElemDelegate<T> read)
    {
        uint n = r.ReadVarUInt32();
        var list = new System.Collections.Generic.List<T>((int)n);
        for (uint i = 0; i < n; i++) list.Add(read(ref r));
        return list;
    }

    public static T[] ReadArray<T>(ref XPRpcReader r, ReadElemDelegate<T> read)
    {
        uint n = r.ReadVarUInt32();
        var arr = new T[n];
        for (uint i = 0; i < n; i++) arr[i] = read(ref r);
        return arr;
    }

    public static System.Collections.Generic.Dictionary<K, V> ReadDict<K, V>(
        ref XPRpcReader r, ReadElemDelegate<K> readK, ReadElemDelegate<V> readV) where K : notnull
    {
        uint n = r.ReadVarUInt32();
        var d = new System.Collections.Generic.Dictionary<K, V>((int)n);
        for (uint i = 0; i < n; i++)
        {
            var k = readK(ref r);
            var v = readV(ref r);
            d[k] = v;
        }
        return d;
    }
}
```

> Соответственно в `ReadEmitter` поправьте вызовы `_ReadList`/`_ReadArray`/`_ReadDict` на
> `global::XPacketRpc.XPRpcReaderHelpers.ReadList`/`ReadArray`/`ReadDict` (полное FQ-имя).

- [ ] **Step 5: Прогнать тесты**

```
dotnet test XPacketRpc.Generators.Tests --filter "FullyQualifiedName~ReadEmitterTests"
dotnet test XPacketRpc.Tests
```

Expected: ReadEmitter-тесты passed; runtime-тесты не сломались.

- [ ] **Step 6: Commit**

```
git add XPacketRpc.Generators/Emit/ReadEmitter.cs XPacketRpc/XPRpcReader.cs XPacketRpc.Generators.Tests/ReadEmitterTests.cs
git commit -m "feat(generator): add ReadEmitter (parameterless ctor + setters; runtime helpers for list/array/dict)"
```

---

### Task 7.3: `RegistryEmitter` — `[ModuleInitializer]` агрегатор

**Files:**
- Create: `XPacketRpc.Generators/Emit/RegistryEmitter.cs`
- Create: `XPacketRpc.Generators.Tests/RegistryEmitterTests.cs`

- [ ] **Step 1: Failing test**

`XPacketRpc.Generators.Tests/RegistryEmitterTests.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XPacketRpc.Generators.Emit;

namespace XPacketRpc.Generators.Tests;

public class RegistryEmitterTests
{
    [Test]
    public async Task Emits_module_initializer_with_per_type_register_calls()
    {
        var src = """
            public class A { public int X; }
            public class B { public int Y; }
            """;
        var tree = CSharpSyntaxTree.ParseText(src);
        var refs = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
        var comp = CSharpCompilation.Create("MyAsm", new[] { tree }, refs);

        var types = new[] { comp.GetTypeByMetadataName("A")!, comp.GetTypeByMetadataName("B")! };
        var emitter = new RegistryEmitter();
        var code = emitter.Emit(types, assemblyName: "MyAsm");

        await Assert.That(code).Contains("namespace XPacketRpc.Generated.MyAsm");
        await Assert.That(code).Contains("[global::System.Runtime.CompilerServices.ModuleInitializer]");
        await Assert.That(code).Contains("global::XPacketRpc.XPRpc.Register<global::A>");
        await Assert.That(code).Contains("global::XPacketRpc.XPRpc.Register<global::B>");
        await Assert.That(code).Contains("__XPRpcGen_A.Write");
        await Assert.That(code).Contains("__XPRpcGen_A.Read");
    }

    [Test]
    public async Task Sanitizes_assembly_name_for_namespace()
    {
        var src = "public class A {}";
        var tree = CSharpSyntaxTree.ParseText(src);
        var comp = CSharpCompilation.Create("My-Project.X",
            new[] { tree },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var emitter = new RegistryEmitter();
        var code = emitter.Emit(new[] { comp.GetTypeByMetadataName("A")! }, "My-Project.X");

        // hyphen → underscore, dot kept (legal в namespace)
        await Assert.That(code).Contains("namespace XPacketRpc.Generated.My_Project.X");
    }
}
```

- [ ] **Step 2: Build + test — упадут**

```
dotnet test XPacketRpc.Generators.Tests --filter "FullyQualifiedName~RegistryEmitterTests"
```

- [ ] **Step 3: Реализовать**

`XPacketRpc.Generators/Emit/RegistryEmitter.cs`:

```csharp
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace XPacketRpc.Generators.Emit;

internal sealed class RegistryEmitter
{
    public string Emit(IEnumerable<INamedTypeSymbol> types, string assemblyName)
    {
        var nsSafe = SanitizeNamespace(assemblyName);
        var sb = new IndentedStringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine($"namespace XPacketRpc.Generated.{nsSafe};");
        sb.AppendLine();
        sb.AppendLine("internal static class __XPRpcRegistry");
        sb.AppendLine("{");
        using (sb.Indent())
        {
            sb.AppendLine("[global::System.Runtime.CompilerServices.ModuleInitializer]");
            sb.AppendLine("internal static void Init()");
            sb.AppendLine("{");
            using (sb.Indent())
            {
                foreach (var t in types)
                {
                    var fq = "global::" + t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");
                    var sname = Sanitize(t.Name);
                    sb.AppendLine($"global::XPacketRpc.XPRpc.Register<{fq}>(__XPRpcGen_{sname}.Write, __XPRpcGen_{sname}.Read);");
                }
            }
            sb.AppendLine("}");
        }
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string SanitizeNamespace(string s)
    {
        // Заменить любой не-id символ кроме '.' на '_'
        var chars = new char[s.Length];
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            chars[i] = (char.IsLetterOrDigit(c) || c == '_' || c == '.') ? c : '_';
        }
        // Если начинается с цифры — добавить префикс
        if (chars.Length > 0 && char.IsDigit(chars[0])) return "_" + new string(chars);
        return new string(chars);
    }

    private static string Sanitize(string s) =>
        new(s.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
}
```

- [ ] **Step 4: Прогнать тесты**

```
dotnet test XPacketRpc.Generators.Tests --filter "FullyQualifiedName~RegistryEmitterTests"
```

- [ ] **Step 5: Commit**

```
git add XPacketRpc.Generators/Emit/RegistryEmitter.cs XPacketRpc.Generators.Tests/RegistryEmitterTests.cs
git commit -m "feat(generator): add RegistryEmitter for ModuleInitializer aggregator"
```

---

## Phase 8 — Generator: ctor-binding для record/immutable

### Task 8.1: `CtorBinder` — выбор стратегии конструирования + расширение ReadEmitter

**Files:**
- Create: `XPacketRpc.Generators/Discovery/CtorBinder.cs`
- Modify: `XPacketRpc.Generators/Emit/ReadEmitter.cs` (использовать `CtorBinder`)
- Create: `XPacketRpc.Generators.Tests/CtorBinderTests.cs`

- [ ] **Step 1: Failing test**

`XPacketRpc.Generators.Tests/CtorBinderTests.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XPacketRpc.Generators.Discovery;

namespace XPacketRpc.Generators.Tests;

public class CtorBinderTests
{
    private static (Compilation comp, INamedTypeSymbol t) Compile(string src, string typeName)
    {
        var tree = CSharpSyntaxTree.ParseText(src);
        var refs = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
        var comp = CSharpCompilation.Create("t", new[] { tree }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        return (comp, comp.GetTypeByMetadataName(typeName)!);
    }

    [Test]
    public async Task Parameterless_ctor_chosen_when_available()
    {
        var src = "public class Foo { public int X { get; set; } }";
        var (_, t) = Compile(src, "Foo");
        var binder = new CtorBinder(new TypeWalker(_!));
        var plan = binder.Bind(t);

        await Assert.That(plan.Strategy).IsEqualTo(CtorStrategy.Parameterless);
        await Assert.That(plan.CtorParams.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Record_primary_ctor_chosen_for_immutable()
    {
        var src = """
            public record class Foo(int X, string Name);
            """;
        var (comp, t) = Compile(src, "Foo");
        var binder = new CtorBinder(new TypeWalker(comp));
        var plan = binder.Bind(t);

        await Assert.That(plan.Strategy).IsEqualTo(CtorStrategy.AllParams);
        await Assert.That(plan.CtorParams.Select(p => p.Name)).IsEquivalentTo(new[] { "X", "Name" });
    }

    [Test]
    public async Task Mixed_ctor_plus_init_setters()
    {
        var src = """
            #nullable enable
            public class Foo
            {
                public int X { get; }
                public string? Comment { get; init; }
                public Foo(int x) { X = x; }
            }
            """;
        var (comp, t) = Compile(src, "Foo");
        var binder = new CtorBinder(new TypeWalker(comp));
        var plan = binder.Bind(t);

        await Assert.That(plan.Strategy).IsEqualTo(CtorStrategy.Mixed);
        await Assert.That(plan.CtorParams.Select(p => p.Name)).IsEquivalentTo(new[] { "X" });
        await Assert.That(plan.SetterMembers.Select(m => m.Name)).IsEquivalentTo(new[] { "Comment" });
    }

    [Test]
    public async Task Returns_diagnostic_when_no_ctor_covers_immutable_member()
    {
        var src = """
            public class Foo
            {
                public int X { get; }    // нет setter, нет ctor — невозможно построить
            }
            """;
        var (comp, t) = Compile(src, "Foo");
        var binder = new CtorBinder(new TypeWalker(comp));
        var plan = binder.Bind(t);

        await Assert.That(plan.Strategy).IsEqualTo(CtorStrategy.Impossible);
    }
}
```

- [ ] **Step 2: Build + test — упадут**

```
dotnet test XPacketRpc.Generators.Tests --filter "FullyQualifiedName~CtorBinderTests"
```

- [ ] **Step 3: Реализовать `CtorBinder`**

`XPacketRpc.Generators/Discovery/CtorBinder.cs`:

```csharp
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace XPacketRpc.Generators.Discovery;

internal enum CtorStrategy
{
    Parameterless,
    AllParams,
    Mixed,
    Impossible,
}

internal sealed record CtorPlan(
    CtorStrategy Strategy,
    IMethodSymbol? Ctor,
    MemberDescriptor[] CtorParams,
    MemberDescriptor[] SetterMembers,
    string? Reason = null);

internal sealed class CtorBinder
{
    private readonly TypeWalker walker;

    public CtorBinder(TypeWalker walker) { this.walker = walker; }

    public CtorPlan Bind(INamedTypeSymbol type)
    {
        var members = walker.GetMembers(type);

        // 1. parameterless?
        var parameterless = type.InstanceConstructors
            .FirstOrDefault(c => c.Parameters.Length == 0 && c.DeclaredAccessibility == Accessibility.Public);
        if (parameterless is not null)
        {
            return new CtorPlan(CtorStrategy.Parameterless, parameterless,
                CtorParams: Array.Empty<MemberDescriptor>(),
                SetterMembers: members.Where(m => CanSet(type, m)).ToArray());
        }

        // 2. найти public ctor с максимальным числом параметров, имена которых subset members
        var memberByName = members.ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);
        var candidate = type.InstanceConstructors
            .Where(c => c.DeclaredAccessibility == Accessibility.Public)
            .Where(c => c.Parameters.All(p => memberByName.ContainsKey(p.Name)))
            .OrderByDescending(c => c.Parameters.Length)
            .FirstOrDefault();

        if (candidate is null)
        {
            return new CtorPlan(CtorStrategy.Impossible, null,
                Array.Empty<MemberDescriptor>(), Array.Empty<MemberDescriptor>(),
                $"No public constructor whose parameters match property/field names of {type.Name}.");
        }

        var ctorParams = candidate.Parameters
            .Select(p => memberByName[p.Name])
            .ToArray();
        var ctorSet = new HashSet<string>(ctorParams.Select(p => p.Name), StringComparer.Ordinal);
        var remaining = members.Where(m => !ctorSet.Contains(m.Name)).ToArray();

        // Все remaining должны быть set'абельны
        var notSettable = remaining.Where(m => !CanSet(type, m)).ToArray();
        if (notSettable.Length > 0)
        {
            return new CtorPlan(CtorStrategy.Impossible, null,
                Array.Empty<MemberDescriptor>(), Array.Empty<MemberDescriptor>(),
                $"Members [{string.Join(",", notSettable.Select(m => m.Name))}] cannot be set " +
                $"(no setter/init) and are not in any constructor.");
        }

        var strategy = remaining.Length == 0 ? CtorStrategy.AllParams : CtorStrategy.Mixed;
        return new CtorPlan(strategy, candidate, ctorParams, remaining);
    }

    private static bool CanSet(INamedTypeSymbol type, MemberDescriptor m)
    {
        // Field — ok если public + не readonly
        var field = type.GetMembers(m.Name).OfType<IFieldSymbol>().FirstOrDefault();
        if (field is not null) return !field.IsReadOnly;

        // Property — нужен setter (включая init)
        var prop = type.GetMembers(m.Name).OfType<IPropertySymbol>().FirstOrDefault();
        if (prop is not null) return prop.SetMethod is not null;

        return false;
    }
}
```

- [ ] **Step 4: Расширить `ReadEmitter` (использовать `CtorBinder`)**

В `XPacketRpc.Generators/Emit/ReadEmitter.cs` модифицировать `EmitReadMethod` так, чтобы вместо
неконтекстного "new ... { ... }" вызывать выбранную стратегию из `CtorBinder.Bind(type)`.
Полная замена object-construction блока:

```csharp
            // Object construction
            var binder = new global::XPacketRpc.Generators.Discovery.CtorBinder(this.walker);
            var plan = binder.Bind(type);

            switch (plan.Strategy)
            {
                case global::XPacketRpc.Generators.Discovery.CtorStrategy.Parameterless:
                    sb.AppendLine($"return new {fq}");
                    sb.AppendLine("{");
                    using (sb.Indent())
                    {
                        for (int i = 0; i < sorted.Length; i++)
                        {
                            var m = sorted[i];
                            var sep = i == sorted.Length - 1 ? "" : ",";
                            sb.AppendLine($"{m.Name} = {Camel(m.Name)}{sep}");
                        }
                    }
                    sb.AppendLine("};");
                    break;

                case global::XPacketRpc.Generators.Discovery.CtorStrategy.AllParams:
                    {
                        var args = string.Join(", ", plan.CtorParams.Select(p => Camel(p.Name)));
                        sb.AppendLine($"return new {fq}({args});");
                    }
                    break;

                case global::XPacketRpc.Generators.Discovery.CtorStrategy.Mixed:
                    {
                        var args = string.Join(", ", plan.CtorParams.Select(p => Camel(p.Name)));
                        sb.AppendLine($"return new {fq}({args})");
                        sb.AppendLine("{");
                        using (sb.Indent())
                        {
                            for (int i = 0; i < plan.SetterMembers.Length; i++)
                            {
                                var m = plan.SetterMembers[i];
                                var sep = i == plan.SetterMembers.Length - 1 ? "" : ",";
                                sb.AppendLine($"{m.Name} = {Camel(m.Name)}{sep}");
                            }
                        }
                        sb.AppendLine("};");
                    }
                    break;

                case global::XPacketRpc.Generators.Discovery.CtorStrategy.Impossible:
                    sb.AppendLine($"throw new global::XPacketRpc.RpcSerializationException(\"XPRPC003: cannot construct {type.Name}: \" + {EscapeForCs(plan.Reason ?? "")});");
                    break;
            }
```

> Helper `EscapeForCs(string)` — простое экранирование кавычек, добавьте в `IndentedStringBuilder` или
> отдельным static-методом в `ReadEmitter`.

(Удалите старый блок построения через `new ... { ... }` который был добавлен в Phase 7.)

- [ ] **Step 5: Прогнать тесты**

```
dotnet test XPacketRpc.Generators.Tests --filter "FullyQualifiedName~CtorBinderTests"
dotnet test XPacketRpc.Generators.Tests --filter "FullyQualifiedName~ReadEmitterTests"
```

Expected: всё passed (старые ReadEmitter-тесты должны продолжать работать через `Parameterless`-ветку).

- [ ] **Step 6: Commit**

```
git add XPacketRpc.Generators/Discovery/CtorBinder.cs XPacketRpc.Generators/Emit/ReadEmitter.cs XPacketRpc.Generators.Tests/CtorBinderTests.cs
git commit -m "feat(generator): add CtorBinder + integrate into ReadEmitter (parameterless/all-params/mixed)"
```

---

## Phase 9 — Generator: pipeline integration + smoke E2E

### Task 9.1: Подключить `CallSiteCollector` + `TypeWalker` + `WriteEmitter` + `ReadEmitter` + `RegistryEmitter` в `XPacketRpcGenerator`

**Files:**
- Modify: `XPacketRpc.Generators/XPacketRpcGenerator.cs`
- Create: `XPacketRpc.Tests/E2E/SmokeRoundtripTests.cs`

- [ ] **Step 1: Расширить генератор**

Замените тело `XPacketRpc.Generators/XPacketRpcGenerator.cs`:

```csharp
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using XPacketRpc.Generators.Diagnostics;
using XPacketRpc.Generators.Discovery;
using XPacketRpc.Generators.Emit;

namespace XPacketRpc.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class XPacketRpcGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
        {
            const string marker = """
                // <auto-generated/>
                namespace XPacketRpc.Generated;
                internal static class __GeneratorMarker { public const string Version = "0.1"; }
                """;
            ctx.AddSource("__GeneratorMarker.g.cs", SourceText.From(marker, Encoding.UTF8));
        });

        // Найти все вызовы 5 целевых методов
        var callSites = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is InvocationExpressionSyntax,
                transform: static (ctx, ct) =>
                {
                    var inv = (InvocationExpressionSyntax)ctx.Node;
                    return (inv, ctx.SemanticModel);
                })
            .Collect();

        var compilationAndCallSites = context.CompilationProvider.Combine(callSites);

        context.RegisterSourceOutput(compilationAndCallSites, static (spc, pair) =>
        {
            var (compilation, sites) = pair;
            Execute(spc, compilation, sites);
        });
    }

    private static void Execute(
        SourceProductionContext spc,
        Compilation compilation,
        ImmutableArray<(InvocationExpressionSyntax inv, SemanticModel model)> sites)
    {
        var collector = new CallSiteCollector();
        var walker = new TypeWalker(compilation);

        var discovered = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var (inv, model) in sites)
        {
            // collector работает с syntax tree; адаптируем сюда, проверяем по invocation
            var symbol = model.GetSymbolInfo(inv).Symbol as IMethodSymbol;
            if (symbol is null) continue;
            if (symbol.TypeArguments.Length != 1) continue;

            // Используем тот же фильтр, что и в CallSiteCollector
            var container = symbol.ContainingType;
            if (container is null) continue;
            var fq = container.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            bool isXPRpc = fq == "global::XPacketRpc.XPRpc" &&
                           (symbol.Name is "Touch" or "Write" or "Read");
            bool isSerializer = (fq == "global::XPacketRpc.IRpcSerializer" ||
                                 container.AllInterfaces.Any(i =>
                                     i.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
                                     "global::XPacketRpc.IRpcSerializer"))
                && (symbol.Name is "Serialize" or "Deserialize");
            if (!isXPRpc && !isSerializer) continue;

            var t = symbol.TypeArguments[0];
            if (t.TypeKind == TypeKind.TypeParameter)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    Descriptors.OpenGenericCallSite,
                    inv.GetLocation(),
                    symbol.Name, t.Name));
                continue;
            }

            if (t is INamedTypeSymbol nts) discovered.Add(nts);
        }

        // Транзитивно — все DTO в closure
        var allTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var d in discovered)
        {
            foreach (var t in walker.Closure(d))
            {
                allTypes.Add(t);
            }
        }

        // Эмит per-type
        var writeEmitter = new WriteEmitter(walker);
        var readEmitter = new ReadEmitter(walker);
        foreach (var t in allTypes)
        {
            // Skip встроенные — они не DTO
            if (IsBuiltinSkipType(t)) continue;

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("namespace XPacketRpc.Generated;");
            sb.AppendLine();
            sb.AppendLine($"internal static class __XPRpcGen_{Sanitize(t.Name)}");
            sb.AppendLine("{");
            sb.AppendLine(writeEmitter.EmitWriteMethod(t));
            sb.AppendLine();
            sb.AppendLine(readEmitter.EmitReadMethod(t));
            sb.AppendLine("}");

            spc.AddSource($"__XPRpcGen_{Sanitize(t.Name)}.g.cs",
                SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        // Registry
        if (allTypes.Count > 0)
        {
            var reg = new RegistryEmitter().Emit(
                allTypes.Where(t => !IsBuiltinSkipType(t)),
                compilation.AssemblyName ?? "Unknown");
            spc.AddSource("__XPRpcRegistry.g.cs", SourceText.From(reg, Encoding.UTF8));
        }
    }

    private static bool IsBuiltinSkipType(INamedTypeSymbol t)
    {
        // встроенные типы из mscorlib — не должны попадать в emit
        var ns = t.ContainingNamespace?.ToDisplayString();
        return ns == "System" || ns?.StartsWith("System.") == true;
    }

    private static string Sanitize(string s) =>
        new(s.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
}
```

- [ ] **Step 2: Smoke E2E test (DTO + Roundtrip через генератор)**

`XPacketRpc.Tests/E2E/SmokeRoundtripTests.cs`:

```csharp
using XPacketRpc;

namespace XPacketRpc.Tests.E2E;

public sealed class SmokeDto
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string? Comment { get; init; }
}

public class SmokeRoundtripTests
{
    [Test]
    public async Task Roundtrip_simple_dto()
    {
        var s = new XPacketRpcSerializer();
        var input = new SmokeDto { Id = 7, Name = "Bob", Comment = null };

        var bytes = s.Serialize(input);
        var got = s.Deserialize<SmokeDto>(bytes);

        await Assert.That(got).IsNotNull();
        await Assert.That(got!.Id).IsEqualTo(7);
        await Assert.That(got.Name).IsEqualTo("Bob");
        await Assert.That(got.Comment).IsNull();
    }

    [Test]
    public async Task Roundtrip_with_comment()
    {
        var s = new XPacketRpcSerializer();
        var input = new SmokeDto { Id = 9, Name = "Alice", Comment = "test" };

        var bytes = s.Serialize(input);
        var got = s.Deserialize<SmokeDto>(bytes);

        await Assert.That(got).IsNotNull();
        await Assert.That(got!.Id).IsEqualTo(9);
        await Assert.That(got.Name).IsEqualTo("Alice");
        await Assert.That(got.Comment).IsEqualTo("test");
    }
}
```

- [ ] **Step 3: Build + Test (E2E)**

```
dotnet build XPacketRpc.Tests -c Debug
dotnet test XPacketRpc.Tests --filter "FullyQualifiedName~SmokeRoundtripTests"
```

Expected: 2 tests passed. Если падает — диагностика:
- проверьте, что generator активирован (присутствует `XPacketRpc.Generated.__XPRpcGen_SmokeDto` в `obj/Debug/net10.0/generated/`),
- проверьте FNV-1a порядок полей в generated-файле,
- проверьте bitmap-логику.

- [ ] **Step 4: Полный test-run**

```
dotnet test TCPProtocol.sln -c Debug
```

Expected: все XPacketRpc.Tests + XPacketRpc.Generators.Tests + legacy XProtocol.Tests passed.

- [ ] **Step 5: Commit**

```
git add XPacketRpc.Generators/XPacketRpcGenerator.cs XPacketRpc.Tests/E2E/
git commit -m "feat(generator): wire up full pipeline (discovery → emit → registry) + E2E smoke"
```

---

## Phase 10 — Tests: full §8 coverage

Тесты группируются по категориям. Каждая категория — отдельный таск, отдельный коммит.

### Task 10.1: Roundtrip tests для всех 8 DTO из spec

**Files:**
- Create: `XPacketRpc.Tests/Dtos/BenchmarkDtos.cs` (DTOs из §7.1 spec'а)
- Create: `XPacketRpc.Tests/RoundtripTests.cs`

- [ ] **Step 1: DTO определения**

`XPacketRpc.Tests/Dtos/BenchmarkDtos.cs`:

```csharp
namespace XPacketRpc.Tests.Dtos;

public sealed class Vector3
{
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
}

public sealed class LogEntry
{
    public DateTimeOffset Timestamp { get; init; }
    public byte Level { get; init; }
    public string Message { get; init; } = "";
    public Guid TraceId { get; init; }
    public Guid SpanId { get; init; }
}

public sealed class OrderItem
{
    public int ProductId { get; init; }
    public int Quantity { get; init; }
    public decimal Price { get; init; }
}

public sealed class OrderRequest
{
    public Guid Id { get; init; }
    public int CustomerId { get; init; }
    public List<OrderItem> Items { get; init; } = new();
}

public sealed class Address
{
    public string Street { get; init; } = "";
    public string City { get; init; } = "";
    public string Country { get; init; } = "";
}

public sealed class UserProfile
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public Address Address { get; init; } = new();
    public string[] Tags { get; init; } = Array.Empty<string>();
}

public sealed class ChunkPayload
{
    public Guid Id { get; init; }
    public byte[] Data { get; init; } = Array.Empty<byte>();
}

public sealed class BigDictionary
{
    public Dictionary<string, int> Data { get; init; } = new();
}

public sealed class Level5 { public int Value { get; init; } }
public sealed class Level4 { public Level5 Inner { get; init; } = new(); public int X { get; init; } }
public sealed class Level3 { public Level4 Inner { get; init; } = new(); public int X { get; init; } }
public sealed class Level2 { public Level3 Inner { get; init; } = new(); public int X { get; init; } }
public sealed class DeepNested { public Level2 Inner { get; init; } = new(); public int X { get; init; } }

public sealed record class RecordRequest(
    Guid Id, int CustomerId, string Name, DateTimeOffset CreatedAt, decimal Total)
{
    public string? Comment { get; init; }
}
```

- [ ] **Step 2: Roundtrip-тесты с фабрикой**

`XPacketRpc.Tests/RoundtripTests.cs`:

```csharp
using XPacketRpc;
using XPacketRpc.Tests.Dtos;

namespace XPacketRpc.Tests;

public class RoundtripTests
{
    private readonly XPacketRpcSerializer s = new();

    [Test]
    public async Task Vector3_roundtrip()
    {
        var input = new Vector3 { X = 1.5f, Y = -2.25f, Z = 3.0f };
        var got = s.Deserialize<Vector3>(s.Serialize(input));

        await Assert.That(got!.X).IsEqualTo(1.5f);
        await Assert.That(got.Y).IsEqualTo(-2.25f);
        await Assert.That(got.Z).IsEqualTo(3.0f);
    }

    [Test]
    public async Task LogEntry_roundtrip()
    {
        var input = new LogEntry
        {
            Timestamp = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.FromHours(3)),
            Level = 4,
            Message = "Hello, мир!",
            TraceId = Guid.Parse("01020304-0506-0708-090A-0B0C0D0E0F10"),
            SpanId = Guid.NewGuid(),
        };
        var got = s.Deserialize<LogEntry>(s.Serialize(input));

        await Assert.That(got!.Timestamp).IsEqualTo(input.Timestamp);
        await Assert.That(got.Level).IsEqualTo(input.Level);
        await Assert.That(got.Message).IsEqualTo(input.Message);
        await Assert.That(got.TraceId).IsEqualTo(input.TraceId);
        await Assert.That(got.SpanId).IsEqualTo(input.SpanId);
    }

    [Test]
    public async Task OrderRequest_roundtrip_with_5_items()
    {
        var input = new OrderRequest
        {
            Id = Guid.NewGuid(),
            CustomerId = 42,
            Items = Enumerable.Range(0, 5).Select(i => new OrderItem
            {
                ProductId = i, Quantity = i + 1, Price = i * 1.5m
            }).ToList()
        };

        var got = s.Deserialize<OrderRequest>(s.Serialize(input));

        await Assert.That(got!.Id).IsEqualTo(input.Id);
        await Assert.That(got.CustomerId).IsEqualTo(42);
        await Assert.That(got.Items.Count).IsEqualTo(5);
        for (int i = 0; i < 5; i++)
        {
            await Assert.That(got.Items[i].ProductId).IsEqualTo(input.Items[i].ProductId);
            await Assert.That(got.Items[i].Price).IsEqualTo(input.Items[i].Price);
        }
    }

    [Test]
    public async Task UserProfile_roundtrip()
    {
        var input = new UserProfile
        {
            Id = Guid.NewGuid(),
            Name = "Anna",
            Address = new Address { Street = "Main", City = "Tashkent", Country = "UZ" },
            Tags = new[] { "vip", "early-adopter" }
        };
        var got = s.Deserialize<UserProfile>(s.Serialize(input));

        await Assert.That(got!.Name).IsEqualTo("Anna");
        await Assert.That(got.Address.City).IsEqualTo("Tashkent");
        await Assert.That(got.Tags).IsEquivalentTo(input.Tags);
    }

    [Test]
    public async Task ChunkPayload_16K_roundtrip()
    {
        var data = new byte[16 * 1024];
        new Random(42).NextBytes(data);
        var input = new ChunkPayload { Id = Guid.NewGuid(), Data = data };

        var got = s.Deserialize<ChunkPayload>(s.Serialize(input));
        await Assert.That(got!.Data).IsEquivalentTo(data);
    }

    [Test]
    public async Task BigDictionary_1000_roundtrip()
    {
        var input = new BigDictionary
        {
            Data = Enumerable.Range(0, 1000).ToDictionary(i => $"key-{i}", i => i)
        };

        var got = s.Deserialize<BigDictionary>(s.Serialize(input));
        await Assert.That(got!.Data.Count).IsEqualTo(1000);
        await Assert.That(got.Data["key-500"]).IsEqualTo(500);
    }

    [Test]
    public async Task DeepNested_roundtrip()
    {
        var input = new DeepNested
        {
            X = 1,
            Inner = new Level2
            {
                X = 2,
                Inner = new Level3
                {
                    X = 3,
                    Inner = new Level4
                    {
                        X = 4,
                        Inner = new Level5 { Value = 99 }
                    }
                }
            }
        };
        var got = s.Deserialize<DeepNested>(s.Serialize(input));

        await Assert.That(got!.Inner.Inner.Inner.Inner.Value).IsEqualTo(99);
        await Assert.That(got.X).IsEqualTo(1);
    }

    [Test]
    public async Task RecordRequest_roundtrip_via_ctor_binding()
    {
        var input = new RecordRequest(
            Guid.NewGuid(), 7, "test", DateTimeOffset.UtcNow, 99.99m)
        {
            Comment = "approved"
        };

        var got = s.Deserialize<RecordRequest>(s.Serialize(input));

        await Assert.That(got).IsEqualTo(input);
    }
}
```

- [ ] **Step 3: Прогнать тесты**

```
dotnet test XPacketRpc.Tests --filter "FullyQualifiedName~RoundtripTests"
```

Expected: 8 tests passed.

- [ ] **Step 4: Commit**

```
git add XPacketRpc.Tests/Dtos/ XPacketRpc.Tests/RoundtripTests.cs
git commit -m "test(rpc): roundtrip coverage for all 8 spec DTOs"
```

---

### Task 10.2: Wire-format golden tests (regression-guard на байтовое представление)

**Files:**
- Create: `XPacketRpc.Tests/WireFormatTests.cs`

- [ ] **Step 1: Тесты с явным байтовым ожиданием**

`XPacketRpc.Tests/WireFormatTests.cs`:

```csharp
using XPacketRpc;
using XPacketRpc.Tests.Dtos;

namespace XPacketRpc.Tests;

public class WireFormatTests
{
    private readonly XPacketRpcSerializer s = new();

    [Test]
    public async Task Vector3_wire_layout()
    {
        // Vector3 поля: X, Y, Z (3 × float)
        // hash-сорт: Fnv1a("X")=0xA9C5DDB7, Fnv1a("Y")=0xA0A4DDB7, Fnv1a("Z")=0xB7A4DDB7
        // (значения иллюстративные; пересчитайте через Fnv1aTests)
        // Ожидаемый порядок после сортировки — зависит от реальных hash-значений.
        var input = new Vector3 { X = 1.0f, Y = 2.0f, Z = 3.0f };
        var bytes = s.Serialize(input);

        // 0 nullable полей → bitmap отсутствует. 3 × 4 = 12 байт.
        await Assert.That(bytes.Length).IsEqualTo(12);
    }

    [Test]
    public async Task Empty_string_emits_zero_length_byte()
    {
        // record с одной string ожидаемо: bitmap (если нужен) + varint(0)
        var input = new LogEntry { Timestamp = default, Level = 0, Message = "", TraceId = default, SpanId = default };
        var bytes = s.Serialize(input);

        // Найти, что Message=`""` → 1 байт (varint 0)
        // Проверка: есть один байт со значением 0x00 (varint length)
        await Assert.That(bytes).Contains((byte)0x00);
    }
}
```

- [ ] **Step 2: Прогнать**

```
dotnet test XPacketRpc.Tests --filter "FullyQualifiedName~WireFormatTests"
```

> **Note:** Для точного byte-by-byte ожидания пересчитайте FNV-1a порядок при первом запуске,
> зафиксируйте actual в тесте как expected (это regression-baseline). Файл `WireFormatTests.cs`
> играет роль **golden master** — любое изменение wire-формата падает на этих тестах.

- [ ] **Step 3: Commit**

```
git add XPacketRpc.Tests/WireFormatTests.cs
git commit -m "test(rpc): add wire-format golden tests (regression-guard)"
```

---

### Task 10.3: Nullability tests (null в required throw; null в nullable serializes)

**Files:**
- Create: `XPacketRpc.Tests/NullabilityTests.cs`

- [ ] **Step 1: Тесты**

`XPacketRpc.Tests/NullabilityTests.cs`:

```csharp
using XPacketRpc;

namespace XPacketRpc.Tests;

public sealed class NullableDto
{
    public int Id { get; init; }
    public string Name { get; init; } = "";        // required
    public string? Comment { get; init; }          // nullable
    public int? Score { get; init; }               // value-type nullable
}

public class NullabilityTests
{
    private readonly XPacketRpcSerializer s = new();

    [Test]
    public async Task Required_string_null_throws_on_serialize()
    {
        var input = new NullableDto { Id = 1, Name = null!, Comment = null, Score = null };

        await Assert.That(() => s.Serialize(input))
            .Throws<RpcSerializationException>()
            .WithMessageContaining("Name");
    }

    [Test]
    public async Task Nullable_string_null_roundtrips()
    {
        var input = new NullableDto { Id = 1, Name = "ok", Comment = null, Score = null };
        var got = s.Deserialize<NullableDto>(s.Serialize(input));

        await Assert.That(got!.Comment).IsNull();
        await Assert.That(got.Score).IsNull();
    }

    [Test]
    public async Task Nullable_int_with_value_roundtrips()
    {
        var input = new NullableDto { Id = 1, Name = "ok", Comment = null, Score = 42 };
        var got = s.Deserialize<NullableDto>(s.Serialize(input));

        await Assert.That(got!.Score).IsEqualTo(42);
    }

    [Test]
    public async Task Both_nullable_filled_roundtrips()
    {
        var input = new NullableDto { Id = 1, Name = "ok", Comment = "yes", Score = 7 };
        var got = s.Deserialize<NullableDto>(s.Serialize(input));

        await Assert.That(got!.Comment).IsEqualTo("yes");
        await Assert.That(got.Score).IsEqualTo(7);
    }
}
```

- [ ] **Step 2: Прогнать + commit**

```
dotnet test XPacketRpc.Tests --filter "FullyQualifiedName~NullabilityTests"
git add XPacketRpc.Tests/NullabilityTests.cs
git commit -m "test(rpc): nullability — required throws, optional roundtrips"
```

---

### Task 10.4: Edge cases — strings (empty, Unicode BMP + supplementary)

**Files:**
- Create: `XPacketRpc.Tests/Edge/StringEdgeTests.cs`

- [ ] **Step 1: Тесты**

`XPacketRpc.Tests/Edge/StringEdgeTests.cs`:

```csharp
using XPacketRpc;

namespace XPacketRpc.Tests.Edge;

public sealed class StringHolder
{
    public string S { get; init; } = "";
}

public class StringEdgeTests
{
    private readonly XPacketRpcSerializer s = new();

    [Test] public async Task Empty_string_roundtrips()
    {
        var got = s.Deserialize<StringHolder>(s.Serialize(new StringHolder { S = "" }));
        await Assert.That(got!.S).IsEqualTo("");
    }

    [Test] public async Task Cyrillic_BMP_roundtrips()
    {
        var input = "Привет, мир! Тест на кириллицу.";
        var got = s.Deserialize<StringHolder>(s.Serialize(new StringHolder { S = input }));
        await Assert.That(got!.S).IsEqualTo(input);
    }

    [Test] public async Task Emoji_supplementary_roundtrips()
    {
        var input = "Hello 🌍🔥 emoji";
        var got = s.Deserialize<StringHolder>(s.Serialize(new StringHolder { S = input }));
        await Assert.That(got!.S).IsEqualTo(input);
    }

    [Test] public async Task Long_string_uses_multi_byte_varint()
    {
        // 200 chars → length=200, varint занимает 2 байта (>127)
        var input = new string('x', 200);
        var got = s.Deserialize<StringHolder>(s.Serialize(new StringHolder { S = input }));
        await Assert.That(got!.S.Length).IsEqualTo(200);
    }
}
```

- [ ] **Step 2: Прогнать + commit**

```
dotnet test XPacketRpc.Tests --filter "FullyQualifiedName~StringEdgeTests"
git add XPacketRpc.Tests/Edge/StringEdgeTests.cs
git commit -m "test(rpc): string edge cases — empty/BMP/supplementary/long"
```

---

### Task 10.5: Edge cases — collections (empty, varint > 1 байт)

**Files:**
- Create: `XPacketRpc.Tests/Edge/CollectionEdgeTests.cs`

- [ ] **Step 1: Тесты**

`XPacketRpc.Tests/Edge/CollectionEdgeTests.cs`:

```csharp
using XPacketRpc;

namespace XPacketRpc.Tests.Edge;

public sealed class IntListHolder { public List<int> L { get; init; } = new(); }
public sealed class StringArrayHolder { public string[] A { get; init; } = Array.Empty<string>(); }

public class CollectionEdgeTests
{
    private readonly XPacketRpcSerializer s = new();

    [Test] public async Task Empty_list_roundtrips()
    {
        var got = s.Deserialize<IntListHolder>(s.Serialize(new IntListHolder { L = new() }));
        await Assert.That(got!.L.Count).IsEqualTo(0);
    }

    [Test] public async Task Empty_array_roundtrips()
    {
        var got = s.Deserialize<StringArrayHolder>(s.Serialize(new StringArrayHolder { A = Array.Empty<string>() }));
        await Assert.That(got!.A.Length).IsEqualTo(0);
    }

    [Test] public async Task List_with_varint_size_above_127()
    {
        var input = new IntListHolder { L = Enumerable.Range(0, 500).ToList() };
        var got = s.Deserialize<IntListHolder>(s.Serialize(input));
        await Assert.That(got!.L.Count).IsEqualTo(500);
        await Assert.That(got.L[499]).IsEqualTo(499);
    }
}
```

- [ ] **Step 2: Прогнать + commit**

```
dotnet test XPacketRpc.Tests --filter "FullyQualifiedName~CollectionEdgeTests"
git add XPacketRpc.Tests/Edge/CollectionEdgeTests.cs
git commit -m "test(rpc): collection edge cases — empty + varint>127"
```

---

### Task 10.6: Edge cases — numerics (decimal sign, DateTime boundaries, enum byte/long)

**Files:**
- Create: `XPacketRpc.Tests/Edge/NumericEdgeTests.cs`

- [ ] **Step 1: Тесты**

`XPacketRpc.Tests/Edge/NumericEdgeTests.cs`:

```csharp
using XPacketRpc;

namespace XPacketRpc.Tests.Edge;

public enum ByteEnum : byte { A = 0, B = 255 }
public enum LongEnum : long { Min = long.MinValue, Max = long.MaxValue }

public sealed class NumericHolder
{
    public decimal D { get; init; }
    public DateTime Dt { get; init; }
    public DateTimeOffset Dto { get; init; }
    public ByteEnum BE { get; init; }
    public LongEnum LE { get; init; }
}

public class NumericEdgeTests
{
    private readonly XPacketRpcSerializer s = new();

    [Test] public async Task Decimal_min_roundtrips()
    {
        var got = s.Deserialize<NumericHolder>(s.Serialize(new NumericHolder
        {
            D = decimal.MinValue, Dt = DateTime.UnixEpoch, Dto = DateTimeOffset.MinValue,
            BE = ByteEnum.A, LE = LongEnum.Min
        }));
        await Assert.That(got!.D).IsEqualTo(decimal.MinValue);
    }

    [Test] public async Task Decimal_max_roundtrips()
    {
        var got = s.Deserialize<NumericHolder>(s.Serialize(new NumericHolder
        {
            D = decimal.MaxValue, Dt = DateTime.UnixEpoch, Dto = DateTimeOffset.UtcNow,
            BE = ByteEnum.B, LE = LongEnum.Max
        }));
        await Assert.That(got!.D).IsEqualTo(decimal.MaxValue);
        await Assert.That(got.LE).IsEqualTo(LongEnum.Max);
    }

    [Test] public async Task DateTime_min_max_roundtrip()
    {
        var got = s.Deserialize<NumericHolder>(s.Serialize(new NumericHolder
        {
            D = 0m, Dt = DateTime.MinValue, Dto = DateTimeOffset.MaxValue,
            BE = ByteEnum.A, LE = LongEnum.Min
        }));
        await Assert.That(got!.Dt).IsEqualTo(DateTime.MinValue);
        await Assert.That(got.Dto).IsEqualTo(DateTimeOffset.MaxValue);
    }

    [Test] public async Task DateTime_kind_preserved()
    {
        var dt = new DateTime(2026, 5, 8, 12, 0, 0, DateTimeKind.Utc);
        var got = s.Deserialize<NumericHolder>(s.Serialize(new NumericHolder
        {
            D = 0m, Dt = dt, Dto = DateTimeOffset.UtcNow,
            BE = ByteEnum.A, LE = LongEnum.Min
        }));
        await Assert.That(got!.Dt.Kind).IsEqualTo(DateTimeKind.Utc);
    }
}
```

- [ ] **Step 2: Прогнать + commit**

```
dotnet test XPacketRpc.Tests --filter "FullyQualifiedName~NumericEdgeTests"
git add XPacketRpc.Tests/Edge/NumericEdgeTests.cs
git commit -m "test(rpc): numeric edge cases — decimal, DateTime, enum byte/long"
```

---

### Task 10.7: Generator snapshot tests

**Files:**
- Create: `XPacketRpc.Generators.Tests/GeneratorSnapshotTests.cs`

> **Note:** snapshot-тесты сравнивают сгенерированный код со встроенной string-baseline.
> Для упрощения **не используем** Verify-based snapshot library; пишем `Contains`-asserts.
> Если нужен полноценный snapshot-сравнитель — добавьте `Verify.SourceGenerators` пакетом отдельно.

- [ ] **Step 1: Тесты**

`XPacketRpc.Generators.Tests/GeneratorSnapshotTests.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XPacketRpc.Generators;

namespace XPacketRpc.Generators.Tests;

public class GeneratorSnapshotTests
{
    private static GeneratorDriverRunResult Run(string userSource)
    {
        var fakeRuntime = """
            #nullable enable
            using System;
            using System.Buffers;
            namespace XPacketRpc
            {
                public interface IRpcSerializer
                {
                    string ContentType { get; }
                    byte[] Serialize<T>(T value);
                    T? Deserialize<T>(ReadOnlyMemory<byte> payload);
                }
                public ref struct XPRpcReader { public XPRpcReader(ReadOnlySpan<byte> s) { } }
                public static class XPRpc
                {
                    public delegate void WriteDelegate<T>(T value, IBufferWriter<byte> w);
                    public delegate T ReadDelegate<T>(ref XPRpcReader r);
                    public static void Touch<T>() {}
                    public static void Register<T>(WriteDelegate<T> w, ReadDelegate<T> r) {}
                    public static void Write<T>(T value, IBufferWriter<byte> w) {}
                    public static T? Read<T>(ReadOnlySpan<byte> source) => default;
                }
            }
            """;
        var trees = new[]
        {
            CSharpSyntaxTree.ParseText(fakeRuntime),
            CSharpSyntaxTree.ParseText(userSource)
        };
        var refs = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IBufferWriter<>).Assembly.Location),
        };
        var comp = CSharpCompilation.Create("UserAsm", trees, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver.Create(new XPacketRpcGenerator());
        return driver.RunGenerators(comp).GetRunResult();
    }

    [Test]
    public async Task Generator_emits_GenClass_for_discovered_DTO()
    {
        var src = """
            using XPacketRpc;
            public class Foo { public int X; }
            public static class Boot { public static void Init() => XPRpc.Touch<Foo>(); }
            """;
        var result = Run(src);
        var sources = result.GeneratedTrees.Select(t => t.GetText().ToString()).ToArray();

        await Assert.That(sources.Any(s => s.Contains("__XPRpcGen_Foo"))).IsTrue();
        await Assert.That(sources.Any(s => s.Contains("__XPRpcRegistry"))).IsTrue();
        await Assert.That(sources.Any(s => s.Contains("ModuleInitializer"))).IsTrue();
    }

    [Test]
    public async Task Generator_emits_no_DTO_class_when_no_call_sites()
    {
        var src = """
            public class Foo { public int X; }
            """;
        var result = Run(src);
        var sources = result.GeneratedTrees.Select(t => t.GetText().ToString()).ToArray();

        await Assert.That(sources.Any(s => s.Contains("__XPRpcGen_Foo"))).IsFalse();
    }
}
```

- [ ] **Step 2: Прогнать + commit**

```
dotnet test XPacketRpc.Generators.Tests --filter "FullyQualifiedName~GeneratorSnapshotTests"
git add XPacketRpc.Generators.Tests/GeneratorSnapshotTests.cs
git commit -m "test(generator): snapshot tests for generated DTO class + registry"
```

---

### Task 10.8: Diagnostic tests `XPRPC001..006`

**Files:**
- Create: `XPacketRpc.Generators.Tests/DiagnosticTests.cs`

- [ ] **Step 1: Тесты для XPRPC001 (open generic call-site) и XPRPC002 (open generic type)**

`XPacketRpc.Generators.Tests/DiagnosticTests.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XPacketRpc.Generators;

namespace XPacketRpc.Generators.Tests;

public class DiagnosticTests
{
    private static (Compilation comp, IEnumerable<Diagnostic> diags) Run(string userSource)
    {
        var fakeRuntime = """
            #nullable enable
            using System;
            using System.Buffers;
            namespace XPacketRpc
            {
                public interface IRpcSerializer { string ContentType { get; } byte[] Serialize<T>(T v); T? Deserialize<T>(ReadOnlyMemory<byte> p); }
                public ref struct XPRpcReader { public XPRpcReader(ReadOnlySpan<byte> s) {} }
                public static class XPRpc
                {
                    public delegate void WriteDelegate<T>(T value, IBufferWriter<byte> w);
                    public delegate T ReadDelegate<T>(ref XPRpcReader r);
                    public static void Touch<T>() {}
                    public static void Register<T>(WriteDelegate<T> w, ReadDelegate<T> r) {}
                    public static void Write<T>(T value, IBufferWriter<byte> w) {}
                    public static T? Read<T>(ReadOnlySpan<byte> source) => default;
                }
            }
            """;
        var trees = new[]
        {
            CSharpSyntaxTree.ParseText(fakeRuntime),
            CSharpSyntaxTree.ParseText(userSource)
        };
        var refs = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IBufferWriter<>).Assembly.Location),
        };
        var comp = CSharpCompilation.Create("UserAsm", trees, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver.Create(new XPacketRpcGenerator());
        var run = driver.RunGenerators(comp).GetRunResult();
        return (comp, run.Diagnostics);
    }

    [Test]
    public async Task XPRPC001_open_generic_call_site()
    {
        var src = """
            using XPacketRpc;
            public static class Foo { public static void Generic<T>() => XPRpc.Touch<T>(); }
            """;
        var (_, diags) = Run(src);

        await Assert.That(diags.Any(d => d.Id == "XPRPC001")).IsTrue();
    }

    // XPRPC002, XPRPC003, XPRPC004, XPRPC006 — добавьте по мере появления соответствующих
    // путей через `Closure`/`CtorBinder`/`ClassifyType`. Каждый — отдельный тест.
}
```

> **Note:** XPRPC003 покрывается в Task 8.1 (CtorBinder). XPRPC005 — defensive, не пишем тест.
> XPRPC002/004/006 — добавьте по аналогии с XPRPC001 после reproducible-сценариев в integration.

- [ ] **Step 2: Прогнать + commit**

```
dotnet test XPacketRpc.Generators.Tests --filter "FullyQualifiedName~DiagnosticTests"
git add XPacketRpc.Generators.Tests/DiagnosticTests.cs
git commit -m "test(generator): diagnostic test for XPRPC001 (open-generic call-site)"
```

---

## Phase 11 — Benchmarks

### Task 11.1: BDN-config + base setup

**Files:**
- Create: `XPacketRpc.Benchmarks/BenchConfig.cs`
- Modify: `XPacketRpc.Benchmarks/Program.cs`

- [ ] **Step 1: Config**

`XPacketRpc.Benchmarks/BenchConfig.cs`:

```csharp
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Jobs;

namespace XPacketRpc.Benchmarks;

public sealed class BenchConfig : ManualConfig
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
        AddExporter(MarkdownExporter.GitHub);
        AddExporter(CsvExporter.Default);
        AddColumnProvider(DefaultColumnProviders.Instance);
    }
}
```

- [ ] **Step 2: Program.cs**

```csharp
using BenchmarkDotNet.Running;

namespace XPacketRpc.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly)
            .Run(args, new BenchConfig());
    }
}
```

- [ ] **Step 3: Build + commit**

```
dotnet build XPacketRpc.Benchmarks -c Release
git add XPacketRpc.Benchmarks/
git commit -m "feat(benchmarks): add BDN config and switcher entry"
```

---

### Task 11.2: 8 DTO + per-serializer варианты + DtoFactory

**Files:**
- Create: `XPacketRpc.Benchmarks/Dtos/Variants.cs` (per-serializer marked DTO copies)
- Create: `XPacketRpc.Benchmarks/Dtos/DtoFactory.cs` (seeded random data)

> **Note:** для XPacketRpc, MessagePack contractless, System.Text.Json reflection-based, protobuf-net
> RuntimeTypeModel — DTO без атрибутов. Для **MemoryPack** — нужны `[MemoryPackable] partial`-копии.
> Для **Bond** — `.bond`-схема (см. Task 11.6, может быть выкинуто per spec §7.2).

- [ ] **Step 1: Per-serializer варианты (MemoryPack)**

`XPacketRpc.Benchmarks/Dtos/Variants.MemoryPack.cs`:

```csharp
using MemoryPack;

namespace XPacketRpc.Benchmarks.Dtos.MP;

[MemoryPackable] public partial class Vector3 { public float X; public float Y; public float Z; }
[MemoryPackable] public partial class LogEntry
{
    public DateTimeOffset Timestamp;
    public byte Level;
    public string Message = "";
    public Guid TraceId;
    public Guid SpanId;
}
// ... остальные 6 DTO по аналогии
```

> **Important:** скопируйте все 8 DTO по аналогии. Для record/init-only — оставьте properties с
> setter'ами в MemoryPack-варианте (он не поддерживает init из коробки).

- [ ] **Step 2: protobuf-net runtime model setup**

`XPacketRpc.Benchmarks/Dtos/Variants.ProtoNet.cs`:

```csharp
using ProtoBuf.Meta;

namespace XPacketRpc.Benchmarks.Dtos.PB;

public static class ProtoSetup
{
    private static bool initialized;
    public static void EnsureRegistered()
    {
        if (initialized) return;
        var m = RuntimeTypeModel.Default;
        m.Add(typeof(Tests.Dtos.Vector3), false).Add(1, "X").Add(2, "Y").Add(3, "Z");
        m.Add(typeof(Tests.Dtos.LogEntry), false)
            .Add(1, "Timestamp").Add(2, "Level").Add(3, "Message")
            .Add(4, "TraceId").Add(5, "SpanId");
        // ... аналогично 6 остальным DTO; имена должны совпадать с XPacketRpc DTO именами.
        initialized = true;
    }
}
```

- [ ] **Step 3: DtoFactory с seeded random**

`XPacketRpc.Benchmarks/Dtos/DtoFactory.cs`:

```csharp
using XPacketRpc.Tests.Dtos;

namespace XPacketRpc.Benchmarks.Dtos;

public static class DtoFactory
{
    public static Vector3 Vector3() => new() { X = 1.5f, Y = 2.25f, Z = 3.0f };

    public static LogEntry LogEntry() => new()
    {
        Timestamp = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.FromHours(3)),
        Level = 4,
        Message = "Hello, мир!",
        TraceId = Guid.Parse("01020304-0506-0708-090A-0B0C0D0E0F10"),
        SpanId = Guid.Parse("11121314-1516-1718-191A-1B1C1D1E1F20"),
    };

    public static OrderRequest OrderRequest5() => OrderRequest(5, seed: 42);
    public static OrderRequest OrderRequest50() => OrderRequest(50, seed: 42);

    private static OrderRequest OrderRequest(int n, int seed)
    {
        var rnd = new Random(seed);
        return new()
        {
            Id = Guid.NewGuid(),
            CustomerId = rnd.Next(1, 100000),
            Items = Enumerable.Range(0, n).Select(i => new OrderItem
            {
                ProductId = i, Quantity = rnd.Next(1, 10), Price = (decimal)rnd.NextDouble() * 1000m
            }).ToList()
        };
    }

    public static UserProfile UserProfile() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Anna",
        Address = new Address { Street = "Main 1", City = "Tashkent", Country = "UZ" },
        Tags = new[] { "vip", "early-adopter", "premium" },
    };

    public static ChunkPayload ChunkPayload16K() => ChunkPayload(16 * 1024);
    public static ChunkPayload ChunkPayload64K() => ChunkPayload(64 * 1024);
    private static ChunkPayload ChunkPayload(int size)
    {
        var data = new byte[size];
        new Random(42).NextBytes(data);
        return new() { Id = Guid.NewGuid(), Data = data };
    }

    public static BigDictionary BigDict100() => BigDict(100);
    public static BigDictionary BigDict1000() => BigDict(1000);
    private static BigDictionary BigDict(int n) =>
        new() { Data = Enumerable.Range(0, n).ToDictionary(i => $"key-{i}", i => i) };

    public static DeepNested DeepNested() => new()
    {
        X = 1,
        Inner = new Level2 { X = 2, Inner = new Level3 { X = 3, Inner = new Level4 { X = 4, Inner = new Level5 { Value = 99 } } } }
    };

    public static RecordRequest RecordRequest() =>
        new(Guid.NewGuid(), 7, "test", DateTimeOffset.UtcNow, 99.99m) { Comment = "approved" };
}
```

- [ ] **Step 4: Reference XPacketRpc.Tests.Dtos из benchmarks**

В `XPacketRpc.Benchmarks/XPacketRpc.Benchmarks.csproj` добавьте `Compile Include` linking к
DTO-файлу из tests-проекта (чтобы не дублировать код):

```xml
<ItemGroup>
  <Compile Include="..\XPacketRpc.Tests\Dtos\BenchmarkDtos.cs" Link="Dtos\BenchmarkDtos.cs" />
</ItemGroup>
```

- [ ] **Step 5: Build + commit**

```
dotnet build XPacketRpc.Benchmarks -c Release
git add XPacketRpc.Benchmarks/Dtos/ XPacketRpc.Benchmarks/XPacketRpc.Benchmarks.csproj
git commit -m "feat(benchmarks): add 8 DTOs + per-serializer variants + DtoFactory"
```

---

### Task 11.3: SerializeBenchmarks

**Files:**
- Create: `XPacketRpc.Benchmarks/Benchmarks/SerializeBenchmarks.cs`

> **Note:** Для упрощения — отдельный benchmark-класс **per DTO**, чтобы избежать `dynamic`-overhead и
> чтобы каждый serializer работал с конкретным типом без бокса. Это даёт более чистые цифры.

- [ ] **Step 1: Bench для Vector3 (template — повторите для остальных 7 DTO)**

`XPacketRpc.Benchmarks/Benchmarks/SerializeVector3Benchmarks.cs`:

```csharp
using BenchmarkDotNet.Attributes;
using MemoryPack;
using MessagePack;
using MessagePack.Resolvers;
using ProtoBuf;
using System.Buffers;
using System.Text.Json;
using XPacketRpc;
using XPacketRpc.Benchmarks.Dtos;
using XPacketRpc.Internal;
using XPacketRpc.Tests.Dtos;

namespace XPacketRpc.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class SerializeVector3Benchmarks
{
    private Vector3 dto = null!;
    private XPacketRpcSerializer xprpc = null!;
    private MessagePackSerializerOptions mpOpts = null!;
    private Dtos.MP.Vector3 dtoMp = null!;

    [GlobalSetup]
    public void Setup()
    {
        XPRpc.Touch<Vector3>();
        this.dto = DtoFactory.Vector3();
        this.xprpc = new XPacketRpcSerializer();
        this.mpOpts = ContractlessStandardResolver.Options;
        this.dtoMp = new Dtos.MP.Vector3 { X = this.dto.X, Y = this.dto.Y, Z = this.dto.Z };

        Dtos.PB.ProtoSetup.EnsureRegistered();

        // verify roundtrip equivalence per §7.5
        var bytes = this.xprpc.Serialize(this.dto);
        var got = this.xprpc.Deserialize<Vector3>(bytes);
        if (got!.X != this.dto.X) throw new InvalidOperationException("XPacketRpc roundtrip mismatch");
    }

    [Benchmark(Baseline = true)]
    public byte[] XPacketRpc() => this.xprpc.Serialize(this.dto);

    [Benchmark]
    public byte[] MessagePackContractless() => MessagePackSerializer.Serialize(this.dto, this.mpOpts);

    [Benchmark]
    public byte[] MemoryPack() => MemoryPackSerializer.Serialize(this.dtoMp);

    [Benchmark]
    public byte[] SystemTextJson() => JsonSerializer.SerializeToUtf8Bytes(this.dto);

    [Benchmark]
    public byte[] ProtobufNet()
    {
        using var ms = new MemoryStream();
        Serializer.Serialize(ms, this.dto);
        return ms.ToArray();
    }
}
```

- [ ] **Step 2: Build + smoke-run одного benchmark'а**

```
dotnet build XPacketRpc.Benchmarks -c Release
dotnet run --project XPacketRpc.Benchmarks -c Release -- --filter "*SerializeVector3*" --runtimes net10.0
```

Expected: BDN запускается, не падает, выдаёт результаты.

- [ ] **Step 3: Создать остальные 7 SerializeBenchmark классов по template'у выше**

Файлы:
- `SerializeLogEntryBenchmarks.cs`
- `SerializeOrderRequest5Benchmarks.cs`, `SerializeOrderRequest50Benchmarks.cs`
- `SerializeUserProfileBenchmarks.cs`
- `SerializeChunkPayload16KBenchmarks.cs`, `SerializeChunkPayload64KBenchmarks.cs`
- `SerializeBigDict100Benchmarks.cs`, `SerializeBigDict1000Benchmarks.cs`
- `SerializeDeepNestedBenchmarks.cs`
- `SerializeRecordRequestBenchmarks.cs`

Каждый — копия SerializeVector3Benchmarks с заменой типа. Не сокращайте через `dynamic` —
дайте каждому serializer'у дойти до конкретного типа.

- [ ] **Step 4: Commit**

```
git add XPacketRpc.Benchmarks/Benchmarks/
git commit -m "feat(benchmarks): add Serialize benchmarks for all 8 DTO scenarios"
```

---

### Task 11.4: DeserializeBenchmarks + roundtrip-validation в [GlobalSetup]

**Files:**
- Create: `XPacketRpc.Benchmarks/Benchmarks/DeserializeVector3Benchmarks.cs` (template)
- Create: остальные 9 Deserialize-классов по аналогии

- [ ] **Step 1: Template**

```csharp
using BenchmarkDotNet.Attributes;
using MemoryPack;
using MessagePack;
using MessagePack.Resolvers;
using ProtoBuf;
using System.Text.Json;
using XPacketRpc;
using XPacketRpc.Benchmarks.Dtos;
using XPacketRpc.Tests.Dtos;

namespace XPacketRpc.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class DeserializeVector3Benchmarks
{
    private Vector3 dto = null!;
    private XPacketRpcSerializer xprpc = null!;
    private MessagePackSerializerOptions mpOpts = null!;

    private byte[] payloadXpRpc = null!;
    private byte[] payloadMp = null!;
    private byte[] payloadMemPack = null!;
    private byte[] payloadJson = null!;
    private byte[] payloadProto = null!;

    [GlobalSetup]
    public void Setup()
    {
        XPRpc.Touch<Vector3>();
        this.dto = DtoFactory.Vector3();
        this.xprpc = new XPacketRpcSerializer();
        this.mpOpts = ContractlessStandardResolver.Options;
        Dtos.PB.ProtoSetup.EnsureRegistered();

        this.payloadXpRpc = this.xprpc.Serialize(this.dto);
        this.payloadMp = MessagePackSerializer.Serialize(this.dto, this.mpOpts);
        this.payloadMemPack = MemoryPackSerializer.Serialize(new Dtos.MP.Vector3 { X = dto.X, Y = dto.Y, Z = dto.Z });
        this.payloadJson = JsonSerializer.SerializeToUtf8Bytes(this.dto);
        using (var ms = new MemoryStream())
        {
            Serializer.Serialize(ms, this.dto);
            this.payloadProto = ms.ToArray();
        }

        // verify roundtrip
        var got = this.xprpc.Deserialize<Vector3>(this.payloadXpRpc);
        if (got!.X != this.dto.X) throw new InvalidOperationException("Roundtrip mismatch");
    }

    [Benchmark(Baseline = true)]
    public Vector3? XPacketRpc() => this.xprpc.Deserialize<Vector3>(this.payloadXpRpc);

    [Benchmark]
    public Vector3 MessagePackContractless() => MessagePackSerializer.Deserialize<Vector3>(this.payloadMp, this.mpOpts);

    [Benchmark]
    public Dtos.MP.Vector3 MemoryPack() => MemoryPackSerializer.Deserialize<Dtos.MP.Vector3>(this.payloadMemPack)!;

    [Benchmark]
    public Vector3? SystemTextJson() => JsonSerializer.Deserialize<Vector3>(this.payloadJson);

    [Benchmark]
    public Vector3 ProtobufNet()
    {
        using var ms = new MemoryStream(this.payloadProto);
        return Serializer.Deserialize<Vector3>(ms);
    }
}
```

- [ ] **Step 2: Создать оставшиеся 9 Deserialize-benchmark классов по аналогии**

- [ ] **Step 3: Build + commit**

```
dotnet build XPacketRpc.Benchmarks -c Release
git add XPacketRpc.Benchmarks/Benchmarks/Deserialize*.cs
git commit -m "feat(benchmarks): add Deserialize benchmarks for all 8 DTO scenarios"
```

---

### Task 11.5: Wire-size measurement (отдельный non-BDN отчёт)

**Files:**
- Create: `XPacketRpc.Benchmarks/WireSizeReport.cs`
- Modify: `XPacketRpc.Benchmarks/Program.cs` (добавить флаг `--wire-size`)

- [ ] **Step 1: Утилита**

`XPacketRpc.Benchmarks/WireSizeReport.cs`:

```csharp
using MemoryPack;
using MessagePack;
using MessagePack.Resolvers;
using ProtoBuf;
using System.Text;
using System.Text.Json;
using XPacketRpc;
using XPacketRpc.Benchmarks.Dtos;
using XPacketRpc.Tests.Dtos;

namespace XPacketRpc.Benchmarks;

public static class WireSizeReport
{
    public static string Generate()
    {
        var sb = new StringBuilder();
        sb.AppendLine("| DTO | XPacketRpc | MessagePack | MemoryPack | System.Text.Json | protobuf-net |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|");

        XPRpc.Touch<Vector3>(); XPRpc.Touch<LogEntry>(); /* … остальные */
        var xp = new XPacketRpcSerializer();
        var mpOpts = ContractlessStandardResolver.Options;
        Dtos.PB.ProtoSetup.EnsureRegistered();

        AddRow(sb, "Vector3", DtoFactory.Vector3(), xp, mpOpts,
            mp => new Dtos.MP.Vector3 { X = mp.X, Y = mp.Y, Z = mp.Z });
        // … остальные DTO

        return sb.ToString();
    }

    private static void AddRow<T, TMp>(StringBuilder sb, string name, T dto,
        XPacketRpcSerializer xp, MessagePackSerializerOptions mpOpts, Func<T, TMp> toMp)
    {
        var xpSize = xp.Serialize(dto!).Length;
        var mpSize = MessagePackSerializer.Serialize(dto, mpOpts).Length;
        var memPackSize = MemoryPackSerializer.Serialize(toMp(dto)).Length;
        var jsonSize = JsonSerializer.SerializeToUtf8Bytes(dto).Length;
        using var ms = new MemoryStream();
        Serializer.Serialize(ms, dto);
        var protoSize = ms.Length;

        sb.AppendLine($"| {name} | {xpSize} | {mpSize} | {memPackSize} | {jsonSize} | {protoSize} |");
    }
}
```

- [ ] **Step 2: Подключить в Program.cs**

```csharp
public static void Main(string[] args)
{
    if (args.Length > 0 && args[0] == "--wire-size")
    {
        Console.WriteLine(WireSizeReport.Generate());
        return;
    }
    BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, new BenchConfig());
}
```

- [ ] **Step 3: Прогнать**

```
dotnet run --project XPacketRpc.Benchmarks -c Release -- --wire-size
```

Expected: markdown-таблица с размерами в байтах.

- [ ] **Step 4: Commit**

```
git add XPacketRpc.Benchmarks/WireSizeReport.cs XPacketRpc.Benchmarks/Program.cs
git commit -m "feat(benchmarks): add wire-size measurement report"
```

---

### Task 11.6: Bond integration (опционально, time-boxed на 1 час)

**Files:**
- Possibly create: `XPacketRpc.Benchmarks/Dtos/Variants.Bond.cs` + `.bond`-схема

> **Note:** Bond требует `.bond`-schema файлов и `gbc.exe`-codegen. Это значительно сложнее остальных
> serializers. Per spec §9 R1 — если за час не интегрируется, **выкидываем Bond** из бенчмарков
> и удаляем `<PackageReference Include="Bond.CSharp" .../>` из csproj.

- [ ] **Step 1: Попытка интегрировать Bond**

Создайте `.bond`-файл, настройте `bond.exe`-codegen, добавьте `[Bond.Schema]`-атрибуты, запустите тест-bench.

- [ ] **Step 2: Решение — оставить или выкинуть**

Если за 1 час не работает:

```
git rm XPacketRpc.Benchmarks/Dtos/Variants.Bond.cs  # если был создан
# В csproj удалить:
#   <PackageReference Include="Bond.CSharp" Version="13.0.1" />
git add XPacketRpc.Benchmarks/XPacketRpc.Benchmarks.csproj
git commit -m "chore(benchmarks): drop Bond.CSharp — setup overhead exceeds value (per spec §9 R1)"
```

Если работает — добавьте в каждый Serialize/Deserialize benchmark класс `[Benchmark] public byte[] Bond()`.

- [ ] **Step 3: Решение зафиксировать в README**

В корне репозитория создайте/обновите `XPacketRpc.Benchmarks/README.md`:

```markdown
# Benchmarks

Compares XPacketRpc against:

- MessagePack-CSharp 3.x (ContractlessStandardResolver)
- MemoryPack 1.21
- System.Text.Json
- protobuf-net 3.x (RuntimeTypeModel)
- Bond.CSharp 13.x  ← если интегрирован

8 DTO scenarios from spec §7.1.

## Run

```bash
dotnet run -c Release -- --filter '*'
dotnet run -c Release -- --wire-size
```
```

- [ ] **Step 4: Commit**

```
git add XPacketRpc.Benchmarks/README.md
git commit -m "docs(benchmarks): add README with run instructions"
```

---

### Task 11.7: Full benchmark run + сохранение результатов

**Files:**
- Modify: `docs/benchmarks/2026-05-08-results.md` (создать с результатами)

- [ ] **Step 1: Полный прогон**

```
dotnet run --project XPacketRpc.Benchmarks -c Release -- --filter "*Serialize*"
dotnet run --project XPacketRpc.Benchmarks -c Release -- --filter "*Deserialize*"
dotnet run --project XPacketRpc.Benchmarks -c Release -- --wire-size > docs/benchmarks/wire-size.md
```

- [ ] **Step 2: Скопировать результаты**

```
mkdir -p docs/benchmarks
cp -r XPacketRpc.Benchmarks/BenchmarkDotNet.Artifacts/results/*.md docs/benchmarks/
```

- [ ] **Step 3: Commit**

```
git add docs/benchmarks/
git commit -m "docs(benchmarks): commit initial benchmark results vs MessagePack/MemoryPack/STJ/protobuf-net"
```

---

## Definition of Done — финальный чеклист

- [ ] Все 4 (или 5 — Bond опционально) проекта собираются в `dotnet build TCPProtocol.sln -c Release`.
- [ ] `dotnet test TCPProtocol.sln -c Debug` — все XPacketRpc.Tests + XPacketRpc.Generators.Tests passed; legacy XProtocol.Tests не сломаны.
- [ ] Roundtrip-проверка в `[GlobalSetup]` всех Benchmark классов проходит (не throws при `dotnet run`).
- [ ] `dotnet run --project XPacketRpc.Benchmarks -c Release -- --filter "*"` отрабатывает до конца, выдаёт markdown-results.
- [ ] `docs/benchmarks/wire-size.md` сгенерирован.
- [ ] Все коммиты в master, branch чистый.

---

## Risks reminder (из spec §9)

При выполнении плана держите в голове:

- **R2/R3:** generator работает per-assembly. Если consumer-assembly (например, `MyService.dll`)
  использует `XPacketRpc`, она должна сама ссылаться на `XPacketRpc.Generators` как Analyzer.
  Это уже сделано в `XPacketRpc.Tests` и `XPacketRpc.Benchmarks`.
- **R4:** roundtrip-validation в `[GlobalSetup]` — обязательное условие, чтобы быстрая
  фальшивая сериализация (мимо корректности) не попала в результаты.
- **R5:** FNV-1a в `XPacketRpc/Internal/Fnv1a.cs` и в `XPacketRpc.Generators/Emit/WriteEmitter.cs:Fnv1aGen`
  — две копии одного алгоритма. Любые правки синхронизировать в обоих местах + Fnv1aTests.
- **R6:** добавьте раздел в `README.md` корня про `XPRpc.Touch<T>()` для DI/MakeGenericMethod-кейсов.
- **R8:** wire-формат не имеет version-byte. Добавление поля в DTO ломает старый wire.
  Phase 8.1 (CtorBinder) теперь корректно обрабатывает immutable record'ы, но schema evolution
  остаётся манульной (координация client/server).

