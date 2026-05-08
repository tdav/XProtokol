# XPacketRpc.Generators.Tests

Unit and snapshot tests for the `XPacketRpc.Generators` Roslyn source generator.

## Target Framework

- .NET 10

## Test Coverage

| Test Class | What is tested |
|---|---|
| `CallSiteCollectorTests` | Discovers `XPRpc.Write/Read/Touch<T>` call sites from syntax trees |
| `TypeWalkerTests` | Transitive closure of DTO member types |
| `CtorBinderTests` | Constructor selection and parameter binding |
| `WriteEmitterTests` | Generated `Write<T>` method bodies |
| `ReadEmitterTests` | Generated `Read<T>` method bodies |
| `RegistryEmitterTests` | Generated `[ModuleInitializer]` registration code |
| `IndentedStringBuilderTests` | Indented code builder utility |
| `DiagnosticTests` | XPRPC001–XPRPC004 diagnostics are emitted correctly |
| `GeneratorSnapshotTests` | Golden-file snapshot tests for full generator output |

## Running

```pwsh
dotnet test XPacketRpc.Generators.Tests
```
