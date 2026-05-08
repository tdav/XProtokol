# XProtocol

**English** | [Русский](README-ru.md)

A lightweight binary TCP packet protocol library for .NET. Provides packet creation, struct-based serialization/deserialization, and optional AES-256 encryption.

Source for the article [«DIY: writing your own protocol over TCP»](https://xakep.ru/2019/02/01/diy-tcp/) on Xakep.ru.

---

## Table of Contents

1. [Binary Packet Format](#1-binary-packet-format)
2. [XPacketType — packet type enum](#2-xpackettype--packet-type-enum)
3. [XPacketTypeManager — type registry](#3-xpackettypemanager--type-registry)
4. [XPacketConverter — serializer](#4-xpacketconverter--serializer)
5. [Encryption](#5-encryption)
6. [End-to-end example: handshake](#6-end-to-end-example-handshake)
7. [Adding a new packet type](#7-adding-a-new-packet-type)
8. [Type reference](#8-type-reference)

---

## 1. Binary Packet Format

Every packet serializes to a byte array with this layout:

```
┌─────────────────────────────────────────────────────────────────┐
│  Header (5 bytes)                                               │
│  [0xAF 0xAA 0xAF]  PacketType(1)  PacketSubtype(1)             │
├─────────────────────────────────────────────────────────────────┤
│  FieldCount (1 byte)                                            │
├─────────────────────────────────────────────────────────────────┤
│  Per field:                                                     │
│  FieldSize(1)  Contents(FieldSize bytes)                        │
├─────────────────────────────────────────────────────────────────┤
│  Terminator (2 bytes): 0xFF 0x00                                │
└─────────────────────────────────────────────────────────────────┘
```

Encrypted packets replace the first 3 header bytes with `0x95 0xAA 0xFF`.

**Constraints:**
- max packet size: 256 bytes (TCPServer/TCPClient limit)
- max single field size: 255 bytes
- max field count: 255

Build a packet manually:

```csharp
var packet = XPacket.Create(XPacketType.Handshake);
packet.AppendValue(42);           // int → 4 bytes
packet.AppendValue(3.14f);        // float → 4 bytes
byte[] raw = packet.ToPacket();
```

Parse incoming bytes:

```csharp
XPacket packet = XPacket.Parse(raw);        // null if header is invalid
int val   = packet.GetValueAt<int>(0);
float flt = packet.GetValueAt<float>(1);
```

---

## 2. XPacketType — packet type enum

`XPacketType` is the central enum that names every **logical** packet type in the protocol.

```csharp
public enum XPacketType
{
    Unknown           = 0,  // unrecognised / reserved
    Handshake         = 1,  // connection initialisation
    GetOrderAllMethod = 2,  // request all orders
    GetPriceAllMethod = 3,  // request all prices
}
```

### How XPacketType maps to wire bytes

`XPacketType` is a C#-side identifier only. The binary packet carries two independent bytes — `PacketType` and `PacketSubtype`. The mapping is established by `XPacketTypeManager.Register<T>()`:

```
XPacketType.Handshake  →  (Type=1, Subtype=0)  →  bytes [0x01 0x00] on the wire
```

When parsing an incoming packet the reverse lookup runs:

```
bytes [0x01 0x00]  →  XPacketTypeManager.GetTypeFromPacket()  →  XPacketType.Handshake
```

### Usage on the server

`ConnectedClient` resolves the type and dispatches:

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
            break;

        default:
            throw new ArgumentOutOfRangeException();
    }
}
```

### Usage on the client

The client uses the same pattern for server responses:

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

## 3. XPacketTypeManager — type registry

`XPacketTypeManager` is a static registry that binds three things together:
- `XPacketType` (enum value)
- a `(Type, Subtype)` byte pair written into the binary header
- a C# DTO class whose fields are cached via reflection

### Registration

```csharp
// Called once at startup.
// XPacketHandshake is pre-registered automatically in XPacketTypeManager's static constructor.
XPacketTypeManager.Register<XPacketHandshake>(XPacketType.Handshake, type: 1, subtype: 0);

// Custom type:
XPacketTypeManager.Register<OrderRequest>(XPacketType.GetOrderAllMethod, type: 2, subtype: 0);
```

> Registering the same `XPacketType` twice throws `InvalidOperationException`.

### Resolve type from a packet

```csharp
XPacketType type = XPacketTypeManager.GetTypeFromPacket(packet);
// Returns XPacketType.Unknown when the (Type, Subtype) pair is not registered.
```

### Resolve wire bytes from enum

```csharp
var (btype, bsubtype) = XPacketTypeManager.GetType(XPacketType.Handshake);
// → (1, 0)
```

---

## 4. XPacketConverter — serializer

`XPacketConverter` converts a C# class to an `XPacket` and back. Fields are discovered automatically via reflection in `MetadataToken` order (declaration order in source).

**Constraint:** all public instance fields on the DTO must be value types (`int`, `float`, `bool`, custom struct, etc.). Reference types are not supported.

### Serialize

```csharp
var dto = new XPacketHandshake { MagicHandshakeNumber = 99_999 };
byte[] raw = XPacketConverter.Serialize(dto).ToPacket();
```

### Deserialize

```csharp
XPacket packet = XPacket.Parse(raw);
var dto = XPacketConverter.Deserialize<XPacketHandshake>(packet);
Console.WriteLine(dto.MagicHandshakeNumber); // 99999
```

### Custom DTO example

```csharp
public class OrderRequest
{
    public int   OrderId;
    public float Price;
    public bool  IsActive;
}

// Register once at startup:
XPacketTypeManager.Register<OrderRequest>(XPacketType.GetOrderAllMethod, type: 2, subtype: 0);

// Send (client):
byte[] raw = XPacketConverter.Serialize(new OrderRequest
{
    OrderId  = 7,
    Price    = 199.99f,
    IsActive = true
}).ToPacket();
client.QueuePacketSend(raw);

// Receive (server):
var req = XPacketConverter.Deserialize<OrderRequest>(packet);
Console.WriteLine($"Order #{req.OrderId}, price={req.Price}");
```

---

## 5. Encryption

Encryption is implemented via `RijndaelHandler` (AES-256-CBC). The built-in `XProtocolEncryptor` uses a fixed internal key.

| Parameter | Value |
|---|---|
| Algorithm | AES-256 CBC |
| Key length | 256 bits |
| Key derivation | PBKDF2 / SHA-256 / 1000 iterations |
| Salt | 32 random bytes prepended to ciphertext |
| IV | 16 random bytes after salt |

```csharp
// Encrypt:
byte[] raw       = XPacketConverter.Serialize(dto).ToPacket();
byte[] encrypted = XProtocolEncryptor.Encrypt(raw);

// Decrypt:
byte[] decrypted = XProtocolEncryptor.Decrypt(encrypted);
XPacket packet   = XPacket.Parse(decrypted);
```

Encrypted packets carry the `0x95 0xAA 0xFF` header — `XPacket.Parse` detects this automatically and decrypts transparently.

---

## 6. End-to-end example: handshake

Full flow from TCPClient and TCPServer.

### Step 1. Client generates a magic number and sends Handshake

```csharp
// TCPClient/Program.cs
var rand = new Random();
_handshakeMagic = rand.Next();  // e.g. 582341

client.QueuePacketSend(
    XPacketConverter.Serialize(
        new XPacketHandshake { MagicHandshakeNumber = _handshakeMagic }
    ).ToPacket()
);
```

Wire bytes for magic=582341 (0x0008E285):

```
AF AA AF  01 00  01  04  85 E2 08 00  FF 00
─────────  ─────  ──  ──  ──────────  ─────
 header    T  ST  FC  FS  int 582341  EOF
```

### Step 2. Client receives incoming bytes

```csharp
// TCPClient/XClient.cs — RecievePackets()
var buff = new byte[256];
_socket.Receive(buff);

// Trim to the 0xFF 0x00 terminator
buff = buff.TakeWhile((b, i) =>
{
    if (b != 0xFF) return true;
    return buff[i + 1] != 0;
}).Concat(new byte[] { 0xFF, 0 }).ToArray();

OnPacketRecieve?.Invoke(buff);
```

### Step 3. Server receives Handshake, subtracts 15, replies

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

### Step 4. Client verifies the response

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

## 7. Adding a new packet type

### 1. Add a value to `XPacketType`

```csharp
// XProtocol/XPacketType.cs
public enum XPacketType
{
    Unknown           = 0,
    Handshake         = 1,
    GetOrderAllMethod = 2,
    GetPriceAllMethod = 3,
    SetPriceMethod    = 4,   // ← new
}
```

### 2. Create the DTO class

```csharp
public class SetPriceRequest
{
    public int   ProductId;
    public float NewPrice;
}
```

### 3. Register at startup

```csharp
XPacketTypeManager.Register<SetPriceRequest>(XPacketType.SetPriceMethod, type: 4, subtype: 0);
```

### 4. Handle in the server switch

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

### 5. Send from the client

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

## 8. Type reference

| Type | Description |
|---|---|
| `XPacket` | Packet container: header + list of `XPacketField` |
| `XPacketField` | Single binary field (up to 255 bytes) |
| `XPacketType` | Enum of logical packet types |
| `XPacketTypeManager` | Registry: `XPacketType` ↔ `(byte Type, byte Subtype)` ↔ C# class |
| `XPacketConverter` | Reflection-based serialize/deserialize helper |
| `XPacketHandshake` | Built-in handshake payload (`MagicHandshakeNumber`) |
| `RijndaelHandler` | AES-256-CBC encrypt/decrypt |
| `XProtocolEncryptor` | Wrapper over `RijndaelHandler` with a fixed built-in key |

---

## Target Framework

.NET 10


## Building the NuGet Package

```bash
dotnet pack -c Release

or

dotnet pack -c Release --include-symbols
```