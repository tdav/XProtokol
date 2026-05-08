# XPacketRpc.Tests

Unit and integration tests for the `XPacketRpc` runtime library — covering primitive encoding, variable-length types, collections, edge cases, and full end-to-end round-trips.

## Target Framework

- .NET 10

## Test Coverage

| Test Class / Folder | What is tested |
|---|---|
| `RoundtripTests` | Serialize + deserialize round-trips for common DTO types |
| `WireFormatTests` | Exact byte layout written to the wire |
| `WritersPrimitivesTests` | `Writers` helpers for primitive types (int, float, bool, …) |
| `WritersVariableTests` | `Writers` helpers for variable-length types (string, byte[]) |
| `XPRpcReaderPrimitivesTests` | `XPRpcReader` primitive reads |
| `XPRpcReaderVariableTests` | `XPRpcReader` variable-length reads |
| `XPRpcRegistryTests` | `XPRpc.Register` / `Write` / `Read` dispatch |
| `XPacketRpcSerializerTests` | `XPacketRpcSerializer` high-level API |
| `GeneratorSmokeTests` | Verifies source-generated serializers exist at runtime |
| `NullabilityTests` | Nullable reference and value-type handling |
| `ExceptionTests` | `MissingSerializerException`, `RpcSerializationException` |
| `Fnv1aTests` | FNV-1a hash correctness |
| `PooledBufferWriterTests` | `PooledBufferWriter` grow / advance / dispose |
| `Edge\NumericEdgeTests` | Boundary values (min/max int, NaN, infinity, …) |
| `Edge\StringEdgeTests` | Empty string, null, Unicode, large strings |
| `Edge\CollectionEdgeTests` | Empty arrays, null collections, nested lists |
| `E2E\SmokeRoundtripTests` | Full pipeline smoke test |

## Running

```pwsh
dotnet test XPacketRpc.Tests
```
