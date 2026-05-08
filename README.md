# XProtokol

> Исходный код приложения для статьи на Xakep.ru — [«Своими руками: пишем свой протокол поверх TCP»](https://xakep.ru/2019/02/[...])

A lightweight TCP client-server framework for .NET 10 with a custom binary protocol, field-based packet serialization, and built-in AES-256 encryption.

## About

**XProtokol** — учебно-практический проект на **C# / .NET 10**, демонстрирующий реализацию **кастомного бинарного протокола поверх TCP**.

Ключевые цели репозитория:

- показать устройство пакетов (тип/подтип + набор полей)
- реализовать сериализацию/десериализацию DTO в поля пакета
- продемонстрировать шифрование полезной нагрузки (**AES-256-CBC**) и handshake
- предоставить примеры клиента и сервера на `TcpClient`/`TcpListener`

Если вы используете проект как основу для собственного протокола/транспорта — рекомендуется:

- вынести контрактные DTO в отдельный проект
- добавить версионирование протокола
- покрыть критические участки тестами и бенчмарками (они уже есть в репозитории)

## Projects

| Project | Type | Description |
|---------|------|-------------|
| [`XProtocol`](XProtocol/README.md) | Class Library | Core protocol: packet structure, serialization, encryption |
| [`XPacketRpc`](XPacketRpc/README.md) | Class Library | Zero-reflection source-generated RPC serialization runtime |
| [`XPacketRpc.Generators`](XPacketRpc.Generators/README.md) | Roslyn Generator | Compile-time serializer code generation |
| [`TCPServer`](TCPServer/README.md) | Console App | Example TCP server using `XProtocol` |
| [`TCPClient`](TCPClient/README.md) | Console App | Example TCP client using `XProtocol` |
| [`Test`](Test/README.md) | Console App | Serialization + encryption round-trip smoke test |
| [`XProtocol.Tests`](XProtocol.Tests/README.md) | Test Project | Unit tests for XProtocol |
| [`XPacketRpc.Tests`](XPacketRpc.Tests/README.md) | Test Project | Unit tests for XPacketRpc |
| [`XPacketRpc.Generators.Tests`](XPacketRpc.Generators.Tests/README.md) | Test Project | Unit tests for the source generator |
| [`XPacketRpc.Benchmarks`](XPacketRpc.Benchmarks/README.md) | Benchmark | Performance comparison vs MemoryPack, MessagePack, protobuf-net, STJ |

## Features

- **Custom binary protocol** — packets with type, subtype, and typed fields
- **Field-based serialization** — serialize/deserialize any struct into packet fields using `[XField]` attribute
- **AES-256 encryption** — optional per-packet encryption via `RijndaelHandler` (CBC mode, PBKDF2 key derivation)
- **Handshake support** — built-in handshake packet type for connection initialization
- **Extensible packet types** — add new types via `XPacketType` enum and `XPacketTypeManager`

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Build

```bash
dotnet build
```

### Run

Start the server:

```bash
cd TCPServer
dotnet run
```

Start the client (in a separate terminal):

```bash
cd TCPClient
dotnet run
```

The client connects to `127.0.0.1:4910`, performs a handshake, and exchanges packets with the server.

## Protocol Overview

### Packet Structure

Each `XPacket` contains:
- `PacketType` (byte) — identifies the packet category
- `PacketSubtype` (byte) — sub-category within the type
- `Fields` — list of `XPacketField` entries (ID + raw bytes)
- `Protected` (bool) — whether the packet payload is AES-encrypted

### Serialization

Use `XPacketConverter` to serialize/deserialize packets:

```csharp
// Register the DTO type once at startup
XPacketTypeManager.Register<XPacketHandshake>(XPacketType.Handshake, type: 1, subtype: 0);

// Serialize an object into a packet
var packet = XPacketConverter.Serialize(new XPacketHandshake
{
    MagicHandshakeNumber = 12345
}).ToPacket();

// Deserialize a packet back to an object
var handshake = XPacketConverter.Deserialize<XPacketHandshake>(XPacket.Parse(packet));
```

DTO fields are discovered automatically via reflection (all public instance fields):

```csharp
public class XPacketHandshake
{
    public int MagicHandshakeNumber;
}
```

### Encryption

Packets can be encrypted using `XProtocolEncryptor` (built-in key) or `RijndaelHandler` (custom passphrase):

```csharp
// Using the built-in XProtocolEncryptor (fixed internal key)
byte[] encrypted = XProtocolEncryptor.Encrypt(rawPacketBytes);
byte[] decrypted = XProtocolEncryptor.Decrypt(encrypted);

// Or use RijndaelHandler directly with a custom passphrase
byte[] encrypted2 = RijndaelHandler.Encrypt(data, "my-passphrase");
byte[] decrypted2 = RijndaelHandler.Decrypt(encrypted2, "my-passphrase");
```

- Algorithm: AES-256-CBC  
- Key derivation: PBKDF2 / SHA-256 / 1000 iterations  
- Random salt (32 bytes) and IV (16 bytes) prepended to each ciphertext

### Adding a New Packet Type

1. Add a value to `XPacketType`:
   ```csharp
   public enum XPacketType { Unknown, Handshake, MyNewType }
   ```
2. Register it in `XPacketTypeManager`.
3. Create a class for the payload with public instance fields.
4. Handle the new type in the client/server `switch` statement.

## Project Structure

```
XProtokol/
├── XProtocol/
│   ├── XPacket.cs               # Core packet class (parse/build)
│   ├── XPacketField.cs          # Single field (ID + bytes)
│   ├── XPacketType.cs           # Packet type enum
│   ├── XPacketTypeManager.cs    # Type registration & lookup
│   ├── XPacketHandshake.cs      # Handshake payload class
│   ├── XProtocolEncryptor.cs    # Encryption integration
│   ├── RijndaelHandler.cs       # AES-256 encrypt/decrypt
│   └── Serializator/
│       ├── XPacketConverter.cs  # Serialize/deserialize helpers
│       └── FieldDescriptor.cs   # Reflection-based field accessor
├── TCPServer/
│   ├── Program.cs
│   ├── XServer.cs               # TCP listener & client management
│   └── ConnectedClient.cs       # Per-client state & packet handling
├── TCPClient/
│   ├── Program.cs
│   └── XClient.cs               # TCP client wrapper
└── Test/                        # Unit tests
```

## License

MIT
