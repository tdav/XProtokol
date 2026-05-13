# XProtocol Collections / Nested DTO Support — Design

**Date:** 2026-05-13
**Branch target:** `feature/xprotocol-collections-support`
**Builds on:** `feature/xprotocol-string-support` (merged into master as commit `d69017c`)

---

## 1. Goal

Extend XProtocol serializer to accept the following CLR field types on registered DTOs:

- `T[]` — single-dimension arrays of any supported element shape
- `List<T>` — list of any supported element shape
- `Dictionary<K, V>` — `K` is a value-type or `string`; `V` is any supported shape
- Custom classes (nested DTOs) — recursively serialized as inline fields

Recursion is **unbounded**: collections may contain collections, collections may contain nested DTOs, nested DTOs may contain collections, etc.

The wire format remains chunked through the existing `XPacket.AppendChunks` mechanism. The public API (`XPacketTypeManager.Register<T>`, `XPacketConverter.Serialize<T>`, `XPacketConverter.Deserialize<T>`) keeps its current signatures. Magic bytes, terminator, and the 255-byte / 255-wire-field caps remain unchanged.

DTOs that use **only** value-types and/or `string` produce byte-identical packets to the previous implementation.

---

## 2. Locked-in design decisions

| # | Question | Decision |
|---|----------|----------|
| 1 | Recursion depth within collections | Unbounded — collection in collection, collection in DTO, DTO in collection, DTO in DTO. Any combination. |
| 2 | Container types | `T[]`, `List<T>`, `Dictionary<K,V>` only. `HashSet`, `Queue`, `IList<>`, `IEnumerable<>`, etc. → reject. |
| 3 | Null normalisation | `null` collection → empty collection on the wire; `null` nested DTO → fresh `new T()` (all fields default). Symmetric to existing `null` string → `""` rule. |
| 4 | Nested DTO registration | Auto-built when parent is registered. No standalone `Register<>` required for nested-only DTOs. Cycle in type graph → `InvalidOperationException` at `Register` time. |
| 5 | Dictionary key shape | `K` ∈ {value-type, `string`}. Any other shape (nested DTO as key, collection as key) → reject at `Resolve` time. |
| 6 | Implementation approach | Recursive `FieldShape` tree + single `ShapeCodec`. (Approach A from brainstorming.) |

---

## 3. Architecture

### 3.1 Component overview

```
XPacketTypeManager.Register<T>
        │
        ▼
ShapeResolver.Resolve(typeof(T), visiting)         ── builds FieldShape tree, detects cycles
        │
        ▼
FieldDescriptor[] (cached per top-level T)
        │
        ▼
┌─────────────────────────────────────┐
│  XPacketConverter.Serialize<T>       │
│    for each descriptor:              │
│      bytes = ShapeCodec.WriteField   │
│      packet.AppendChunks(bytes)      │
│                                      │
│  XPacketConverter.Deserialize<T>     │
│    reader = ChunkReader(packet, 0)   │
│    for each descriptor:              │
│      value = ShapeCodec.ReadField    │
│      descriptor.Setter(instance, v)  │
└─────────────────────────────────────┘
```

### 3.2 Public API surface

Unchanged. No new public types or methods.

### 3.3 Internal types introduced

- `FieldShape` (abstract) + sealed subtypes `ValueShape`, `StringShape`, `ArrayShape`, `ListShape`, `DictShape`, `NestedShape`
- `ShapeResolver` (static) — `Resolve(Type, HashSet<Type>) → FieldShape`
- `ShapeCodec` (static) — `WriteField(FieldShape, object) → byte[]` and `ReadField(FieldShape, ChunkReader) → object`
- `ChunkReader` — reads bytes across consecutive wire-fields of an `XPacket`, advances `WireIdx`

### 3.4 Refactor of existing types

- `FieldDescriptor`: drop `FieldKind`, drop `StringGetter`/`StringSetter`. Keep `Field`, replace with `Shape` (the resolved tree), unify accessors as `Getter: object → object` / `Setter: object, object → void`. Kind-specific behaviour moves to `ShapeCodec`.
- `XPacketConverter.Serialize/Deserialize`: collapse the two-branch dispatch (ValueType vs String) into a single uniform loop driven by `ShapeCodec`.
- `XPacketTypeManager.BuildDescriptors`: delegate `FieldDescriptor` construction to `ShapeResolver` (which handles cycle detection and nested DTO auto-build).
- Extract `FixedObjectToByteArray` and `ByteArrayToFixedObject` from `XPacket` (currently private) into a new internal static class `XProtocol.Serializator.MarshalHelpers` so that both `XPacket.AppendValue` (public, retained for external consumers) and `ShapeCodec` can call them. No behavioural change.

---

## 4. Shape model

```csharp
internal abstract class FieldShape { }

internal sealed class ValueShape : FieldShape
{
    public Type ClrType { get; }    // e.g. typeof(int), typeof(Guid), typeof(DateTime)
}

internal sealed class StringShape : FieldShape { }

internal sealed class ArrayShape : FieldShape
{
    public Type ElementClrType { get; }
    public FieldShape Element { get; }
}

internal sealed class ListShape : FieldShape
{
    public Type ElementClrType { get; }
    public FieldShape Element { get; }
}

internal sealed class DictShape : FieldShape
{
    public Type KeyClrType { get; }
    public Type ValueClrType { get; }
    public FieldShape Key { get; }
    public FieldShape Value { get; }
}

internal sealed class NestedShape : FieldShape
{
    public Type ClrType { get; }
    public FieldDescriptor[] Fields { get; }
}
```

### 4.1 FieldDescriptor (rewritten)

```csharp
internal sealed class FieldDescriptor
{
    public FieldInfo Field { get; }
    public FieldShape Shape { get; }
    public Func<object, object> Getter { get; }    // boxes value-types
    public Action<object, object> Setter { get; }  // unboxes / casts on assignment
}
```

The Getter/Setter are compiled from `Expression<>` (same approach as current code). Boxing for value-types is accepted (no regression vs current).

---

## 5. Wire format

All multi-byte fields are little-endian. Counts and lengths are `ushort` (0..65535).

A single logical descriptor produces a single contiguous payload byte sequence which is then split into 255-byte wire-fields via `XPacket.AppendChunks`. The byte stream uses no per-shape type tag — the shape is determined statically from the receiver-side descriptor.

### 5.1 Per-shape encoding

| Shape | Encoding |
|-------|----------|
| `ValueShape(t)` | `Marshal.SizeOf(t)` bytes, blittable struct layout. No prefix. |
| `StringShape` | `[ushort utf8ByteCount LE][utf8 bytes]`. Unchanged from string-support branch. |
| `ArrayShape(elem)` general case | `[ushort count LE]` followed by `count` × `elem` payloads, concatenated. |
| `ArrayShape(ValueShape(byte))` fast path | `[ushort count LE][count bytes]` — single `Buffer.BlockCopy`. |
| `ListShape(elem)` | `[ushort count LE]` followed by `count` × `elem` payloads. |
| `DictShape(k, v)` | `[ushort count LE]` followed by `count` × (`k` payload, `v` payload). Pairs are emitted in `IDictionary<K,V>` iteration order. |
| `NestedShape(t, fields)` | Concatenation of each `fields[i]` payload, in `MetadataToken` order. No prefix. |

### 5.2 Worked example

`class Foo { public List<string> Tags; }` with `Tags = new List<string> { "a", "bb" }`:

```
[02 00]          // List count = 2
[01 00] [61]     // "a": utf8 len = 1, bytes 'a'
[02 00] [62 62]  // "bb": utf8 len = 2, bytes 'b','b'
```

Payload = 9 bytes total → fits in one 255-byte wire-field.

### 5.3 Caps

- Total wire-fields per packet: ≤ 255 (existing limit).
- Per-collection element count: ≤ 65535 (`ushort`).
- Per-string UTF-8 byte length: ≤ 65535 (existing limit).
- Maximum total payload per packet: ≤ 255 × 255 = 65 025 bytes (existing limit).

For element types larger than one byte, the `ushort` element count limit is rarely the binding constraint; the wire-field cap is hit first.

### 5.4 Backward compatibility

A DTO whose every field has shape `ValueShape(t)` or `StringShape` produces the exact same byte sequence as the current implementation. Existing test fixtures (`StringDto`, `MultiStringDto`, `TestPacket` in `Test/Program.cs`) round-trip without change.

---

## 6. Shape resolver

```csharp
internal static class ShapeResolver
{
    public static FieldShape Resolve(Type t, HashSet<Type> visiting);
}
```

### 6.1 Dispatch rules

| CLR type | Result |
|----------|--------|
| Any value-type `t` (`int`, `long`, `double`, `bool`, `Guid`, `DateTime`, `decimal`, user struct, etc.) | `ValueShape(t)` — subject to runtime `Marshal.SizeOf` / `Marshal.StructureToPtr` success on first use |
| `string` | `StringShape` |
| `T[]` (single-dim) | `ArrayShape(Resolve(T))` |
| `List<T>` | `ListShape(Resolve(T))` |
| `Dictionary<K, V>` | `DictShape(Resolve(K), Resolve(V))`, gated by §6.2 |
| Any other class with public parameterless ctor (not `string`), with **at least one** public instance field (after hierarchy walk + `!IsLiteral` filter) | `NestedShape(t, BuildDescriptors(t))` — recurses through fields |
| Anything else | throw |

The "at least one field" requirement on nested classes guarantees every logical descriptor produces a positive-length payload — see §9.1.

`BuildDescriptors(t)` mirrors the existing logic in `XPacketTypeManager.BuildDescriptors`: walks `Instance | Public | DeclaredOnly` fields through the base-type chain, filters out `IsLiteral`, orders by `MetadataToken`, and wraps each `FieldInfo` in a `FieldDescriptor` whose `Shape` comes from a recursive `Resolve` call.

### 6.2 Rejected types

The following raise `InvalidOperationException` from `Resolve`:

- Interfaces, abstract classes, open generic types
- `HashSet<T>`, `Queue<T>`, `Stack<T>`, `LinkedList<T>`, `SortedDictionary`, `SortedList`, `ConcurrentDictionary`, `Immutable*`
- Any `IEnumerable<T>`/`IList<T>`/`IReadOnlyList<T>`/`IDictionary<K,V>` as a field type
- Multi-dim arrays (`int[,]`) — only jagged (`int[][]`) is supported (via recursive `ArrayShape`)
- `Tuple`/`ValueTuple` (not blittable in general) — throws via `Marshal.SizeOf` if attempted as ValueShape; resolver does not special-case them
- `Nullable<T>` — out of scope (no wire-format support for null sentinel)
- `Dictionary<K, V>` where `K` is not value-type and not `string`
- Class without a public parameterless constructor
- Class with **zero** public instance fields (after hierarchy walk + `!IsLiteral` filter) — empty payload not representable on wire
- `object`, `dynamic`, polymorphic base references

### 6.3 Cycle detection

Before constructing a `NestedShape`, `Resolve` checks whether `t ∈ visiting`. If yes, it throws with the offending chain. After constructing, it removes `t` from `visiting` so siblings in the type graph see a clean stack.

Examples that throw:

- `class A { public B b; } class B { public A a; }` — mutual reference
- `class Tree { public Tree Child; }` — self reference
- `class A { public B b; } class B { public C c; } class C { public A a; }` — 3-cycle

### 6.4 Concurrency

`ShapeResolver.Resolve` is invoked from inside `XPacketTypeManager.Register<T>`, which already holds `syncRoot`. The resolver itself does not need additional locking. Resolution mutates only its own `visiting` set (per-call); the `descriptorCache` mutation happens in the caller under the lock.

---

## 7. Codec

```csharp
internal static class ShapeCodec
{
    public static byte[] WriteField(FieldShape shape, object value);
    public static object ReadField(FieldShape shape, ChunkReader reader);
}
```

### 7.1 ChunkReader

```csharp
internal sealed class ChunkReader
{
    public ChunkReader(XPacket packet, int startWireIdx);
    public int WireIdx { get; }
    public int Available { get; }    // bytes remaining until end-of-packet
    public byte ReadByte();
    public ushort ReadUInt16LE();
    public void ReadBytes(byte[] dst, int offset, int count);
}
```

Internally tracks `(wireIdx, byteOffsetInChunk)`. When the current chunk is exhausted, advances to the next wire-field. Throws `InvalidOperationException("payload truncated")` if requested bytes exceed `Available`.

**Natural alignment.** `XPacket.AppendChunks` emits exactly `ceil(N/255)` wire-fields for an `N>0`-byte payload, with no padding. As the codec consumes those `N` bytes via `ChunkReader`, the reader naturally advances through wire-fields and lands at the boundary between wire-field groups (i.e. at the start of the next descriptor's first wire-field). No explicit alignment step is required, **provided every descriptor's codec payload is at least 1 byte**. §6.2's empty-nested rejection guarantees this property: every logical descriptor produces ≥ 1 byte, so descriptor↔wire-field-group alignment is automatic.

### 7.2 Write dispatch

`WriteField` allocates a `MemoryStream`, dispatches on shape, and returns `ToArray()`.

```
ValueShape(t):
    byte[] arr = FixedObjectToByteArray(value, t);  // Marshal.StructureToPtr path
    ms.Write(arr, 0, arr.Length)

StringShape:
    string s = (string)value ?? string.Empty;
    byte[] utf8 = Encoding.UTF8.GetBytes(s);
    if (utf8.Length > 65535) throw "...string exceeds 65535...";
    WriteUInt16LE(ms, utf8.Length);
    ms.Write(utf8, 0, utf8.Length);

ArrayShape(elem):
    Array arr = (Array)value ?? Array.CreateInstance(ElementClrType, 0);
    if (arr.Length > 65535) throw "...exceeds 65535 elements...";
    WriteUInt16LE(ms, arr.Length);
    if (elem is ValueShape(byte)):
        ms.Write((byte[])arr, 0, arr.Length);
    else:
        foreach (e in arr): WriteFieldInto(ms, elem, e);

ListShape(elem):
    IList list = (IList)value ?? Activator.CreateInstance(typeof(List<>).MakeGenericType(ElementClrType));
    if (list.Count > 65535) throw "...exceeds 65535 elements...";
    WriteUInt16LE(ms, list.Count);
    foreach (e in list): WriteFieldInto(ms, elem, e);

DictShape(k, v):
    IDictionary dict = (IDictionary)value ?? Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(KeyClrType, ValueClrType));
    if (dict.Count > 65535) throw "...exceeds 65535 elements...";
    WriteUInt16LE(ms, dict.Count);
    foreach (DictionaryEntry kv in dict):
        WriteFieldInto(ms, k, kv.Key);
        WriteFieldInto(ms, v, kv.Value);

NestedShape(t, fields):
    object instance = value ?? Activator.CreateInstance(t);
    foreach (desc in fields):
        WriteFieldInto(ms, desc.Shape, desc.Getter(instance));
```

`WriteFieldInto(ms, shape, value)` is an internal helper that performs the same dispatch as `WriteField` but writes directly into the supplied `MemoryStream` instead of allocating a new one. This keeps the recursive packing single-buffer.

### 7.3 Read dispatch

`ReadField` reads from `ChunkReader` and returns an object of the appropriate CLR type.

```
ValueShape(t):
    size = Marshal.SizeOf(t)
    var buf = new byte[size]
    reader.ReadBytes(buf, 0, size)
    return ByteArrayToFixedObject(buf, t)

StringShape:
    len = reader.ReadUInt16LE()
    var buf = new byte[len]
    reader.ReadBytes(buf, 0, len)
    return Encoding.UTF8.GetString(buf)

ArrayShape(elem):
    count = reader.ReadUInt16LE()
    var arr = Array.CreateInstance(ElementClrType, count)
    if (elem is ValueShape(byte)):
        reader.ReadBytes((byte[])arr, 0, count)
    else:
        for (i = 0..count-1): arr.SetValue(ReadField(elem, reader), i)
    return arr

ListShape(elem):
    count = reader.ReadUInt16LE()
    var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(ElementClrType), count)
    for (i = 0..count-1): list.Add(ReadField(elem, reader))
    return list

DictShape(k, v):
    count = reader.ReadUInt16LE()
    var dict = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(KeyClrType, ValueClrType))
    for (i = 0..count-1):
        var key = ReadField(k, reader)
        var val = ReadField(v, reader)
        dict.Add(key, val)
    return dict

NestedShape(t, fields):
    var instance = Activator.CreateInstance(t)
    foreach (desc in fields):
        desc.Setter(instance, ReadField(desc.Shape, reader))
    return instance
```

### 7.4 Converter loop

```csharp
public static XPacket Serialize<T>(T obj) where T : class
{
    if (obj == null) throw new ArgumentNullException(nameof(obj));
    var (btype, bsubtype) = XPacketTypeManager.GetBytesFor(typeof(T));
    var descriptors = XPacketTypeManager.GetDescriptors(typeof(T));
    var packet = XPacket.Create(btype, bsubtype);
    foreach (var desc in descriptors)
    {
        var bytes = ShapeCodec.WriteField(desc.Shape, desc.Getter(obj));
        // Resolver guarantees bytes.Length > 0 for every descriptor (see §6.2).
        packet.AppendChunks(bytes);
    }
    if (packet.Fields.Count > byte.MaxValue)
        throw new InvalidOperationException($"{typeof(T).Name}: packet exceeds 255 wire fields (actual: {packet.Fields.Count}).");
    return packet;
}

public static T Deserialize<T>(XPacket packet) where T : class, new()
{
    if (packet == null) throw new ArgumentNullException(nameof(packet));
    var descriptors = XPacketTypeManager.GetDescriptors(typeof(T));
    var instance = new T();
    var reader = new ChunkReader(packet, 0);
    foreach (var desc in descriptors)
    {
        var value = ShapeCodec.ReadField(desc.Shape, reader);
        desc.Setter(instance, value);
        // Reader is positioned at the start of the next wire-field by construction
        // — see "Natural alignment" note in §7.1.
    }
    if (reader.Available != 0)
        throw new InvalidOperationException(
            $"Field count mismatch for {typeof(T).Name}: trailing bytes after all descriptors consumed (remaining: {reader.Available}, wireIdx: {reader.WireIdx}, fields: {packet.Fields.Count}).");
    return instance;
}
```

The post-loop check uses `Available == 0` rather than `WireIdx == Fields.Count` because the reader lands at the start of a (non-existent) "next wire-field" exactly when all bytes have been consumed; `Available` is the most direct expression of "no garbage remains".

---

## 8. Error catalog

Exact substrings used by tests (`.Contains(...)` assertions). All errors raise `InvalidOperationException` unless noted.

| Site | Trigger | Message substring |
|------|---------|-------------------|
| `ShapeResolver.Resolve` | unsupported CLR type | `"is not supported"` |
| `ShapeResolver.Resolve` | `Dictionary<K,V>` with non-key-eligible `K` | `"Dictionary"` + `"key must be value-type or string"` |
| `ShapeResolver.Resolve` | nested class without public parameterless ctor | `"nested DTO must have public parameterless constructor"` |
| `ShapeResolver.Resolve` | nested class with zero serialisable fields | `"nested DTO must have at least one serialisable field"` |
| `ShapeResolver.Resolve` | cycle in type graph | `"Cycle detected in type graph"` |
| `ShapeCodec.Write` (`StringShape`) | UTF-8 length > 65535 | `"string exceeds 65535 UTF-8 bytes"` |
| `ShapeCodec.Write` (`ArrayShape`/`ListShape`/`DictShape`) | count > 65535 | `"exceeds 65535 elements"` |
| `ShapeCodec.Read` (any shape) | byte-stream exhausted before completion | `"payload truncated"` |
| `XPacketConverter.Serialize` | packet wire-fields > 255 | `"packet exceeds 255 wire fields"` |
| `XPacketConverter.Deserialize` | bytes remaining after all descriptors consumed | `"Field count mismatch"` + `"trailing bytes"` |
| `XPacketTypeManager.Register` (existing) | already registered | `"is already registered"` |
| `XPacketTypeManager.Get*` (existing) | unregistered type | `"is not registered"` |

The exact full message text may evolve during implementation; tests assert on stable substrings as listed above.

---

## 9. Edge cases

### 9.1 Empty payloads — rejected at Resolve

A descriptor whose value would serialise to zero bytes cannot be represented on the wire because `XPacket.AppendChunks` rejects an empty payload. The only way to produce a zero-byte payload from this codec is a `NestedShape(t, fields=[])` — an empty DTO class. `ShapeResolver` therefore rejects any class with zero public instance fields (after the standard hierarchy walk + `!IsLiteral` filter) at registration time.

Consequence: every descriptor produces at least 1 byte on the wire, and the descriptor↔wire-field-group alignment described in §7.1 holds without any filler or explicit alignment step.

If `Marshal.SizeOf(t) == 0` for some user value-type (theoretically possible with an empty struct), `Marshal.StructureToPtr` may raise at runtime; this is the user's responsibility — the resolver does not pre-validate marshallability.

### 9.2 Empty collection

`[ushort 0]` payload → 2 bytes → 1 wire-field. Read returns an empty array/list/dictionary of the correct CLR type.

### 9.3 Empty `byte[]`

`[00 00]` payload → 1 wire-field. Read returns `new byte[0]`.

### 9.4 Empty string inside collection

`[ushort 0]` payload of the inner string. Round-trips to empty string.

### 9.5 `null` element of collection / dictionary value

For element shape `StringShape` or `NestedShape`, null elements use the standard null normalisation (empty string / fresh `new T()`). Round-trip turns `null` into the normalised value.

### 9.6 Dictionary iteration order

`Dictionary<K,V>` iteration order is unspecified by the runtime contract. The on-wire byte sequence depends on iteration order. Round-trip equality must compare on `Count` and key-wise lookup, not on byte-sequence equality of two independently-serialised dictionaries with the same logical content.

### 9.7 Concurrent registration

Resolver runs inside `Register<>` under `XPacketTypeManager.syncRoot`. Cycle detection per-call uses a fresh `HashSet<Type>` so concurrent registrations of unrelated types do not interfere.

### 9.8 Nested DTO already registered as top-level

If both `Register<A>` and `Register<B>` happen (and `A` has a field of type `B`), the `NestedShape` for `B` inside `A`'s descriptor tree is independent of the cache entry for top-level `B`. Re-registering top-level `B` later (which would currently throw `"is already registered"` if the packet-type byte clashes) does not invalidate the `NestedShape` inside `A`. Snapshot semantics: shape trees are captured at parent registration time.

### 9.9 Wire-field cap on encryption

The encryption path (`EncryptPacket`) wraps the serialised packet in another packet via `AppendChunks` and checks the 255-wire-field cap explicitly (introduced in the string-support branch). No change required for this work.

---

## 10. Backward compatibility

| Aspect | Status |
|--------|--------|
| Magic bytes (`AF AA AF`, `95 AA FF`) | Unchanged |
| Terminator (`FF 00`) | Unchanged |
| 255-byte chunk size | Unchanged |
| 255-wire-field cap | Unchanged |
| Public API signatures | Unchanged |
| Wire bytes for value-type-only and string-only DTOs | Identical to current |
| `FieldDescriptor` (internal) | Rewritten; impacts `XProtocol.Tests` only (via InternalsVisibleTo) |
| `FieldKind` enum | Removed |

Previously-failing registrations (e.g. `class { List<int> X; }`) now succeed — that is the goal, not a regression.

---

## 11. Files affected

| File | Change |
|------|--------|
| `XProtocol/Serializator/FieldShape.cs` | new — abstract `FieldShape` + 6 sealed subtypes (ValueShape, StringShape, ArrayShape, ListShape, DictShape, NestedShape) |
| `XProtocol/Serializator/ShapeResolver.cs` | new — `Resolve(Type, HashSet<Type>)` |
| `XProtocol/Serializator/ShapeCodec.cs` | new — `WriteField` / `ReadField` |
| `XProtocol/Serializator/ChunkReader.cs` | new — wire-field-spanning byte reader |
| `XProtocol/Serializator/MarshalHelpers.cs` | new — internal static `ToBytes(object, Type)` / `FromBytes(byte[], Type)`, extracted from `XPacket` |
| `XProtocol/Serializator/FieldDescriptor.cs` | rewrite — drop `FieldKind`/`StringGetter`, add `Shape` |
| `XProtocol/Serializator/XPacketConverter.cs` | rewrite — uniform codec-driven loop |
| `XProtocol/XPacketTypeManager.cs` | small — delegate descriptor build to `ShapeResolver` |
| `XProtocol/XPacket.cs` | `FixedObjectToByteArray` / `ByteArrayToFixedObject` moved out to `MarshalHelpers`; `AppendValue` adjusted to call them; otherwise unchanged |
| `XProtocol.Tests/TestDtos.cs` | add new DTOs (arrays, lists, dicts, nested, recursion mixes) |
| `XProtocol.Tests/AssemblyFixture.cs` | register new top-level DTOs |
| `XProtocol.Tests/FieldDescriptorTests.cs` | adapt to new `Shape` API |
| `XProtocol.Tests/FieldShapeTests.cs` | new — resolver unit tests |
| `XProtocol.Tests/ShapeCodecTests.cs` | new — codec unit tests |
| `XProtocol.Tests/RoundtripArrayTests.cs` | new |
| `XProtocol.Tests/RoundtripListTests.cs` | new |
| `XProtocol.Tests/RoundtripDictTests.cs` | new |
| `XProtocol.Tests/RoundtripNestedTests.cs` | new |
| `XProtocol.Tests/RoundtripRecursionTests.cs` | new |
| `Test/Program.cs` | unchanged |

---

## 12. Testing strategy

Framework: TUnit + Microsoft.Testing.Platform (unchanged). `AssemblyFixture` registers all new top-level DTOs at packet-type/subtype slots distinct from the string-support fixture (e.g. 300/n series).

### 12.1 Positive coverage

For each new shape family (Array, List, Dict, Nested):

- empty
- 1 element
- N elements (small)
- > 1000 elements (forces chunk-boundary crossing)
- per-shape valid maximum at the relevant cap (count = 65535 where tractable; otherwise wire-field cap)
- through `Encrypt()` + `Parse()` end-to-end

For each element type inside collections:

- value-type primitives: `byte`, `int`, `long`, `double`, `bool`, `Guid`, `DateTime`, `decimal`
- `string` (including empty, ASCII, Cyrillic, emoji surrogate-pair)
- nested DTO (single level)

For Dictionary keys: each allowed `K` (every value-type primitive + `string`).

### 12.2 Nested DTO coverage

- single nested level: `A { B Inner; }`
- two nested levels: `A → B → C`
- nested with mix of all shapes: `class Mixed { int N; string S; int[] Arr; List<string> L; Dictionary<int, Mixed2> D; Mixed2 Inner; }`
- nested with one field (minimal)
- null nested field → round-trip yields fresh `new T()` with default-valued sub-fields

### 12.3 Recursion combinations

- `int[][]` — jagged 2D
- `List<int[]>`
- `List<List<string>>`
- `Dictionary<string, MyDto>` where `MyDto` contains `List<int>`
- `MyDto` containing `Dictionary<int, OtherDto[]>`

### 12.4 Negative coverage (`ShapeResolver`)

- `HashSet<int>` → `"is not supported"`
- `IEnumerable<int>` → `"is not supported"`
- `Queue<int>` → `"is not supported"`
- `int[,]` (multi-dim) → `"is not supported"`
- `Dictionary<MyDto, int>` → `"key must be value-type or string"`
- nested class with parameterised constructor only → `"public parameterless constructor"`
- mutual reference `A→B→A` → `"Cycle detected"`
- self-reference `Tree → Tree` → `"Cycle detected"`
- empty nested class `class Empty {}` as field → `"nested DTO must have at least one serialisable field"`
- top-level `Register<Empty>` → `"nested DTO must have at least one serialisable field"` (same Resolver path)

### 12.5 Negative coverage (`ShapeCodec`)

- `string` > 65535 UTF-8 bytes → `"string exceeds 65535"`
- collection with 70 000 elements → `"exceeds 65535 elements"`
- truncated packet on read → `"payload truncated"`

### 12.6 Negative coverage (`XPacketConverter`)

- DTO whose serialisation requires > 255 wire-fields → `"packet exceeds 255 wire fields"`
- unregistered type → `"is not registered"` (existing)

### 12.7 Concurrency

- parallel `Register<>` of unrelated types under load (smoke; existing lock already proven)
- parallel `Serialize`/`Deserialize` of one registered type (read-only fast path)

### 12.8 Round-trip equality helper

Tests compare round-tripped DTOs through a helper that recursively compares:

- value-types: `Equals`
- strings: `(a == b) || (a == null && b == "")` or vice versa
- arrays/lists: same length + element-wise compare
- dictionaries: same `Count` + key-wise lookup compare (order-independent)
- nested DTOs: recursive field-by-field

### 12.9 Existing tests preserved

All 68 tests from the string-support branch continue to pass after the `FieldDescriptor` refactor. `Test/Program.cs` smoke remains unchanged.

---

## 13. Out of scope

The following are explicitly deferred:

- `HashSet<T>`, `Queue<T>`, `Stack<T>`, `LinkedList<T>`, `Sorted*`, `Concurrent*`, `Immutable*`
- `IEnumerable<T>`/`IList<T>`/`IReadOnlyList<T>`/`IDictionary<K,V>` as field types
- Multi-dimensional arrays (`int[,]`)
- `Tuple`/`ValueTuple`, `KeyValuePair<K,V>` as field types
- `object`, `dynamic`, polymorphic base references
- `Nullable<T>` (`int?`, `Guid?`)
- Properties (only public instance fields)
- Custom serialiser attributes / opt-out attributes
- Schema versioning / field reordering tolerance
- Polymorphic nested types
- Reference deduplication / DAG encoding
- Performance optimisation (Span<byte>, ArrayPool, IL emit)
- Async API
- Protocol version header

---

## 14. Branch and merge strategy

- New branch: `feature/xprotocol-collections-support` from `master`
- Implementation: subagent-driven development (one task per implementer subagent, two-stage review)
- Final integration: local merge into `master` (no push, no PR), matching the string-support branch workflow

---
