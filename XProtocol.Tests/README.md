# XProtocol.Tests

Unit tests for the `XProtocol` library covering packet construction, serialization, encryption, type registration, and round-trip correctness.

## Target Framework

- .NET 10

## Test Coverage

| Test Class | What is tested |
|---|---|
| `XPacketTests` | `XPacket.Create`, field appending, binary `ToPacket` / `Parse` |
| `XPacketConverterTests` | `XPacketConverter.Serialize` / `Deserialize` for various DTO shapes |
| `RijndaelHandlerTests` | AES encrypt / decrypt correctness and error cases |
| `RoundtripTests` | Full serialize → encrypt → decrypt → deserialize round-trips |
| `RegistrationTests` | `XPacketTypeManager.Register` happy-path and duplicate handling |
| `UnregisteredTypeTests` | Exceptions thrown for unregistered types |
| `StrictCountTests` | Field count mismatches during deserialization |
| `XPacketTypeManagerTests` | `GetType`, `GetTypeFromPacket`, `GetBytesFor` |

## Running

```pwsh
dotnet test XProtocol.Tests
```
