# Test

A console application that exercises the `XProtocol` serialization and encryption round-trip end-to-end.

## Target Framework

- .NET 10

## Dependencies

- `XProtocol` (project reference)

## Running

```pwsh
dotnet run --project Test
```

## What It Does

1. Registers `TestPacket` with `XPacketTypeManager`.
2. Creates a `TestPacket` with sample values (`int`, `double`, `bool`).
3. Serializes → encrypts → decrypts → deserializes the packet.
4. Prints the round-tripped values to the console for verification.

```
TestNumber=12345, TestDouble=3.14, TestBoolean=True
```
