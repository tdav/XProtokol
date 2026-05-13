# XProtocol: добавление поддержки `string`-полей в сериализатор

**Дата:** 2026-05-13
**Статус:** Design / awaiting implementation
**Затрагиваемые проекты:** `XProtocol`, `XProtocol.Tests`, `Test`

## Постановка задачи

`XPacketTypeManager.BuildDescriptors` сейчас отклоняет любые поля
DTO, не являющиеся value-type:

```text
System.InvalidOperationException
  TestPacket.TestString: only value-type fields are supported.
```

`TestPacket` содержит `public string TestString` (~900 байт UTF-8 в
`Test/Program.cs`). Регистрация падает на этапе
`XPacketTypeManager.Register<TestPacket>(...)`.

Цель — добавить поддержку **`string`-полей** в DTO протокола без
изменения wire-байтов магической последовательности, заголовка пакета,
размера `FieldSize` (1 байт) и трейлера (`0xFF 0x00`). Поддержка
произвольных reference-типов вне scope (YAGNI).

## Принятые решения

| # | Решение | Обоснование |
|---|--------|------------|
| 1 | Поддерживается только `string` (а не любой ref-type) | YAGNI; точечный фикс под имеющийся TestPacket |
| 2 | Wire-format магия / заголовок / трейлер не меняются | Сохраняем совместимость с уже разосланными пакетами без строк |
| 3 | Логический string-descriptor → 1..N wire-полей (chunked) | `FieldSize` остаётся 1 байт; длинные строки укладываются |
| 4 | Кодировка UTF-8 без BOM | Стандарт для byte-oriented протоколов |
| 5 | Length-префикс: ushort little-endian (2 байта) | Соответствует endianness `Marshal.StructureToPtr` на x86/x64 |
| 6 | Максимальная длина строки = 65535 байт UTF-8 (ushort.MaxValue) | Естественный предел префикса |
| 7 | `null` сериализуется как пустая строка; десериализуется в `""` | Выбор пользователя; никогда не возвращается `null` |
| 8 | `AppendValue(object)` оставляем value-type-only; новый low-level помощник `AppendChunks(byte[])` | Не размываем существующий контракт |
| 9 | Тесты — полный набор позитивных и негативных | Пользователь явно подтвердил |

## Wire-format строки

Магия (`0xAF 0xAA 0xAF` / `0x95 0xAA 0xFF`), header, `FieldSize` (1 байт),
`Fields.Count` (1 байт), трейлер `0xFF 0x00` — **не меняются**.

Меняется только то, как один логический string-descriptor ложится на
последовательность wire-полей.

**Логическая полезная нагрузка строки:**

```text
[ length: ushort little-endian (2 bytes) ] [ utf8_bytes (L bytes) ]
```

- `null` → `length = 0`, нет UTF-8 байтов.
- `""` → `length = 0`, нет UTF-8 байтов.
- UTF-8 без BOM (`Encoding.UTF8.GetBytes`).
- `length > 65535` → `InvalidOperationException` на serialize.

**Раскладка на wire-поля:**

Логический payload `(L + 2)` байт нарезается на блоки по `byte.MaxValue`
(255) байт. Каждый блок → отдельное `XPacketField`
(`FieldSize = блок.Length`, `Contents = блок`). Последний блок может быть
короче 255.

Для строки длиной L байт UTF-8:

| L | Wire-полей | Размеры |
|---|-----------|---------|
| 0 | 1 | `[2]` |
| 1 | 1 | `[3]` |
| 253 | 1 | `[255]` |
| 254 | 2 | `[255, 1]` |
| 510 | 3 | `[255, 255, 2]` |
| ~900 (TestPacket) | 4 | `[255, 255, 255, 137]` |
| 65535 | 257 | `[255, 255, ..., 254]` |

Общий cap пакета — `Fields.Count` пишется одним байтом, значит суммарное
число wire-полей всех descriptors ≤ 255. Проверяется на serialize
после нарезки всех descriptors.

## Компоненты и изменения

### `XProtocol/Serializator/FieldDescriptor.cs`

Добавить:

```csharp
internal enum FieldKind { ValueType, String }
```

Поля:

- `FieldKind Kind { get; }`
- `Func<object, string> StringGetter { get; }` — собран Expression-методом
  только при `Kind == String`, иначе `null`.
- `Action<object, string> StringSetter { get; }` — аналогично.

Конструктор:

```csharp
public FieldDescriptor(FieldInfo field)
{
    this.Field = field;

    if (field.FieldType == typeof(string))
    {
        this.Kind = FieldKind.String;
        this.StringGetter = BuildStringGetter(field);
        this.StringSetter = BuildStringSetter(field);
    }
    else if (field.FieldType.IsValueType)
    {
        this.Kind = FieldKind.ValueType;
        this.Getter = BuildGetter(field);
        this.Setter = BuildSetter(field);
    }
    else
    {
        throw new InvalidOperationException(
            $"Unsupported field type for {field.DeclaringType.Name}.{field.Name}: " +
            $"{field.FieldType.Name}. Only value-type fields and string are supported.");
    }
}
```

`BuildStringGetter` / `BuildStringSetter` — типизированные Expression-делегаты,
без boxing'а:

```csharp
private static Func<object, string> BuildStringGetter(FieldInfo f)
{
    var p = Expression.Parameter(typeof(object), "o");
    var body = Expression.Field(Expression.Convert(p, f.DeclaringType), f);
    return Expression.Lambda<Func<object, string>>(body, p).Compile();
}

private static Action<object, string> BuildStringSetter(FieldInfo f)
{
    var p = Expression.Parameter(typeof(object), "o");
    var v = Expression.Parameter(typeof(string), "v");
    var body = Expression.Assign(
        Expression.Field(Expression.Convert(p, f.DeclaringType), f), v);
    return Expression.Lambda<Action<object, string>>(body, p, v).Compile();
}
```

### `XProtocol/XPacketTypeManager.cs`

`BuildDescriptors`: убрать blanket `!IsValueType` throw. Проверку типа
делегируем конструктору `FieldDescriptor`. Текст ошибки регистрации
форматируется в `FieldDescriptor`. Cap на 255 descriptors остаётся.

```csharp
// Per-field validation removed; FieldDescriptor ctor now throws for
// unsupported types.

if (sorted.Length > byte.MaxValue)
{
    throw new InvalidOperationException(
        $"{t.Name} has more than {byte.MaxValue} fields.");
}

return sorted.Select(f => new FieldDescriptor(f)).ToArray();
```

### `XProtocol/XPacket.cs`

`AppendValue(object)` — **без изменений**. Контракт: только value-type.

Новые внутренние методы:

```csharp
internal void AppendChunks(byte[] payload)
{
    if (payload == null) throw new ArgumentNullException(nameof(payload));

    int offset = 0;
    while (offset < payload.Length)
    {
        int size = Math.Min(byte.MaxValue, payload.Length - offset);
        var chunk = new byte[size];
        Buffer.BlockCopy(payload, offset, chunk, 0, size);
        Fields.Add(new XPacketField
        {
            FieldSize = (byte)size,
            Contents = chunk
        });
        offset += size;
    }
}

internal byte[] GetRawAt(int index)
{
    if (index < 0 || index >= Fields.Count)
    {
        throw new ArgumentOutOfRangeException(nameof(index));
    }
    return Fields[index].Contents ?? Array.Empty<byte>();
}
```

`AppendChunks` рассчитан на payload длиной ≥ 1; пустой массив не вызывается
(string-payload всегда ≥ 2 байта префикса).

### `XProtocol/Serializator/XPacketConverter.cs`

**Serialize:**

```csharp
public static XPacket Serialize<T>(T obj) where T : class
{
    if (obj == null) throw new ArgumentNullException(nameof(obj));

    var (btype, bsubtype) = XPacketTypeManager.GetBytesFor(typeof(T));
    var descriptors = XPacketTypeManager.GetDescriptors(typeof(T));
    var packet = XPacket.Create(btype, bsubtype);

    foreach (var desc in descriptors)
    {
        if (desc.Kind == FieldKind.ValueType)
        {
            packet.AppendValue(desc.Getter(obj));
        }
        else // String
        {
            var s = desc.StringGetter(obj) ?? string.Empty;
            var utf8 = Encoding.UTF8.GetBytes(s);

            if (utf8.Length > ushort.MaxValue)
            {
                throw new InvalidOperationException(
                    $"{typeof(T).Name}.{desc.Field.Name}: string exceeds " +
                    $"{ushort.MaxValue} UTF-8 bytes (actual: {utf8.Length}).");
            }

            var payload = new byte[utf8.Length + 2];
            payload[0] = (byte)(utf8.Length & 0xFF);
            payload[1] = (byte)((utf8.Length >> 8) & 0xFF);
            Buffer.BlockCopy(utf8, 0, payload, 2, utf8.Length);
            packet.AppendChunks(payload);
        }
    }

    if (packet.Fields.Count > byte.MaxValue)
    {
        throw new InvalidOperationException(
            $"{typeof(T).Name}: packet exceeds {byte.MaxValue} wire fields " +
            $"(actual: {packet.Fields.Count}). Reduce string field sizes.");
    }

    return packet;
}
```

**Deserialize (walker):**

```csharp
public static T Deserialize<T>(XPacket packet) where T : class, new()
{
    if (packet == null) throw new ArgumentNullException(nameof(packet));

    var descriptors = XPacketTypeManager.GetDescriptors(typeof(T));
    var instance = new T();

    int wireIdx = 0;
    foreach (var desc in descriptors)
    {
        if (desc.Kind == FieldKind.ValueType)
        {
            if (wireIdx >= packet.Fields.Count)
            {
                ThrowFieldCountMismatch<T>(descriptors, wireIdx, packet);
            }
            var raw = packet.GetValueAt(wireIdx, desc.Field.FieldType);
            desc.Setter(instance, raw);
            wireIdx++;
        }
        else // String
        {
            if (wireIdx >= packet.Fields.Count)
            {
                ThrowFieldCountMismatch<T>(descriptors, wireIdx, packet);
            }
            var first = packet.GetRawAt(wireIdx++);
            if (first.Length < 2)
            {
                throw new InvalidOperationException(
                    $"{typeof(T).Name}.{desc.Field.Name}: string header " +
                    $"truncated (first chunk size {first.Length} < 2).");
            }
            int L = first[0] | (first[1] << 8);

            // copy first chunk's payload portion
            int have = first.Length - 2;
            var acc = new byte[L];
            int copy = Math.Min(have, L);
            Buffer.BlockCopy(first, 2, acc, 0, copy);
            int filled = copy;

            while (filled < L)
            {
                if (wireIdx >= packet.Fields.Count)
                {
                    throw new InvalidOperationException(
                        $"{typeof(T).Name}.{desc.Field.Name}: string truncated " +
                        $"(need {L} bytes, have {filled} after consuming all " +
                        "remaining wire fields).");
                }
                var next = packet.GetRawAt(wireIdx++);
                int take = Math.Min(next.Length, L - filled);
                Buffer.BlockCopy(next, 0, acc, filled, take);
                filled += take;
            }

            var str = Encoding.UTF8.GetString(acc);
            desc.StringSetter(instance, str);
        }
    }

    if (wireIdx != packet.Fields.Count)
    {
        // Reuses the historical message shape from StrictCountTests.
        throw new InvalidOperationException(
            $"Field count mismatch for {typeof(T).Name}: " +
            $"expected {wireIdx}, got {packet.Fields.Count}.");
    }

    return instance;
}

private static void ThrowFieldCountMismatch<T>(
    FieldDescriptor[] descriptors, int wireIdx, XPacket packet)
{
    // Number of wire fields that would be consumed if the packet had been complete:
    // at minimum descriptors.Length (1 per value-type, 1+ per string).
    // For the mismatch message, surface the count we expected to walk up to.
    throw new InvalidOperationException(
        $"Field count mismatch for {typeof(T).Name}: " +
        $"expected {descriptors.Length}, got {packet.Fields.Count}.");
}
```

Замечание о backward-compat сообщения: `StrictCountTests` использует
`SimpleDto` (3 value-поля). При удалении одного поля walker встретит
ValueType-descriptor с `wireIdx == packet.Fields.Count` → бросит
`ThrowFieldCountMismatch` с `expected=3, got=2`. Текст совпадает с
оригинальным; тест проходит без правок.

### `Test/Program.cs`

Заменить строку 39 — добавить `TestString` в вывод, чтобы ручной smoke
проверял весь roundtrip:

```csharp
Console.WriteLine(
    $"TestNumber={roundtrip.TestNumber}, TestDouble={roundtrip.TestDouble}, " +
    $"TestBoolean={roundtrip.TestBoolean}, TestString.Length={roundtrip.TestString.Length}");
Console.WriteLine($"TestString equal: {dto.TestString == roundtrip.TestString}");
```

## Обработка ошибок (сводка сообщений)

| Стадия | Условие | Текст |
|--------|---------|-------|
| Register | ref-type ≠ string | `Unsupported field type for {T}.{f}: {Type}. Only value-type fields and string are supported.` |
| Register | descriptors > 255 | `{T.Name} has more than 255 fields.` (без изменений) |
| Serialize | `obj == null` | `ArgumentNullException` (без изменений) |
| Serialize | UTF-8 > 65535 байт | `{T.Name}.{f.Name}: string exceeds 65535 UTF-8 bytes (actual: {n}).` |
| Serialize | wire fields > 255 | `{T.Name}: packet exceeds 255 wire fields (actual: {n}). Reduce string field sizes.` |
| Deserialize | `packet == null` | `ArgumentNullException` (без изменений) |
| Deserialize | wireIdx ≠ Fields.Count | `Field count mismatch for {T.Name}: expected {descriptors.Length}, got {Fields.Count}.` |
| Deserialize | first chunk < 2 байт у string | `{T.Name}.{f.Name}: string header truncated (first chunk size {n} < 2).` |
| Deserialize | string-payload оборвался | `{T.Name}.{f.Name}: string truncated (need {L} bytes, have {filled} after consuming all remaining wire fields).` |

## Тестирование

Файлы:

- `XProtocol.Tests/RoundtripTests.cs` — расширить.
- `XProtocol.Tests/StrictCountTests.cs` — проверить, что текст ошибки
  совпадает; правки не требуются (SimpleDto без строк).
- `XProtocol.Tests/TestDtos.cs` — добавить новые DTO.
- `XProtocol.Tests/RegistrationTests.cs` — добавить кейс на «неподдерживаемый
  ref-type не = string».
- `Test/Program.cs` — обновить Console.WriteLine (см. выше).

Framework: TUnit (`await Assert.That(...)`).

### Новые DTO в `TestDtos.cs`

```csharp
public class StringDto
{
    public int A;
    public string S;
    public bool B;
}

public class MultiStringDto
{
    public string First;
    public int Middle;
    public string Last;
}

public class UnsupportedRefDto
{
    public int A;
    public int[] Bad;   // array — not value-type, not string
}
```

### Позитивные roundtrip-тесты

| # | Имя | Значение `S` | Ожидание |
|---|-----|--------------|----------|
| 1 | `Roundtrip_Empty` | `""` | `S == ""`, 3 wire-поля |
| 2 | `Roundtrip_Null` | `null` | `S == ""` (нормализация в пустую) |
| 3 | `Roundtrip_ShortAscii` | `"hello"` | exact roundtrip |
| 4 | `Roundtrip_BoundarySingleChunk` | `new string('a', 253)` | exact, 3 wire-поля |
| 5 | `Roundtrip_BoundaryTwoChunk` | `new string('a', 254)` | exact, 4 wire-поля |
| 6 | `Roundtrip_TestPacketSize` | `new string('x', 900)` (соответствует TestPacket.TestString из `Test/Program.cs` по порядку величины — строка хранится в самом тесте, чтобы не зависеть от внешнего файла) | exact; ceil((900+2)/255) = 4 string-чанка → 6 wire-полей в `StringDto` |
| 7 | `Roundtrip_MultiByteUtf8` | `"привет мир"` | exact |
| 8 | `Roundtrip_Emoji` | `"abc 🚀 xyz"` | exact |
| 9 | `Roundtrip_Max` | `new string('x', 16000)` (ASCII = 1 байт UTF-8; payload ≈ 16002 байт → ≈ 63 wire-чанка) | exact, DTO `StringDto` помещается в 255 wire-полей |
| 10 | `Roundtrip_MultiStringDto` | `First = "alpha"`, `Last = "omega"` | walker корректно обрабатывает несколько string-descriptors подряд и вперемешку с value-type |
| 11 | `Roundtrip_Encrypted` | `Test/Program.cs`-строка через `Encrypt().ToPacket() → Parse → Deserialize` | encryption path работает с многочанковыми строками |

### Негативные тесты

| # | Имя | Сценарий | Ожидание |
|---|-----|----------|----------|
| N1 | `Serialize_StringOverflow_Throws` | `new string('x', 65536)` в `StringDto.S` | `InvalidOperationException` с подстрокой `"exceeds 65535"` |
| N2 | `Serialize_TotalWireOverflow_Throws` | DTO, у которого общее число wire-полей превысит 255 (DTO с 254 value-полями + строка ≥ 254 байт) | `InvalidOperationException` с подстрокой `"exceeds 255 wire fields"` |
| N3 | `Register_UnsupportedRefType_Throws` | регистрация `UnsupportedRefDto` | `InvalidOperationException` с подстрокой `"Unsupported field type"` и `"Only value-type fields and string are supported"` |
| N4 | `Deserialize_StringTruncated_Throws` | взять валидный packet со строкой ≥ 256 байт, `Fields.RemoveAt(Fields.Count - 1)` | `InvalidOperationException` с подстрокой `"string truncated"` |
| N5 | `Deserialize_HeaderTruncated_Throws` | подменить first chunk строки на `Contents = new byte[]{0}`, `FieldSize = 1` | `InvalidOperationException` с подстрокой `"string header truncated"` |
| N6 | `Deserialize_FieldCountMismatch_Throws` (существующий) | без правок | проходит как раньше |

### Pre-test setup

Каждый тест регистрирует свой `XPacketType` под уникальным subtype, чтобы
не конфликтовать с другими тестами (см. существующие `RegistrationTests`).
Если регистрация глобальная и невыгружаемая — использовать выделенные
`XPacketType` enum-значения и регистрировать однократно в фикстуре
(`ClassDataSource` / static init).

## Файлы, которые правим

1. `XProtocol/Serializator/FieldDescriptor.cs`
2. `XProtocol/Serializator/XPacketConverter.cs`
3. `XProtocol/XPacketTypeManager.cs`
4. `XProtocol/XPacket.cs`
5. `XProtocol.Tests/TestDtos.cs`
6. `XProtocol.Tests/RoundtripTests.cs`
7. `XProtocol.Tests/RegistrationTests.cs`
8. `Test/Program.cs`

## Не входит в scope

- Поддержка произвольных reference-типов (массивы, классы, `byte[]`).
- Изменение wire-format магических байтов / заголовка / трейлера.
- Поддержка `string` ≥ 65536 байт.
- Поддержка nullable value-types (`int?`, `bool?` и т. п.).
- Изменения в RPC-генераторе `XPacketRpc.*` (отдельный wire-format,
  не пересекается).

## Открытые риски

| Риск | Митигация |
|------|-----------|
| TUnit-фикстуры с глобальным `XPacketTypeManager` могут конфликтовать между тестами | Использовать уникальные `XPacketType` enum-значения; убедиться, что повторная регистрация под одним типом кидает (текущее поведение `Register`) |
| `Marshal.SizeOf` для value-types на разных платформах может отличаться (паддинг) | Не меняется существующее поведение для value-types; новый риск не вводим |
| Полная строка из `Test/Program.cs` может не пройти, если её фактическая длина окажется > 255 байт UTF-8 (она > 255) — это **ожидаемый сценарий**, тест должен на ней проходить | См. test #6 |
