# XProtokol — Attribute-Free Serialization Design

**Дата:** 2026-05-07
**Статус:** Утверждён (брейншторм)
**Область:** `XProtocol/` (XPacket, XPacketField, XPacketConverter, XPacketTypeManager, XPacketHandshake) и затрагиваемые потребители (`TCPClient/`, `TCPServer/`, `Test/`)

---

## 1. Цель

Перевести сериализацию DTO в `XProtocol` с явных атрибутов (`[XField(byte)]`) на автоматическое определение порядка полей. Один раз при регистрации типа определять порядок полей и кешировать его. Сериализация и десериализация работают с закешированным дескриптором без рефлексии в горячем пути.

Совместимость со старым wire-протоколом и старым атрибутом `XField` не сохраняется.

---

## 2. Принятые решения (резюме брейншторма)

| # | Решение |
|---|---------|
| 1 | Сериализуются **поля** (`FieldInfo`), не свойства. Соответствует существующей модели `XPacketConverter`. |
| 2 | Атрибут `XFieldAttribute` **удаляется полностью**. |
| 3 | Wire format меняется: **`FieldID` убирается** из сериализации поля. |
| 4 | Порядок полей: **`MetadataToken`** (declaration order, реализационно стабилен в Roslyn). |
| 5 | Область полей: **`Public | Instance` + наследованные `public`** поля по цепочке `BaseType` до `object`. Static и const пропускаются. |
| 6 | Иерархия: **flat global** — поля всех уровней наследования сортируются единым массивом по `MetadataToken`. |
| 7 | Versioning: **strict + length header**. Пакет содержит `FieldCount`. Несовпадение `FieldCount` с ожидаемым числом полей у получателя → throw с диагностикой. |
| 8 | Регистрация: **explicit per-type registry** (вариант C). DTO регистрируется через `XPacketTypeManager.Register<T>(...)`. Без регистрации — throw на сериализации/десериализации. |

### Caveat — flat global by MetadataToken

`MetadataToken` уникален в пределах сборки. Для DTO, у которых иерархия пересекает границы сборок (base в одной assembly, derived в другой), глобальная сортировка по `MetadataToken` может дать порядок, зависящий от порядка загрузки сборок. На практике DTO протокола обычно живут в одной сборке, поэтому ограничение приемлемо.

---

## 3. Архитектура

```
XPacketTypeManager (static)
  ├── typeRegistry         : Dictionary<XPacketType, (byte Type, byte Subtype)>
  ├── descriptorCache      : Dictionary<Type, FieldDescriptor[]>
  ├── Register<T>(packetType, type, subtype)   ← основной API
  ├── GetType(packetType)                      ← существующий
  ├── GetTypeFromPacket(packet)                ← существующий
  └── GetDescriptors(Type)                     ← internal, для XPacketConverter

XPacketConverter (static)
  ├── Serialize<T>(XPacketType, T) → XPacket
  └── Deserialize<T>(XPacket) → T

FieldDescriptor (internal sealed)
  ├── FieldInfo Field
  ├── Func<object, object> Getter      ← Expression.Compile()
  └── Action<object, object> Setter    ← Expression.Compile()

XPacket
  ├── AppendValue(object structure)    ← serialize-side
  ├── GetValueAt<T>(int index)         ← deserialize-side
  ├── GetValueAt(int index, Type)      ← reflection-driven helper
  ├── ToPacket() / Parse(byte[])       ← новый wire format
  └── PacketType / PacketSubtype / Fields / Protected / ChangeHeaders

XPacketField
  └── FieldSize, Contents              ← FieldID удалён

[удалено] XFieldAttribute
[удалено] XPacket.SetValue(byte id, …) / GetValue<T>(byte id) / GetField(byte id) / HasField(byte id)
[удалено] XPacketConverter.Serialize(byte, byte, object, bool strict) overloads
[удалено] параметр bool strict (теперь strict всегда)
```

---

## 4. Wire Format

### 4.1 Новый формат пакета

```
[Header   : 3 bytes]    0xAF 0xAA 0xAF (plain) | 0x95 0xAA 0xFF (encrypted)
[Type     : 1 byte ]
[Subtype  : 1 byte ]
[FieldCnt : 1 byte ]    ← новое
Repeat FieldCnt times (без FieldID):
  [FieldSize : 1 byte ]
  [Contents  : FieldSize bytes]
[Footer   : 2 bytes]    0xFF 0x00
```

- Минимальный размер пакета: `3 + 1 + 1 + 1 + 2 = 8 bytes` (раньше 7).
- `FieldCount: byte` — лимит 255 полей на DTO. Превышение → throw на регистрации/сериализации.
- `FieldSize: byte` — лимит 255 байт на значение (как сейчас). Для `IsValueType` value blittable types этот лимит почти недостижим (decimal=16, double=8, long=8 и т.п.).

### 4.2 Шифрование

`EncryptPacket`/`DecryptPacket` (через `XProtocolEncryptor` / `RijndaelHandler`) работают с уже сформированным `byte[]` пакета и не зависят от внутренней структуры payload. Изменения wire format не затрагивают шифрование.

### 4.3 Strict count check

При `Deserialize<T>(packet)`:
1. Получить `descriptors = XPacketTypeManager.GetDescriptors(typeof(T))`.
2. Если `packet.Fields.Count != descriptors.Length` → `InvalidOperationException` с диагностикой `"Field count mismatch for {T}: expected {N}, got {M}."`.

---

## 5. Field Discovery (BuildDescriptors)

Алгоритм формирования `FieldDescriptor[]` при регистрации:

```csharp
private static FieldDescriptor[] BuildDescriptors(Type t)
{
    var fields = new List<FieldInfo>();
    for (var current = t; current != null && current != typeof(object); current = current.BaseType)
    {
        fields.AddRange(
            current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                   .Where(f => !f.IsLiteral)); // skip const
    }

    var sorted = fields.OrderBy(f => f.MetadataToken).ToArray();

    foreach (var f in sorted)
    {
        if (!f.FieldType.IsValueType)
            throw new InvalidOperationException($"{t.Name}.{f.Name}: only value-type fields supported.");
    }

    if (sorted.Length > byte.MaxValue)
        throw new InvalidOperationException($"{t.Name} has >255 fields.");

    return sorted.Select(f => new FieldDescriptor(f)).ToArray();
}
```

Правила:

- **Только `Public | Instance`** на каждом уровне иерархии.
- **`DeclaredOnly`** на уровне — наследованные поля собираются вручную через обход `BaseType`. Это необходимо для устойчивого `MetadataToken` (унаследованные поля имеют токен своей assembly).
- **Static / `IsLiteral` (const) пропускаются**. Readonly остаются (FieldInfo.SetValue работает для readonly через рефлексию и compiled-делегат).
- **Только `IsValueType`** — соответствует существующему ограничению `XPacket.SetValue` / `Marshal`-based `FixedObjectToByteArray`. Reference-типы, arrays, strings — out of scope.
- **Лимит 255 полей** — определяется wire format'ом (`FieldCount: byte`).

---

## 6. API Изменения

### 6.1 XPacketTypeManager (расширен)

```csharp
public static class XPacketTypeManager
{
    private static readonly Dictionary<XPacketType, (byte Type, byte Subtype)> typeRegistry = new();
    private static readonly Dictionary<Type, FieldDescriptor[]> descriptorCache = new();
    private static readonly object syncRoot = new();

    static XPacketTypeManager()
    {
        // встроенные DTO
        Register<XPacketHandshake>(XPacketType.Handshake, 1, 0);
    }

    public static void Register<T>(XPacketType packetType, byte type, byte subtype) where T : class
    {
        lock (syncRoot)
        {
            if (typeRegistry.ContainsKey(packetType))
                throw new InvalidOperationException($"Packet type {packetType:G} already registered.");

            var descriptors = BuildDescriptors(typeof(T));
            descriptorCache[typeof(T)] = descriptors;
            typeRegistry[packetType] = (type, subtype);
        }
    }

    public static (byte Type, byte Subtype) GetType(XPacketType packetType) { /* как раньше, кортеж вместо Tuple */ }
    public static XPacketType GetTypeFromPacket(XPacket packet)            { /* без изменений */ }

    internal static FieldDescriptor[] GetDescriptors(Type t)
    {
        if (!descriptorCache.TryGetValue(t, out var d))
            throw new InvalidOperationException(
                $"Type {t.Name} is not registered. Call XPacketTypeManager.Register<{t.Name}>(...) first.");
        return d;
    }
}
```

Thread-safety: запись под `lock`, чтение из `Dictionary` после регистрации lock-free (cache не мутирует после `Register` для данного `Type`).

### 6.2 FieldDescriptor (internal)

```csharp
internal sealed class FieldDescriptor
{
    public FieldInfo Field { get; }
    public Func<object, object> Getter { get; }
    public Action<object, object> Setter { get; }

    public FieldDescriptor(FieldInfo field)
    {
        Field = field;
        Getter = BuildGetter(field);
        Setter = BuildSetter(field);
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
```

### 6.3 XPacketConverter (переписан)

```csharp
public static class XPacketConverter
{
    public static XPacket Serialize<T>(XPacketType type, T obj) where T : class
    {
        if (obj == null) throw new ArgumentNullException(nameof(obj));

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
        if (packet == null) throw new ArgumentNullException(nameof(packet));

        var descriptors = XPacketTypeManager.GetDescriptors(typeof(T));

        if (packet.Fields.Count != descriptors.Length)
            throw new InvalidOperationException(
                $"Field count mismatch for {typeof(T).Name}: expected {descriptors.Length}, got {packet.Fields.Count}.");

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
```

Удалены старые сигнатуры `Serialize(XPacketType, object, bool strict)`, `Serialize(byte, byte, object, bool strict)`, и параметр `bool strict`.

### 6.4 XPacket — новые методы

```csharp
public void AppendValue(object structure)
{
    if (!structure.GetType().IsValueType)
        throw new ArgumentException("Only value types are supported.");

    var bytes = FixedObjectToByteArray(structure);
    if (bytes.Length > byte.MaxValue)
        throw new InvalidOperationException("Field too large (>255 bytes).");

    Fields.Add(new XPacketField { FieldSize = (byte)bytes.Length, Contents = bytes });
}

public T GetValueAt<T>(int index) where T : struct
{
    if (index < 0 || index >= Fields.Count)
        throw new ArgumentOutOfRangeException(nameof(index));
    return ByteArrayToFixedObject<T>(Fields[index].Contents);
}

public object GetValueAt(int index, Type t)
{
    return typeof(XPacket)
        .GetMethod(nameof(GetValueAt), new[] { typeof(int) })
        .MakeGenericMethod(t)
        .Invoke(this, new object[] { index });
}
```

Удалены: `SetValue(byte id, …)`, `GetValue<T>(byte id)`, `GetField(byte id)`, `HasField(byte id)`, `GetValueRaw(byte id)`, `SetValueRaw(byte id, byte[])` (последние два — если на них нет внешних зависимостей; проверяется на этапе плана).

### 6.5 ToPacket / Parse — новый wire format

`ToPacket()`:

```csharp
public byte[] ToPacket()
{
    if (Fields.Count > byte.MaxValue)
        throw new InvalidOperationException("Too many fields (>255).");

    var ms = new MemoryStream();
    ms.Write(ChangeHeaders
        ? new byte[] { 0x95, 0xAA, 0xFF, PacketType, PacketSubtype }
        : new byte[] { 0xAF, 0xAA, 0xAF, PacketType, PacketSubtype }, 0, 5);

    ms.WriteByte((byte)Fields.Count);

    foreach (var f in Fields)
    {
        ms.WriteByte(f.FieldSize);
        if (f.FieldSize > 0) ms.Write(f.Contents, 0, f.FieldSize);
    }

    ms.Write(new byte[] { 0xFF, 0x00 }, 0, 2);
    return ms.ToArray();
}
```

`Parse(byte[], bool markAsEncrypted)`:

```csharp
public static XPacket Parse(byte[] packet, bool markAsEncrypted = false)
{
    if (packet.Length < 8) return null;

    bool encrypted = false;
    if (!(packet[0] == 0xAF && packet[1] == 0xAA && packet[2] == 0xAF))
    {
        if (packet[0] == 0x95 && packet[1] == 0xAA && packet[2] == 0xFF) encrypted = true;
        else return null;
    }

    var type = packet[3];
    var subtype = packet[4];
    var fieldCount = packet[5];

    var xp = new XPacket { PacketType = type, PacketSubtype = subtype, Protected = markAsEncrypted };
    int pos = 6;

    for (int i = 0; i < fieldCount; i++)
    {
        if (pos + 1 > packet.Length - 2) return null;
        var size = packet[pos++];
        if (pos + size > packet.Length - 2) return null;
        var contents = size != 0 ? packet.Skip(pos).Take(size).ToArray() : null;
        pos += size;
        xp.Fields.Add(new XPacketField { FieldSize = size, Contents = contents });
    }

    if (pos != packet.Length - 2 || packet[pos] != 0xFF || packet[pos + 1] != 0x00)
        return null;

    return encrypted ? DecryptPacket(xp) : xp;
}
```

### 6.6 XPacketField (упрощён)

```csharp
public class XPacketField
{
    public byte FieldSize { get; set; }
    public byte[] Contents { get; set; }
    // FieldID — удалён
}
```

---

## 7. Migration Impact

### 7.1 Файлы для изменения

| Файл | Действие |
|------|----------|
| `XProtocol/Serializator/XFieldAttribute.cs` | **Удалить** |
| `XProtocol/Serializator/XPacketConverter.cs` | **Переписать** (см. §6.3) |
| `XProtocol/XPacketField.cs` | Убрать `FieldID` (см. §6.6) |
| `XProtocol/XPacket.cs` | Переписать API (см. §6.4) и `ToPacket`/`Parse` (см. §6.5). Удалить `SetValue/GetValue/GetField/HasField` методы по `byte id`. |
| `XProtocol/XPacketTypeManager.cs` | Расширить `Register<T>`, `GetDescriptors`. Static ctor регистрирует `XPacketHandshake`. Добавить `FieldDescriptor` (internal sealed). Добавить `BuildDescriptors`. |
| `XProtocol/XPacketHandshake.cs` | Убрать `[XField(1)]`. Тип становится `public class XPacketHandshake { public int MagicHandshakeNumber; }`. |
| `Test/Program.cs` | Убрать `[XField(0/1/2)]` с `TestPacket`. Добавить `XPacketTypeManager.Register<TestPacket>(...)` в инициализации. |
| `TCPClient/Program.cs` | Регистрация `XPacketHandshake` уже выполняется в static ctor `XPacketTypeManager` — изменения только в `using` импортах при необходимости. |
| `TCPServer/ConnectedClient.cs` | То же. |

### 7.2 Семантическое соответствие старого/нового порядка

После удаления `[XField(0/1/2)]` с `TestPacket` порядок полей по `MetadataToken` (Roslyn declaration order) — `TestNumber → TestDouble → TestBoolean`, что совпадает с прежними FieldID `0 → 1 → 2`. То есть семантический порядок сохраняется, меняется только wire-кодирование (нет id-байта на поле, есть один FieldCount-байт перед списком).

### 7.3 Wire-протокол старых клиентов

Несовместим. Старый клиент/сервер не сможет общаться с новым (header один, но layout payload разный — старый ожидает FieldID на каждом поле). Это сознательное решение (см. ответ на вопрос Q8).

---

## 8. Out of Scope

- Reference-типы, `string`, массивы, nested DTO в полях. Остаётся ограничение `IsValueType`.
- Расширение `FieldSize` до `int` (>255 байт на поле).
- Расширение `FieldCount` до `int` (>255 полей).
- Рефакторинг `Test/Program.cs` за пределами регистрации `TestPacket` и удаления атрибутов.
- Unit-тесты на новый сериализатор (по правилу пользователя — тесты не пишутся без явного подтверждения).
- Изменения в `1/AsbtCore.Broker.*` — этот код не использует `XProtocol.Serializator`.

---

## 9. Acceptance Criteria

1. После регистрации DTO без атрибута, `XPacketConverter.Serialize` возвращает `XPacket` с `Fields.Count == descriptors.Length` в ожидаемом порядке.
2. `XPacketConverter.Deserialize<T>` восстанавливает DTO с теми же значениями полей при roundtrip `Serialize → ToPacket → Parse → Deserialize`.
3. Попытка десериализовать пакет с `FieldCount != descriptors.Length` бросает `InvalidOperationException` с диагностикой.
4. Попытка `Serialize`/`Deserialize` для незарегистрированного типа бросает `InvalidOperationException` с указанием имени типа.
5. Существующий handshake между `TCPClient` и `TCPServer` работает без явной регистрации со стороны пользовательского кода (регистрация выполнена в static ctor `XPacketTypeManager`).
6. `Test/Program.cs` после удаления атрибутов и добавления `Register<TestPacket>(...)` сериализует/десериализует roundtrip без ошибок.
7. Сборка проекта `XProtocol.csproj` под .NET 10 проходит без ошибок и предупреждений уровня error.

---

## 10. Известные ограничения и риски

1. **Cross-assembly inheritance + MetadataToken.** Если DTO наследует от типа в другой assembly, `MetadataToken`-сортировка может дать порядок зависящий от порядка загрузки сборок. Mitigation: документация (этот спек), runtime-проверка на этапе регистрации не выполняется (слишком сложно для покрытия всех случаев). Пользователю рекомендуется держать DTO-иерархию в одной assembly.
2. **Roslyn-implementation-defined declaration order.** `MetadataToken` для полей внутри одного типа стабилен по declaration order на текущих компиляторах Roslyn. Формально это implementation detail. Mitigation: документация; при изменении порядка полей в DTO протокол ломается на стороне получателя — это нормально и ожидаемо (любая мутация структуры DTO ломает совместимость).
3. **Reference types в DTO** — детектируются на этапе `Register<T>` через проверку `IsValueType` и приводят к throw. Без compile-time гарантии.
4. **Static ctor `XPacketTypeManager` бросает при ошибке регистрации `XPacketHandshake`.** Это происходит в первом обращении к классу. При сбое — `TypeInitializationException`. Считается приемлемым: если встроенный DTO не регистрируется, протокол неработоспособен.
