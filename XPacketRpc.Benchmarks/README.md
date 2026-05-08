# XPacketRpc.Benchmarks

BenchmarkDotNet benchmarks comparing `XPacketRpc` serialization performance against popular .NET serialization libraries.

## Target Framework

- .NET 10

## Compared Libraries

| Library | Notes |
|---|---|
| **XPacketRpc** | This project's source-generated binary serializer |
| **MemoryPack** | Zero-encoding binary serializer |
| **MessagePack** | MessagePack binary format (ContractlessStandardResolver) |
| **protobuf-net** | Protocol Buffers |
| **System.Text.Json** | Built-in JSON serializer |

## Benchmarks

| Benchmark | Description |
|---|---|
| `Vector3SerializeBenchmarks` | Serialize a `Vector3` (3 floats) — throughput + allocation |
| `Vector3DeserializeBenchmarks` | Deserialize a `Vector3` from pre-encoded bytes |
| `WireSizeReport` | Prints the encoded byte size for each serializer |

## Running

```pwsh
dotnet run --project XPacketRpc.Benchmarks -c Release
```

> **Note:** Always run benchmarks in **Release** mode. Debug builds produce unreliable results.

### Run a specific benchmark

```pwsh

# Wire-size report
dotnet run -c Release --project XPacketRpc.Benchmarks -- --wire-size

# Все бенчмарки
dotnet run -c Release --project XPacketRpc.Benchmarks -- --filter *

# Только Serialize бенчмарки
dotnet run -c Release --project XPacketRpc.Benchmarks -- --filter *Serialize*

dotnet run --project XPacketRpc.Benchmarks -c Release -- --filter *Vector3*
```

## Sample Results

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i7-13700 2.10GHz, 1 CPU, 24 logical and 16 physical cores
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3
  Job-ZVGFQP : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3

Concurrent=True  Server=True  IterationCount=10  
WarmupCount=3  

| Method                  | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------ |----------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| XPacketRpc              |  29.99 ns |  1.979 ns |  1.178 ns |  1.00 |    0.05 | 0.0014 |      80 B |        1.00 |
| MessagePackContractless |  47.88 ns |  4.408 ns |  2.915 ns |  1.60 |    0.11 | 0.0008 |      48 B |        0.60 |
| MemoryPack              |  16.37 ns |  1.237 ns |  0.736 ns |  0.55 |    0.03 | 0.0007 |      40 B |        0.50 |
| SystemTextJson          | 259.62 ns | 20.594 ns | 12.255 ns |  8.67 |    0.51 | 0.0007 |      48 B |        0.60 |
| ProtobufNet             | 134.60 ns |  6.559 ns |  3.903 ns |  4.49 |    0.21 | 0.0067 |     384 B |        4.80 |