# XProtocol Collections / Nested DTO Support Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend the XProtocol serializer to accept `T[]`, `List<T>`, `Dictionary<K,V>`, and custom (nested) DTO fields, with unbounded recursion, while keeping the wire format byte-identical for DTOs that use only value-types and strings.

**Architecture:** Replace the existing `FieldKind`-based dispatch with a recursive `FieldShape` tree (Value/String/Array/List/Dict/Nested). A `ShapeResolver` walks the field type graph at registration time (detecting cycles and rejecting empty nested classes). A `ShapeCodec` writes each logical field's payload into a single byte buffer that `XPacket.AppendChunks` splits into 255-byte wire fields. Deserialisation uses a `ChunkReader` that reads bytes contiguously across wire-fields, with natural alignment guaranteed by every descriptor producing at least one byte of payload.

**Tech Stack:** .NET 10 / C#; TUnit + Microsoft.Testing.Platform for tests; `Marshal` for blittable value-type serialisation; `System.Linq.Expressions` for compiled getters/setters; `System.Text.Encoding.UTF8` for strings.

**Spec:** [docs/superpowers/specs/2026-05-13-xprotocol-collections-support-design.md](../specs/2026-05-13-xprotocol-collections-support-design.md)

---

## File Structure

| File | Disposition | Responsibility |
|------|-------------|----------------|
| `XProtocol/Serializator/FieldShape.cs` | new | Abstract `FieldShape` + 6 sealed subtypes (`ValueShape`, `StringShape`, `ArrayShape`, `ListShape`, `DictShape`, `NestedShape`) — the shape model |
| `XProtocol/Serializator/MarshalHelpers.cs` | new | `ToBytes(object, Type)` / `FromBytes(byte[], Type)` — extracted from `XPacket` |
| `XProtocol/Serializator/ChunkReader.cs` | new | Reads bytes spanning consecutive wire-fields of an `XPacket` |
| `XProtocol/Serializator/ShapeResolver.cs` | new | `Resolve(Type, HashSet<Type>) → FieldShape`; auto-builds nested descriptors; detects cycles and empty classes |
| `XProtocol/Serializator/ShapeCodec.cs` | new | `WriteField(FieldShape, object) → byte[]` and `ReadField(FieldShape, ChunkReader) → object` |
| `XProtocol/Serializator/FieldDescriptor.cs` | rewrite | Drop `FieldKind`/`StringGetter`/`StringSetter`; add `Shape`; one unified Getter/Setter |
| `XProtocol/Serializator/XPacketConverter.cs` | rewrite | Uniform codec-driven loop |
| `XProtocol/XPacketTypeManager.cs` | modify | `BuildDescriptors` delegates to `ShapeResolver` |
| `XProtocol/XPacket.cs` | modify | Move `FixedObjectToByteArray` / `ByteArrayToFixedObject` to `MarshalHelpers`; `AppendValue` delegates |
| `XProtocol.Tests/TestDtos.cs` | modify | Add new DTOs for arrays, lists, dicts, nested, recursion mixes; convert `EmptyDto` registration to negative-test fixture |
| `XProtocol.Tests/RoundtripTests.cs` | modify | Convert `EmptyDto_RoundtripProducesZeroFields` test to assert registration throws |
| `XProtocol.Tests/FieldDescriptorTests.cs` | rewrite | Adapt to new `Shape` API |
| `XProtocol.Tests/MarshalHelpersTests.cs` | new | Unit tests for extracted helpers |
| `XProtocol.Tests/ChunkReaderTests.cs` | new | Unit tests for ChunkReader |
| `XProtocol.Tests/FieldShapeResolverTests.cs` | new | Resolver unit tests (positive + negative) |
| `XProtocol.Tests/ShapeCodecTests.cs` | new | Codec byte-level unit tests |
| `XProtocol.Tests/RoundtripArrayTests.cs` | new | Roundtrip tests for arrays |
| `XProtocol.Tests/RoundtripListTests.cs` | new | Roundtrip tests for lists |
| `XProtocol.Tests/RoundtripDictTests.cs` | new | Roundtrip tests for dictionaries |
| `XProtocol.Tests/RoundtripNestedTests.cs` | new | Roundtrip tests for nested DTOs |
| `XProtocol.Tests/RoundtripRecursionTests.cs` | new | Combination tests (List<List<int>>, MyDto[][], etc.) |

---

## Conventions

### Building

`dotnet build XProtocolSol.sln -c Debug`

Expected: `Build succeeded`. Treat any warning that is also an error as a failure.

### Running tests

Full suite:

```
dotnet run --project XProtocol.Tests -c Debug
```

Single test class:

```
dotnet run --project XProtocol.Tests -c Debug -- --treenode-filter "/*/XProtocol.Tests/XProtocol.Tests/<ClassName>"
```

Single test method:

```
dotnet run --project XProtocol.Tests -c Debug -- --treenode-filter "/*/XProtocol.Tests/XProtocol.Tests/<ClassName>/<MethodName>"
```

TUnit returns non-zero exit on failure. Tests run in parallel by default.

### Commit message style

```
<scope>: <imperative short description>

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

Examples: `serializer: extract Marshal helpers`, `tests: cover ChunkReader edge cases`.

### Code style (per CLAUDE.md)

- No `_` prefix on private fields. Use `private readonly IFooService fooService;` and access through `this.fooService`.
- Identifiers in English, commit messages in English, comments in English.

---

## Task 0: Create feature branch

**Files:** none changed by this task.

- [ ] **Step 0.1: Verify clean master**

```
git status
```

Expected output includes `On branch master` and **only** the expected pre-existing untracked files (`docs/superpowers/specs/2026-05-13-xprotocol-collections-support-design.md` should already be committed). No tracked modifications.

- [ ] **Step 0.2: Run baseline test suite on master**

```
dotnet build XProtocolSol.sln -c Debug
dotnet run --project XProtocol.Tests -c Debug
```

Expected: build succeeds, all 68 tests pass.

- [ ] **Step 0.3: Create and switch to feature branch**

```
git checkout -b feature/xprotocol-collections-support
```

Expected: `Switched to a new branch 'feature/xprotocol-collections-support'`.

- [ ] **Step 0.4: Verify branch**

```
git branch --show-current
```

Expected: `feature/xprotocol-collections-support`.

---

## Task 1: Extract Marshal helpers

Pure refactor. Move `FixedObjectToByteArray` and `ByteArrayToFixedObject` out of `XPacket.cs` into a new `MarshalHelpers.cs`. `XPacket.AppendValue` and `GetValueAt<T>` keep working through delegation. All existing tests must still pass after this task.

**Files:**
- Create: `XProtocol/Serializator/MarshalHelpers.cs`
- Modify: `XProtocol/XPacket.cs`
- Create: `XProtocol.Tests/MarshalHelpersTests.cs`

- [ ] **Step 1.1: Write failing test for MarshalHelpers**

Create `XProtocol.Tests/MarshalHelpersTests.cs`:

```csharp
using System;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    public class MarshalHelpersTests
    {
        [Test]
        public async Task ToBytes_Int32_ProducesFourBytesLE()
        {
            var bytes = MarshalHelpers.ToBytes(0x01020304, typeof(int));

            await Assert.That(bytes.Length).IsEqualTo(4);
            await Assert.That(bytes[0]).IsEqualTo((byte)0x04);
            await Assert.That(bytes[1]).IsEqualTo((byte)0x03);
            await Assert.That(bytes[2]).IsEqualTo((byte)0x02);
            await Assert.That(bytes[3]).IsEqualTo((byte)0x01);
        }

        [Test]
        public async Task FromBytes_Int32_Roundtrips()
        {
            var bytes = MarshalHelpers.ToBytes(42, typeof(int));
            var back = (int)MarshalHelpers.FromBytes(bytes, typeof(int));

            await Assert.That(back).IsEqualTo(42);
        }

        [Test]
        public async Task ToBytes_Guid_Roundtrips()
        {
            var g = Guid.NewGuid();
            var bytes = MarshalHelpers.ToBytes(g, typeof(Guid));
            var back = (Guid)MarshalHelpers.FromBytes(bytes, typeof(Guid));

            await Assert.That(back).IsEqualTo(g);
        }
    }
}
```

- [ ] **Step 1.2: Run test, expect compile error**

```
dotnet build XProtocolSol.sln -c Debug
```

Expected: `error CS0103: The name 'MarshalHelpers' does not exist`.

- [ ] **Step 1.3: Create MarshalHelpers.cs**

Create `XProtocol/Serializator/MarshalHelpers.cs`:

```csharp
using System;
using System.Runtime.InteropServices;

namespace XProtocol.Serializator
{
    internal static class MarshalHelpers
    {
        public static byte[] ToBytes(object value, Type t)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            var size = Marshal.SizeOf(t);
            var arr = new byte[size];
            var ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(value, ptr, true);
                Marshal.Copy(ptr, arr, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            return arr;
        }

        public static object FromBytes(byte[] bytes, Type t)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                return Marshal.PtrToStructure(handle.AddrOfPinnedObject(), t);
            }
            finally
            {
                handle.Free();
            }
        }
    }
}
```

- [ ] **Step 1.4: Run MarshalHelpersTests, expect pass**

```
dotnet run --project XProtocol.Tests -c Debug -- --treenode-filter "/*/XProtocol.Tests/XProtocol.Tests/MarshalHelpersTests"
```

Expected: 3/3 tests pass.

- [ ] **Step 1.5: Replace XPacket private helpers with delegations**

Edit `XProtocol/XPacket.cs`. Remove the bottom two private methods (`FixedObjectToByteArray` and `ByteArrayToFixedObject`, around lines 290–320) and adjust the call sites to use `MarshalHelpers`:

Replace `AppendValue` body (around lines 34–57):

```csharp
public void AppendValue(object structure)
{
    if (structure == null)
    {
        throw new ArgumentNullException(nameof(structure));
    }

    if (!structure.GetType().IsValueType)
    {
        throw new ArgumentException("Only value types are supported.", nameof(structure));
    }

    var bytes = XProtocol.Serializator.MarshalHelpers.ToBytes(structure, structure.GetType());
    if (bytes.Length > byte.MaxValue)
    {
        throw new InvalidOperationException("Field is too large (>255 bytes).");
    }

    Fields.Add(new XPacketField
    {
        FieldSize = (byte)bytes.Length,
        Contents = bytes
    });
}
```

Replace `GetValueAt<T>` body (around lines 95–104):

```csharp
public T GetValueAt<T>(int index) where T : struct
{
    if (index < 0 || index >= Fields.Count)
    {
        throw new ArgumentOutOfRangeException(nameof(index));
    }

    var field = Fields[index];
    return (T)XProtocol.Serializator.MarshalHelpers.FromBytes(field.Contents, typeof(T));
}
```

Delete the two private methods at the end of the class (`FixedObjectToByteArray` and `ByteArrayToFixedObject`).

- [ ] **Step 1.6: Run full test suite, expect all 71 tests pass (68 existing + 3 new)**

```
dotnet build XProtocolSol.sln -c Debug
dotnet run --project XProtocol.Tests -c Debug
```

Expected: every test that previously passed still passes.

- [ ] **Step 1.7: Commit**

```
git add XProtocol/Serializator/MarshalHelpers.cs XProtocol/XPacket.cs XProtocol.Tests/MarshalHelpersTests.cs
git commit -m "serializer: extract Marshal helpers to shared internal class

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: ChunkReader

A new internal reader that walks bytes across consecutive wire-fields of an `XPacket`. Used by `ShapeCodec.ReadField`. Independent of any shape logic.

**Files:**
- Create: `XProtocol/Serializator/ChunkReader.cs`
- Create: `XProtocol.Tests/ChunkReaderTests.cs`

- [ ] **Step 2.1: Write failing tests**

Create `XProtocol.Tests/ChunkReaderTests.cs`:

```csharp
using System;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    public class ChunkReaderTests
    {
        private static XPacket Pack(params byte[][] chunks)
        {
            var p = XPacket.Create(0, 0);
            foreach (var c in chunks)
            {
                p.Fields.Add(new XPacketField
                {
                    FieldSize = (byte)c.Length,
                    Contents = c
                });
            }
            return p;
        }

        [Test]
        public async Task ReadByte_FromSingleChunk_AdvancesOffset()
        {
            var p = Pack(new byte[] { 0x01, 0x02, 0x03 });
            var r = new ChunkReader(p, 0);

            await Assert.That(r.ReadByte()).IsEqualTo((byte)0x01);
            await Assert.That(r.ReadByte()).IsEqualTo((byte)0x02);
            await Assert.That(r.ReadByte()).IsEqualTo((byte)0x03);
        }

        [Test]
        public async Task ReadByte_CrossesChunkBoundary()
        {
            var p = Pack(new byte[] { 0x01, 0x02 }, new byte[] { 0x03, 0x04 });
            var r = new ChunkReader(p, 0);

            await Assert.That(r.ReadByte()).IsEqualTo((byte)0x01);
            await Assert.That(r.ReadByte()).IsEqualTo((byte)0x02);
            await Assert.That(r.WireIdx).IsEqualTo(1);
            await Assert.That(r.ReadByte()).IsEqualTo((byte)0x03);
            await Assert.That(r.ReadByte()).IsEqualTo((byte)0x04);
        }

        [Test]
        public async Task ReadUInt16LE_AcrossChunkBoundary()
        {
            var p = Pack(new byte[] { 0x01 }, new byte[] { 0x02 });
            var r = new ChunkReader(p, 0);

            await Assert.That(r.ReadUInt16LE()).IsEqualTo((ushort)0x0201);
        }

        [Test]
        public async Task ReadBytes_LargerThanChunk()
        {
            var p = Pack(
                new byte[] { 0x01, 0x02 },
                new byte[] { 0x03, 0x04, 0x05 });
            var r = new ChunkReader(p, 0);
            var buf = new byte[5];
            r.ReadBytes(buf, 0, 5);

            await Assert.That(buf[0]).IsEqualTo((byte)0x01);
            await Assert.That(buf[4]).IsEqualTo((byte)0x05);
            await Assert.That(r.Available).IsEqualTo(0);
        }

        [Test]
        public async Task ReadByte_BeyondEnd_Throws()
        {
            var p = Pack(new byte[] { 0x01 });
            var r = new ChunkReader(p, 0);
            r.ReadByte();

            var ex = await Assert.That(() => r.ReadByte())
                .ThrowsExactly<InvalidOperationException>();
            await Assert.That(ex.Message).Contains("payload truncated");
        }

        [Test]
        public async Task Available_ReflectsRemainingBytes()
        {
            var p = Pack(new byte[] { 1, 2, 3 }, new byte[] { 4, 5 });
            var r = new ChunkReader(p, 0);

            await Assert.That(r.Available).IsEqualTo(5);
            r.ReadByte();
            await Assert.That(r.Available).IsEqualTo(4);
        }

        [Test]
        public async Task StartWireIdx_OffsetsInitialPosition()
        {
            var p = Pack(new byte[] { 1, 2 }, new byte[] { 3, 4 });
            var r = new ChunkReader(p, 1);

            await Assert.That(r.Available).IsEqualTo(2);
            await Assert.That(r.ReadByte()).IsEqualTo((byte)3);
        }
    }
}
```

- [ ] **Step 2.2: Run tests, expect compile error**

```
dotnet build XProtocolSol.sln -c Debug
```

Expected: `error CS0246: The type or namespace name 'ChunkReader' could not be found`.

- [ ] **Step 2.3: Implement ChunkReader**

Create `XProtocol/Serializator/ChunkReader.cs`:

```csharp
using System;

namespace XProtocol.Serializator
{
    internal sealed class ChunkReader
    {
        private readonly XPacket packet;
        private int wireIdx;
        private int offsetInChunk;

        public ChunkReader(XPacket packet, int startWireIdx)
        {
            this.packet = packet ?? throw new ArgumentNullException(nameof(packet));
            if (startWireIdx < 0 || startWireIdx > packet.Fields.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(startWireIdx));
            }
            this.wireIdx = startWireIdx;
            this.offsetInChunk = 0;
        }

        public int WireIdx => this.wireIdx;

        public int Available
        {
            get
            {
                int total = 0;
                if (this.wireIdx < this.packet.Fields.Count)
                {
                    total += this.packet.Fields[this.wireIdx].FieldSize - this.offsetInChunk;
                    for (int i = this.wireIdx + 1; i < this.packet.Fields.Count; i++)
                    {
                        total += this.packet.Fields[i].FieldSize;
                    }
                }
                return total;
            }
        }

        public byte ReadByte()
        {
            EnsureCanRead(1);
            var b = this.packet.Fields[this.wireIdx].Contents[this.offsetInChunk++];
            AdvanceIfChunkExhausted();
            return b;
        }

        public ushort ReadUInt16LE()
        {
            var lo = ReadByte();
            var hi = ReadByte();
            return (ushort)(lo | (hi << 8));
        }

        public void ReadBytes(byte[] dst, int offset, int count)
        {
            if (dst == null)
            {
                throw new ArgumentNullException(nameof(dst));
            }
            EnsureCanRead(count);

            int remaining = count;
            int dstOffset = offset;
            while (remaining > 0)
            {
                var field = this.packet.Fields[this.wireIdx];
                int take = Math.Min(remaining, field.FieldSize - this.offsetInChunk);
                Buffer.BlockCopy(field.Contents, this.offsetInChunk, dst, dstOffset, take);
                this.offsetInChunk += take;
                dstOffset += take;
                remaining -= take;
                AdvanceIfChunkExhausted();
            }
        }

        private void EnsureCanRead(int count)
        {
            if (count > Available)
            {
                throw new InvalidOperationException(
                    $"payload truncated: requested {count} bytes, only {Available} remaining (wireIdx={this.wireIdx}, fields={this.packet.Fields.Count}).");
            }
        }

        private void AdvanceIfChunkExhausted()
        {
            while (this.wireIdx < this.packet.Fields.Count
                && this.offsetInChunk >= this.packet.Fields[this.wireIdx].FieldSize)
            {
                this.wireIdx++;
                this.offsetInChunk = 0;
            }
        }
    }
}
```

- [ ] **Step 2.4: Run ChunkReaderTests, expect all pass**

```
dotnet run --project XProtocol.Tests -c Debug -- --treenode-filter "/*/XProtocol.Tests/XProtocol.Tests/ChunkReaderTests"
```

Expected: 7/7 pass.

- [ ] **Step 2.5: Run full test suite, expect no regression**

```
dotnet run --project XProtocol.Tests -c Debug
```

- [ ] **Step 2.6: Commit**

```
git add XProtocol/Serializator/ChunkReader.cs XProtocol.Tests/ChunkReaderTests.cs
git commit -m "serializer: add ChunkReader for wire-field-spanning reads

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: FieldShape hierarchy

Define the six shape subtypes. No behaviour yet — just the data model. The hierarchy is internal, but tests reach it via the existing `InternalsVisibleTo("XProtocol.Tests")` attribute.

**Files:**
- Create: `XProtocol/Serializator/FieldShape.cs`

- [ ] **Step 3.1: Define the hierarchy**

Create `XProtocol/Serializator/FieldShape.cs`:

```csharp
using System;

namespace XProtocol.Serializator
{
    internal abstract class FieldShape
    {
    }

    internal sealed class ValueShape : FieldShape
    {
        public Type ClrType { get; }

        public ValueShape(Type clrType)
        {
            this.ClrType = clrType ?? throw new ArgumentNullException(nameof(clrType));
        }
    }

    internal sealed class StringShape : FieldShape
    {
        public static readonly StringShape Instance = new StringShape();
        private StringShape() { }
    }

    internal sealed class ArrayShape : FieldShape
    {
        public Type ElementClrType { get; }
        public FieldShape Element { get; }

        public ArrayShape(Type elementClrType, FieldShape element)
        {
            this.ElementClrType = elementClrType ?? throw new ArgumentNullException(nameof(elementClrType));
            this.Element = element ?? throw new ArgumentNullException(nameof(element));
        }
    }

    internal sealed class ListShape : FieldShape
    {
        public Type ElementClrType { get; }
        public FieldShape Element { get; }

        public ListShape(Type elementClrType, FieldShape element)
        {
            this.ElementClrType = elementClrType ?? throw new ArgumentNullException(nameof(elementClrType));
            this.Element = element ?? throw new ArgumentNullException(nameof(element));
        }
    }

    internal sealed class DictShape : FieldShape
    {
        public Type KeyClrType { get; }
        public Type ValueClrType { get; }
        public FieldShape Key { get; }
        public FieldShape Value { get; }

        public DictShape(Type keyClrType, Type valueClrType, FieldShape key, FieldShape value)
        {
            this.KeyClrType = keyClrType ?? throw new ArgumentNullException(nameof(keyClrType));
            this.ValueClrType = valueClrType ?? throw new ArgumentNullException(nameof(valueClrType));
            this.Key = key ?? throw new ArgumentNullException(nameof(key));
            this.Value = value ?? throw new ArgumentNullException(nameof(value));
        }
    }

    internal sealed class NestedShape : FieldShape
    {
        public Type ClrType { get; }
        public FieldDescriptor[] Fields { get; }

        public NestedShape(Type clrType, FieldDescriptor[] fields)
        {
            this.ClrType = clrType ?? throw new ArgumentNullException(nameof(clrType));
            this.Fields = fields ?? throw new ArgumentNullException(nameof(fields));
        }
    }
}
```

- [ ] **Step 3.2: Verify it compiles**

```
dotnet build XProtocolSol.sln -c Debug
```

Expected: succeeds (no test exercises this yet; the hierarchy is just declared).

- [ ] **Step 3.3: Commit**

```
git add XProtocol/Serializator/FieldShape.cs
git commit -m "serializer: introduce FieldShape hierarchy

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: FieldDescriptor rewrite + Resolver/Codec for Value + String

This is the biggest single task because it must atomically replace `FieldKind`-based dispatch. After it, all existing 71 tests must still pass (with `FieldDescriptorTests.cs` updated to the new API). New shapes (Array/List/Dict/Nested) will arrive in later tasks; for now the resolver only knows Value and String.

**Files:**
- Modify: `XProtocol/Serializator/FieldDescriptor.cs`
- Create: `XProtocol/Serializator/ShapeResolver.cs`
- Create: `XProtocol/Serializator/ShapeCodec.cs`
- Modify: `XProtocol/Serializator/XPacketConverter.cs`
- Modify: `XProtocol/XPacketTypeManager.cs`
- Modify: `XProtocol.Tests/FieldDescriptorTests.cs`

- [ ] **Step 4.1: Replace FieldDescriptor**

Replace the entire contents of `XProtocol/Serializator/FieldDescriptor.cs` with:

```csharp
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace XProtocol.Serializator
{
    internal sealed class FieldDescriptor
    {
        public FieldInfo Field { get; }
        public FieldShape Shape { get; }
        public Func<object, object> Getter { get; }
        public Action<object, object> Setter { get; }

        public FieldDescriptor(FieldInfo field, FieldShape shape)
        {
            this.Field = field ?? throw new ArgumentNullException(nameof(field));
            this.Shape = shape ?? throw new ArgumentNullException(nameof(shape));
            this.Getter = BuildGetter(field);
            this.Setter = BuildSetter(field);
        }

        private static Func<object, object> BuildGetter(FieldInfo f)
        {
            var p = Expression.Parameter(typeof(object), "o");
            var body = Expression.Convert(
                Expression.Field(Expression.Convert(p, f.DeclaringType), f),
                typeof(object));
            return Expression.Lambda<Func<object, object>>(body, p).Compile();
        }

        private static Action<object, object> BuildSetter(FieldInfo f)
        {
            var p = Expression.Parameter(typeof(object), "o");
            var v = Expression.Parameter(typeof(object), "v");
            var body = Expression.Assign(
                Expression.Field(Expression.Convert(p, f.DeclaringType), f),
                Expression.Convert(v, f.FieldType));
            return Expression.Lambda<Action<object, object>>(body, p, v).Compile();
        }
    }
}
```

`FieldKind` enum is gone; the typed `StringGetter`/`StringSetter` are gone. One unified pair of `object`-typed accessors.

- [ ] **Step 4.2: Create ShapeResolver (Value + String only for now)**

Create `XProtocol/Serializator/ShapeResolver.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace XProtocol.Serializator
{
    internal static class ShapeResolver
    {
        public static FieldShape Resolve(Type t, HashSet<Type> visiting)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (visiting == null) throw new ArgumentNullException(nameof(visiting));

            if (t == typeof(string))
            {
                return StringShape.Instance;
            }

            if (t.IsValueType)
            {
                return new ValueShape(t);
            }

            throw new InvalidOperationException($"Type {t.Name} is not supported.");
        }

        public static FieldDescriptor[] BuildDescriptors(Type t, HashSet<Type> visiting)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (visiting == null) throw new ArgumentNullException(nameof(visiting));

            var fields = new List<FieldInfo>();
            for (var current = t; current != null && current != typeof(object); current = current.BaseType)
            {
                fields.AddRange(
                    current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                           .Where(f => !f.IsLiteral));
            }

            var sorted = fields.OrderBy(f => f.MetadataToken).ToArray();

            if (sorted.Length > byte.MaxValue)
            {
                throw new InvalidOperationException($"{t.Name} has more than {byte.MaxValue} fields.");
            }

            return sorted
                .Select(f => new FieldDescriptor(f, Resolve(f.FieldType, visiting)))
                .ToArray();
        }
    }
}
```

- [ ] **Step 4.3: Create ShapeCodec (Value + String only for now)**

Create `XProtocol/Serializator/ShapeCodec.cs`:

```csharp
using System;
using System.IO;
using System.Text;

namespace XProtocol.Serializator
{
    internal static class ShapeCodec
    {
        public static byte[] WriteField(FieldShape shape, object value)
        {
            using var ms = new MemoryStream();
            WriteFieldInto(ms, shape, value);
            return ms.ToArray();
        }

        public static object ReadField(FieldShape shape, ChunkReader reader)
        {
            switch (shape)
            {
                case ValueShape v:
                    return ReadValue(v, reader);
                case StringShape:
                    return ReadString(reader);
                default:
                    throw new InvalidOperationException($"Unsupported shape: {shape.GetType().Name}");
            }
        }

        private static void WriteFieldInto(MemoryStream ms, FieldShape shape, object value)
        {
            switch (shape)
            {
                case ValueShape v:
                    WriteValue(ms, v, value);
                    break;
                case StringShape:
                    WriteString(ms, value);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported shape: {shape.GetType().Name}");
            }
        }

        private static void WriteValue(MemoryStream ms, ValueShape shape, object value)
        {
            var bytes = MarshalHelpers.ToBytes(value, shape.ClrType);
            ms.Write(bytes, 0, bytes.Length);
        }

        private static object ReadValue(ValueShape shape, ChunkReader reader)
        {
            var size = System.Runtime.InteropServices.Marshal.SizeOf(shape.ClrType);
            var buf = new byte[size];
            reader.ReadBytes(buf, 0, size);
            return MarshalHelpers.FromBytes(buf, shape.ClrType);
        }

        private static void WriteString(MemoryStream ms, object value)
        {
            var s = (string)value ?? string.Empty;
            var utf8 = Encoding.UTF8.GetBytes(s);
            if (utf8.Length > ushort.MaxValue)
            {
                throw new InvalidOperationException(
                    $"string exceeds {ushort.MaxValue} UTF-8 bytes (actual: {utf8.Length}).");
            }
            WriteUInt16LE(ms, (ushort)utf8.Length);
            ms.Write(utf8, 0, utf8.Length);
        }

        private static string ReadString(ChunkReader reader)
        {
            int len = reader.ReadUInt16LE();
            var buf = new byte[len];
            if (len > 0)
            {
                reader.ReadBytes(buf, 0, len);
            }
            return Encoding.UTF8.GetString(buf);
        }

        private static void WriteUInt16LE(MemoryStream ms, ushort v)
        {
            ms.WriteByte((byte)(v & 0xFF));
            ms.WriteByte((byte)((v >> 8) & 0xFF));
        }
    }
}
```

- [ ] **Step 4.4: Rewrite XPacketConverter**

Replace the entire contents of `XProtocol/Serializator/XPacketConverter.cs` with:

```csharp
using System;

namespace XProtocol.Serializator
{
    public static class XPacketConverter
    {
        public static XPacket Serialize<T>(T obj) where T : class
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            var (btype, bsubtype) = XPacketTypeManager.GetBytesFor(typeof(T));
            var descriptors = XPacketTypeManager.GetDescriptors(typeof(T));
            var packet = XPacket.Create(btype, bsubtype);

            foreach (var desc in descriptors)
            {
                var bytes = ShapeCodec.WriteField(desc.Shape, desc.Getter(obj));
                packet.AppendChunks(bytes);
            }

            if (packet.Fields.Count > byte.MaxValue)
            {
                throw new InvalidOperationException(
                    $"{typeof(T).Name}: packet exceeds {byte.MaxValue} wire fields (actual: {packet.Fields.Count}).");
            }

            return packet;
        }

        public static T Deserialize<T>(XPacket packet) where T : class, new()
        {
            if (packet == null)
            {
                throw new ArgumentNullException(nameof(packet));
            }

            var descriptors = XPacketTypeManager.GetDescriptors(typeof(T));
            var instance = new T();
            var reader = new ChunkReader(packet, 0);

            foreach (var desc in descriptors)
            {
                var value = ShapeCodec.ReadField(desc.Shape, reader);
                desc.Setter(instance, value);
            }

            if (reader.Available != 0)
            {
                throw new InvalidOperationException(
                    $"Field count mismatch for {typeof(T).Name}: trailing bytes after all descriptors consumed (remaining: {reader.Available}, wireIdx: {reader.WireIdx}, fields: {packet.Fields.Count}).");
            }

            return instance;
        }
    }
}
```

- [ ] **Step 4.5: Update XPacketTypeManager to delegate to ShapeResolver**

Edit `XProtocol/XPacketTypeManager.cs`. Replace the `BuildDescriptors` method (around lines 99–117) with:

```csharp
private static FieldDescriptor[] BuildDescriptors(Type t)
{
    return ShapeResolver.BuildDescriptors(t, new HashSet<Type>());
}
```

The rest of the file stays the same.

- [ ] **Step 4.6: Rewrite FieldDescriptorTests for new API**

Replace contents of `XProtocol.Tests/FieldDescriptorTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    public class FieldDescriptorTests
    {
        [Test]
        public async Task Descriptor_ForValueTypeField_HasValueShape()
        {
            var f = typeof(SimpleDto).GetField(nameof(SimpleDto.A), BindingFlags.Instance | BindingFlags.Public);
            var shape = ShapeResolver.Resolve(f.FieldType, new HashSet<Type>());
            var d = new FieldDescriptor(f, shape);

            await Assert.That(d.Shape).IsTypeOf<ValueShape>();
            await Assert.That(((ValueShape)d.Shape).ClrType).IsEqualTo(typeof(int));
            await Assert.That(d.Getter).IsNotNull();
            await Assert.That(d.Setter).IsNotNull();
        }

        [Test]
        public async Task Descriptor_ForStringField_HasStringShape()
        {
            var f = typeof(StringDto).GetField(nameof(StringDto.S), BindingFlags.Instance | BindingFlags.Public);
            var shape = ShapeResolver.Resolve(f.FieldType, new HashSet<Type>());
            var d = new FieldDescriptor(f, shape);

            await Assert.That(d.Shape).IsTypeOf<StringShape>();
        }

        [Test]
        public async Task GetterSetter_RoundtripsString()
        {
            var f = typeof(StringDto).GetField(nameof(StringDto.S), BindingFlags.Instance | BindingFlags.Public);
            var shape = ShapeResolver.Resolve(f.FieldType, new HashSet<Type>());
            var d = new FieldDescriptor(f, shape);

            var obj = new StringDto();
            d.Setter(obj, "hello");

            await Assert.That((string)d.Getter(obj)).IsEqualTo("hello");
        }

        [Test]
        public async Task GetterSetter_RoundtripsInt()
        {
            var f = typeof(SimpleDto).GetField(nameof(SimpleDto.A), BindingFlags.Instance | BindingFlags.Public);
            var shape = ShapeResolver.Resolve(f.FieldType, new HashSet<Type>());
            var d = new FieldDescriptor(f, shape);

            var obj = new SimpleDto();
            d.Setter(obj, 42);

            await Assert.That((int)d.Getter(obj)).IsEqualTo(42);
        }
    }
}
```

- [ ] **Step 4.7: Run full test suite**

```
dotnet build XProtocolSol.sln -c Debug
dotnet run --project XProtocol.Tests -c Debug
```

Expected: every test passes. Specifically:

- `EmptyDto_RoundtripProducesZeroFields` still passes because the resolver does not yet reject empty classes (Task 10 introduces that rejection). `BuildDescriptors(typeof(EmptyDto))` returns an empty array; the converter loop produces zero wire-fields; deserialise returns a new `EmptyDto()`. The test's existing assertions are satisfied.
- The old `Descriptor_ForUnsupportedRefType_Throws` test was removed in Step 4.6 along with the rest of the `FieldKind`-based test surface; resolver-level rejection is covered by tests added in Tasks 5, 8, 10, and 15.

If there are compile failures in other test files because of removed `FieldKind`/`StringGetter` references, fix them by following the same pattern: use `d.Shape` for the kind check, `d.Getter(obj)` for read, `d.Setter(obj, value)` for write. The most likely candidates are any tests that still reference `FieldKind.ValueType`, `FieldKind.String`, `d.StringGetter`, or `d.StringSetter`.

- [ ] **Step 4.8: Commit**

```
git add XProtocol/Serializator/FieldDescriptor.cs XProtocol/Serializator/ShapeResolver.cs XProtocol/Serializator/ShapeCodec.cs XProtocol/Serializator/XPacketConverter.cs XProtocol/XPacketTypeManager.cs XProtocol.Tests/FieldDescriptorTests.cs
git commit -m "serializer: replace FieldKind dispatch with FieldShape tree

ShapeResolver currently handles ValueShape and StringShape only.
ShapeCodec preserves byte-identical wire format for value-types and
strings. New shapes will be added in subsequent commits.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 5: ArrayShape — resolver

Extend `ShapeResolver.Resolve` to recognise `T[]` (single-dimension only). Adds a new test class for resolver behaviour.

**Files:**
- Modify: `XProtocol/Serializator/ShapeResolver.cs`
- Create: `XProtocol.Tests/FieldShapeResolverTests.cs`

- [ ] **Step 5.1: Write failing test for ArrayShape resolution**

Create `XProtocol.Tests/FieldShapeResolverTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    public class FieldShapeResolverTests
    {
        [Test]
        public async Task Resolve_IntArray_ReturnsArrayShapeOfValueInt()
        {
            var shape = ShapeResolver.Resolve(typeof(int[]), new HashSet<Type>());

            await Assert.That(shape).IsTypeOf<ArrayShape>();
            var arr = (ArrayShape)shape;
            await Assert.That(arr.ElementClrType).IsEqualTo(typeof(int));
            await Assert.That(arr.Element).IsTypeOf<ValueShape>();
        }

        [Test]
        public async Task Resolve_StringArray_ReturnsArrayShapeOfString()
        {
            var shape = ShapeResolver.Resolve(typeof(string[]), new HashSet<Type>());

            var arr = (ArrayShape)shape;
            await Assert.That(arr.Element).IsTypeOf<StringShape>();
        }

        [Test]
        public async Task Resolve_TwoDimArray_Throws()
        {
            var ex = await Assert.That(() => ShapeResolver.Resolve(typeof(int[,]), new HashSet<Type>()))
                .ThrowsExactly<InvalidOperationException>();
            await Assert.That(ex.Message).Contains("is not supported");
        }
    }
}
```

- [ ] **Step 5.2: Run, expect failure**

```
dotnet build XProtocolSol.sln -c Debug
dotnet run --project XProtocol.Tests -c Debug -- --treenode-filter "/*/XProtocol.Tests/XProtocol.Tests/FieldShapeResolverTests"
```

Expected: tests fail with "is not supported" on int[].

- [ ] **Step 5.3: Add ArrayShape dispatch in resolver**

Modify `XProtocol/Serializator/ShapeResolver.cs`. Replace the `Resolve` method body with the version that recognises arrays:

```csharp
public static FieldShape Resolve(Type t, HashSet<Type> visiting)
{
    if (t == null) throw new ArgumentNullException(nameof(t));
    if (visiting == null) throw new ArgumentNullException(nameof(visiting));

    if (t == typeof(string))
    {
        return StringShape.Instance;
    }

    if (t.IsValueType)
    {
        return new ValueShape(t);
    }

    if (t.IsArray)
    {
        if (t.GetArrayRank() != 1)
        {
            throw new InvalidOperationException(
                $"Multi-dimensional array {t.Name} is not supported. Use jagged arrays instead.");
        }
        var elementType = t.GetElementType();
        var elementShape = Resolve(elementType, visiting);
        return new ArrayShape(elementType, elementShape);
    }

    throw new InvalidOperationException($"Type {t.Name} is not supported.");
}
```

- [ ] **Step 5.4: Run, expect pass**

```
dotnet run --project XProtocol.Tests -c Debug -- --treenode-filter "/*/XProtocol.Tests/XProtocol.Tests/FieldShapeResolverTests"
```

Expected: 3/3 pass.

- [ ] **Step 5.5: Commit**

```
git add XProtocol/Serializator/ShapeResolver.cs XProtocol.Tests/FieldShapeResolverTests.cs
git commit -m "serializer: resolve T[] to ArrayShape

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 6: ArrayShape — codec (general path + byte[] fast path)

Add write/read for `ArrayShape`. Special-case `byte[]` for bulk copy.

**Files:**
- Modify: `XProtocol/Serializator/ShapeCodec.cs`
- Create: `XProtocol.Tests/ShapeCodecTests.cs`
- Create: `XProtocol.Tests/RoundtripArrayTests.cs`
- Modify: `XProtocol.Tests/TestDtos.cs` (add array-bearing DTOs + register them)

- [ ] **Step 6.1: Write failing codec unit tests**

Create `XProtocol.Tests/ShapeCodecTests.cs`:

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    public class ShapeCodecTests
    {
        private static XPacket WrapAsPacket(byte[] payload)
        {
            var p = XPacket.Create(0, 0);
            p.AppendChunks(payload);
            return p;
        }

        [Test]
        public async Task WriteArray_Int3Elements_ProducesExpectedBytes()
        {
            var shape = new ArrayShape(typeof(int), new ValueShape(typeof(int)));
            var bytes = ShapeCodec.WriteField(shape, new[] { 1, 2, 3 });

            // [count=3 LE]   [int 1] [int 2] [int 3]
            // [03 00]        [01 00 00 00] [02 00 00 00] [03 00 00 00]
            await Assert.That(bytes.Length).IsEqualTo(2 + 3 * 4);
            await Assert.That(bytes[0]).IsEqualTo((byte)0x03);
            await Assert.That(bytes[1]).IsEqualTo((byte)0x00);
            await Assert.That(bytes[2]).IsEqualTo((byte)0x01);
        }

        [Test]
        public async Task ReadArray_RoundtripsInt()
        {
            var shape = new ArrayShape(typeof(int), new ValueShape(typeof(int)));
            var bytes = ShapeCodec.WriteField(shape, new[] { 10, 20, 30 });
            var reader = new ChunkReader(WrapAsPacket(bytes), 0);

            var back = (int[])ShapeCodec.ReadField(shape, reader);

            await Assert.That(back.Length).IsEqualTo(3);
            await Assert.That(back[0]).IsEqualTo(10);
            await Assert.That(back[2]).IsEqualTo(30);
        }

        [Test]
        public async Task WriteArray_NullValue_TreatedAsEmpty()
        {
            var shape = new ArrayShape(typeof(int), new ValueShape(typeof(int)));
            var bytes = ShapeCodec.WriteField(shape, null);

            await Assert.That(bytes.Length).IsEqualTo(2);
            await Assert.That(bytes[0]).IsEqualTo((byte)0);
            await Assert.That(bytes[1]).IsEqualTo((byte)0);
        }

        [Test]
        public async Task WriteArray_ByteFastPath_ProducesContiguousBytes()
        {
            var shape = new ArrayShape(typeof(byte), new ValueShape(typeof(byte)));
            var bytes = ShapeCodec.WriteField(shape, new byte[] { 0xAA, 0xBB, 0xCC });

            // [03 00] [AA BB CC]
            await Assert.That(bytes.Length).IsEqualTo(2 + 3);
            await Assert.That(bytes[2]).IsEqualTo((byte)0xAA);
        }

        [Test]
        public async Task WriteArray_TooLarge_Throws()
        {
            var shape = new ArrayShape(typeof(byte), new ValueShape(typeof(byte)));
            var big = new byte[ushort.MaxValue + 1];

            var ex = await Assert.That(() => ShapeCodec.WriteField(shape, big))
                .ThrowsExactly<InvalidOperationException>();
            await Assert.That(ex.Message).Contains("exceeds 65535 elements");
        }

        [Test]
        public async Task WriteArray_StringElements_Roundtrips()
        {
            var shape = new ArrayShape(typeof(string), StringShape.Instance);
            var bytes = ShapeCodec.WriteField(shape, new[] { "a", "bb" });
            var reader = new ChunkReader(WrapAsPacket(bytes), 0);

            var back = (string[])ShapeCodec.ReadField(shape, reader);

            await Assert.That(back.Length).IsEqualTo(2);
            await Assert.That(back[0]).IsEqualTo("a");
            await Assert.That(back[1]).IsEqualTo("bb");
        }
    }
}
```

- [ ] **Step 6.2: Run, expect failure (codec doesn't know ArrayShape yet)**

```
dotnet run --project XProtocol.Tests -c Debug -- --treenode-filter "/*/XProtocol.Tests/XProtocol.Tests/ShapeCodecTests"
```

Expected: all 6 fail with `"Unsupported shape: ArrayShape"`.

- [ ] **Step 6.3: Add ArrayShape Write/Read in codec**

Modify `XProtocol/Serializator/ShapeCodec.cs`. Update the two switch statements (`WriteFieldInto` and `ReadField`) and add the array helpers:

```csharp
public static object ReadField(FieldShape shape, ChunkReader reader)
{
    switch (shape)
    {
        case ValueShape v:    return ReadValue(v, reader);
        case StringShape:     return ReadString(reader);
        case ArrayShape a:    return ReadArray(a, reader);
        default:
            throw new InvalidOperationException($"Unsupported shape: {shape.GetType().Name}");
    }
}

private static void WriteFieldInto(MemoryStream ms, FieldShape shape, object value)
{
    switch (shape)
    {
        case ValueShape v:    WriteValue(ms, v, value); break;
        case StringShape:     WriteString(ms, value); break;
        case ArrayShape a:    WriteArray(ms, a, value); break;
        default:
            throw new InvalidOperationException($"Unsupported shape: {shape.GetType().Name}");
    }
}

private static void WriteArray(MemoryStream ms, ArrayShape shape, object value)
{
    var arr = (Array)value ?? Array.CreateInstance(shape.ElementClrType, 0);
    if (arr.Length > ushort.MaxValue)
    {
        throw new InvalidOperationException(
            $"collection exceeds {ushort.MaxValue} elements (actual: {arr.Length}).");
    }
    WriteUInt16LE(ms, (ushort)arr.Length);

    if (shape.Element is ValueShape vs && vs.ClrType == typeof(byte))
    {
        var src = (byte[])arr;
        ms.Write(src, 0, src.Length);
        return;
    }

    for (int i = 0; i < arr.Length; i++)
    {
        WriteFieldInto(ms, shape.Element, arr.GetValue(i));
    }
}

private static object ReadArray(ArrayShape shape, ChunkReader reader)
{
    int count = reader.ReadUInt16LE();
    var arr = Array.CreateInstance(shape.ElementClrType, count);

    if (shape.Element is ValueShape vs && vs.ClrType == typeof(byte))
    {
        if (count > 0)
        {
            reader.ReadBytes((byte[])arr, 0, count);
        }
        return arr;
    }

    for (int i = 0; i < count; i++)
    {
        arr.SetValue(ReadField(shape.Element, reader), i);
    }
    return arr;
}
```

- [ ] **Step 6.4: Run ShapeCodecTests, expect pass**

```
dotnet run --project XProtocol.Tests -c Debug -- --treenode-filter "/*/XProtocol.Tests/XProtocol.Tests/ShapeCodecTests"
```

Expected: 6/6 pass.

- [ ] **Step 6.5: Write roundtrip integration tests for arrays**

Append to `XProtocol.Tests/TestDtos.cs` (inside the namespace, alongside other DTOs):

```csharp
public class IntArrayDto
{
    public int A;
    public int[] Values;
}

public class ByteArrayDto
{
    public byte[] Payload;
}

public class StringArrayDto
{
    public string[] Tags;
}
```

Add registrations inside `AssemblyFixture.Init`:

```csharp
XPacketTypeManager.Register<IntArrayDto>((XPacketType)300, 300, 0);
XPacketTypeManager.Register<ByteArrayDto>((XPacketType)301, 301, 0);
XPacketTypeManager.Register<StringArrayDto>((XPacketType)302, 302, 0);
```

Create `XProtocol.Tests/RoundtripArrayTests.cs`:

```csharp
using System.Linq;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using XProtocol;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    public class RoundtripArrayTests
    {
        [Test]
        public async Task IntArray_Roundtrips()
        {
            var dto = new IntArrayDto { A = 7, Values = new[] { 1, 2, 3, 4, 5 } };
            var packet = XPacketConverter.Serialize(dto);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var back = XPacketConverter.Deserialize<IntArrayDto>(parsed);

            await Assert.That(back.A).IsEqualTo(7);
            await Assert.That(back.Values).IsEquivalentTo(new[] { 1, 2, 3, 4, 5 });
        }

        [Test]
        public async Task IntArray_Null_BecomesEmpty()
        {
            var dto = new IntArrayDto { A = 1, Values = null };
            var packet = XPacketConverter.Serialize(dto);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var back = XPacketConverter.Deserialize<IntArrayDto>(parsed);

            await Assert.That(back.Values).IsNotNull();
            await Assert.That(back.Values.Length).IsEqualTo(0);
        }

        [Test]
        public async Task ByteArray_Large_CrossesChunks()
        {
            var src = Enumerable.Range(0, 1000).Select(i => (byte)(i % 256)).ToArray();
            var dto = new ByteArrayDto { Payload = src };

            var packet = XPacketConverter.Serialize(dto);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var back = XPacketConverter.Deserialize<ByteArrayDto>(parsed);

            await Assert.That(back.Payload.Length).IsEqualTo(1000);
            await Assert.That(back.Payload).IsEquivalentTo(src);
        }

        [Test]
        public async Task StringArray_RoundtripsWithUnicode()
        {
            var dto = new StringArrayDto { Tags = new[] { "ascii", "Привет", "🚀" } };
            var packet = XPacketConverter.Serialize(dto);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var back = XPacketConverter.Deserialize<StringArrayDto>(parsed);

            await Assert.That(back.Tags.Length).IsEqualTo(3);
            await Assert.That(back.Tags[1]).IsEqualTo("Привет");
            await Assert.That(back.Tags[2]).IsEqualTo("🚀");
        }

        [Test]
        public async Task IntArray_Empty_Roundtrips()
        {
            var dto = new IntArrayDto { A = 2, Values = new int[0] };
            var packet = XPacketConverter.Serialize(dto);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var back = XPacketConverter.Deserialize<IntArrayDto>(parsed);

            await Assert.That(back.Values.Length).IsEqualTo(0);
        }
    }
}
```

- [ ] **Step 6.6: Run full suite, expect pass**

```
dotnet run --project XProtocol.Tests -c Debug
```

Expected: all tests pass (including the 6 new codec tests and 5 new roundtrip tests).

- [ ] **Step 6.7: Commit**

```
git add XProtocol/Serializator/ShapeCodec.cs XProtocol.Tests/ShapeCodecTests.cs XProtocol.Tests/RoundtripArrayTests.cs XProtocol.Tests/TestDtos.cs
git commit -m "serializer: add ArrayShape codec with byte[] fast path

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 7: ListShape — resolver + codec + tests

`List<T>` mirrors `T[]` semantically. Same encoding (count + element payloads). Different CLR construction (use `Activator.CreateInstance(typeof(List<>).MakeGenericType(elem))`).

**Files:**
- Modify: `XProtocol/Serializator/ShapeResolver.cs`
- Modify: `XProtocol/Serializator/ShapeCodec.cs`
- Modify: `XProtocol.Tests/FieldShapeResolverTests.cs`
- Modify: `XProtocol.Tests/ShapeCodecTests.cs`
- Create: `XProtocol.Tests/RoundtripListTests.cs`
- Modify: `XProtocol.Tests/TestDtos.cs`

- [ ] **Step 7.1: Write failing resolver test**

Append to `XProtocol.Tests/FieldShapeResolverTests.cs`:

```csharp
[Test]
public async Task Resolve_ListOfDouble_ReturnsListShape()
{
    var shape = ShapeResolver.Resolve(typeof(System.Collections.Generic.List<double>), new HashSet<Type>());

    await Assert.That(shape).IsTypeOf<ListShape>();
    var lst = (ListShape)shape;
    await Assert.That(lst.ElementClrType).IsEqualTo(typeof(double));
    await Assert.That(lst.Element).IsTypeOf<ValueShape>();
}

[Test]
public async Task Resolve_ListOfString_ReturnsListOfString()
{
    var shape = ShapeResolver.Resolve(typeof(System.Collections.Generic.List<string>), new HashSet<Type>());

    var lst = (ListShape)shape;
    await Assert.That(lst.Element).IsTypeOf<StringShape>();
}
```

- [ ] **Step 7.2: Run, expect failure**

```
dotnet run --project XProtocol.Tests -c Debug -- --treenode-filter "/*/XProtocol.Tests/XProtocol.Tests/FieldShapeResolverTests"
```

Expected: 2 new tests fail with "is not supported".

- [ ] **Step 7.3: Add ListShape dispatch in resolver**

Modify `XProtocol/Serializator/ShapeResolver.cs`. Add a branch before the final throw in `Resolve`:

```csharp
if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>))
{
    var elementType = t.GetGenericArguments()[0];
    var elementShape = Resolve(elementType, visiting);
    return new ListShape(elementType, elementShape);
}
```

Place this right after the `IsArray` block, before the final throw.

- [ ] **Step 7.4: Run resolver tests, expect pass**

```
dotnet run --project XProtocol.Tests -c Debug -- --treenode-filter "/*/XProtocol.Tests/XProtocol.Tests/FieldShapeResolverTests"
```

- [ ] **Step 7.5: Write failing codec unit tests**

Append to `XProtocol.Tests/ShapeCodecTests.cs`:

```csharp
[Test]
public async Task WriteList_IntElements_RoundtripsViaReader()
{
    var shape = new ListShape(typeof(int), new ValueShape(typeof(int)));
    var bytes = ShapeCodec.WriteField(shape, new System.Collections.Generic.List<int> { 100, 200, 300 });
    var reader = new ChunkReader(WrapAsPacket(bytes), 0);

    var back = (System.Collections.Generic.List<int>)ShapeCodec.ReadField(shape, reader);

    await Assert.That(back.Count).IsEqualTo(3);
    await Assert.That(back[0]).IsEqualTo(100);
    await Assert.That(back[2]).IsEqualTo(300);
}

[Test]
public async Task WriteList_NullValue_TreatedAsEmpty()
{
    var shape = new ListShape(typeof(int), new ValueShape(typeof(int)));
    var bytes = ShapeCodec.WriteField(shape, null);

    await Assert.That(bytes.Length).IsEqualTo(2);
    await Assert.That(bytes[0]).IsEqualTo((byte)0);
    await Assert.That(bytes[1]).IsEqualTo((byte)0);
}

[Test]
public async Task WriteList_StringElements_Roundtrips()
{
    var shape = new ListShape(typeof(string), StringShape.Instance);
    var bytes = ShapeCodec.WriteField(shape, new System.Collections.Generic.List<string> { "x", "yy" });
    var reader = new ChunkReader(WrapAsPacket(bytes), 0);

    var back = (System.Collections.Generic.List<string>)ShapeCodec.ReadField(shape, reader);

    await Assert.That(back).IsEquivalentTo(new[] { "x", "yy" });
}
```

- [ ] **Step 7.6: Run, expect failure**

Expected: codec doesn't know ListShape yet — `"Unsupported shape: ListShape"`.

- [ ] **Step 7.7: Add ListShape codec dispatch**

Modify `XProtocol/Serializator/ShapeCodec.cs`. Add to both switches and add helpers:

```csharp
// In ReadField switch:
case ListShape l:     return ReadList(l, reader);

// In WriteFieldInto switch:
case ListShape l:     WriteList(ms, l, value); break;

private static void WriteList(MemoryStream ms, ListShape shape, object value)
{
    var listType = typeof(System.Collections.Generic.List<>).MakeGenericType(shape.ElementClrType);
    var list = (System.Collections.IList)(value ?? Activator.CreateInstance(listType));
    if (list.Count > ushort.MaxValue)
    {
        throw new InvalidOperationException(
            $"collection exceeds {ushort.MaxValue} elements (actual: {list.Count}).");
    }
    WriteUInt16LE(ms, (ushort)list.Count);

    foreach (var item in list)
    {
        WriteFieldInto(ms, shape.Element, item);
    }
}

private static object ReadList(ListShape shape, ChunkReader reader)
{
    int count = reader.ReadUInt16LE();
    var listType = typeof(System.Collections.Generic.List<>).MakeGenericType(shape.ElementClrType);
    var list = (System.Collections.IList)Activator.CreateInstance(listType);
    for (int i = 0; i < count; i++)
    {
        list.Add(ReadField(shape.Element, reader));
    }
    return list;
}
```

- [ ] **Step 7.8: Run codec tests, expect pass**

```
dotnet run --project XProtocol.Tests -c Debug -- --treenode-filter "/*/XProtocol.Tests/XProtocol.Tests/ShapeCodecTests"
```

- [ ] **Step 7.9: Add integration roundtrip tests**

Append to `XProtocol.Tests/TestDtos.cs`:

```csharp
public class IntListDto
{
    public System.Collections.Generic.List<int> Numbers;
}

public class StringListDto
{
    public string Header;
    public System.Collections.Generic.List<string> Items;
}
```

Add registrations:

```csharp
XPacketTypeManager.Register<IntListDto>((XPacketType)310, 310, 0);
XPacketTypeManager.Register<StringListDto>((XPacketType)311, 311, 0);
```

Create `XProtocol.Tests/RoundtripListTests.cs`:

```csharp
using System.Linq;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using XProtocol;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    public class RoundtripListTests
    {
        [Test]
        public async Task IntList_Roundtrips()
        {
            var dto = new IntListDto { Numbers = new System.Collections.Generic.List<int> { 1, 2, 3 } };
            var packet = XPacketConverter.Serialize(dto);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var back = XPacketConverter.Deserialize<IntListDto>(parsed);

            await Assert.That(back.Numbers).IsEquivalentTo(new[] { 1, 2, 3 });
        }

        [Test]
        public async Task IntList_Null_BecomesEmptyList()
        {
            var dto = new IntListDto { Numbers = null };
            var packet = XPacketConverter.Serialize(dto);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var back = XPacketConverter.Deserialize<IntListDto>(parsed);

            await Assert.That(back.Numbers).IsNotNull();
            await Assert.That(back.Numbers.Count).IsEqualTo(0);
        }

        [Test]
        public async Task StringList_Empty_Roundtrips()
        {
            var dto = new StringListDto { Header = "h", Items = new System.Collections.Generic.List<string>() };
            var packet = XPacketConverter.Serialize(dto);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var back = XPacketConverter.Deserialize<StringListDto>(parsed);

            await Assert.That(back.Header).IsEqualTo("h");
            await Assert.That(back.Items.Count).IsEqualTo(0);
        }

        [Test]
        public async Task StringList_WithMany_Roundtrips()
        {
            var items = Enumerable.Range(0, 50).Select(i => $"item{i}").ToList();
            var dto = new StringListDto { Header = "head", Items = items };
            var packet = XPacketConverter.Serialize(dto);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var back = XPacketConverter.Deserialize<StringListDto>(parsed);

            await Assert.That(back.Items.Count).IsEqualTo(50);
            await Assert.That(back.Items[49]).IsEqualTo("item49");
        }

        [Test]
        public async Task StringList_WithNullElement_NormalizesToEmpty()
        {
            var dto = new StringListDto
            {
                Header = "h",
                Items = new System.Collections.Generic.List<string> { "x", null, "z" }
            };
            var packet = XPacketConverter.Serialize(dto);
            var parsed = XPacket.Parse(packet.ToPacket());
            var back = XPacketConverter.Deserialize<StringListDto>(parsed);

            await Assert.That(back.Items.Count).IsEqualTo(3);
            await Assert.That(back.Items[1]).IsEqualTo("");
        }
    }
}
```

- [ ] **Step 7.10: Run full suite, expect pass**

```
dotnet run --project XProtocol.Tests -c Debug
```

- [ ] **Step 7.11: Commit**

```
git add XProtocol/Serializator/ShapeResolver.cs XProtocol/Serializator/ShapeCodec.cs XProtocol.Tests/FieldShapeResolverTests.cs XProtocol.Tests/ShapeCodecTests.cs XProtocol.Tests/RoundtripListTests.cs XProtocol.Tests/TestDtos.cs
git commit -m "serializer: add ListShape support

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 8: DictShape — resolver with key validation

`Dictionary<K, V>` where `K` ∈ {value-type, string}. Anything else throws.

**Files:**
- Modify: `XProtocol/Serializator/ShapeResolver.cs`
- Modify: `XProtocol.Tests/FieldShapeResolverTests.cs`

- [ ] **Step 8.1: Write failing tests**

Append to `XProtocol.Tests/FieldShapeResolverTests.cs`:

```csharp
[Test]
public async Task Resolve_DictIntString_ReturnsDictShape()
{
    var shape = ShapeResolver.Resolve(
        typeof(System.Collections.Generic.Dictionary<int, string>),
        new HashSet<Type>());

    await Assert.That(shape).IsTypeOf<DictShape>();
    var d = (DictShape)shape;
    await Assert.That(d.KeyClrType).IsEqualTo(typeof(int));
    await Assert.That(d.ValueClrType).IsEqualTo(typeof(string));
    await Assert.That(d.Key).IsTypeOf<ValueShape>();
    await Assert.That(d.Value).IsTypeOf<StringShape>();
}

[Test]
public async Task Resolve_DictStringInt_StringKeyAllowed()
{
    var shape = ShapeResolver.Resolve(
        typeof(System.Collections.Generic.Dictionary<string, int>),
        new HashSet<Type>());

    var d = (DictShape)shape;
    await Assert.That(d.Key).IsTypeOf<StringShape>();
}

[Test]
public async Task Resolve_DictWithArrayKey_Throws()
{
    var ex = await Assert.That(() => ShapeResolver.Resolve(
            typeof(System.Collections.Generic.Dictionary<int[], int>),
            new HashSet<Type>()))
        .ThrowsExactly<InvalidOperationException>();
    await Assert.That(ex.Message).Contains("key must be value-type or string");
}

[Test]
public async Task Resolve_DictWithListKey_Throws()
{
    var ex = await Assert.That(() => ShapeResolver.Resolve(
            typeof(System.Collections.Generic.Dictionary<System.Collections.Generic.List<int>, int>),
            new HashSet<Type>()))
        .ThrowsExactly<InvalidOperationException>();
    await Assert.That(ex.Message).Contains("key must be value-type or string");
}
```

- [ ] **Step 8.2: Run, expect failure**

- [ ] **Step 8.3: Add DictShape dispatch with key validation**

Modify `XProtocol/Serializator/ShapeResolver.cs`. Add this block before the final throw, after the List branch:

```csharp
if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(System.Collections.Generic.Dictionary<,>))
{
    var genericArgs = t.GetGenericArguments();
    var keyType = genericArgs[0];
    var valueType = genericArgs[1];

    if (!(keyType.IsValueType || keyType == typeof(string)))
    {
        throw new InvalidOperationException(
            $"Dictionary<{keyType.Name}, {valueType.Name}>: key must be value-type or string (got {keyType.Name}).");
    }

    var keyShape = Resolve(keyType, visiting);
    var valueShape = Resolve(valueType, visiting);
    return new DictShape(keyType, valueType, keyShape, valueShape);
}
```

- [ ] **Step 8.4: Run, expect pass**

- [ ] **Step 8.5: Commit**

```
git add XProtocol/Serializator/ShapeResolver.cs XProtocol.Tests/FieldShapeResolverTests.cs
git commit -m "serializer: resolve Dictionary<K,V> to DictShape with key validation

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 9: DictShape — codec + roundtrip

**Files:**
- Modify: `XProtocol/Serializator/ShapeCodec.cs`
- Modify: `XProtocol.Tests/ShapeCodecTests.cs`
- Modify: `XProtocol.Tests/TestDtos.cs`
- Create: `XProtocol.Tests/RoundtripDictTests.cs`

- [ ] **Step 9.1: Write failing codec test**

Append to `XProtocol.Tests/ShapeCodecTests.cs`:

```csharp
[Test]
public async Task WriteDict_IntToString_Roundtrips()
{
    var shape = new DictShape(typeof(int), typeof(string),
        new ValueShape(typeof(int)), StringShape.Instance);

    var dict = new System.Collections.Generic.Dictionary<int, string> { { 1, "one" }, { 2, "two" } };
    var bytes = ShapeCodec.WriteField(shape, dict);
    var reader = new ChunkReader(WrapAsPacket(bytes), 0);

    var back = (System.Collections.Generic.Dictionary<int, string>)ShapeCodec.ReadField(shape, reader);

    await Assert.That(back.Count).IsEqualTo(2);
    await Assert.That(back[1]).IsEqualTo("one");
    await Assert.That(back[2]).IsEqualTo("two");
}

[Test]
public async Task WriteDict_NullValue_TreatedAsEmpty()
{
    var shape = new DictShape(typeof(int), typeof(int),
        new ValueShape(typeof(int)), new ValueShape(typeof(int)));
    var bytes = ShapeCodec.WriteField(shape, null);

    await Assert.That(bytes.Length).IsEqualTo(2);
    await Assert.That(bytes[0]).IsEqualTo((byte)0);
}
```

- [ ] **Step 9.2: Run, expect failure**

Expected: `"Unsupported shape: DictShape"`.

- [ ] **Step 9.3: Add DictShape codec dispatch**

Modify `XProtocol/Serializator/ShapeCodec.cs`. Add to both switches and add helpers:

```csharp
// In ReadField switch:
case DictShape d:     return ReadDict(d, reader);

// In WriteFieldInto switch:
case DictShape d:     WriteDict(ms, d, value); break;

private static void WriteDict(MemoryStream ms, DictShape shape, object value)
{
    var dictType = typeof(System.Collections.Generic.Dictionary<,>).MakeGenericType(shape.KeyClrType, shape.ValueClrType);
    var dict = (System.Collections.IDictionary)(value ?? Activator.CreateInstance(dictType));
    if (dict.Count > ushort.MaxValue)
    {
        throw new InvalidOperationException(
            $"collection exceeds {ushort.MaxValue} elements (actual: {dict.Count}).");
    }
    WriteUInt16LE(ms, (ushort)dict.Count);

    foreach (System.Collections.DictionaryEntry entry in dict)
    {
        WriteFieldInto(ms, shape.Key, entry.Key);
        WriteFieldInto(ms, shape.Value, entry.Value);
    }
}

private static object ReadDict(DictShape shape, ChunkReader reader)
{
    int count = reader.ReadUInt16LE();
    var dictType = typeof(System.Collections.Generic.Dictionary<,>).MakeGenericType(shape.KeyClrType, shape.ValueClrType);
    var dict = (System.Collections.IDictionary)Activator.CreateInstance(dictType);
    for (int i = 0; i < count; i++)
    {
        var key = ReadField(shape.Key, reader);
        var val = ReadField(shape.Value, reader);
        dict.Add(key, val);
    }
    return dict;
}
```

- [ ] **Step 9.4: Run codec tests, expect pass**

- [ ] **Step 9.5: Write roundtrip tests**

Append to `XProtocol.Tests/TestDtos.cs`:

```csharp
public class IntStringDictDto
{
    public System.Collections.Generic.Dictionary<int, string> Map;
}

public class StringIntDictDto
{
    public System.Collections.Generic.Dictionary<string, int> Map;
}
```

Add registrations:

```csharp
XPacketTypeManager.Register<IntStringDictDto>((XPacketType)320, 320, 0);
XPacketTypeManager.Register<StringIntDictDto>((XPacketType)321, 321, 0);
```

Create `XProtocol.Tests/RoundtripDictTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using XProtocol;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    public class RoundtripDictTests
    {
        [Test]
        public async Task IntStringDict_Roundtrips()
        {
            var dto = new IntStringDictDto
            {
                Map = new Dictionary<int, string> { { 1, "one" }, { 2, "two" }, { 3, "three" } }
            };
            var packet = XPacketConverter.Serialize(dto);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var back = XPacketConverter.Deserialize<IntStringDictDto>(parsed);

            await Assert.That(back.Map.Count).IsEqualTo(3);
            await Assert.That(back.Map[1]).IsEqualTo("one");
            await Assert.That(back.Map[3]).IsEqualTo("three");
        }

        [Test]
        public async Task StringIntDict_Empty_Roundtrips()
        {
            var dto = new StringIntDictDto { Map = new Dictionary<string, int>() };
            var packet = XPacketConverter.Serialize(dto);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var back = XPacketConverter.Deserialize<StringIntDictDto>(parsed);

            await Assert.That(back.Map.Count).IsEqualTo(0);
        }

        [Test]
        public async Task StringIntDict_Null_BecomesEmpty()
        {
            var dto = new StringIntDictDto { Map = null };
            var packet = XPacketConverter.Serialize(dto);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var back = XPacketConverter.Deserialize<StringIntDictDto>(parsed);

            await Assert.That(back.Map).IsNotNull();
            await Assert.That(back.Map.Count).IsEqualTo(0);
        }

        [Test]
        public async Task IntStringDict_Many_Roundtrips()
        {
            var src = Enumerable.Range(0, 30).ToDictionary(i => i, i => $"v{i}");
            var dto = new IntStringDictDto { Map = src };
            var packet = XPacketConverter.Serialize(dto);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var back = XPacketConverter.Deserialize<IntStringDictDto>(parsed);

            await Assert.That(back.Map.Count).IsEqualTo(30);
            foreach (var kv in src)
            {
                await Assert.That(back.Map[kv.Key]).IsEqualTo(kv.Value);
            }
        }
    }
}
```

- [ ] **Step 9.6: Run full suite, expect pass**

- [ ] **Step 9.7: Commit**

```
git add XProtocol/Serializator/ShapeCodec.cs XProtocol.Tests/ShapeCodecTests.cs XProtocol.Tests/RoundtripDictTests.cs XProtocol.Tests/TestDtos.cs
git commit -m "serializer: add DictShape codec

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 10: NestedShape — resolver with cycle and empty-class detection

This is the most complex resolver case: it must recursively build descriptors for the nested type, detect cycles via the `visiting` set, and reject classes with zero serialisable fields.

**Files:**
- Modify: `XProtocol/Serializator/ShapeResolver.cs`
- Modify: `XProtocol.Tests/FieldShapeResolverTests.cs`
- Modify: `XProtocol.Tests/TestDtos.cs` (remove `EmptyDto` registration; keep the class for negative tests)
- Modify: `XProtocol.Tests/RoundtripTests.cs` (convert `EmptyDto_RoundtripProducesZeroFields` to a registration-rejection test)

- [ ] **Step 10.1: Write failing tests**

Append to `XProtocol.Tests/FieldShapeResolverTests.cs`:

```csharp
public class NestedDtoNeedsFields
{
    public int X;
}

public class NestedDtoNoFields
{
}

public class NestedDtoNoPublicCtor
{
    public int X;
    public NestedDtoNoPublicCtor(int x) { this.X = x; }
}

public class NestedDtoCycleA
{
    public NestedDtoCycleB B;
}

public class NestedDtoCycleB
{
    public NestedDtoCycleA A;
}

public class NestedDtoSelfCycle
{
    public NestedDtoSelfCycle Child;
}
```

(Note: these helper types are at the namespace level inside `FieldShapeResolverTests.cs`, in the same `namespace XProtocol.Tests`.)

```csharp
[Test]
public async Task Resolve_NestedDto_ReturnsNestedShapeWithDescriptors()
{
    var shape = ShapeResolver.Resolve(typeof(NestedDtoNeedsFields), new HashSet<Type>());

    await Assert.That(shape).IsTypeOf<NestedShape>();
    var n = (NestedShape)shape;
    await Assert.That(n.ClrType).IsEqualTo(typeof(NestedDtoNeedsFields));
    await Assert.That(n.Fields.Length).IsEqualTo(1);
    await Assert.That(n.Fields[0].Shape).IsTypeOf<ValueShape>();
}

[Test]
public async Task Resolve_EmptyNestedDto_Throws()
{
    var ex = await Assert.That(() => ShapeResolver.Resolve(typeof(NestedDtoNoFields), new HashSet<Type>()))
        .ThrowsExactly<InvalidOperationException>();
    await Assert.That(ex.Message).Contains("nested DTO must have at least one serialisable field");
}

[Test]
public async Task Resolve_NestedDtoWithoutPublicCtor_Throws()
{
    var ex = await Assert.That(() => ShapeResolver.Resolve(typeof(NestedDtoNoPublicCtor), new HashSet<Type>()))
        .ThrowsExactly<InvalidOperationException>();
    await Assert.That(ex.Message).Contains("public parameterless constructor");
}

[Test]
public async Task Resolve_MutualCycle_Throws()
{
    var ex = await Assert.That(() => ShapeResolver.Resolve(typeof(NestedDtoCycleA), new HashSet<Type>()))
        .ThrowsExactly<InvalidOperationException>();
    await Assert.That(ex.Message).Contains("Cycle detected");
}

[Test]
public async Task Resolve_SelfCycle_Throws()
{
    var ex = await Assert.That(() => ShapeResolver.Resolve(typeof(NestedDtoSelfCycle), new HashSet<Type>()))
        .ThrowsExactly<InvalidOperationException>();
    await Assert.That(ex.Message).Contains("Cycle detected");
}
```

- [ ] **Step 10.2: Run, expect failure**

Expected: tests fail because resolver throws `"is not supported"` for nested classes.

- [ ] **Step 10.3: Add NestedShape dispatch with cycle and empty-class detection**

Modify `XProtocol/Serializator/ShapeResolver.cs`. Add this block after the Dict branch, before the final throw:

```csharp
if (t.IsClass)
{
    if (t.IsAbstract || t.IsInterface || t.ContainsGenericParameters)
    {
        throw new InvalidOperationException(
            $"Type {t.Name} is not supported (abstract/interface/open generic).");
    }
    if (t.GetConstructor(Type.EmptyTypes) == null)
    {
        throw new InvalidOperationException(
            $"{t.Name}: nested DTO must have public parameterless constructor.");
    }
    if (visiting.Contains(t))
    {
        var chain = string.Join(" → ", visiting) + $" → {t.Name}";
        throw new InvalidOperationException(
            $"Cycle detected in type graph: {chain}.");
    }
    visiting.Add(t);
    try
    {
        var fields = BuildDescriptors(t, visiting);
        if (fields.Length == 0)
        {
            throw new InvalidOperationException(
                $"{t.Name}: nested DTO must have at least one serialisable field.");
        }
        return new NestedShape(t, fields);
    }
    finally
    {
        visiting.Remove(t);
    }
}
```

- [ ] **Step 10.4: Run resolver tests, expect pass**

```
dotnet run --project XProtocol.Tests -c Debug -- --treenode-filter "/*/XProtocol.Tests/XProtocol.Tests/FieldShapeResolverTests"
```

- [ ] **Step 10.5: Convert `EmptyDto` registration test**

`EmptyDto` is currently registered at slot 101 in `AssemblyFixture`. With the new resolver, that registration must throw. Two changes:

In `XProtocol.Tests/TestDtos.cs`, remove the registration line:

Delete this line from `AssemblyFixture.Init`:

```csharp
XPacketTypeManager.Register<EmptyDto>(EmptyDtoType, 101, 0);
```

Keep the `EmptyDto` class definition and the `EmptyDtoType` constant — they are still referenced by negative tests.

In `XProtocol.Tests/RoundtripTests.cs`, replace the `EmptyDto_RoundtripProducesZeroFields` test with:

```csharp
[Test]
public async Task EmptyDto_Register_Throws()
{
    // EmptyDto has zero serialisable fields. The resolver rejects it at registration time.
    // (Cannot call Register directly because it would corrupt global registry state;
    // instead exercise the resolver to assert the rejection contract.)
    var ex = await Assert.That(() =>
            XProtocol.Serializator.ShapeResolver.Resolve(typeof(EmptyDto), new System.Collections.Generic.HashSet<System.Type>()))
        .ThrowsExactly<System.InvalidOperationException>();
    await Assert.That(ex.Message).Contains("nested DTO must have at least one serialisable field");
}
```

- [ ] **Step 10.6: Run full suite**

```
dotnet run --project XProtocol.Tests -c Debug
```

Expected: all tests pass. The previous `EmptyDto_RoundtripProducesZeroFields` is replaced; everything else continues to pass.

- [ ] **Step 10.7: Commit**

```
git add XProtocol/Serializator/ShapeResolver.cs XProtocol.Tests/FieldShapeResolverTests.cs XProtocol.Tests/TestDtos.cs XProtocol.Tests/RoundtripTests.cs
git commit -m "serializer: resolve nested DTOs with cycle and empty-class detection

EmptyDto registration is now rejected by the resolver; corresponding
roundtrip test is converted to a registration-rejection assertion.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 11: NestedShape — codec + roundtrip

Inline-pack a nested DTO's fields into the parent payload. Null nested → fresh `new T()`.

**Files:**
- Modify: `XProtocol/Serializator/ShapeCodec.cs`
- Modify: `XProtocol.Tests/ShapeCodecTests.cs`
- Modify: `XProtocol.Tests/TestDtos.cs`
- Create: `XProtocol.Tests/RoundtripNestedTests.cs`

- [ ] **Step 11.1: Write failing codec tests**

Append to `XProtocol.Tests/ShapeCodecTests.cs`:

```csharp
[Test]
public async Task WriteNested_Roundtrips()
{
    var shape = ShapeResolver.Resolve(typeof(NestedDtoNeedsFields), new HashSet<System.Type>());
    var dto = new NestedDtoNeedsFields { X = 999 };
    var bytes = ShapeCodec.WriteField(shape, dto);
    var reader = new ChunkReader(WrapAsPacket(bytes), 0);

    var back = (NestedDtoNeedsFields)ShapeCodec.ReadField(shape, reader);

    await Assert.That(back.X).IsEqualTo(999);
}

[Test]
public async Task WriteNested_NullInstance_BecomesDefault()
{
    var shape = ShapeResolver.Resolve(typeof(NestedDtoNeedsFields), new HashSet<System.Type>());
    var bytes = ShapeCodec.WriteField(shape, null);
    var reader = new ChunkReader(WrapAsPacket(bytes), 0);

    var back = (NestedDtoNeedsFields)ShapeCodec.ReadField(shape, reader);

    await Assert.That(back).IsNotNull();
    await Assert.That(back.X).IsEqualTo(0);
}
```

Add `using System.Collections.Generic;` to the file's using-block if it isn't already there, and ensure `NestedDtoNeedsFields` is visible (it is in `XProtocol.Tests` namespace because it lives in `FieldShapeResolverTests.cs`; both files share the namespace).

- [ ] **Step 11.2: Run, expect failure**

Expected: `"Unsupported shape: NestedShape"`.

- [ ] **Step 11.3: Add NestedShape codec dispatch**

Modify `XProtocol/Serializator/ShapeCodec.cs`. Add to both switches and add helpers:

```csharp
// In ReadField switch:
case NestedShape n:   return ReadNested(n, reader);

// In WriteFieldInto switch:
case NestedShape n:   WriteNested(ms, n, value); break;

private static void WriteNested(MemoryStream ms, NestedShape shape, object value)
{
    var instance = value ?? Activator.CreateInstance(shape.ClrType);
    foreach (var desc in shape.Fields)
    {
        WriteFieldInto(ms, desc.Shape, desc.Getter(instance));
    }
}

private static object ReadNested(NestedShape shape, ChunkReader reader)
{
    var instance = Activator.CreateInstance(shape.ClrType);
    foreach (var desc in shape.Fields)
    {
        desc.Setter(instance, ReadField(desc.Shape, reader));
    }
    return instance;
}
```

- [ ] **Step 11.4: Run codec tests, expect pass**

- [ ] **Step 11.5: Add roundtrip integration tests**

Append to `XProtocol.Tests/TestDtos.cs`:

```csharp
public class Address
{
    public string Street;
    public int Zip;
}

public class Person
{
    public string Name;
    public int Age;
    public Address Home;
}
```

Add registration:

```csharp
XPacketTypeManager.Register<Person>((XPacketType)330, 330, 0);
```

Create `XProtocol.Tests/RoundtripNestedTests.cs`:

```csharp
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using XProtocol;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    public class RoundtripNestedTests
    {
        [Test]
        public async Task Person_WithAddress_Roundtrips()
        {
            var dto = new Person
            {
                Name = "Alice",
                Age = 30,
                Home = new Address { Street = "Main St", Zip = 12345 }
            };
            var packet = XPacketConverter.Serialize(dto);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var back = XPacketConverter.Deserialize<Person>(parsed);

            await Assert.That(back.Name).IsEqualTo("Alice");
            await Assert.That(back.Age).IsEqualTo(30);
            await Assert.That(back.Home).IsNotNull();
            await Assert.That(back.Home.Street).IsEqualTo("Main St");
            await Assert.That(back.Home.Zip).IsEqualTo(12345);
        }

        [Test]
        public async Task Person_NullAddress_RoundtripsToDefault()
        {
            var dto = new Person { Name = "Bob", Age = 25, Home = null };
            var packet = XPacketConverter.Serialize(dto);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var back = XPacketConverter.Deserialize<Person>(parsed);

            await Assert.That(back.Home).IsNotNull();
            await Assert.That(back.Home.Street).IsEqualTo("");
            await Assert.That(back.Home.Zip).IsEqualTo(0);
        }
    }
}
```

- [ ] **Step 11.6: Run full suite, expect pass**

- [ ] **Step 11.7: Commit**

```
git add XProtocol/Serializator/ShapeCodec.cs XProtocol.Tests/ShapeCodecTests.cs XProtocol.Tests/RoundtripNestedTests.cs XProtocol.Tests/TestDtos.cs
git commit -m "serializer: add NestedShape codec

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 12: Recursion combinations

Stress-test recursive shape combinations end-to-end. By this point every shape is wired in and recursion follows naturally from `Resolve` and `WriteFieldInto` calling themselves; the goal here is to add coverage and catch any sneaky bug at shape boundaries.

**Files:**
- Modify: `XProtocol.Tests/TestDtos.cs`
- Create: `XProtocol.Tests/RoundtripRecursionTests.cs`

- [ ] **Step 12.1: Add recursion DTOs**

Append to `XProtocol.Tests/TestDtos.cs`:

```csharp
public class JaggedIntArrayDto
{
    public int[][] Rows;
}

public class ListOfIntArrayDto
{
    public System.Collections.Generic.List<int[]> Buckets;
}

public class ListOfListDto
{
    public System.Collections.Generic.List<System.Collections.Generic.List<string>> Pages;
}

public class DictOfListDto
{
    public System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<int>> Groups;
}

public class NestedWithCollectionsDto
{
    public string Title;
    public Person Owner;
    public System.Collections.Generic.List<Person> Members;
    public System.Collections.Generic.Dictionary<int, Address> Locations;
}
```

Add registrations:

```csharp
XPacketTypeManager.Register<JaggedIntArrayDto>((XPacketType)340, 340, 0);
XPacketTypeManager.Register<ListOfIntArrayDto>((XPacketType)341, 341, 0);
XPacketTypeManager.Register<ListOfListDto>((XPacketType)342, 342, 0);
XPacketTypeManager.Register<DictOfListDto>((XPacketType)343, 343, 0);
XPacketTypeManager.Register<NestedWithCollectionsDto>((XPacketType)344, 344, 0);
```

- [ ] **Step 12.2: Write recursion tests**

Create `XProtocol.Tests/RoundtripRecursionTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using XProtocol;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    public class RoundtripRecursionTests
    {
        [Test]
        public async Task JaggedIntArray_Roundtrips()
        {
            var dto = new JaggedIntArrayDto
            {
                Rows = new[] { new[] { 1, 2 }, new[] { 3, 4, 5 }, new int[0] }
            };
            var packet = XPacketConverter.Serialize(dto);
            var parsed = XPacket.Parse(packet.ToPacket());
            var back = XPacketConverter.Deserialize<JaggedIntArrayDto>(parsed);

            await Assert.That(back.Rows.Length).IsEqualTo(3);
            await Assert.That(back.Rows[0]).IsEquivalentTo(new[] { 1, 2 });
            await Assert.That(back.Rows[1]).IsEquivalentTo(new[] { 3, 4, 5 });
            await Assert.That(back.Rows[2].Length).IsEqualTo(0);
        }

        [Test]
        public async Task ListOfIntArray_Roundtrips()
        {
            var dto = new ListOfIntArrayDto
            {
                Buckets = new List<int[]> { new[] { 10, 20 }, new[] { 30 } }
            };
            var packet = XPacketConverter.Serialize(dto);
            var parsed = XPacket.Parse(packet.ToPacket());
            var back = XPacketConverter.Deserialize<ListOfIntArrayDto>(parsed);

            await Assert.That(back.Buckets.Count).IsEqualTo(2);
            await Assert.That(back.Buckets[0]).IsEquivalentTo(new[] { 10, 20 });
        }

        [Test]
        public async Task ListOfListOfString_Roundtrips()
        {
            var dto = new ListOfListDto
            {
                Pages = new List<List<string>>
                {
                    new List<string> { "a", "b" },
                    new List<string> { "c" }
                }
            };
            var packet = XPacketConverter.Serialize(dto);
            var parsed = XPacket.Parse(packet.ToPacket());
            var back = XPacketConverter.Deserialize<ListOfListDto>(parsed);

            await Assert.That(back.Pages.Count).IsEqualTo(2);
            await Assert.That(back.Pages[0]).IsEquivalentTo(new[] { "a", "b" });
            await Assert.That(back.Pages[1]).IsEquivalentTo(new[] { "c" });
        }

        [Test]
        public async Task DictOfList_Roundtrips()
        {
            var dto = new DictOfListDto
            {
                Groups = new Dictionary<string, List<int>>
                {
                    { "evens", new List<int> { 2, 4, 6 } },
                    { "odds",  new List<int> { 1, 3, 5 } }
                }
            };
            var packet = XPacketConverter.Serialize(dto);
            var parsed = XPacket.Parse(packet.ToPacket());
            var back = XPacketConverter.Deserialize<DictOfListDto>(parsed);

            await Assert.That(back.Groups.Count).IsEqualTo(2);
            await Assert.That(back.Groups["evens"]).IsEquivalentTo(new[] { 2, 4, 6 });
        }

        [Test]
        public async Task NestedWithCollections_NullMemberInList_NormalizesToDefault()
        {
            var dto = new NestedWithCollectionsDto
            {
                Title = "T",
                Owner = new Person { Name = "A", Age = 1, Home = new Address { Street = "S", Zip = 1 } },
                Members = new List<Person> { null, new Person { Name = "B", Age = 2, Home = null } },
                Locations = new Dictionary<int, Address>()
            };

            var packet = XPacketConverter.Serialize(dto);
            var parsed = XPacket.Parse(packet.ToPacket());
            var back = XPacketConverter.Deserialize<NestedWithCollectionsDto>(parsed);

            await Assert.That(back.Members.Count).IsEqualTo(2);
            await Assert.That(back.Members[0]).IsNotNull();
            await Assert.That(back.Members[0].Name).IsEqualTo("");
            await Assert.That(back.Members[1].Name).IsEqualTo("B");
        }

        [Test]
        public async Task NestedWithCollections_Roundtrips()
        {
            var dto = new NestedWithCollectionsDto
            {
                Title = "Project Apollo",
                Owner = new Person { Name = "Alice", Age = 40, Home = new Address { Street = "S1", Zip = 100 } },
                Members = new List<Person>
                {
                    new Person { Name = "Bob", Age = 30, Home = new Address { Street = "S2", Zip = 200 } },
                    new Person { Name = "Carol", Age = 35, Home = null }
                },
                Locations = new Dictionary<int, Address>
                {
                    { 1, new Address { Street = "HQ", Zip = 1000 } },
                    { 2, new Address { Street = "Branch", Zip = 2000 } }
                }
            };

            var packet = XPacketConverter.Serialize(dto);
            var parsed = XPacket.Parse(packet.ToPacket());
            var back = XPacketConverter.Deserialize<NestedWithCollectionsDto>(parsed);

            await Assert.That(back.Title).IsEqualTo("Project Apollo");
            await Assert.That(back.Owner.Name).IsEqualTo("Alice");
            await Assert.That(back.Members.Count).IsEqualTo(2);
            await Assert.That(back.Members[1].Name).IsEqualTo("Carol");
            await Assert.That(back.Members[1].Home.Street).IsEqualTo("");
            await Assert.That(back.Locations[2].Street).IsEqualTo("Branch");
        }
    }
}
```

- [ ] **Step 12.3: Run full suite, expect pass**

```
dotnet run --project XProtocol.Tests -c Debug
```

- [ ] **Step 12.4: Commit**

```
git add XProtocol.Tests/TestDtos.cs XProtocol.Tests/RoundtripRecursionTests.cs
git commit -m "tests: cover recursive shape combinations

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 13: Encrypted end-to-end roundtrip

Verify that `Encrypt()`/`Parse()` works with all new shapes, especially under large payloads that span many wire-fields.

**Files:**
- Modify: `XProtocol.Tests/RoundtripRecursionTests.cs`

- [ ] **Step 13.1: Write failing test**

Append to `XProtocol.Tests/RoundtripRecursionTests.cs`:

```csharp
[Test]
public async Task Encrypted_NestedWithCollections_Roundtrips()
{
    var dto = new NestedWithCollectionsDto
    {
        Title = "Encrypted Project",
        Owner = new Person { Name = "Alice", Age = 40, Home = new Address { Street = "S1", Zip = 100 } },
        Members = new List<Person>
        {
            new Person { Name = "Bob", Age = 30, Home = new Address { Street = "S2", Zip = 200 } }
        },
        Locations = new Dictionary<int, Address>
        {
            { 1, new Address { Street = "HQ", Zip = 1000 } }
        }
    };

    var packet = XPacketConverter.Serialize(dto);
    var encrypted = packet.Encrypt().ToPacket();
    var parsed = XPacket.Parse(encrypted);
    var back = XPacketConverter.Deserialize<NestedWithCollectionsDto>(parsed);

    await Assert.That(back.Title).IsEqualTo("Encrypted Project");
    await Assert.That(back.Owner.Name).IsEqualTo("Alice");
    await Assert.That(back.Members[0].Home.Street).IsEqualTo("S2");
    await Assert.That(back.Locations[1].Zip).IsEqualTo(1000);
}

[Test]
public async Task Encrypted_LargeJaggedArray_Roundtrips()
{
    var rows = Enumerable.Range(0, 20)
        .Select(i => Enumerable.Range(0, 5).Select(j => i * 100 + j).ToArray())
        .ToArray();
    var dto = new JaggedIntArrayDto { Rows = rows };

    var packet = XPacketConverter.Serialize(dto);
    var encrypted = packet.Encrypt().ToPacket();
    var parsed = XPacket.Parse(encrypted);
    var back = XPacketConverter.Deserialize<JaggedIntArrayDto>(parsed);

    await Assert.That(back.Rows.Length).IsEqualTo(20);
    await Assert.That(back.Rows[5][3]).IsEqualTo(503);
}
```

- [ ] **Step 13.2: Run, expect pass (no implementation changes — encryption path is unchanged)**

```
dotnet run --project XProtocol.Tests -c Debug -- --treenode-filter "/*/XProtocol.Tests/XProtocol.Tests/RoundtripRecursionTests"
```

Expected: pass. If failure, debug the encryption-roundtrip pipeline (likely a `Marshal.SizeOf` / `Activator.CreateInstance` edge case).

- [ ] **Step 13.3: Commit**

```
git add XProtocol.Tests/RoundtripRecursionTests.cs
git commit -m "tests: cover encrypted roundtrip with new shapes

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 14: Negative tests — collection overflow + truncation

Round out negative coverage at the system level (resolver-level negatives are already covered).

**Files:**
- Modify: `XProtocol.Tests/ShapeCodecTests.cs`

- [ ] **Step 14.1: Add overflow + truncation tests**

Append to `XProtocol.Tests/ShapeCodecTests.cs`:

```csharp
[Test]
public async Task WriteList_TooManyElements_Throws()
{
    var shape = new ListShape(typeof(int), new ValueShape(typeof(int)));
    var list = new System.Collections.Generic.List<int>();
    for (int i = 0; i < ushort.MaxValue + 1; i++) list.Add(i);

    var ex = await Assert.That(() => ShapeCodec.WriteField(shape, list))
        .ThrowsExactly<System.InvalidOperationException>();
    await Assert.That(ex.Message).Contains("exceeds 65535 elements");
}

[Test]
public async Task WriteDict_TooManyEntries_Throws()
{
    var shape = new DictShape(typeof(int), typeof(int),
        new ValueShape(typeof(int)), new ValueShape(typeof(int)));
    var dict = new System.Collections.Generic.Dictionary<int, int>();
    for (int i = 0; i < ushort.MaxValue + 1; i++) dict[i] = i;

    var ex = await Assert.That(() => ShapeCodec.WriteField(shape, dict))
        .ThrowsExactly<System.InvalidOperationException>();
    await Assert.That(ex.Message).Contains("exceeds 65535 elements");
}

[Test]
public async Task ReadArray_PayloadTruncated_Throws()
{
    var shape = new ArrayShape(typeof(int), new ValueShape(typeof(int)));
    // Forge a payload that claims count=2 but supplies only 4 bytes (one int).
    var payload = new byte[] { 0x02, 0x00, 0x01, 0x00, 0x00, 0x00 };
    var p = XPacket.Create(0, 0);
    p.AppendChunks(payload);
    var reader = new ChunkReader(p, 0);

    var ex = await Assert.That(() => ShapeCodec.ReadField(shape, reader))
        .ThrowsExactly<System.InvalidOperationException>();
    await Assert.That(ex.Message).Contains("payload truncated");
}
```

- [ ] **Step 14.2: Run, expect pass**

- [ ] **Step 14.3: Commit**

```
git add XProtocol.Tests/ShapeCodecTests.cs
git commit -m "tests: cover overflow and truncation paths

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 15: Negative tests — unsupported containers

`HashSet<T>`, `Queue<T>`, `IEnumerable<T>` etc. must throw at registration time.

**Files:**
- Modify: `XProtocol.Tests/FieldShapeResolverTests.cs`

- [ ] **Step 15.1: Add tests**

Append to `XProtocol.Tests/FieldShapeResolverTests.cs`:

```csharp
[Test]
public async Task Resolve_HashSet_Throws()
{
    var ex = await Assert.That(() => ShapeResolver.Resolve(
            typeof(System.Collections.Generic.HashSet<int>),
            new HashSet<System.Type>()))
        .ThrowsExactly<System.InvalidOperationException>();
    await Assert.That(ex.Message).Contains("is not supported");
}

[Test]
public async Task Resolve_Queue_Throws()
{
    var ex = await Assert.That(() => ShapeResolver.Resolve(
            typeof(System.Collections.Generic.Queue<int>),
            new HashSet<System.Type>()))
        .ThrowsExactly<System.InvalidOperationException>();
    await Assert.That(ex.Message).Contains("is not supported");
}

[Test]
public async Task Resolve_IEnumerable_Throws()
{
    var ex = await Assert.That(() => ShapeResolver.Resolve(
            typeof(System.Collections.Generic.IEnumerable<int>),
            new HashSet<System.Type>()))
        .ThrowsExactly<System.InvalidOperationException>();
    await Assert.That(ex.Message).Contains("is not supported");
}

[Test]
public async Task Resolve_IList_Throws()
{
    var ex = await Assert.That(() => ShapeResolver.Resolve(
            typeof(System.Collections.Generic.IList<int>),
            new HashSet<System.Type>()))
        .ThrowsExactly<System.InvalidOperationException>();
    await Assert.That(ex.Message).Contains("is not supported");
}

[Test]
public async Task Resolve_Object_Throws()
{
    var ex = await Assert.That(() => ShapeResolver.Resolve(typeof(object), new HashSet<System.Type>()))
        .ThrowsExactly<System.InvalidOperationException>();
    // 'object' has a public parameterless ctor and zero fields → caught by the empty-class branch.
    await Assert.That(ex.Message).Contains("at least one serialisable field");
}
```

Note on the `object` case: `typeof(object)` does have a public parameterless ctor, so the empty-class branch is what catches it. The substring assertion verifies that branch fires.

- [ ] **Step 15.2: Run, expect pass**

```
dotnet run --project XProtocol.Tests -c Debug -- --treenode-filter "/*/XProtocol.Tests/XProtocol.Tests/FieldShapeResolverTests"
```

- [ ] **Step 15.3: Commit**

```
git add XProtocol.Tests/FieldShapeResolverTests.cs
git commit -m "tests: cover unsupported container rejection paths

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 16: Smoke test — Test/Program.cs still works

The smoke project (`Test/Program.cs`) was not modified; it should continue to work with the new infrastructure. Verify.

- [ ] **Step 16.1: Build + run smoke project**

```
dotnet build Test/Test.csproj -c Debug
dotnet run --project Test -c Debug
```

Expected output (after pressing Enter to dismiss the `Console.ReadLine`):

```
TestNumber=12345, TestDouble=3.14, TestBoolean=True, TestString.Length=706
TestString matches: True
```

If the output is correct, nothing more to do for this task — no commit needed.

If output is wrong, investigate — the wire format for value-types + a single big string must be byte-identical to the previous implementation.

---

## Task 17: Final sweep — full suite + merge

- [ ] **Step 17.1: Full test pass**

```
dotnet run --project XProtocol.Tests -c Debug
```

Expected: every test passes. Count should be the original 68 + new tests added throughout this plan (approximately 50+ new tests).

- [ ] **Step 17.2: Run twice to check for flakiness**

```
dotnet run --project XProtocol.Tests -c Debug
dotnet run --project XProtocol.Tests -c Debug
```

Both runs should be green. If a test is flaky, investigate before merging.

- [ ] **Step 17.3: Switch to master and merge**

```
git checkout master
git merge --no-ff feature/xprotocol-collections-support -m "Merge branch 'feature/xprotocol-collections-support'

Adds T[], List<T>, Dictionary<K,V>, and nested DTO support via a
recursive FieldShape model. Wire format is byte-identical for DTOs
that use only value-types and strings; new shapes use ushort
count-prefixed payloads chunked through XPacket.AppendChunks.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 17.4: Run tests once more on master**

```
dotnet run --project XProtocol.Tests -c Debug
```

- [ ] **Step 17.5: Confirm clean state**

```
git status
git log --oneline -5
```

Expected: working tree matches master post-merge; merge commit is the most recent.

Do **not** push.

---
