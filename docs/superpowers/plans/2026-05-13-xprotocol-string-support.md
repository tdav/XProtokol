# XProtocol String Field Support Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow `string` fields in XProtocol DTOs without changing wire-format magic bytes or trailer, supporting up to 65535 UTF-8 bytes per string via chunked length-prefixed encoding.

**Architecture:** A `string` descriptor logically serializes into a `ushort` little-endian length prefix followed by UTF-8 bytes; that combined payload is split across one or more 255-byte wire fields. Deserialize walks descriptors, consuming exactly one wire field per value-type and 1..N wire fields per string descriptor (driven by the length prefix in the first chunk). `XPacket.AppendValue` stays value-type-only; a new `AppendChunks` helper handles the byte-level split.

**Tech Stack:** .NET 10 / C# (`XProtocol.csproj`), TUnit (`XProtocol.Tests.csproj`, asserts use `await Assert.That(...)`).

**Spec reference:** [`docs/superpowers/specs/2026-05-13-xprotocol-string-support-design.md`](../specs/2026-05-13-xprotocol-string-support-design.md)

---

## File Structure

| Path | Role |
|------|------|
| `XProtocol/Serializator/FieldDescriptor.cs` | Add `FieldKind` enum, typed string getter/setter, ctor validation |
| `XProtocol/XPacketTypeManager.cs` | Remove blanket value-type rejection; rely on descriptor ctor |
| `XProtocol/XPacket.cs` | Add internal `AppendChunks(byte[])` and `GetRawAt(int)` helpers |
| `XProtocol/Serializator/XPacketConverter.cs` | Serialize/Deserialize dispatch by `FieldKind`; walker invariant |
| `XProtocol.Tests/TestDtos.cs` | Add `StringDto`, `MultiStringDto`, `UnsupportedRefDto`; fix `BadDtoWithReferenceField` to use a truly unsupported ref-type |
| `XProtocol.Tests/RegistrationTests.cs` | Add unsupported-ref-type test (string DTO must register) |
| `XProtocol.Tests/RoundtripTests.cs` | Add full positive/negative roundtrip matrix |
| `Test/Program.cs` | Update `Console.WriteLine` to validate TestString roundtrip |

---

## Important Codebase Conventions

- **No `_` prefix on private fields.** Use `private readonly Type fieldName;` and `this.fieldName`.
- **Identifiers / comments / commit messages: English. Responses to user: Russian.**
- **TUnit asserts** look like `await Assert.That(actual).IsEqualTo(expected)` / `await Assert.That(() => action()).ThrowsExactly<T>()` / `await Assert.That(msg).Contains("substr")`. Do not introduce xUnit or NUnit syntax.
- **Error message preserves substring `"only value-type fields"`** so the existing assertion in `RegistrationTests.Register_RejectsReferenceTypeField` keeps matching without amendment.
- **No skipping tests; no `--no-build`/`--no-verify` shortcuts.**
- **Commit at the end of each task.** One green test (or batch) = one commit.

---

## Task 1: Add `FieldKind` enum and extend `FieldDescriptor`

**Files:**
- Modify: `XProtocol/Serializator/FieldDescriptor.cs` (full rewrite of the class)

- [ ] **Step 1: Write the failing test** (file: `XProtocol.Tests/FieldDescriptorTests.cs`, new file)

```csharp
using System;
using System.Reflection;
using TUnit.Core;
using XProtocol.Serializator;
using XProtocol.Tests;

namespace XProtocol.Tests
{
    public class FieldDescriptorTests
    {
        [Test]
        public async Task Descriptor_ForValueTypeField_HasValueTypeKind()
        {
            var f = typeof(SimpleDto).GetField(nameof(SimpleDto.A), BindingFlags.Instance | BindingFlags.Public);
            var d = new FieldDescriptor(f);

            await Assert.That(d.Kind).IsEqualTo(FieldKind.ValueType);
            await Assert.That(d.Getter).IsNotNull();
            await Assert.That(d.Setter).IsNotNull();
        }

        [Test]
        public async Task Descriptor_ForStringField_HasStringKind()
        {
            var f = typeof(StringDto).GetField(nameof(StringDto.S), BindingFlags.Instance | BindingFlags.Public);
            var d = new FieldDescriptor(f);

            await Assert.That(d.Kind).IsEqualTo(FieldKind.String);
            await Assert.That(d.StringGetter).IsNotNull();
            await Assert.That(d.StringSetter).IsNotNull();
        }

        [Test]
        public async Task Descriptor_ForUnsupportedRefType_Throws()
        {
            var f = typeof(BadDtoWithReferenceField).GetField(
                nameof(BadDtoWithReferenceField.Bad),
                BindingFlags.Instance | BindingFlags.Public);

            var ex = await Assert.That(() => new FieldDescriptor(f))
                .ThrowsExactly<InvalidOperationException>();

            await Assert.That(ex.Message).Contains("only value-type fields");
        }

        [Test]
        public async Task StringGetterSetter_RoundtripsValue()
        {
            var f = typeof(StringDto).GetField(nameof(StringDto.S), BindingFlags.Instance | BindingFlags.Public);
            var d = new FieldDescriptor(f);

            var obj = new StringDto();
            d.StringSetter(obj, "hello");

            await Assert.That(d.StringGetter(obj)).IsEqualTo("hello");
        }
    }
}
```

This test depends on `StringDto` and `BadDtoWithReferenceField` having a non-string-but-unsupported `Bad` field. Those changes are in Task 2. **Order matters:** complete Task 2 first, then return here and run this test. If executing in order, write this test file now but leave it failing until Task 2 lands DTOs.

- [ ] **Step 2: Replace `FieldDescriptor.cs` contents**

```csharp
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace XProtocol.Serializator
{
    internal enum FieldKind
    {
        ValueType,
        String
    }

    internal sealed class FieldDescriptor
    {
        public FieldInfo Field { get; }
        public FieldKind Kind { get; }
        public Func<object, object> Getter { get; }
        public Action<object, object> Setter { get; }
        public Func<object, string> StringGetter { get; }
        public Action<object, string> StringSetter { get; }

        public FieldDescriptor(FieldInfo field)
        {
            this.Field = field;

            if (field.FieldType == typeof(string))
            {
                this.Kind = FieldKind.String;
                this.StringGetter = BuildStringGetter(field);
                this.StringSetter = BuildStringSetter(field);
            }
            else if (field.FieldType.IsValueType)
            {
                this.Kind = FieldKind.ValueType;
                this.Getter = BuildGetter(field);
                this.Setter = BuildSetter(field);
            }
            else
            {
                throw new InvalidOperationException(
                    $"{field.DeclaringType.Name}.{field.Name}: only value-type fields and string are supported (got {field.FieldType.Name}).");
            }
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

        private static Func<object, string> BuildStringGetter(FieldInfo f)
        {
            var p = Expression.Parameter(typeof(object), "o");
            var body = Expression.Field(Expression.Convert(p, f.DeclaringType), f);
            return Expression.Lambda<Func<object, string>>(body, p).Compile();
        }

        private static Action<object, string> BuildStringSetter(FieldInfo f)
        {
            var p = Expression.Parameter(typeof(object), "o");
            var v = Expression.Parameter(typeof(string), "v");
            var body = Expression.Assign(
                Expression.Field(Expression.Convert(p, f.DeclaringType), f), v);
            return Expression.Lambda<Action<object, string>>(body, p, v).Compile();
        }
    }
}
```

- [ ] **Step 3: Build to confirm no compile errors**

Run: `dotnet build XProtocol/XProtocol.csproj`
Expected: build succeeds (test project may still error if DTOs missing — that's OK; Task 2 fixes it).

- [ ] **Step 4: Commit**

```bash
git add XProtocol/Serializator/FieldDescriptor.cs XProtocol.Tests/FieldDescriptorTests.cs
git commit -m "Refactor FieldDescriptor with FieldKind enum and string support"
```

---

## Task 2: Update `TestDtos.cs` — add new DTOs, fix `BadDtoWithReferenceField`

**Files:**
- Modify: `XProtocol.Tests/TestDtos.cs`

The existing `BadDtoWithReferenceField` uses `string Bad` — but with this feature `string` becomes valid. Replace it with a truly unsupported reference type (`int[]`) so `RegistrationTests.Register_RejectsReferenceTypeField` keeps testing the correct invariant.

- [ ] **Step 1: Replace `TestDtos.cs` contents**

```csharp
namespace XProtocol.Tests
{
    public class SimpleDto
    {
        public int A;
        public double B;
        public bool C;
    }

    public class EmptyDto
    {
    }

    public class BadDtoWithReferenceField
    {
        public int A;
        public int[] Bad;
    }

    public class StringDto
    {
        public int A;
        public string S;
        public bool B;
    }

    public class MultiStringDto
    {
        public string First;
        public int Middle;
        public string Last;
    }

    public class UnsupportedRefDto
    {
        public int A;
        public object Bad;
    }
}
```

- [ ] **Step 2: Build the test project**

Run: `dotnet build XProtocol.Tests/XProtocol.Tests.csproj`
Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add XProtocol.Tests/TestDtos.cs
git commit -m "Add StringDto/MultiStringDto/UnsupportedRefDto and switch BadDto field to int[]"
```

---

## Task 3: Remove blanket value-type rejection from `XPacketTypeManager`

**Files:**
- Modify: `XProtocol/XPacketTypeManager.cs`

- [ ] **Step 1: Run `FieldDescriptorTests` (from Task 1) to verify it now passes**

Run: `dotnet test XProtocol.Tests/XProtocol.Tests.csproj --filter "FullyQualifiedName~FieldDescriptorTests" -v normal`
Expected: all 4 tests in `FieldDescriptorTests` pass (DTOs now exist).

- [ ] **Step 2: Run existing tests to confirm baseline still green**

Run: `dotnet test XProtocol.Tests/XProtocol.Tests.csproj -v normal`
Expected: existing `RegistrationTests.Register_RejectsReferenceTypeField` still passes because `BadDtoWithReferenceField.Bad` is now `int[]` and rejected by descriptor ctor with a message containing `"only value-type fields"`.

If any test fails, stop and investigate; do not proceed.

- [ ] **Step 3: Modify `BuildDescriptors` in `XProtocol/XPacketTypeManager.cs`**

Replace the validation `foreach` (lines 99–106 of the current file) with just the count check. New `BuildDescriptors` body:

```csharp
private static FieldDescriptor[] BuildDescriptors(Type t)
{
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

    return sorted.Select(f => new FieldDescriptor(f)).ToArray();
}
```

The per-field validation now happens inside `new FieldDescriptor(f)` — unsupported types throw from there.

- [ ] **Step 4: Run the full test suite**

Run: `dotnet test XProtocol.Tests/XProtocol.Tests.csproj -v normal`
Expected: every existing test still green (Roundtrip, Registration, StrictCount, XPacket, etc.).

- [ ] **Step 5: Commit**

```bash
git add XProtocol/XPacketTypeManager.cs
git commit -m "Delegate field-type validation from XPacketTypeManager to FieldDescriptor ctor"
```

---

## Task 4: Add low-level wire helpers — `XPacket.AppendChunks` and `XPacket.GetRawAt`

**Files:**
- Modify: `XProtocol/XPacket.cs`
- Test: `XProtocol.Tests/XPacketChunkTests.cs` (new file)

- [ ] **Step 1: Write the failing tests** (new file `XProtocol.Tests/XPacketChunkTests.cs`)

```csharp
using System;
using TUnit.Core;
using XProtocol;

namespace XProtocol.Tests
{
    public class XPacketChunkTests
    {
        [Test]
        public async Task AppendChunks_PayloadShorterThan255_ProducesOneField()
        {
            var p = XPacket.Create(1, 1);
            var payload = new byte[10];
            for (int i = 0; i < payload.Length; i++) payload[i] = (byte)i;

            p.AppendChunks(payload);

            await Assert.That(p.Fields.Count).IsEqualTo(1);
            await Assert.That(p.Fields[0].FieldSize).IsEqualTo((byte)10);
            await Assert.That(p.Fields[0].Contents).IsEquivalentTo(payload);
        }

        [Test]
        public async Task AppendChunks_PayloadExactly255_ProducesOneField()
        {
            var p = XPacket.Create(1, 1);
            var payload = new byte[255];

            p.AppendChunks(payload);

            await Assert.That(p.Fields.Count).IsEqualTo(1);
            await Assert.That(p.Fields[0].FieldSize).IsEqualTo((byte)255);
        }

        [Test]
        public async Task AppendChunks_PayloadAcross255_ProducesTwoFields()
        {
            var p = XPacket.Create(1, 1);
            var payload = new byte[256];

            p.AppendChunks(payload);

            await Assert.That(p.Fields.Count).IsEqualTo(2);
            await Assert.That(p.Fields[0].FieldSize).IsEqualTo((byte)255);
            await Assert.That(p.Fields[1].FieldSize).IsEqualTo((byte)1);
        }

        [Test]
        public async Task AppendChunks_Payload902Bytes_ProducesFourFields()
        {
            var p = XPacket.Create(1, 1);
            var payload = new byte[902];

            p.AppendChunks(payload);

            await Assert.That(p.Fields.Count).IsEqualTo(4);
            await Assert.That(p.Fields[0].FieldSize).IsEqualTo((byte)255);
            await Assert.That(p.Fields[1].FieldSize).IsEqualTo((byte)255);
            await Assert.That(p.Fields[2].FieldSize).IsEqualTo((byte)255);
            await Assert.That(p.Fields[3].FieldSize).IsEqualTo((byte)137);
        }

        [Test]
        public async Task GetRawAt_ReturnsContents()
        {
            var p = XPacket.Create(1, 1);
            var payload = new byte[] { 9, 8, 7 };
            p.AppendChunks(payload);

            var raw = p.GetRawAt(0);

            await Assert.That(raw).IsEquivalentTo(payload);
        }

        [Test]
        public async Task GetRawAt_OutOfRange_Throws()
        {
            var p = XPacket.Create(1, 1);

            await Assert.That(() => p.GetRawAt(0))
                .ThrowsExactly<ArgumentOutOfRangeException>();
        }
    }
}
```

Note: `AppendChunks` and `GetRawAt` are `internal`. Verify `XProtocol.csproj` exposes internals via `InternalsVisibleTo("XProtocol.Tests")`. If not, add it (see Step 2a).

- [ ] **Step 2a: Verify `InternalsVisibleTo` (one-time check)**

Run: `dotnet test XProtocol.Tests/XProtocol.Tests.csproj --filter "FullyQualifiedName~XPacketChunkTests" -v normal`
Expected: compile error referring to `AppendChunks`/`GetRawAt` being inaccessible.

If error mentions `internal` accessibility: open `XProtocol/XProtocol.csproj` (or create `XProtocol/AssemblyInfo.cs` if cleaner) and add:

```csharp
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("XProtocol.Tests")]
```

If `InternalsVisibleTo` is already present (search for it in `XProtocol/`), skip.

- [ ] **Step 2b: Run the test to confirm it now fails for the right reason**

Run: `dotnet test XProtocol.Tests/XProtocol.Tests.csproj --filter "FullyQualifiedName~XPacketChunkTests" -v normal`
Expected: tests fail because `AppendChunks` / `GetRawAt` don't exist on `XPacket`.

- [ ] **Step 3: Add methods to `XPacket.cs`**

Insert directly after the existing `AppendValue` method (which ends near line 56):

```csharp
internal void AppendChunks(byte[] payload)
{
    if (payload == null)
    {
        throw new ArgumentNullException(nameof(payload));
    }

    int offset = 0;
    while (offset < payload.Length)
    {
        int size = Math.Min(byte.MaxValue, payload.Length - offset);
        var chunk = new byte[size];
        Buffer.BlockCopy(payload, offset, chunk, 0, size);
        Fields.Add(new XPacketField
        {
            FieldSize = (byte)size,
            Contents = chunk
        });
        offset += size;
    }
}

internal byte[] GetRawAt(int index)
{
    if (index < 0 || index >= Fields.Count)
    {
        throw new ArgumentOutOfRangeException(nameof(index));
    }
    return Fields[index].Contents ?? Array.Empty<byte>();
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test XProtocol.Tests/XProtocol.Tests.csproj --filter "FullyQualifiedName~XPacketChunkTests" -v normal`
Expected: all 6 tests pass.

- [ ] **Step 5: Commit**

```bash
git add XProtocol/XPacket.cs XProtocol.Tests/XPacketChunkTests.cs
# include InternalsVisibleTo file/edit if it was changed in Step 2a
git commit -m "Add XPacket.AppendChunks and XPacket.GetRawAt for chunked payloads"
```

---

## Task 5: Wire `XPacketConverter` Serialize/Deserialize for strings — short string roundtrip

**Files:**
- Modify: `XProtocol/Serializator/XPacketConverter.cs`
- Add tests: `XProtocol.Tests/RoundtripTests.cs` (extend; no new file yet)

This task lands the full serialize-and-deserialize walker behind a short-string smoke test. Boundary and long-string tests come in later tasks.

- [ ] **Step 1: Add a TUnit-friendly registration helper to `RoundtripTests.cs`**

If tests share a single static `XPacketTypeManager`, they can step on each other. Use unique `XPacketType` numeric casts per DTO. Add this helper at the top of the existing `RoundtripTests` class so all later tests can call it:

```csharp
private static readonly object registrationLock = new object();
private static bool stringDtoRegistered;
private static bool multiStringDtoRegistered;

private static void EnsureStringDtoRegistered()
{
    lock (registrationLock)
    {
        if (stringDtoRegistered) return;
        XPacketTypeManager.Register<StringDto>((XPacketType)100, 100, 0);
        stringDtoRegistered = true;
    }
}

private static void EnsureMultiStringDtoRegistered()
{
    lock (registrationLock)
    {
        if (multiStringDtoRegistered) return;
        XPacketTypeManager.Register<MultiStringDto>((XPacketType)101, 101, 0);
        multiStringDtoRegistered = true;
    }
}
```

- [ ] **Step 2: Write the failing test** — append inside the `RoundtripTests` class:

```csharp
[Test]
public async Task StringDto_RoundtripShortAscii()
{
    EnsureStringDtoRegistered();

    var original = new StringDto { A = 7, S = "hello", B = true };

    var packet = XPacketConverter.Serialize(original);
    var bytes = packet.ToPacket();
    var parsed = XPacket.Parse(bytes);
    await Assert.That(parsed).IsNotNull();

    var restored = XPacketConverter.Deserialize<StringDto>(parsed);

    await Assert.That(restored.A).IsEqualTo(original.A);
    await Assert.That(restored.S).IsEqualTo(original.S);
    await Assert.That(restored.B).IsEqualTo(original.B);
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test XProtocol.Tests/XProtocol.Tests.csproj --filter "FullyQualifiedName~StringDto_RoundtripShortAscii" -v normal`
Expected: fail — `XPacketConverter.Serialize` calls `AppendValue` on a `string`, which throws `ArgumentException("Only value types are supported.")`.

- [ ] **Step 4: Replace `XPacketConverter.cs` contents**

```csharp
using System;
using System.Text;

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
                if (desc.Kind == FieldKind.ValueType)
                {
                    packet.AppendValue(desc.Getter(obj));
                }
                else
                {
                    var s = desc.StringGetter(obj) ?? string.Empty;
                    var utf8 = Encoding.UTF8.GetBytes(s);

                    if (utf8.Length > ushort.MaxValue)
                    {
                        throw new InvalidOperationException(
                            $"{typeof(T).Name}.{desc.Field.Name}: string exceeds {ushort.MaxValue} UTF-8 bytes (actual: {utf8.Length}).");
                    }

                    var payload = new byte[utf8.Length + 2];
                    payload[0] = (byte)(utf8.Length & 0xFF);
                    payload[1] = (byte)((utf8.Length >> 8) & 0xFF);
                    Buffer.BlockCopy(utf8, 0, payload, 2, utf8.Length);
                    packet.AppendChunks(payload);
                }
            }

            if (packet.Fields.Count > byte.MaxValue)
            {
                throw new InvalidOperationException(
                    $"{typeof(T).Name}: packet exceeds {byte.MaxValue} wire fields (actual: {packet.Fields.Count}). Reduce string field sizes.");
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
            int wireIdx = 0;

            foreach (var desc in descriptors)
            {
                if (desc.Kind == FieldKind.ValueType)
                {
                    if (wireIdx >= packet.Fields.Count)
                    {
                        throw new InvalidOperationException(
                            $"Field count mismatch for {typeof(T).Name}: expected {descriptors.Length}, got {packet.Fields.Count}.");
                    }
                    var raw = packet.GetValueAt(wireIdx, desc.Field.FieldType);
                    desc.Setter(instance, raw);
                    wireIdx++;
                }
                else
                {
                    if (wireIdx >= packet.Fields.Count)
                    {
                        throw new InvalidOperationException(
                            $"Field count mismatch for {typeof(T).Name}: expected {descriptors.Length}, got {packet.Fields.Count}.");
                    }
                    var first = packet.GetRawAt(wireIdx++);
                    if (first.Length < 2)
                    {
                        throw new InvalidOperationException(
                            $"{typeof(T).Name}.{desc.Field.Name}: string header truncated (first chunk size {first.Length} < 2).");
                    }
                    int len = first[0] | (first[1] << 8);

                    var acc = new byte[len];
                    int filled = 0;
                    int firstPayload = Math.Min(first.Length - 2, len);
                    Buffer.BlockCopy(first, 2, acc, 0, firstPayload);
                    filled += firstPayload;

                    while (filled < len)
                    {
                        if (wireIdx >= packet.Fields.Count)
                        {
                            throw new InvalidOperationException(
                                $"{typeof(T).Name}.{desc.Field.Name}: string truncated (need {len} bytes, have {filled} after consuming all remaining wire fields).");
                        }
                        var next = packet.GetRawAt(wireIdx++);
                        int take = Math.Min(next.Length, len - filled);
                        Buffer.BlockCopy(next, 0, acc, filled, take);
                        filled += take;
                    }

                    var str = Encoding.UTF8.GetString(acc);
                    desc.StringSetter(instance, str);
                }
            }

            if (wireIdx != packet.Fields.Count)
            {
                throw new InvalidOperationException(
                    $"Field count mismatch for {typeof(T).Name}: expected {wireIdx}, got {packet.Fields.Count}.");
            }

            return instance;
        }
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test XProtocol.Tests/XProtocol.Tests.csproj --filter "FullyQualifiedName~StringDto_RoundtripShortAscii" -v normal`
Expected: pass.

- [ ] **Step 6: Run the full test suite to confirm no regression**

Run: `dotnet test XProtocol.Tests/XProtocol.Tests.csproj -v normal`
Expected: every test green, including `StrictCountTests.Deserialize_FieldCountMismatch_Throws` (it removes a wire field from `SimpleDto`, walker still produces `expected 3, got 2`).

If `StrictCountTests` fails: open it, check that the test removes a field then deserializes. The walker is supposed to hit `wireIdx >= packet.Fields.Count` on the 3rd value-type descriptor. The error path uses `descriptors.Length` which equals 3. The assertion is `Contains("expected 3")` and `Contains("got 2")` — should match. If it doesn't, **stop and reconcile the message before continuing**.

- [ ] **Step 7: Commit**

```bash
git add XProtocol/Serializator/XPacketConverter.cs XProtocol.Tests/RoundtripTests.cs
git commit -m "Add chunked string serialization with walker-based deserialize"
```

---

## Task 6: Boundary roundtrip tests — empty, null, 253-byte, 254-byte, 510-byte

**Files:**
- Modify: `XProtocol.Tests/RoundtripTests.cs`

- [ ] **Step 1: Append the tests** to the `RoundtripTests` class:

```csharp
[Test]
public async Task StringDto_Roundtrip_EmptyString()
{
    EnsureStringDtoRegistered();

    var original = new StringDto { A = 1, S = "", B = false };
    var packet = XPacketConverter.Serialize(original);

    await Assert.That(packet.Fields.Count).IsEqualTo(3);
    await Assert.That(packet.Fields[1].FieldSize).IsEqualTo((byte)2);

    var bytes = packet.ToPacket();
    var parsed = XPacket.Parse(bytes);
    var restored = XPacketConverter.Deserialize<StringDto>(parsed);

    await Assert.That(restored.S).IsEqualTo("");
}

[Test]
public async Task StringDto_Roundtrip_NullString_NormalizesToEmpty()
{
    EnsureStringDtoRegistered();

    var original = new StringDto { A = 1, S = null, B = true };
    var packet = XPacketConverter.Serialize(original);
    var bytes = packet.ToPacket();
    var parsed = XPacket.Parse(bytes);

    var restored = XPacketConverter.Deserialize<StringDto>(parsed);

    await Assert.That(restored.S).IsEqualTo("");
}

[Test]
public async Task StringDto_Roundtrip_253ByteAscii_SingleChunk()
{
    EnsureStringDtoRegistered();

    var s = new string('a', 253);
    var original = new StringDto { A = 2, S = s, B = false };
    var packet = XPacketConverter.Serialize(original);

    await Assert.That(packet.Fields.Count).IsEqualTo(3);
    await Assert.That(packet.Fields[1].FieldSize).IsEqualTo((byte)255);

    var bytes = packet.ToPacket();
    var parsed = XPacket.Parse(bytes);
    var restored = XPacketConverter.Deserialize<StringDto>(parsed);

    await Assert.That(restored.S).IsEqualTo(s);
}

[Test]
public async Task StringDto_Roundtrip_254ByteAscii_TwoChunks()
{
    EnsureStringDtoRegistered();

    var s = new string('a', 254);
    var original = new StringDto { A = 3, S = s, B = false };
    var packet = XPacketConverter.Serialize(original);

    await Assert.That(packet.Fields.Count).IsEqualTo(4);
    await Assert.That(packet.Fields[1].FieldSize).IsEqualTo((byte)255);
    await Assert.That(packet.Fields[2].FieldSize).IsEqualTo((byte)1);

    var bytes = packet.ToPacket();
    var parsed = XPacket.Parse(bytes);
    var restored = XPacketConverter.Deserialize<StringDto>(parsed);

    await Assert.That(restored.S).IsEqualTo(s);
}

[Test]
public async Task StringDto_Roundtrip_510ByteAscii_ThreeChunks()
{
    EnsureStringDtoRegistered();

    var s = new string('a', 510);
    var original = new StringDto { A = 4, S = s, B = false };
    var packet = XPacketConverter.Serialize(original);

    await Assert.That(packet.Fields.Count).IsEqualTo(5);

    var bytes = packet.ToPacket();
    var parsed = XPacket.Parse(bytes);
    var restored = XPacketConverter.Deserialize<StringDto>(parsed);

    await Assert.That(restored.S).IsEqualTo(s);
}
```

- [ ] **Step 2: Run the new tests**

Run: `dotnet test XProtocol.Tests/XProtocol.Tests.csproj --filter "FullyQualifiedName~StringDto_Roundtrip_" -v normal`
Expected: all 5 new tests pass.

- [ ] **Step 3: Commit**

```bash
git add XProtocol.Tests/RoundtripTests.cs
git commit -m "Add string roundtrip boundary tests (empty, null, 253, 254, 510 bytes)"
```

---

## Task 7: Long-string and multi-byte tests — TestPacket size, UTF-8, emoji

**Files:**
- Modify: `XProtocol.Tests/RoundtripTests.cs`

- [ ] **Step 1: Append tests**

```csharp
[Test]
public async Task StringDto_Roundtrip_900ByteAscii_FourStringChunks()
{
    EnsureStringDtoRegistered();

    var s = new string('x', 900);
    var original = new StringDto { A = 5, S = s, B = true };
    var packet = XPacketConverter.Serialize(original);

    // 1 (int) + ceil((900+2)/255) = 1 + 4 = 5 string-related fields; plus bool = 6 total
    await Assert.That(packet.Fields.Count).IsEqualTo(6);

    var bytes = packet.ToPacket();
    var parsed = XPacket.Parse(bytes);
    var restored = XPacketConverter.Deserialize<StringDto>(parsed);

    await Assert.That(restored.S).IsEqualTo(s);
    await Assert.That(restored.A).IsEqualTo(5);
    await Assert.That(restored.B).IsEqualTo(true);
}

[Test]
public async Task StringDto_Roundtrip_CyrillicMultiByteUtf8()
{
    EnsureStringDtoRegistered();

    var s = "привет мир";
    var original = new StringDto { A = 6, S = s, B = false };
    var packet = XPacketConverter.Serialize(original);
    var bytes = packet.ToPacket();
    var parsed = XPacket.Parse(bytes);

    var restored = XPacketConverter.Deserialize<StringDto>(parsed);

    await Assert.That(restored.S).IsEqualTo(s);
}

[Test]
public async Task StringDto_Roundtrip_EmojiFourByteUtf8()
{
    EnsureStringDtoRegistered();

    var s = "abc 🚀 xyz";
    var original = new StringDto { A = 7, S = s, B = false };
    var packet = XPacketConverter.Serialize(original);
    var bytes = packet.ToPacket();
    var parsed = XPacket.Parse(bytes);

    var restored = XPacketConverter.Deserialize<StringDto>(parsed);

    await Assert.That(restored.S).IsEqualTo(s);
}

[Test]
public async Task StringDto_Roundtrip_16000ByteAscii_FitsInWireCap()
{
    EnsureStringDtoRegistered();

    var s = new string('x', 16000);
    var original = new StringDto { A = 8, S = s, B = false };
    var packet = XPacketConverter.Serialize(original);

    // 16002 / 255 = 62.75 → 63 string chunks; plus int + bool = 65 total wire fields (< 255)
    await Assert.That(packet.Fields.Count).IsLessThanOrEqualTo((int)byte.MaxValue);

    var bytes = packet.ToPacket();
    var parsed = XPacket.Parse(bytes);
    var restored = XPacketConverter.Deserialize<StringDto>(parsed);

    await Assert.That(restored.S).IsEqualTo(s);
}
```

- [ ] **Step 2: Run the new tests**

Run: `dotnet test XProtocol.Tests/XProtocol.Tests.csproj --filter "FullyQualifiedName~StringDto_Roundtrip_" -v normal`
Expected: all (existing + 4 new) pass.

- [ ] **Step 3: Commit**

```bash
git add XProtocol.Tests/RoundtripTests.cs
git commit -m "Add long-string and multi-byte UTF-8 roundtrip tests"
```

---

## Task 8: Multi-string DTO test — walker handles consecutive string descriptors

**Files:**
- Modify: `XProtocol.Tests/RoundtripTests.cs`

- [ ] **Step 1: Append the test**

```csharp
[Test]
public async Task MultiStringDto_Roundtrip_PreservesBothStrings()
{
    EnsureMultiStringDtoRegistered();

    var original = new MultiStringDto
    {
        First = "alpha",
        Middle = 42,
        Last = new string('y', 300)  // multi-chunk
    };

    var packet = XPacketConverter.Serialize(original);
    var bytes = packet.ToPacket();
    var parsed = XPacket.Parse(bytes);

    var restored = XPacketConverter.Deserialize<MultiStringDto>(parsed);

    await Assert.That(restored.First).IsEqualTo(original.First);
    await Assert.That(restored.Middle).IsEqualTo(original.Middle);
    await Assert.That(restored.Last).IsEqualTo(original.Last);
}
```

- [ ] **Step 2: Run the test**

Run: `dotnet test XProtocol.Tests/XProtocol.Tests.csproj --filter "FullyQualifiedName~MultiStringDto_Roundtrip_PreservesBothStrings" -v normal`
Expected: pass.

- [ ] **Step 3: Commit**

```bash
git add XProtocol.Tests/RoundtripTests.cs
git commit -m "Verify multi-string DTO roundtrip with mixed value-type and chunked strings"
```

---

## Task 9: Encrypted roundtrip test

**Files:**
- Modify: `XProtocol.Tests/RoundtripTests.cs`

- [ ] **Step 1: Append the test**

```csharp
[Test]
public async Task StringDto_Roundtrip_EncryptedPath_900Bytes()
{
    EnsureStringDtoRegistered();

    var s = new string('z', 900);
    var original = new StringDto { A = 9, S = s, B = true };

    var packet = XPacketConverter.Serialize(original);
    var encryptedBytes = packet.Encrypt().ToPacket();
    var parsed = XPacket.Parse(encryptedBytes);
    await Assert.That(parsed).IsNotNull();

    var restored = XPacketConverter.Deserialize<StringDto>(parsed);

    await Assert.That(restored.A).IsEqualTo(original.A);
    await Assert.That(restored.S).IsEqualTo(original.S);
    await Assert.That(restored.B).IsEqualTo(original.B);
}
```

- [ ] **Step 2: Run the test**

Run: `dotnet test XProtocol.Tests/XProtocol.Tests.csproj --filter "FullyQualifiedName~StringDto_Roundtrip_EncryptedPath_900Bytes" -v normal`
Expected: pass. If it fails inside `EncryptPacket`/`DecryptPacket`, inspect — those code paths shouldn't care about field count, but a regression here means the encryption layer made an assumption we missed.

- [ ] **Step 3: Commit**

```bash
git add XProtocol.Tests/RoundtripTests.cs
git commit -m "Verify chunked-string roundtrip through Encrypt/Decrypt path"
```

---

## Task 10: Negative test — string > 65535 bytes throws

**Files:**
- Modify: `XProtocol.Tests/RoundtripTests.cs`

- [ ] **Step 1: Append the test**

```csharp
[Test]
public async Task Serialize_StringOverflow_Throws()
{
    EnsureStringDtoRegistered();

    var s = new string('x', ushort.MaxValue + 1);  // 65536
    var dto = new StringDto { A = 10, S = s, B = false };

    var ex = await Assert.That(() => XPacketConverter.Serialize(dto))
        .ThrowsExactly<InvalidOperationException>();

    await Assert.That(ex.Message).Contains("exceeds 65535");
    await Assert.That(ex.Message).Contains("StringDto");
}
```

- [ ] **Step 2: Run the test**

Run: `dotnet test XProtocol.Tests/XProtocol.Tests.csproj --filter "FullyQualifiedName~Serialize_StringOverflow_Throws" -v normal`
Expected: pass.

- [ ] **Step 3: Commit**

```bash
git add XProtocol.Tests/RoundtripTests.cs
git commit -m "Verify string > 65535 UTF-8 bytes throws InvalidOperationException"
```

---

## Task 11: Negative test — total wire fields > 255 throws

The trickiest cap: need a DTO where serialization legitimately produces > 255 wire fields. With `StringDto` (3 logical fields), a max 16383-byte string (16385 byte payload) → 65 chunks → 67 total — under cap. To force overflow, use a string near `ushort.MaxValue`: 65535-byte string → 257 string chunks → with 1 int + 1 bool = 259 wire fields > 255.

**Files:**
- Modify: `XProtocol.Tests/RoundtripTests.cs`

- [ ] **Step 1: Append the test**

```csharp
[Test]
public async Task Serialize_TotalWireOverflow_Throws()
{
    EnsureStringDtoRegistered();

    // 65535 bytes → 257 string chunks; + int + bool = 259 wire fields > 255 cap.
    var s = new string('x', ushort.MaxValue);
    var dto = new StringDto { A = 11, S = s, B = false };

    var ex = await Assert.That(() => XPacketConverter.Serialize(dto))
        .ThrowsExactly<InvalidOperationException>();

    await Assert.That(ex.Message).Contains("exceeds 255 wire fields");
    await Assert.That(ex.Message).Contains("StringDto");
}
```

- [ ] **Step 2: Run the test**

Run: `dotnet test XProtocol.Tests/XProtocol.Tests.csproj --filter "FullyQualifiedName~Serialize_TotalWireOverflow_Throws" -v normal`
Expected: pass.

- [ ] **Step 3: Commit**

```bash
git add XProtocol.Tests/RoundtripTests.cs
git commit -m "Verify packet exceeding 255 wire fields throws"
```

---

## Task 12: Negative test — register unsupported ref-type (not string)

**Files:**
- Modify: `XProtocol.Tests/RegistrationTests.cs`

- [ ] **Step 1: Append the test** (inside the existing `RegistrationTests` class):

```csharp
[Test]
public async Task Register_AllowsStringField()
{
    // StringDto has a `string` field. Should register without throwing.
    // Note: subtype 110 chosen to avoid collisions with other tests.
    XPacketTypeManager.Register<StringDto>((XPacketType)110, 110, 0);

    var (type, subtype) = XPacketTypeManager.GetType((XPacketType)110);
    await Assert.That(type).IsEqualTo((byte)110);
    await Assert.That(subtype).IsEqualTo((byte)0);
}

[Test]
public async Task Register_RejectsObjectField()
{
    var ex = await Assert.That(() =>
        XPacketTypeManager.Register<UnsupportedRefDto>((XPacketType)111, 111, 0))
        .ThrowsExactly<InvalidOperationException>();

    await Assert.That(ex.Message).Contains("only value-type fields");
    await Assert.That(ex.Message).Contains("UnsupportedRefDto");
}
```

**Caveat on `Register_AllowsStringField`:** if any previous test in the suite already registers `StringDto` under `(XPacketType)100`, the second registration here (under `(XPacketType)110`) hits a duplicate `Type` check inside `XPacketTypeManager.Register`. Inspect the existing `Register` body — if it checks duplicates by `XPacketType` enum key only (not by `byte` pair), the test passes. If by `Type` (T → bytes mapping), the helper `EnsureStringDtoRegistered` in `RoundtripTests` already filled `bytesByDtoType[typeof(StringDto)]` → second registration throws.

Mitigation: either change this `Register_AllowsStringField` test to use a fresh DTO type only used here (e.g. add `public class RegistrationOnlyStringDto { public string X; }` to `TestDtos.cs`), or remove the redundant assertion. Pick the cleaner path during execution: add `RegistrationOnlyStringDto` and use it.

If adding the DTO: extend `TestDtos.cs`:

```csharp
public class RegistrationOnlyStringDto
{
    public string X;
}
```

Then change `Register_AllowsStringField` to use `RegistrationOnlyStringDto`. Commit `TestDtos.cs` changes together with the test.

- [ ] **Step 2: Run the tests**

Run: `dotnet test XProtocol.Tests/XProtocol.Tests.csproj --filter "FullyQualifiedName~RegistrationTests" -v normal`
Expected: all RegistrationTests pass, including existing `Register_RejectsReferenceTypeField` (still gated on `int[]`).

- [ ] **Step 3: Commit**

```bash
git add XProtocol.Tests/RegistrationTests.cs XProtocol.Tests/TestDtos.cs
git commit -m "Verify string fields register and unsupported ref-types still reject"
```

---

## Task 13: Negative tests — deserialize corrupt string (header / payload truncated)

**Files:**
- Modify: `XProtocol.Tests/RoundtripTests.cs`

- [ ] **Step 1: Append the tests**

```csharp
[Test]
public async Task Deserialize_StringTruncated_Throws()
{
    EnsureStringDtoRegistered();

    var original = new StringDto { A = 12, S = new string('q', 600), B = true };
    var packet = XPacketConverter.Serialize(original);

    // Drop the last wire chunk of the string (between bool and string payload).
    // Layout: [int][string_chunk_0][string_chunk_1][string_chunk_2][bool]
    // Remove second-to-last to truncate string mid-payload.
    packet.Fields.RemoveAt(packet.Fields.Count - 2);

    var ex = await Assert.That(() => XPacketConverter.Deserialize<StringDto>(packet))
        .ThrowsExactly<InvalidOperationException>();

    // Either "string truncated" (if walker exhausts during string) or
    // "Field count mismatch" (if walker ends before consuming bool).
    var msg = ex.Message;
    var matched = msg.Contains("string truncated") || msg.Contains("Field count mismatch");
    await Assert.That(matched).IsTrue();
}

[Test]
public async Task Deserialize_StringHeaderTruncated_Throws()
{
    EnsureStringDtoRegistered();

    var original = new StringDto { A = 13, S = "hi", B = false };
    var packet = XPacketConverter.Serialize(original);

    // Replace the string descriptor's first chunk (index 1) with a 1-byte field.
    packet.Fields[1] = new XPacketField
    {
        FieldSize = 1,
        Contents = new byte[] { 0 }
    };

    var ex = await Assert.That(() => XPacketConverter.Deserialize<StringDto>(packet))
        .ThrowsExactly<InvalidOperationException>();

    await Assert.That(ex.Message).Contains("string header truncated");
}
```

- [ ] **Step 2: Run the tests**

Run: `dotnet test XProtocol.Tests/XProtocol.Tests.csproj --filter "FullyQualifiedName~Deserialize_String" -v normal`
Expected: both pass.

- [ ] **Step 3: Commit**

```bash
git add XProtocol.Tests/RoundtripTests.cs
git commit -m "Verify corrupt string deserialization throws with clear messages"
```

---

## Task 14: Update `Test/Program.cs` smoke output

**Files:**
- Modify: `Test/Program.cs`

- [ ] **Step 1: Replace the `Console.WriteLine` line (currently line 39)**

Open `Test/Program.cs`. Find the existing `Console.WriteLine($"TestNumber=...")` and replace with:

```csharp
Console.WriteLine(
    $"TestNumber={roundtrip.TestNumber}, TestDouble={roundtrip.TestDouble}, " +
    $"TestBoolean={roundtrip.TestBoolean}, TestString.Length={roundtrip.TestString?.Length ?? 0}");
Console.WriteLine($"TestString matches: {dto.TestString == roundtrip.TestString}");
```

- [ ] **Step 2: Build and run the Test app**

Run: `dotnet run --project Test/Test.csproj`
Expected output (the `Length` will be whatever the actual literal length is, ~900s):

```
TestNumber=12345, TestDouble=3.14, TestBoolean=True, TestString.Length=NNN
TestString matches: True
```

If `TestString matches: False` — abort, investigate; that means a roundtrip bug.

- [ ] **Step 3: Commit**

```bash
git add Test/Program.cs
git commit -m "Include TestString length and equality in Test/Program smoke output"
```

---

## Task 15: Final full-suite verification

**Files:** none.

- [ ] **Step 1: Run the entire test suite cleanly**

Run: `dotnet test XProtocol.Tests/XProtocol.Tests.csproj -v normal`
Expected: every test passes — old (`SimpleDto`, `StrictCount`, `Registration`, `XPacket`, `RijndaelHandler`, `XPacketConverter`) and new (`FieldDescriptorTests`, `XPacketChunkTests`, all string roundtrip + negative tests).

- [ ] **Step 2: Run `Test/Program.cs` once more**

Run: `dotnet run --project Test/Test.csproj`
Expected: smoke output prints with `TestString matches: True`.

- [ ] **Step 3: `git status` should be clean** (apart from any pre-existing unrelated modifications you did not author).

Run: `git status --short`
Expected: clean — or only files outside the scope of this plan.

- [ ] **Step 4: Final summary commit (optional)** — only if there are leftover staged docs or notes; otherwise skip.

---

## Self-Review

**Spec coverage:**

| Spec section | Implemented in task |
|---|---|
| Принятые решения #1 (only string) | Task 1 (FieldDescriptor ctor explicit) |
| #2 wire-format magic unchanged | Tasks 4/5 (no edits to `Parse`/`ToPacket` headers) |
| #3 chunked encoding | Tasks 4 (helpers), 5 (Serialize) |
| #4 UTF-8 no BOM | Task 5 (`Encoding.UTF8.GetBytes`) |
| #5 ushort LE prefix | Task 5 (`payload[0] = len & 0xFF`, `payload[1] = (len >> 8) & 0xFF`) |
| #6 65535 cap | Task 5 (overflow throw), Task 10 (test) |
| #7 null → empty | Task 5 (`?? string.Empty`), Task 6 (test) |
| #8 `AppendValue` unchanged + new `AppendChunks` | Task 4 |
| #9 full test set | Tasks 5–13 |
| Wire-format raskladka table | Tasks 4 chunk tests + Task 5/6/7 roundtrips |
| Общий cap пакета 255 wire-полей | Task 5 (check), Task 11 (test) |
| FieldDescriptor changes | Task 1 |
| XPacketTypeManager changes | Task 3 |
| XPacket changes | Task 4 |
| XPacketConverter changes | Task 5 |
| TestDtos additions / BadDto fix | Task 2 |
| RoundtripTests additions | Tasks 5–11, 13 |
| RegistrationTests additions | Task 12 |
| Test/Program.cs update | Task 14 |
| Error messages: ref-type rejection | Task 1 (ctor) + Task 12 (test) |
| Error: UTF-8 > 65535 | Task 5 + Task 10 |
| Error: wire fields > 255 | Task 5 + Task 11 |
| Error: string truncated | Task 5 + Task 13 |
| Error: string header truncated | Task 5 + Task 13 |
| Encryption path | Task 9 |

All spec items mapped.

**Placeholder scan:** none of `TBD`, `TODO`, `implement later`, `appropriate error handling`, or unspecified `Similar to Task N` appear.

**Type consistency:**
- `FieldKind.ValueType` / `FieldKind.String` — consistent across Tasks 1, 5.
- `AppendChunks(byte[])` / `GetRawAt(int)` — consistent in Tasks 4, 5.
- `StringGetter` / `StringSetter` — consistent in Tasks 1, 5.
- Error substring `"only value-type fields"` — consistent in Tasks 1, 12 and matches the unchanged existing `RegistrationTests.Register_RejectsReferenceTypeField` assertion.
- DTO names (`StringDto`, `MultiStringDto`, `UnsupportedRefDto`, `RegistrationOnlyStringDto`, `BadDtoWithReferenceField`) consistent across Tasks 2, 5–13.
- `EnsureStringDtoRegistered` / `EnsureMultiStringDtoRegistered` — declared in Task 5, used in Tasks 5–11, 13.
