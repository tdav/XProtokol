# XProtocol

[English](README.md) | **Русский**

Лёгковесная бинарная протокольная библиотека для .NET поверх TCP. Реализует создание пакетов, сериализацию/десериализацию C#-объектов и опциональное AES-шифрование.

Исходник для статьи [«Своими руками: пишем свой протокол поверх TCP»](https://xakep.ru/2019/02/01/diy-tcp/) на Xakep.ru.

---

## Оглавление

1. [Бинарный формат пакета](#1-бинарный-формат-пакета)
2. [XPacketType — перечисление типов пакетов](#2-xpackettype--перечисление-типов-пакетов)
3. [XPacketTypeManager — реестр типов](#3-xpackettypemanager--реестр-типов)
4. [XPacketConverter — сериализатор](#4-xpacketconverter--сериализатор)
5. [Шифрование](#5-шифрование)
6. [Сквозной пример: рукопожатие](#6-сквозной-пример-рукопожатие)
7. [Добавление нового типа пакета](#7-добавление-нового-типа-пакета)
8. [Справочник типов](#8-справочник-типов)

---

## 1. Бинарный формат пакета

Каждый пакет сериализуется в массив байт следующей структуры:

```
┌─────────────────────────────────────────────────────────────────┐
│  Заголовок (5 байт)                                             │
│  [0xAF 0xAA 0xAF]  PacketType(1)  PacketSubtype(1)             │
├─────────────────────────────────────────────────────────────────┤
│  FieldCount (1 байт) — количество полей                         │
├─────────────────────────────────────────────────────────────────┤
│  Для каждого поля:                                              │
│  FieldSize(1)  Contents(FieldSize байт)                         │
├─────────────────────────────────────────────────────────────────┤
│  Терминатор (2 байта): 0xFF 0x00                                │
└─────────────────────────────────────────────────────────────────┘
```

Для зашифрованных пакетов первые 3 байта заголовка заменяются на `0x95 0xAA 0xFF`.

**Ограничения:**
- максимальный размер пакета — 256 байт (ограничение TCPServer/TCPClient)
- размер одного поля — не более 255 байт
- количество полей — не более 255

Создание пакета вручную:

```csharp
var packet = XPacket.Create(XPacketType.Handshake);
packet.AppendValue(42);           // int → 4 байта
packet.AppendValue(3.14f);        // float → 4 байта
byte[] raw = packet.ToPacket();   // сериализовать в byte[]
```

Разбор входящих байт:

```csharp
XPacket packet = XPacket.Parse(raw);          // null если заголовок неверный
int val   = packet.GetValueAt<int>(0);        // поле 0 → int
float flt = packet.GetValueAt<float>(1);      // поле 1 → float
```

---

## 2. XPacketType — перечисление типов пакетов

`XPacketType` — центральный enum, определяющий все **логические типы** пакетов протокола.

```csharp
public enum XPacketType
{
    Unknown          = 0,   // неопознанный / резервный
    Handshake        = 1,   // установка соединения
    GetOrderAllMethod = 2,  // запрос всех заказов
    GetPriceAllMethod = 3,  // запрос всех цен
}
```

### Как XPacketType связан с байтами в пакете

`XPacketType` — это только идентификатор на уровне C#. В бинарный пакет уходят два независимых байта: `PacketType` и `PacketSubtype`. Связь между ними задаётся через `XPacketTypeManager.Register<T>()`:

```
XPacketType.Handshake  →  (Type=1, Subtype=0)  →  байты [0x01 0x00] в пакете
```

При разборе входящего пакета происходит обратное:

```
байты [0x01 0x00]  →  XPacketTypeManager.GetTypeFromPacket()  →  XPacketType.Handshake
```

### Как используется в сервере

`ConnectedClient` получает разобранный `XPacket` и определяет его тип через `XPacketTypeManager`:

```csharp
// TCPServer/ConnectedClient.cs
private void ProcessIncomingPacket(XPacket packet)
{
    var type = XPacketTypeManager.GetTypeFromPacket(packet);

    switch (type)
    {
        case XPacketType.Handshake:
            ProcessHandshake(packet);
            break;

        case XPacketType.GetOrderAllMethod:
            ProcessGetOrderAllMethod(packet);
            break;

        case XPacketType.GetPriceAllMethod:
            ProcessGetPriceAllMethod(packet);
            break;

        case XPacketType.Unknown:
            break; // игнорируем неопознанные пакеты

        default:
            throw new ArgumentOutOfRangeException();
    }
}
```

### Как используется в клиенте

Клиент использует тот же паттерн для обработки ответов сервера:

```csharp
// TCPClient/Program.cs
private static void ProcessIncomingPacket(XPacket packet)
{
    var type = XPacketTypeManager.GetTypeFromPacket(packet);

    switch (type)
    {
        case XPacketType.Handshake:
            ProcessHandshake(packet);
            break;

        case XPacketType.Unknown:
            break;

        default:
            throw new ArgumentOutOfRangeException();
    }
}
```

---

## 3. XPacketTypeManager — реестр типов

`XPacketTypeManager` — статический реестр, связывающий три сущности:
- `XPacketType` (enum)
- пару байт `(Type, Subtype)` для бинарного заголовка
- C#-класс DTO с описанием полей (через reflection)

### Регистрация

```csharp
// Выполняется один раз при старте приложения.
// XPacketHandshake регистрируется автоматически в статическом конструкторе XPacketTypeManager.
XPacketTypeManager.Register<XPacketHandshake>(XPacketType.Handshake, type: 1, subtype: 0);

// Свой тип:
XPacketTypeManager.Register<OrderRequest>(XPacketType.GetOrderAllMethod, type: 2, subtype: 0);
```

> Повторная регистрация одного `XPacketType` бросает `InvalidOperationException`.

### Разрешение типа из пакета

```csharp
XPacketType type = XPacketTypeManager.GetTypeFromPacket(packet);
// Возвращает XPacketType.Unknown, если пара (Type, Subtype) не зарегистрирована.
```

### Разрешение байт из enum

```csharp
var (btype, bsubtype) = XPacketTypeManager.GetType(XPacketType.Handshake);
// → (1, 0)
```

---

## 4. XPacketConverter — сериализатор

`XPacketConverter` преобразует C#-класс в `XPacket` и обратно. Поля обнаруживаются автоматически через reflection — в порядке `MetadataToken` (порядок объявления в исходнике).

**Ограничение:** все публичные поля класса-DTO должны быть value types (`int`, `float`, `bool`, custom struct и т.д.). Reference types не поддерживаются.

### Сериализация

```csharp
var dto = new XPacketHandshake { MagicHandshakeNumber = 99_999 };
byte[] raw = XPacketConverter.Serialize(dto).ToPacket();
```

### Десериализация

```csharp
XPacket packet = XPacket.Parse(raw);
var dto = XPacketConverter.Deserialize<XPacketHandshake>(packet);
Console.WriteLine(dto.MagicHandshakeNumber); // 99999
```

### Пример собственного DTO

```csharp
public class OrderRequest
{
    public int OrderId;
    public float Price;
    public bool IsActive;
}

// Регистрируем один раз при старте:
XPacketTypeManager.Register<OrderRequest>(XPacketType.GetOrderAllMethod, type: 2, subtype: 0);

// Отправка (клиент):
byte[] raw = XPacketConverter.Serialize(new OrderRequest
{
    OrderId  = 7,
    Price    = 199.99f,
    IsActive = true
}).ToPacket();
client.QueuePacketSend(raw);

// Приём (сервер):
var req = XPacketConverter.Deserialize<OrderRequest>(packet);
Console.WriteLine($"Order #{req.OrderId}, price={req.Price}");
```

---

## 5. Шифрование

Шифрование реализовано через `RijndaelHandler` (AES-256-CBC). Встроенный `XProtocolEncryptor` использует фиксированный внутренний ключ.

| Параметр | Значение |
|---|---|
| Алгоритм | AES-256 CBC |
| Длина ключа | 256 бит |
| Деривация ключа | PBKDF2 / SHA-256 / 1000 итераций |
| Salt | 32 байта случайных данных (в начале шифртекста) |
| IV | 16 байт случайных данных (после salt) |

```csharp
// Зашифровать пакет:
byte[] raw       = XPacketConverter.Serialize(dto).ToPacket();
byte[] encrypted = XProtocolEncryptor.Encrypt(raw);

// Расшифровать:
byte[] decrypted = XProtocolEncryptor.Decrypt(encrypted);
XPacket packet   = XPacket.Parse(decrypted);
```

Зашифрованные пакеты имеют заголовок `0x95 0xAA 0xFF` (вместо `0xAF 0xAA 0xAF`) — `XPacket.Parse` определяет это автоматически и расшифровывает прозрачно.

---

## 6. Сквозной пример: рукопожатие

Ниже — полный flow из TCPClient и TCPServer.

### Шаг 1. Клиент генерирует magic-число и отправляет Handshake

```csharp
// TCPClient/Program.cs
var rand = new Random();
_handshakeMagic = rand.Next();  // например, 582_341

client.QueuePacketSend(
    XPacketConverter.Serialize(
        new XPacketHandshake { MagicHandshakeNumber = _handshakeMagic }
    ).ToPacket()
);
```

Пакет в байтах (упрощённо для magic=582341=0x0008E285):

```
AF AA AF  01 00  01  04  85 E2 08 00  FF 00
─────────  ─────  ──  ──  ──────────  ─────
заголовок  T  ST  FC  FS  int 582341  EOF
```

### Шаг 2. Клиент принимает входящие байты

```csharp
// TCPClient/XClient.cs — RecievePackets()
var buff = new byte[256];
_socket.Receive(buff);

// Обрезаем по терминатору 0xFF 0x00
buff = buff.TakeWhile((b, i) =>
{
    if (b != 0xFF) return true;
    return buff[i + 1] != 0;
}).Concat(new byte[] { 0xFF, 0 }).ToArray();

OnPacketRecieve?.Invoke(buff);
```

### Шаг 3. Сервер получает Handshake, вычитает 15 и отвечает

```csharp
// TCPServer/ConnectedClient.cs
private void ProcessHandshake(XPacket packet)
{
    Console.WriteLine("Recieved handshake packet.");

    var handshake = XPacketConverter.Deserialize<XPacketHandshake>(packet);
    handshake.MagicHandshakeNumber -= 15;  // 582341 → 582326

    Console.WriteLine("Answering..");
    QueuePacketSend(XPacketConverter.Serialize(handshake).ToPacket());
}
```

### Шаг 4. Клиент проверяет ответ

```csharp
// TCPClient/Program.cs
private static void ProcessHandshake(XPacket packet)
{
    var handshake = XPacketConverter.Deserialize<XPacketHandshake>(packet);

    if (_handshakeMagic - handshake.MagicHandshakeNumber == 15)
    {
        Console.WriteLine("Handshake successful!");
    }
}
```

---

## 7. Добавление нового типа пакета

### 1. Добавить значение в `XPacketType`

```csharp
// XProtocol/XPacketType.cs
public enum XPacketType
{
    Unknown           = 0,
    Handshake         = 1,
    GetOrderAllMethod = 2,
    GetPriceAllMethod = 3,
    SetPriceMethod    = 4,   // ← новый тип
}
```

### 2. Создать DTO-класс

```csharp
public class SetPriceRequest
{
    public int    ProductId;
    public float  NewPrice;
}
```

### 3. Зарегистрировать тип при старте

```csharp
// в Program.cs или инициализации сервера/клиента
XPacketTypeManager.Register<SetPriceRequest>(XPacketType.SetPriceMethod, type: 4, subtype: 0);
```

### 4. Добавить ветку в switch на сервере

```csharp
case XPacketType.SetPriceMethod:
    ProcessSetPrice(packet);
    break;

// ...

private void ProcessSetPrice(XPacket packet)
{
    var req = XPacketConverter.Deserialize<SetPriceRequest>(packet);
    Console.WriteLine($"Set price for product {req.ProductId}: {req.NewPrice}");
}
```

### 5. Отправить с клиента

```csharp
client.QueuePacketSend(
    XPacketConverter.Serialize(new SetPriceRequest
    {
        ProductId = 42,
        NewPrice  = 299.99f
    }).ToPacket()
);
```

---

## 8. Справочник типов

| Тип | Описание |
|---|---|
| `XPacket` | Контейнер пакета: заголовок + список `XPacketField` |
| `XPacketField` | Одно бинарное поле (до 255 байт) |
| `XPacketType` | Enum логических типов пакетов |
| `XPacketTypeManager` | Реестр: `XPacketType` ↔ `(byte Type, byte Subtype)` ↔ C#-класс |
| `XPacketConverter` | Reflection-сериализатор DTO → XPacket и обратно |
| `XPacketHandshake` | Встроенный payload рукопожатия (`MagicHandshakeNumber`) |
| `RijndaelHandler` | AES-256-CBC шифрование/дешифрование |
| `XProtocolEncryptor` | Обёртка над `RijndaelHandler` с фиксированным ключом |

---

## Target Framework

.NET 10
