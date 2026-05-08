# XPacketRpc

A fast, source-generated binary RPC serialization library built on top of `XProtocol`. Serializers are emitted at compile time — zero reflection at runtime.

## Key Design Principle: Zero Model Pollution

> **Your existing models require no changes.**

Unlike most serialization libraries, XPacketRpc places **zero requirements on your data types**:

- No attributes (`[MessagePackObject]`, `[ProtoContract]`, `[DataContract]`, etc.)
- No base classes or interface implementations
- No marker interfaces
- No constructor conventions beyond what your type already has
- No code changes to existing domain models, DTOs, or value objects

Plain C# records, classes, and structs work out of the box:

```csharp
// Nothing to add — this already works as-is
public record PlayerInfo(int Id, string Name, float Health);

public class OrderDto
{
    public Guid Id { get; init; }
    public string CustomerName { get; init; } = "";
    public List<OrderItem> Items { get; init; } = [];
    public decimal Total { get; init; }
}

public struct Vector3
{
    public float X, Y, Z;
}
```

The source generator discovers types entirely from call-site analysis — it reads your `XPRpc.Touch<T>()` and `XPRpc.Read<T>()` / `XPRpc.Write<T>()` usages at build time and emits all serializers automatically. Your model assembly never takes a compile-time or runtime dependency on XPacketRpc.

---

## Features

- **Zero model changes** — no attributes, no base classes, no marker interfaces
- **Source-generated serializers** — `XPacketRpc.Generators` emits `Write` / `Read` delegates at build time
- **Zero runtime reflection** — all code paths are statically resolved
- **Pooled buffer writes** — `ArrayPool<byte>` minimizes allocations on the write path
- **Pluggable serializer interface** — `IRpcSerializer` / `XPacketRpcSerializer` fit any transport
- **Compact binary format** — little-endian primitives, varint-encoded lengths, UTF-8 strings
- **Rich primitive support** — see [Supported Types](#supported-types)

## Target Framework

- .NET 10

## Dependencies

- `XPacketRpc.Generators` (source generator, build-time only — not shipped with the runtime package)

---

## Quick Start

### 1. Define your model (no changes needed)

```csharp
// Existing record — nothing added
public record PlayerInfo(int Id, string Name, float Health);
```

### 2. Register the type at startup

Call `XPRpc.Touch<T>()` once per type at startup. This is a no-op at runtime — it exists solely so the source generator can see the closed generic and emit code:

```csharp
// Program.cs / startup
XPRpc.Touch<PlayerInfo>();
```

For types you only use via `XPRpc.Read<T>()` / `XPRpc.Write<T>()` with explicit type arguments, `Touch` is optional — the generator picks those up directly.

### 3. Serialize

```csharp
using var buffer = new System.Buffers.ArrayBufferWriter<byte>();
XPRpc.Write(new PlayerInfo(1, "Alice", 100f), buffer);
byte[] bytes = buffer.WrittenSpan.ToArray();
```

### 4. Deserialize

```csharp
PlayerInfo? player = XPRpc.Read<PlayerInfo>(bytes.AsSpan());
```

### 5. High-level serializer (recommended for transport integration)

```csharp
var serializer = new XPacketRpcSerializer();

byte[] payload = serializer.Serialize(player);
PlayerInfo? result = serializer.Deserialize<PlayerInfo>(payload);
```

`XPacketRpcSerializer` uses a pooled `ArrayPool<byte>` buffer internally and returns a materialized `byte[]`. Content-type is `application/x-xprotocol-rpc`.

---

## Supported Types

The source generator handles the following types natively, including when nested inside DTOs:

| Category | Types |
|---|---|
| Integers | `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong` |
| Floats | `float`, `double`, `decimal` |
| Text | `string` (UTF-8, varint-prefixed length) |
| Boolean | `bool` |
| Date/Time | `DateTime`, `DateTimeOffset`, `TimeSpan` |
| Identity | `Guid` (16 bytes, little-endian) |
| Binary | `byte[]` (varint-prefixed length) |
| Collections | `List<T>`, `T[]`, `Dictionary<K,V>` (varint-prefixed count) |
| Nested types | Any supported DTO as a field/property — recursively handled |

Nullable value types (`int?`, `Guid?`, etc.) are supported when the generator's transitive closure analysis reaches them.

---

## How It Works

```
Build time:
  Source → XPacketRpc.Generators → emits module-initializers
           (one per type, registers Write/Read delegates via XPRpc.Register<T>)

Runtime startup:
  Module initializers run → Cache<T>.Writer / Cache<T>.Reader populated

Hot path (Write):
  XPRpc.Write<T> → Cache<T>.Writer (direct field load, no dict lookup) → delegate

Hot path (Read):
  XPRpc.Read<T> → Cache<T>.Reader → XPRpcReader (ref struct, stack-only)
```

`Cache<T>` is a private generic class — the JIT specializes it per closed type, so the hot path is a single static field read with no boxing, no dictionary lookup, and no casting.

`XPRpcReader` is a `ref struct` that wraps a `ReadOnlySpan<byte>` — it is stack-allocated and never escapes to the heap.

---

## Core Types

| Type | Role |
|---|---|
| `XPRpc` | Static façade — `Register`, `Write`, `Read`, `Touch` |
| `XPRpcReader` | Low-level `ref struct` reader over `ReadOnlySpan<byte>` |
| `XPRpcReaderHelpers` | Static helpers for reading `List<T>`, `T[]`, `Dictionary<K,V>` |
| `IRpcSerializer` | Serializer abstraction — content-type + serialize/deserialize |
| `XPacketRpcSerializer` | Concrete implementation; content-type `application/x-xprotocol-rpc` |
| `MissingSerializerException` | Thrown when no serializer is registered for a type |
| `RpcSerializationException` | Thrown on malformed payload (truncated bytes, overlong varint, etc.) |

---

## Diagnostics (emitted by the generator)

| Code | Severity | Meaning | Fix |
|---|---|---|---|
| XPRPC001 | Warning | Open-generic call site detected | Add `XPRpc.Touch<ConcreteType>()` |
| XPRPC002 | Error | Open-generic type in transitive DTO closure | Close the generic before using it |
| XPRPC003 | Error | Type has no suitable constructor | Add a public constructor or use a record |
| XPRPC004 | Error | Field/property type is not supported | Use a supported primitive or a nested DTO |

When `MissingSerializerException` is thrown at runtime, the message includes the exact type name and the `Touch<T>()` call needed to fix it.

---

## BenchmarkDotNet Results

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i7-13700 2.10GHz, 1 CPU, 24 logical and 16 physical cores
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3
  Job-ZVGFQP : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3

Concurrent=True  Server=True  IterationCount=10  WarmupCount=3

| Method                  | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------ |----------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| XPacketRpc              |  29.99 ns |  1.979 ns |  1.178 ns |  1.00 |    0.05 | 0.0014 |      80 B |        1.00 |
| MessagePackContractless |  47.88 ns |  4.408 ns |  2.915 ns |  1.60 |    0.11 | 0.0008 |      48 B |        0.60 |
| MemoryPack              |  16.37 ns |  1.237 ns |  0.736 ns |  0.55 |    0.03 | 0.0007 |      40 B |        0.50 |
| SystemTextJson          | 259.62 ns | 20.594 ns | 12.255 ns |  8.67 |    0.51 | 0.0007 |      48 B |        0.60 |
| ProtobufNet             | 134.60 ns |  6.559 ns |  3.903 ns |  4.49 |    0.21 | 0.0067 |     384 B |        4.80 |

XPacketRpc is ~1.6× faster than MessagePack (contractless) and ~8.7× faster than System.Text.Json. MemoryPack remains faster due to its unsafe memory-copy approach — XPacketRpc trades ~2× allocation savings for portable, endian-safe encoding.


## Building the NuGet Package

```bash
dotnet pack -c Release

or

dotnet pack -c Release --include-symbols
```