# XProtokol Attribute-Free Serialization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Перевести сериализацию DTO в `XProtocol` с явных атрибутов `[XField(byte)]` на автоматический порядок по `MetadataToken` с явной регистрацией типов через `XPacketTypeManager.Register<T>(...)`.

**Architecture:** Удаляется `XFieldAttribute` и `FieldID` из wire format. Регистрация DTO един раз вычисляет порядок полей, компилирует getter/setter-делегаты и кеширует `FieldDescriptor[]`. `XPacketConverter.Serialize/Deserialize` использует кеш без рефлексии в горячем пути. Wire format пакета: `header(3) + type(1) + subtype(1) + fieldCount(1) + (fieldSize(1) + contents)* + footer(2)`.

**Tech Stack:** .NET 10, C# 13, MSTest 4 (для тестов).

**Spec:** [docs/superpowers/specs/2026-05-07-xprotokol-attribute-free-serialization-design.md](../specs/2026-05-07-xprotokol-attribute-free-serialization-design.md)

---

## File Structure

### Файлы в `XProtocol/` (изменяются)

| Файл | Ответственность | Действие |
|------|------------------|----------|
| `XProtocol/Serializator/XFieldAttribute.cs` | (был: маркер поля с FieldID) | **Удалить** |
| `XProtocol/Serializator/XPacketConverter.cs` | Сериализация/десериализация DTO ↔ XPacket с использованием закешированного дескриптора | **Переписать** |
| `XProtocol/Serializator/FieldDescriptor.cs` | Описание поля DTO: FieldInfo + compiled getter/setter | **Создать** (internal sealed) |
| `XProtocol/XPacketField.cs` | Поле пакета: размер + содержимое | **Изменить** (убрать `FieldID`) |
| `XProtocol/XPacket.cs` | Контейнер пакета: список полей, header/footer, кодирование | **Изменить** (новый API + новый wire format) |
| `XProtocol/XPacketTypeManager.cs` | Реестр типов пакетов и кеш дескрипторов DTO | **Изменить** (добавить `Register<T>`, `GetDescriptors`, `BuildDescriptors`) |
| `XProtocol/XPacketHandshake.cs` | Встроенный DTO handshake | **Изменить** (убрать `[XField(1)]`) |

### Файлы в `Test/` и `TCPClient/`/`TCPServer/`

| Файл | Действие |
|------|----------|
| `Test/Program.cs` | Убрать `[XField(0/1/2)]` с `TestPacket`, добавить `XPacketTypeManager.Register<TestPacket>(...)` |
| `TCPClient/Program.cs` | (Регистрация `XPacketHandshake` уже в static ctor `XPacketTypeManager` — без изменений по логике) |
| `TCPServer/ConnectedClient.cs` | (Без изменений по логике) |

### Новый тестовый проект

| Файл | Ответственность |
|------|------------------|
| `XProtocol.Tests/XProtocol.Tests.csproj` | MSTest 4 проект на .NET 10 |
| `XProtocol.Tests/RegistrationTests.cs` | Регистрация DTO: успешная, дублирующая, невалидная (reference type, >255 полей) |
| `XProtocol.Tests/RoundtripTests.cs` | `Serialize → ToPacket → Parse → Deserialize` восстанавливает значения |
| `XProtocol.Tests/StrictCountTests.cs` | `FieldCount` mismatch → throw |
| `XProtocol.Tests/UnregisteredTypeTests.cs` | Сериализация/десериализация незарегистрированного типа → throw |
| `XProtocol.Tests/TestDtos.cs` | DTO для тестов (`SimpleDto`, `EmptyDto`, `InheritedDto`, `BadDto` и т.п.) |
| `TCPProtocol.sln` | Добавить `XProtocol.Tests.csproj` в solution |

---

## Phase A — Implementation

### Task 1: Создать FieldDescriptor

**Files:**
- Create: `XProtocol/Serializator/FieldDescriptor.cs`

- [ ] **Step 1: Создать файл с FieldDescriptor**

Создать `XProtocol/Serializator/FieldDescriptor.cs` с содержимым:

```csharp
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace XProtocol.Serializator
{
    internal sealed class FieldDescriptor
    {
        public FieldInfo Field { get; }
        public Func<object, object> Getter { get; }
        public Action<object, object> Setter { get; }

        public FieldDescriptor(FieldInfo field)
        {
            this.Field = field;
            this.Getter = BuildGetter(field);
            this.Setter = BuildSetter(field);
        }

        private static Func<object, object> BuildGetter(FieldInfo f)
        {
            var p = Expression.Parameter(typeof(object), "o");
            var body = Expression.Convert(
                Expression.Field(Expression.Convert(p, f.DeclaringType), f),
                typeof(object));
            return Expression.Lambda<Func<object, object>>(body, p).Compile();
        }

        private static Action<object, object> BuildSetter(FieldInfo f)
        {
            var p = Expression.Parameter(typeof(object), "o");
            var v = Expression.Parameter(typeof(object), "v");
            var body = Expression.Assign(
                Expression.Field(Expression.Convert(p, f.DeclaringType), f),
                Expression.Convert(v, f.FieldType));
            return Expression.Lambda<Action<object, object>>(body, p, v).Compile();
        }
    }
}
```

- [ ] **Step 2: Проверить сборку**

Запустить:
```
dotnet build XProtocol/XProtocol.csproj
```
Ожидаемо: успех (никто пока не использует `FieldDescriptor`, но сам класс компилируется). Если ошибка `XFieldAttribute` или другие связанные — это ожидаемо на этом шаге **только если** `XPacketConverter.cs` ещё ссылается на удалённое; на этом шаге `XPacketConverter` не трогается, поэтому build должен пройти.

- [ ] **Step 3: Commit**

```
git add XProtocol/Serializator/FieldDescriptor.cs
git commit -m "feat: add FieldDescriptor with compiled getter/setter delegates"
```

---

### Task 2: Убрать FieldID из XPacketField

**Files:**
- Modify: `XProtocol/XPacketField.cs`

- [ ] **Step 1: Перезаписать XPacketField**

Заменить полное содержимое `XProtocol/XPacketField.cs`:

```csharp
namespace XProtocol
{
    public class XPacketField
    {
        public byte FieldSize { get; set; }
        public byte[] Contents { get; set; }
    }
}
```

- [ ] **Step 2: Сборка прервётся — это ожидаемо**

Запустить:
```
dotnet build XProtocol/XProtocol.csproj
```
Ожидаемо: ошибки в `XPacket.cs` и `XPacketConverter.cs`, ссылающихся на `field.FieldID`. Не коммитим — продолжаем в следующих задачах.

---

### Task 3: Расширить XPacketTypeManager — Register<T> + кеш

**Files:**
- Modify: `XProtocol/XPacketTypeManager.cs`

- [ ] **Step 1: Перезаписать XPacketTypeManager**

Заменить полное содержимое `XProtocol/XPacketTypeManager.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using XProtocol.Serializator;

namespace XProtocol
{
    public static class XPacketTypeManager
    {
        private static readonly Dictionary<XPacketType, (byte Type, byte Subtype)> typeRegistry =
            new Dictionary<XPacketType, (byte Type, byte Subtype)>();

        private static readonly Dictionary<Type, FieldDescriptor[]> descriptorCache =
            new Dictionary<Type, FieldDescriptor[]>();

        private static readonly object syncRoot = new object();

        static XPacketTypeManager()
        {
            Register<XPacketHandshake>(XPacketType.Handshake, 1, 0);
        }

        public static void Register<T>(XPacketType packetType, byte type, byte subtype) where T : class
        {
            lock (syncRoot)
            {
                if (typeRegistry.ContainsKey(packetType))
                {
                    throw new InvalidOperationException($"Packet type {packetType:G} is already registered.");
                }

                var descriptors = BuildDescriptors(typeof(T));
                descriptorCache[typeof(T)] = descriptors;
                typeRegistry[packetType] = (type, subtype);
            }
        }

        public static (byte Type, byte Subtype) GetType(XPacketType packetType)
        {
            if (!typeRegistry.TryGetValue(packetType, out var pair))
            {
                throw new InvalidOperationException($"Packet type {packetType:G} is not registered.");
            }
            return pair;
        }

        public static XPacketType GetTypeFromPacket(XPacket packet)
        {
            var type = packet.PacketType;
            var subtype = packet.PacketSubtype;

            foreach (var kv in typeRegistry)
            {
                if (kv.Value.Type == type && kv.Value.Subtype == subtype)
                {
                    return kv.Key;
                }
            }
            return XPacketType.Unknown;
        }

        internal static FieldDescriptor[] GetDescriptors(Type t)
        {
            if (!descriptorCache.TryGetValue(t, out var d))
            {
                throw new InvalidOperationException(
                    $"Type {t.Name} is not registered. Call XPacketTypeManager.Register<{t.Name}>(...) first.");
            }
            return d;
        }

        private static FieldDescriptor[] BuildDescriptors(Type t)
        {
            var fields = new List<FieldInfo>();
            for (var current = t; current != null && current != typeof(object); current = current.BaseType)
            {
                fields.AddRange(
                    current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                           .Where(f => !f.IsLiteral));
            }

            var sorted = fields.OrderBy(f => f.MetadataToken).ToArray();

            foreach (var f in sorted)
            {
                if (!f.FieldType.IsValueType)
                {
                    throw new InvalidOperationException(
                        $"{t.Name}.{f.Name}: only value-type fields are supported.");
                }
            }

            if (sorted.Length > byte.MaxValue)
            {
                throw new InvalidOperationException($"{t.Name} has more than {byte.MaxValue} fields.");
            }

            return sorted.Select(f => new FieldDescriptor(f)).ToArray();
        }
    }
}
```

- [ ] **Step 2: Сборка ещё не пройдёт**

Запустить:
```
dotnet build XProtocol/XProtocol.csproj
```
Ожидаемо: ошибки сохраняются в `XPacket.cs` (`SetValue/GetValue/GetField/HasField`), `XPacketConverter.cs`, `XPacketHandshake.cs` (там пока `[XField(1)]` — компилируется но привяжем к удалению позже). Не коммитим, продолжаем.

---

### Task 4: Удалить XFieldAttribute

**Files:**
- Delete: `XProtocol/Serializator/XFieldAttribute.cs`

- [ ] **Step 1: Удалить файл**

Запустить:
```
git rm XProtocol/Serializator/XFieldAttribute.cs
```

- [ ] **Step 2: Сборка ещё не пройдёт**

Ожидаемо: ошибки в `XPacketHandshake.cs` (`[XField(1)]`), `Test/Program.cs` (`[XField(...)]` на `TestPacket`), `XPacketConverter.cs`.

---

### Task 5: Убрать [XField] из XPacketHandshake

**Files:**
- Modify: `XProtocol/XPacketHandshake.cs`

- [ ] **Step 1: Перезаписать XPacketHandshake**

Заменить полное содержимое `XProtocol/XPacketHandshake.cs`:

```csharp
namespace XProtocol
{
    public class XPacketHandshake
    {
        public int MagicHandshakeNumber;
    }
}
```

---

### Task 6: Переписать XPacket — новый API и wire format

**Files:**
- Modify: `XProtocol/XPacket.cs`

- [ ] **Step 1: Перезаписать XPacket**

Заменить полное содержимое `XProtocol/XPacket.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace XProtocol
{
    public class XPacket
    {
        public byte PacketType { get; set; }
        public byte PacketSubtype { get; set; }
        public List<XPacketField> Fields { get; } = new List<XPacketField>();
        public bool Protected { get; set; }
        public bool ChangeHeaders { get; set; }

        private XPacket() { }

        public static XPacket Create(byte type, byte subtype)
        {
            return new XPacket
            {
                PacketType = type,
                PacketSubtype = subtype
            };
        }

        public static XPacket Create(XPacketType type)
        {
            var (btype, bsubtype) = XPacketTypeManager.GetType(type);
            return Create(btype, bsubtype);
        }

        public void AppendValue(object structure)
        {
            if (structure == null)
            {
                throw new ArgumentNullException(nameof(structure));
            }

            if (!structure.GetType().IsValueType)
            {
                throw new ArgumentException("Only value types are supported.", nameof(structure));
            }

            var bytes = FixedObjectToByteArray(structure);
            if (bytes.Length > byte.MaxValue)
            {
                throw new InvalidOperationException("Field is too large (>255 bytes).");
            }

            Fields.Add(new XPacketField
            {
                FieldSize = (byte)bytes.Length,
                Contents = bytes
            });
        }

        public T GetValueAt<T>(int index) where T : struct
        {
            if (index < 0 || index >= Fields.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var field = Fields[index];
            return ByteArrayToFixedObject<T>(field.Contents);
        }

        public object GetValueAt(int index, Type t)
        {
            if (index < 0 || index >= Fields.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return typeof(XPacket)
                .GetMethod(nameof(GetValueAt), new[] { typeof(int) })
                .MakeGenericMethod(t)
                .Invoke(this, new object[] { index });
        }

        public byte[] ToPacket()
        {
            if (Fields.Count > byte.MaxValue)
            {
                throw new InvalidOperationException("Too many fields (>255).");
            }

            var ms = new MemoryStream();
            ms.Write(ChangeHeaders
                ? new byte[] { 0x95, 0xAA, 0xFF, PacketType, PacketSubtype }
                : new byte[] { 0xAF, 0xAA, 0xAF, PacketType, PacketSubtype }, 0, 5);

            ms.WriteByte((byte)Fields.Count);

            foreach (var f in Fields)
            {
                ms.WriteByte(f.FieldSize);
                if (f.FieldSize > 0)
                {
                    ms.Write(f.Contents, 0, f.FieldSize);
                }
            }

            ms.Write(new byte[] { 0xFF, 0x00 }, 0, 2);
            return ms.ToArray();
        }

        public static XPacket Parse(byte[] packet, bool markAsEncrypted = false)
        {
            if (packet == null || packet.Length < 8)
            {
                return null;
            }

            bool encrypted = false;
            if (!(packet[0] == 0xAF && packet[1] == 0xAA && packet[2] == 0xAF))
            {
                if (packet[0] == 0x95 && packet[1] == 0xAA && packet[2] == 0xFF)
                {
                    encrypted = true;
                }
                else
                {
                    return null;
                }
            }

            var type = packet[3];
            var subtype = packet[4];
            var fieldCount = packet[5];

            var xp = new XPacket
            {
                PacketType = type,
                PacketSubtype = subtype,
                Protected = markAsEncrypted
            };

            int pos = 6;
            int payloadEnd = packet.Length - 2;

            for (int i = 0; i < fieldCount; i++)
            {
                if (pos + 1 > payloadEnd)
                {
                    return null;
                }

                var size = packet[pos++];
                if (pos + size > payloadEnd)
                {
                    return null;
                }

                var contents = size != 0 ? packet.Skip(pos).Take(size).ToArray() : null;
                pos += size;

                xp.Fields.Add(new XPacketField
                {
                    FieldSize = size,
                    Contents = contents
                });
            }

            if (pos != payloadEnd || packet[pos] != 0xFF || packet[pos + 1] != 0x00)
            {
                return null;
            }

            return encrypted ? DecryptPacket(xp) : xp;
        }

        public XPacket Encrypt()
        {
            return EncryptPacket(this);
        }

        public static XPacket EncryptPacket(XPacket packet)
        {
            if (packet == null)
            {
                return null;
            }

            var rawBytes = packet.ToPacket();
            var encrypted = XProtocolEncryptor.Encrypt(rawBytes);

            var p = Create(0, 0);
            p.AppendRawBytes(encrypted);
            p.ChangeHeaders = true;
            return p;
        }

        public static XPacket Decrypt(XPacket packet)
        {
            return DecryptPacket(packet);
        }

        private static XPacket DecryptPacket(XPacket packet)
        {
            if (packet == null || packet.Fields.Count != 1)
            {
                return null;
            }

            var rawData = packet.Fields[0].Contents;
            var decrypted = XProtocolEncryptor.Decrypt(rawData);
            return Parse(decrypted, true);
        }

        internal void AppendRawBytes(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }
            if (bytes.Length > byte.MaxValue)
            {
                throw new InvalidOperationException("Field is too large (>255 bytes).");
            }

            Fields.Add(new XPacketField
            {
                FieldSize = (byte)bytes.Length,
                Contents = bytes
            });
        }

        private static byte[] FixedObjectToByteArray(object value)
        {
            var size = Marshal.SizeOf(value.GetType());
            var arr = new byte[size];
            var ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(value, ptr, true);
                Marshal.Copy(ptr, arr, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            return arr;
        }

        private static T ByteArrayToFixedObject<T>(byte[] bytes) where T : struct
        {
            T value;
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                value = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            }
            finally
            {
                handle.Free();
            }
            return value;
        }
    }
}
```

> **Note:** Старые методы `SetValue(byte id, …)`, `GetValue<T>(byte id)`, `GetField(byte id)`, `HasField(byte id)`, `SetValueRaw(byte id, …)`, `GetValueRaw(byte id)` **удалены**. Шифрование переписано на `AppendRawBytes`/`Fields[0].Contents`. На момент написания спецификации внешних ссылок на удалённые методы не обнаружено в `TCPClient/`, `TCPServer/`, `Test/`. Step 2 ниже это перепроверяет.

- [ ] **Step 2: Проверить отсутствие внешних ссылок на старые методы XPacket**

Запустить (в репозитории):
```
git grep -nE "\.(SetValue|GetValue|GetField|HasField|SetValueRaw|GetValueRaw)\(" -- "*.cs"
```
Ожидаемо: матчи только внутри `XProtocol/Serializator/XPacketConverter.cs` (его перепишем в Task 7). Если есть совпадения в `TCPClient/`, `TCPServer/`, `Test/`, `1/` — это блокер; задокументировать и обработать в отдельной задаче перед продолжением.

---

### Task 7: Переписать XPacketConverter

**Files:**
- Modify: `XProtocol/Serializator/XPacketConverter.cs`

- [ ] **Step 1: Перезаписать XPacketConverter**

Заменить полное содержимое `XProtocol/Serializator/XPacketConverter.cs`:

```csharp
using System;

namespace XProtocol.Serializator
{
    public static class XPacketConverter
    {
        public static XPacket Serialize<T>(XPacketType type, T obj) where T : class
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            var (btype, bsubtype) = XPacketTypeManager.GetType(type);
            var descriptors = XPacketTypeManager.GetDescriptors(typeof(T));

            var packet = XPacket.Create(btype, bsubtype);

            foreach (var desc in descriptors)
            {
                var value = desc.Getter(obj);
                packet.AppendValue(value);
            }

            return packet;
        }

        public static T Deserialize<T>(XPacket packet) where T : class, new()
        {
            if (packet == null)
            {
                throw new ArgumentNullException(nameof(packet));
            }

            var descriptors = XPacketTypeManager.GetDescriptors(typeof(T));

            if (packet.Fields.Count != descriptors.Length)
            {
                throw new InvalidOperationException(
                    $"Field count mismatch for {typeof(T).Name}: expected {descriptors.Length}, got {packet.Fields.Count}.");
            }

            var instance = new T();

            for (int i = 0; i < descriptors.Length; i++)
            {
                var desc = descriptors[i];
                var raw = packet.GetValueAt(i, desc.Field.FieldType);
                desc.Setter(instance, raw);
            }

            return instance;
        }
    }
}
```

- [ ] **Step 2: Сборка XProtocol**

Запустить:
```
dotnet build XProtocol/XProtocol.csproj
```
Ожидаемо: успех (0 errors, 0 warnings уровня error).

Если есть ошибка про `XPacket.Create(byte, byte)` отсутствует/private ctor — проверить Task 6 (метод `Create(byte type, byte subtype)` должен быть `public static`).

- [ ] **Step 3: Commit (вся базовая часть библиотеки)**

```
git add XProtocol/Serializator/FieldDescriptor.cs XProtocol/Serializator/XPacketConverter.cs XProtocol/XPacketField.cs XProtocol/XPacketTypeManager.cs XProtocol/XPacketHandshake.cs XProtocol/XPacket.cs
git commit -m "refactor: replace XField attribute with explicit registration

- Remove XFieldAttribute and FieldID from wire format
- Add XPacketTypeManager.Register<T> with cached FieldDescriptor[]
- Compile getter/setter delegates via Expression.Compile()
- New wire format: header(3)+type(1)+subtype(1)+fieldCount(1)+(fieldSize(1)+contents)*+footer(2)
- Field order derived from MetadataToken (declaration order) of public instance fields"
```

---

### Task 8: Обновить Test/Program.cs

**Files:**
- Modify: `Test/Program.cs`

- [ ] **Step 1: Прочитать текущее содержимое**

Прочитать `Test/Program.cs` целиком (Read tool).

- [ ] **Step 2: Удалить `[XField(N)]` с полей TestPacket**

Заменить блок:
```csharp
internal class TestPacket
{
    [XField(0)]
    public int TestNumber;

    [XField(1)]
    public double TestDouble;

    [XField(2)]
    public bool TestBoolean;
}
```
на:
```csharp
internal class TestPacket
{
    public int TestNumber;

    public double TestDouble;

    public bool TestBoolean;
}
```

- [ ] **Step 3: Удалить `using XProtocol.Serializator;` если использовался только для атрибута**

Найти и удалить (если не используется для `XPacketConverter`):
```csharp
using XProtocol.Serializator;
```
Внимание: `using XProtocol.Serializator;` нужен для `XPacketConverter.Serialize/Deserialize`. Оставить если есть такие вызовы; убрать только если файл больше не использует пространство.

- [ ] **Step 4: Добавить значение в XPacketType enum**

Прочитать `XProtocol/XPacketType.cs`. Если в нём нет значения `TestPacket`, добавить его рядом с `Handshake = 1`:

```csharp
public enum XPacketType
{
    Unknown = 0,
    Handshake = 1,
    TestPacket = 2
}
```

(Если `TestPacket` уже присутствует — оставить как есть, шаг no-op.)

- [ ] **Step 5: Зарегистрировать TestPacket в начале Main**

В `Test/Program.cs` найти метод `Main` и добавить **первой строкой** в нём:

```csharp
XPacketTypeManager.Register<TestPacket>(XPacketType.TestPacket, 2, 0);
```

(Это обеспечивает регистрацию до любых вызовов `XPacketConverter.Serialize/Deserialize` с `TestPacket`.)

- [ ] **Step 6: Сборка Test**

Запустить:
```
dotnet build Test/Test.csproj
```
Ожидаемо: успех.

- [ ] **Step 7: Commit**

```
git add Test/Program.cs XProtocol/XPacketType.cs
git commit -m "test: migrate TestPacket sample to attribute-free serialization"
```

---

### Task 9: Sanity-build всей сборки

**Files:** —

- [ ] **Step 1: Сборка солюшна**

Запустить:
```
dotnet build TCPProtocol.sln
```
Ожидаемо: успех всех проектов (`XProtocol`, `Test`, `TCPClient`, `TCPServer`).

Если `TCPClient`/`TCPServer` ломаются на устаревшем API `XPacket.SetValue/GetValue` (но мы в Task 6 проверили — их там не должно быть): проверить grep ещё раз. Любые runtime-зависимости от старого wire format — задокументировать и закрыть в отдельной задаче.

- [ ] **Step 2: Commit (если были изменения от sanity-build)**

```
git status
git add -u
git commit -m "fix: sanity build cleanup after attribute removal" || echo "no changes"
```

---

## Phase B — Tests

### Task 10: Создать тестовый проект XProtocol.Tests

**Files:**
- Create: `XProtocol.Tests/XProtocol.Tests.csproj`
- Create: `XProtocol.Tests/TestDtos.cs`
- Modify: `TCPProtocol.sln`

- [ ] **Step 1: Создать csproj**

Создать `XProtocol.Tests/XProtocol.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <Nullable>disable</Nullable>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
    <PackageReference Include="MSTest.TestAdapter" Version="4.0.0" />
    <PackageReference Include="MSTest.TestFramework" Version="4.0.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\XProtocol\XProtocol.csproj" />
  </ItemGroup>
</Project>
```

> **Note:** Версии MSTest 4.0 актуальны на момент написания плана (2026-05). Если `dotnet restore` сообщает "package not found", использовать последнюю доступную из `dotnet package search MSTest.TestFramework`.

- [ ] **Step 2: Создать TestDtos.cs (с DTO + AssemblyInitialize)**

Создать `XProtocol.Tests/TestDtos.cs`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace XProtocol.Tests
{
    public class SimpleDto
    {
        public int A;
        public double B;
        public bool C;
    }

    public class EmptyDto
    {
    }

    public class BadDtoWithReferenceField
    {
        public int A;
        public string Bad;
    }

    public class UnregisteredDto
    {
        public int X;
    }

    [TestClass]
    public static class AssemblyFixture
    {
        // XPacketType id'ы для тестовых DTO. Не пересекаются с production значениями (Handshake=1, TestPacket=2).
        public const XPacketType SimpleDtoType = (XPacketType)100;
        public const XPacketType EmptyDtoType = (XPacketType)101;

        [AssemblyInitialize]
        public static void Init(TestContext _)
        {
            XPacketTypeManager.Register<SimpleDto>(SimpleDtoType, 100, 0);
            XPacketTypeManager.Register<EmptyDto>(EmptyDtoType, 101, 0);
        }
    }
}
```

> **Note:** `AssemblyInitialize` запускается ровно один раз перед всеми тестами в сборке, независимо от порядка test class'ов. Это устраняет проблему недетерминированного порядка `ClassInitialize`.

- [ ] **Step 3: Добавить проект в solution**

Запустить:
```
dotnet sln TCPProtocol.sln add XProtocol.Tests/XProtocol.Tests.csproj
```

- [ ] **Step 4: Restore + build**

Запустить:
```
dotnet restore XProtocol.Tests/XProtocol.Tests.csproj
dotnet build XProtocol.Tests/XProtocol.Tests.csproj
```
Ожидаемо: успех.

- [ ] **Step 5: Commit**

```
git add XProtocol.Tests/ TCPProtocol.sln
git commit -m "test: add XProtocol.Tests MSTest project"
```

---

### Task 11: Тесты регистрации DTO

**Files:**
- Create: `XProtocol.Tests/RegistrationTests.cs`

- [ ] **Step 1: Создать тестовый класс**

Создать `XProtocol.Tests/RegistrationTests.cs`:

```csharp
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace XProtocol.Tests
{
    [TestClass]
    public class RegistrationTests
    {
        // NOTE: XPacketTypeManager статичен между тестами. Каждый тестовый DTO регистрируем
        //       ровно один раз в первом подходящем тесте, а проверки на повторную регистрацию
        //       используют отдельные XPacketType-значения.

        [TestMethod]
        public void Register_RejectsReferenceTypeField()
        {
            var ex = Assert.ThrowsException<InvalidOperationException>(() =>
                XPacketTypeManager.Register<BadDtoWithReferenceField>(
                    (XPacketType)90, 90, 0));

            StringAssert.Contains(ex.Message, "only value-type fields");
            StringAssert.Contains(ex.Message, "Bad");
        }

        [TestMethod]
        public void Register_RejectsDuplicatePacketType()
        {
            // первый раз регистрируем Handshake уже сделан в static ctor
            var ex = Assert.ThrowsException<InvalidOperationException>(() =>
                XPacketTypeManager.Register<XPacketHandshake>(XPacketType.Handshake, 1, 0));

            StringAssert.Contains(ex.Message, "already registered");
        }

        [TestMethod]
        public void GetType_ReturnsRegisteredPair()
        {
            var (type, subtype) = XPacketTypeManager.GetType(XPacketType.Handshake);
            Assert.AreEqual((byte)1, type);
            Assert.AreEqual((byte)0, subtype);
        }

        [TestMethod]
        public void GetType_ThrowsForUnregistered()
        {
            var ex = Assert.ThrowsException<InvalidOperationException>(() =>
                XPacketTypeManager.GetType((XPacketType)999));
            StringAssert.Contains(ex.Message, "not registered");
        }
    }
}
```

- [ ] **Step 2: Прогнать тесты**

Запустить:
```
dotnet test XProtocol.Tests/XProtocol.Tests.csproj --filter FullyQualifiedName~RegistrationTests
```
Ожидаемо: 4/4 PASS.

- [ ] **Step 3: Commit**

```
git add XProtocol.Tests/RegistrationTests.cs
git commit -m "test: add registration tests for XPacketTypeManager"
```

---

### Task 12: Тесты roundtrip Serialize → ToPacket → Parse → Deserialize

**Files:**
- Create: `XProtocol.Tests/RoundtripTests.cs`

> **Регистрация тестовых DTO** уже выполнена в `AssemblyFixture` (Task 10). Тесты используют `AssemblyFixture.SimpleDtoType` и `AssemblyFixture.EmptyDtoType`.

- [ ] **Step 1: Создать RoundtripTests.cs**

Создать `XProtocol.Tests/RoundtripTests.cs`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    [TestClass]
    public class RoundtripTests
    {
        [TestMethod]
        public void SimpleDto_RoundtripPreservesValues()
        {
            var original = new SimpleDto { A = 42, B = 3.1415, C = true };

            var packet = XPacketConverter.Serialize(AssemblyFixture.SimpleDtoType, original);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            Assert.IsNotNull(parsed);

            var restored = XPacketConverter.Deserialize<SimpleDto>(parsed);

            Assert.AreEqual(original.A, restored.A);
            Assert.AreEqual(original.B, restored.B);
            Assert.AreEqual(original.C, restored.C);
        }

        [TestMethod]
        public void SimpleDto_FieldOrderMatchesDeclarationOrder()
        {
            var dto = new SimpleDto { A = 1, B = 2.0, C = false };
            var packet = XPacketConverter.Serialize(AssemblyFixture.SimpleDtoType, dto);

            Assert.AreEqual(3, packet.Fields.Count);
            // первое поле — A (int=4 bytes), второе — B (double=8 bytes), третье — C
            Assert.AreEqual(4, packet.Fields[0].FieldSize);
            Assert.AreEqual(8, packet.Fields[1].FieldSize);
            Assert.IsTrue(packet.Fields[2].FieldSize >= 1);
            // На .NET Marshal.SizeOf(typeof(bool)) обычно равен 4 (Win32 BOOL).
            // Если этот тест падает с got=4, заменить ассерт на Assert.AreEqual(4, packet.Fields[2].FieldSize).
        }

        [TestMethod]
        public void EmptyDto_RoundtripProducesZeroFields()
        {
            var original = new EmptyDto();

            var packet = XPacketConverter.Serialize(AssemblyFixture.EmptyDtoType, original);
            Assert.AreEqual(0, packet.Fields.Count);

            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            Assert.IsNotNull(parsed);
            Assert.AreEqual(0, parsed.Fields.Count);

            var restored = XPacketConverter.Deserialize<EmptyDto>(parsed);
            Assert.IsNotNull(restored);
        }

        [TestMethod]
        public void XPacketHandshake_RoundtripPreservesValue()
        {
            var original = new XPacketHandshake { MagicHandshakeNumber = 12345 };

            var packet = XPacketConverter.Serialize(XPacketType.Handshake, original);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            Assert.IsNotNull(parsed);

            var restored = XPacketConverter.Deserialize<XPacketHandshake>(parsed);
            Assert.AreEqual(original.MagicHandshakeNumber, restored.MagicHandshakeNumber);
        }
    }
}
```

- [ ] **Step 2: Прогнать**

Запустить:
```
dotnet test XProtocol.Tests/XProtocol.Tests.csproj --filter FullyQualifiedName~RoundtripTests
```
Ожидаемо: 4/4 PASS.

- [ ] **Step 3: Commit**

```
git add XProtocol.Tests/RoundtripTests.cs
git commit -m "test: add roundtrip tests for XPacketConverter"
```

---

### Task 13: Тест строгой проверки FieldCount

**Files:**
- Create: `XProtocol.Tests/StrictCountTests.cs`

> Регистрация `SimpleDto` уже выполнена в `AssemblyFixture` (Task 10). Тест использует `AssemblyFixture.SimpleDtoType`.

- [ ] **Step 1: Создать тест**

Создать `XProtocol.Tests/StrictCountTests.cs`:

```csharp
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    [TestClass]
    public class StrictCountTests
    {
        [TestMethod]
        public void Deserialize_FieldCountMismatch_Throws()
        {
            var dto = new SimpleDto { A = 1, B = 2.0, C = true };
            var packet = XPacketConverter.Serialize(AssemblyFixture.SimpleDtoType, dto);

            // Намеренно ломаем количество полей: убираем последнее.
            packet.Fields.RemoveAt(packet.Fields.Count - 1);

            var ex = Assert.ThrowsException<InvalidOperationException>(() =>
                XPacketConverter.Deserialize<SimpleDto>(packet));

            StringAssert.Contains(ex.Message, "Field count mismatch");
            StringAssert.Contains(ex.Message, "expected 3");
            StringAssert.Contains(ex.Message, "got 2");
        }
    }
}
```

- [ ] **Step 2: Прогнать**

Запустить:
```
dotnet test XProtocol.Tests/XProtocol.Tests.csproj --filter FullyQualifiedName~StrictCountTests
```
Ожидаемо: 1/1 PASS.

- [ ] **Step 3: Commit**

```
git add XProtocol.Tests/StrictCountTests.cs
git commit -m "test: add strict field-count mismatch test"
```

---

### Task 14: Тест незарегистрированного типа

**Files:**
- Create: `XProtocol.Tests/UnregisteredTypeTests.cs`

- [ ] **Step 1: Создать тест**

Создать `XProtocol.Tests/UnregisteredTypeTests.cs`:

```csharp
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    [TestClass]
    public class UnregisteredTypeTests
    {
        [TestMethod]
        public void Serialize_UnregisteredType_Throws()
        {
            var dto = new UnregisteredDto { X = 7 };

            var ex = Assert.ThrowsException<InvalidOperationException>(() =>
                XPacketConverter.Serialize((XPacketType)1, dto));

            StringAssert.Contains(ex.Message, nameof(UnregisteredDto));
            StringAssert.Contains(ex.Message, "not registered");
        }

        [TestMethod]
        public void Deserialize_UnregisteredType_Throws()
        {
            var pkt = XPacket.Create(0, 0);

            var ex = Assert.ThrowsException<InvalidOperationException>(() =>
                XPacketConverter.Deserialize<UnregisteredDto>(pkt));

            StringAssert.Contains(ex.Message, nameof(UnregisteredDto));
            StringAssert.Contains(ex.Message, "not registered");
        }
    }
}
```

- [ ] **Step 2: Прогнать**

Запустить:
```
dotnet test XProtocol.Tests/XProtocol.Tests.csproj --filter FullyQualifiedName~UnregisteredTypeTests
```
Ожидаемо: 2/2 PASS.

- [ ] **Step 3: Commit**

```
git add XProtocol.Tests/UnregisteredTypeTests.cs
git commit -m "test: throw on serialize/deserialize unregistered DTO"
```

---

### Task 15: Полный прогон + финальная сборка

**Files:** —

- [ ] **Step 1: Полный прогон тестов**

Запустить:
```
dotnet test TCPProtocol.sln
```
Ожидаемо: все тесты XProtocol.Tests PASS, остальные проекты собираются.

- [ ] **Step 2: Запустить sample Test/Program.cs**

Запустить:
```
dotnet run --project Test/Test.csproj
```
Ожидаемо: программа отрабатывает roundtrip `TestPacket` без exception (если это входит в её логику). Если sample требует runtime пары `TCPClient`/`TCPServer` — пропустить и проверить руками.

- [ ] **Step 3: Commit финального состояния (если есть необвязанные изменения)**

```
git status
git add -u
git diff --cached --stat
git commit -m "chore: finalize attribute-free serialization migration" || echo "nothing to commit"
```

---

## Self-Review Checklist (для исполнителя)

После последнего commit пройтись по spec'у и убедиться, что все Acceptance Criteria из section 9 покрыты:

1. ✅ AC1 — order соответствует declaration → проверено в `RoundtripTests.SimpleDto_FieldOrderMatchesDeclarationOrder` + `RoundtripTests.SimpleDto_RoundtripPreservesValues`.
2. ✅ AC2 — roundtrip восстанавливает значения → `RoundtripTests.*RoundtripPreservesValues`, `XPacketHandshake_RoundtripPreservesValue`.
3. ✅ AC3 — `FieldCount` mismatch → throw → `StrictCountTests.Deserialize_FieldCountMismatch_Throws`.
4. ✅ AC4 — незарегистрированный тип → throw → `UnregisteredTypeTests.*`.
5. ✅ AC5 — handshake между клиентом и сервером работает без ручной регистрации → static ctor `XPacketTypeManager` регистрирует `XPacketHandshake`. Ручной check: запустить пару `TCPServer`+`TCPClient`.
6. ✅ AC6 — `Test/Program.cs` без атрибутов работает → Task 8 + ручной запуск в Task 15.
7. ✅ AC7 — XProtocol.csproj собирается под .NET 10 → Task 7 step 2.

Если какой-то AC не покрыт — добавить тест/шаг.
