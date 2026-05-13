# XProtocol.Tests TUnit Migration + 90% Coverage Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert XProtocol.Tests from MSTest 4 to TUnit 1.43.11, then add tests until line coverage of `XProtocol/` assembly reaches 90%.

**Architecture:** Phase A — mechanical migration of 11 existing tests + AssemblyFixture; build broken between mid-tasks, single commit at the end of Phase A. Phase B — four new test files added one per commit, each measured against cobertura output until coverage threshold met.

**Tech Stack:** .NET 10, C# 13, TUnit 1.43.11, Microsoft.Testing.Extensions.CodeCoverage 17.13.0.

**Spec:** [docs/superpowers/specs/2026-05-08-tunit-migration-coverage-90.md](../specs/2026-05-08-tunit-migration-coverage-90.md)

---

## File Structure

### Files modified in `XProtocol.Tests/`

| File | Phase | Action |
|------|-------|--------|
| `XProtocol.Tests/XProtocol.Tests.csproj` | A | Replace MSTest packages with TUnit + add code-coverage extension |
| `XProtocol.Tests/TestDtos.cs` | A | `AssemblyFixture` migrated to `[Before(HookType.Assembly)]` |
| `XProtocol.Tests/RegistrationTests.cs` | A | 4 tests migrated to TUnit |
| `XProtocol.Tests/RoundtripTests.cs` | A | 4 tests migrated to TUnit |
| `XProtocol.Tests/StrictCountTests.cs` | A | 1 test migrated to TUnit |
| `XProtocol.Tests/UnregisteredTypeTests.cs` | A | 2 tests migrated to TUnit |

### Files created in Phase B

| File | Action |
|------|--------|
| `XProtocol.Tests/XPacketTests.cs` | New: Parse malformed, AppendValue/GetValueAt edges, Encrypt/Decrypt invariants |
| `XProtocol.Tests/XPacketTypeManagerTests.cs` | New: GetTypeFromPacket, GetBytesFor, BuildDescriptors with inheritance |
| `XProtocol.Tests/XPacketConverterTests.cs` | New: null-arg checks for Serialize/Deserialize |
| `XProtocol.Tests/RijndaelHandlerTests.cs` | New: roundtrip across sizes, corrupted ciphertext |

### Files NOT modified

- `XProtocol/**` (production code)
- `Test/**` (sample console)
- `TCPClient/**`, `TCPServer/**`
- `TCPProtocol.sln`

---

## Phase A — TUnit Migration

### Task 1: Update XProtocol.Tests.csproj

**Files:**
- Modify: `XProtocol.Tests/XProtocol.Tests.csproj`

- [ ] **Step 1: Replace whole content of csproj**

Overwrite `XProtocol.Tests/XProtocol.Tests.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <Nullable>disable</Nullable>
    <LangVersion>latest</LangVersion>
    <OutputType>Exe</OutputType>
    <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="TUnit" Version="1.43.11" />
    <PackageReference Include="Microsoft.Testing.Extensions.CodeCoverage" Version="17.13.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\XProtocol\XProtocol.csproj" />
  </ItemGroup>
</Project>
```

> **Note on package versions:** If `dotnet restore` cannot find `TUnit 1.43.11` or `Microsoft.Testing.Extensions.CodeCoverage 17.13.0`, run `dotnet package search TUnit` and `dotnet package search Microsoft.Testing.Extensions.CodeCoverage` to find the latest available versions, update both `Version="…"` attributes accordingly, and document the actual versions used in the final commit message.

- [ ] **Step 2: Restore packages**

Run: `dotnet restore XProtocol.Tests/XProtocol.Tests.csproj`

Expected: success. The build will FAIL because the C# source files still reference `Microsoft.VisualStudio.TestTools.UnitTesting`. That is fixed in Tasks 2–6.

Do NOT commit yet.

---

### Task 2: Migrate TestDtos.cs

**Files:**
- Modify: `XProtocol.Tests/TestDtos.cs`

- [ ] **Step 1: Replace whole content**

Overwrite `XProtocol.Tests/TestDtos.cs` with:

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
        public string Bad;
    }

    public class UnregisteredDto
    {
        public int X;
    }

    public static class AssemblyFixture
    {
        public const XPacketType SimpleDtoType = (XPacketType)100;
        public const XPacketType EmptyDtoType = (XPacketType)101;

        [Before(HookType.Assembly)]
        public static void Init()
        {
            XPacketTypeManager.Register<SimpleDto>(SimpleDtoType, 100, 0);
            XPacketTypeManager.Register<EmptyDto>(EmptyDtoType, 101, 0);
        }
    }
}
```

Changes from previous version:
- Removed `using Microsoft.VisualStudio.TestTools.UnitTesting;` (TUnit's `[Before]` and `HookType` are auto-imported via `TUnit` package; if not, add `using TUnit.Core;`).
- Removed `[TestClass]` from `AssemblyFixture` (TUnit does not require it).
- `[AssemblyInitialize]` with `(TestContext _)` parameter replaced by `[Before(HookType.Assembly)]` with no parameters.

- [ ] **Step 2: Build is still broken — that is expected**

Don't run build yet; remaining files in Tasks 3–6 still reference MSTest. Do NOT commit.

---

### Task 3: Migrate RegistrationTests.cs

**Files:**
- Modify: `XProtocol.Tests/RegistrationTests.cs`

- [ ] **Step 1: Replace whole content**

Overwrite `XProtocol.Tests/RegistrationTests.cs` with:

```csharp
using System;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace XProtocol.Tests
{
    public class RegistrationTests
    {
        // NOTE: XPacketTypeManager статичен между тестами. Каждый тестовый DTO регистрируем
        //       ровно один раз в первом подходящем тесте, а проверки на повторную регистрацию
        //       используют отдельные XPacketType-значения.

        [Test]
        public async Task Register_RejectsReferenceTypeField()
        {
            await Assert.That(() =>
                XPacketTypeManager.Register<BadDtoWithReferenceField>((XPacketType)90, 90, 0))
                .ThrowsExactly<InvalidOperationException>()
                .WithMessageContaining("only value-type fields")
                .WithMessageContaining("Bad");
        }

        [Test]
        public async Task Register_RejectsDuplicatePacketType()
        {
            // первый раз регистрируем Handshake уже сделан в static ctor
            await Assert.That(() =>
                XPacketTypeManager.Register<XPacketHandshake>(XPacketType.Handshake, 1, 0))
                .ThrowsExactly<InvalidOperationException>()
                .WithMessageContaining("already registered");
        }

        [Test]
        public async Task GetType_ReturnsRegisteredPair()
        {
            var (type, subtype) = XPacketTypeManager.GetType(XPacketType.Handshake);
            await Assert.That(type).IsEqualTo((byte)1);
            await Assert.That(subtype).IsEqualTo((byte)0);
        }

        [Test]
        public async Task GetType_ThrowsForUnregistered()
        {
            await Assert.That(() => XPacketTypeManager.GetType((XPacketType)999))
                .ThrowsExactly<InvalidOperationException>()
                .WithMessageContaining("not registered");
        }
    }
}
```

> **Fallback if `WithMessageContaining(...)` chain does not compile:** capture the exception with `var ex = await Assert.That(...).ThrowsExactly<InvalidOperationException>();` then `await Assert.That(ex.Message).Contains("…");` — this requires `ThrowsExactly<T>` to return an awaitable yielding `T`, which is the documented behavior. Verify on the first failing test only and update all four tests consistently. Document the fallback in the Phase A commit message.

- [ ] **Step 2: Build is still broken — Tasks 4–6 outstanding**

Do NOT commit.

---

### Task 4: Migrate RoundtripTests.cs

**Files:**
- Modify: `XProtocol.Tests/RoundtripTests.cs`

- [ ] **Step 1: Replace whole content**

Overwrite `XProtocol.Tests/RoundtripTests.cs` with:

```csharp
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    public class RoundtripTests
    {
        [Test]
        public async Task SimpleDto_RoundtripPreservesValues()
        {
            var original = new SimpleDto { A = 42, B = 3.1415, C = true };

            var packet = XPacketConverter.Serialize(original);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            await Assert.That(parsed).IsNotNull();

            var restored = XPacketConverter.Deserialize<SimpleDto>(parsed);

            await Assert.That(restored.A).IsEqualTo(original.A);
            await Assert.That(restored.B).IsEqualTo(original.B);
            await Assert.That(restored.C).IsEqualTo(original.C);
        }

        [Test]
        public async Task SimpleDto_FieldOrderMatchesDeclarationOrder()
        {
            var dto = new SimpleDto { A = 1, B = 2.0, C = false };
            var packet = XPacketConverter.Serialize(dto);

            await Assert.That(packet.Fields.Count).IsEqualTo(3);
            // первое поле — A (int=4 bytes), второе — B (double=8 bytes), третье — C
            await Assert.That(packet.Fields[0].FieldSize).IsEqualTo((byte)4);
            await Assert.That(packet.Fields[1].FieldSize).IsEqualTo((byte)8);
            await Assert.That(packet.Fields[2].FieldSize).IsGreaterThanOrEqualTo((byte)1);
        }

        [Test]
        public async Task EmptyDto_RoundtripProducesZeroFields()
        {
            var original = new EmptyDto();

            var packet = XPacketConverter.Serialize(original);
            await Assert.That(packet.Fields.Count).IsEqualTo(0);

            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            await Assert.That(parsed).IsNotNull();
            await Assert.That(parsed.Fields.Count).IsEqualTo(0);

            var restored = XPacketConverter.Deserialize<EmptyDto>(parsed);
            await Assert.That(restored).IsNotNull();
        }

        [Test]
        public async Task XPacketHandshake_RoundtripPreservesValue()
        {
            var original = new XPacketHandshake { MagicHandshakeNumber = 12345 };

            var packet = XPacketConverter.Serialize(original);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            await Assert.That(parsed).IsNotNull();

            var restored = XPacketConverter.Deserialize<XPacketHandshake>(parsed);
            await Assert.That(restored.MagicHandshakeNumber).IsEqualTo(original.MagicHandshakeNumber);
        }
    }
}
```

> **Note on `IsGreaterThanOrEqualTo`:** TUnit ships this assertion natively. If it isn't available on the byte type, use `await Assert.That((int)packet.Fields[2].FieldSize).IsGreaterThanOrEqualTo(1);` instead.

- [ ] **Step 2: Build is still broken — Tasks 5–6 outstanding**

---

### Task 5: Migrate StrictCountTests.cs

**Files:**
- Modify: `XProtocol.Tests/StrictCountTests.cs`

- [ ] **Step 1: Replace whole content**

Overwrite `XProtocol.Tests/StrictCountTests.cs` with:

```csharp
using System;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    public class StrictCountTests
    {
        [Test]
        public async Task Deserialize_FieldCountMismatch_Throws()
        {
            var dto = new SimpleDto { A = 1, B = 2.0, C = true };
            var packet = XPacketConverter.Serialize(dto);

            // Намеренно ломаем количество полей: убираем последнее.
            packet.Fields.RemoveAt(packet.Fields.Count - 1);

            await Assert.That(() => XPacketConverter.Deserialize<SimpleDto>(packet))
                .ThrowsExactly<InvalidOperationException>()
                .WithMessageContaining("Field count mismatch")
                .WithMessageContaining("expected 3")
                .WithMessageContaining("got 2");
        }
    }
}
```

---

### Task 6: Migrate UnregisteredTypeTests.cs

**Files:**
- Modify: `XProtocol.Tests/UnregisteredTypeTests.cs`

- [ ] **Step 1: Replace whole content**

Overwrite `XProtocol.Tests/UnregisteredTypeTests.cs` with:

```csharp
using System;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    public class UnregisteredTypeTests
    {
        [Test]
        public async Task Serialize_UnregisteredType_Throws()
        {
            var dto = new UnregisteredDto { X = 7 };

            await Assert.That(() => XPacketConverter.Serialize(dto))
                .ThrowsExactly<InvalidOperationException>()
                .WithMessageContaining(nameof(UnregisteredDto))
                .WithMessageContaining("not registered");
        }

        [Test]
        public async Task Deserialize_UnregisteredType_Throws()
        {
            var pkt = XPacket.Create(0, 0);

            await Assert.That(() => XPacketConverter.Deserialize<UnregisteredDto>(pkt))
                .ThrowsExactly<InvalidOperationException>()
                .WithMessageContaining(nameof(UnregisteredDto))
                .WithMessageContaining("not registered");
        }
    }
}
```

---

### Task 7: Build, run all tests, commit Phase A

**Files:** —

- [ ] **Step 1: Build the test project**

Run: `dotnet build XProtocol.Tests/XProtocol.Tests.csproj`

Expected: 0 errors. (Warnings about implicit nullability or `string Bad;` field unused are acceptable.)

If errors mention missing TUnit types (`Test`, `Assert.That`, `HookType`, `Before`), check the `using` statements at the top of each test file. If errors persist, see fallback note in Task 3 Step 1.

- [ ] **Step 2: Run all tests**

Run: `dotnet test XProtocol.Tests/XProtocol.Tests.csproj`

Expected: all 11 tests pass.

If a single test fails on the `WithMessageContaining(...)` chain because the chain isn't supported, apply the fallback from Task 3 Step 1 (capture the exception via `var ex = await ...`) to **all four files using exception assertions** — `RegistrationTests.cs`, `StrictCountTests.cs`, `UnregisteredTypeTests.cs` — in this single update before continuing.

- [ ] **Step 3: Stage all Phase A changes and commit**

Run:
```
git add XProtocol.Tests/XProtocol.Tests.csproj XProtocol.Tests/TestDtos.cs XProtocol.Tests/RegistrationTests.cs XProtocol.Tests/RoundtripTests.cs XProtocol.Tests/StrictCountTests.cs XProtocol.Tests/UnregisteredTypeTests.cs
```

Then commit:
```
git commit -m "test: migrate XProtocol.Tests from MSTest 4 to TUnit 1.43.11

- Replace Microsoft.NET.Test.Sdk + MSTest.* with TUnit + Microsoft.Testing.Extensions.CodeCoverage
- Add OutputType=Exe and TestingPlatformDotnetTestSupport=true (MTP)
- Convert AssemblyFixture from [AssemblyInitialize] to [Before(HookType.Assembly)]
- Convert all 11 tests to async Task with await Assert.That(...) pattern
- Replace Assert.ThrowsExactly<T> with Assert.That(...).ThrowsExactly<T>().WithMessageContaining(...)"
```

- [ ] **Step 4: Confirm clean working tree**

Run: `git status --short`

Expected: only untracked items (`.claude/`, `.serena/`) — no modifications outside `XProtocol.Tests/`.

---

## Phase B — Coverage Expansion to 90%

### Task 8: Establish baseline coverage

**Files:** —

- [ ] **Step 1: Run coverage**

Run from working directory:
```
dotnet test XProtocol.Tests/XProtocol.Tests.csproj --coverage --coverage-output coverage-baseline.cobertura.xml --coverage-output-format cobertura
```

Expected: 11/11 pass. The cobertura XML is written to the test output directory (typically `XProtocol.Tests/bin/Debug/net10.0/TestResults/<guid>/coverage-baseline.cobertura.xml`). Use PowerShell `Get-ChildItem -Path XProtocol.Tests/bin -Recurse -Filter coverage-baseline.cobertura.xml` to locate it.

- [ ] **Step 2: Read line-rate**

Open the cobertura XML. Find the top-level `<coverage line-rate="X.YYY" …>` attribute. Record the value as `BASELINE_LINE_RATE`. Expected: somewhere in `0.50`–`0.65` range.

> **Note on missing tooling:** if `--coverage` is not recognized, the `Microsoft.Testing.Extensions.CodeCoverage` package may not have been restored correctly. Re-run `dotnet restore` and verify `XProtocol.Tests/bin/Debug/net10.0/Microsoft.Testing.Extensions.CodeCoverage.dll` exists.

- [ ] **Step 3: Document baseline**

No commit needed for measurement only. Note `BASELINE_LINE_RATE` for comparison after Phase B.

---

### Task 9: Add XPacketTests.cs — Parse, AppendValue, GetValueAt edges, Encrypt invariants

**Files:**
- Create: `XProtocol.Tests/XPacketTests.cs`

- [ ] **Step 1: Create XPacketTests.cs**

Create `XProtocol.Tests/XPacketTests.cs` with:

```csharp
using System;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace XProtocol.Tests
{
    public class XPacketTests
    {
        // -------- Parse: malformed inputs --------

        [Test]
        public async Task Parse_NullInput_ReturnsNull()
        {
            await Assert.That(XPacket.Parse(null)).IsNull();
        }

        [Test]
        public async Task Parse_TooShort_ReturnsNull()
        {
            var bytes = new byte[7];
            await Assert.That(XPacket.Parse(bytes)).IsNull();
        }

        [Test]
        public async Task Parse_WrongHeader_ReturnsNull()
        {
            var bytes = new byte[] { 0xDE, 0xAD, 0xBE, 0x00, 0x00, 0x00, 0xFF, 0x00 };
            await Assert.That(XPacket.Parse(bytes)).IsNull();
        }

        [Test]
        public async Task Parse_PlainHeaderEmptyFields_Succeeds()
        {
            var bytes = new byte[] { 0xAF, 0xAA, 0xAF, 0x00, 0x00, 0x00, 0xFF, 0x00 };
            var p = XPacket.Parse(bytes);
            await Assert.That(p).IsNotNull();
            await Assert.That(p.Fields.Count).IsEqualTo(0);
        }

        [Test]
        public async Task Parse_TruncatedField_ReturnsNull()
        {
            // header(5) + fieldCount=1 + size=10 + only 5 bytes contents + footer = malformed
            var bytes = new byte[] {
                0xAF, 0xAA, 0xAF, 0x00, 0x00,
                0x01,
                0x0A, 0x01, 0x02, 0x03, 0x04, 0x05,
                0xFF, 0x00
            };
            await Assert.That(XPacket.Parse(bytes)).IsNull();
        }

        [Test]
        public async Task Parse_MissingFooter_ReturnsNull()
        {
            // header(5) + fieldCount=0 but footer not 0xFF 0x00
            var bytes = new byte[] { 0xAF, 0xAA, 0xAF, 0x00, 0x00, 0x00, 0xAB, 0xCD };
            await Assert.That(XPacket.Parse(bytes)).IsNull();
        }

        [Test]
        public async Task Parse_OneByteField_RoundtripBytes()
        {
            // header(5) + fieldCount=1 + size=2 + 0x10, 0x20 + footer
            var bytes = new byte[] {
                0xAF, 0xAA, 0xAF, 0x05, 0x06,
                0x01,
                0x02, 0x10, 0x20,
                0xFF, 0x00
            };
            var p = XPacket.Parse(bytes);
            await Assert.That(p).IsNotNull();
            await Assert.That(p.Fields.Count).IsEqualTo(1);
            await Assert.That(p.Fields[0].FieldSize).IsEqualTo((byte)2);
            await Assert.That(p.Fields[0].Contents[0]).IsEqualTo((byte)0x10);
            await Assert.That(p.Fields[0].Contents[1]).IsEqualTo((byte)0x20);
        }

        // -------- AppendValue: edge cases --------

        [Test]
        public async Task AppendValue_Null_ThrowsArgumentNullException()
        {
            var p = XPacket.Create(0, 0);
            await Assert.That(() => p.AppendValue(null))
                .ThrowsExactly<ArgumentNullException>();
        }

        [Test]
        public async Task AppendValue_ReferenceType_ThrowsArgumentException()
        {
            var p = XPacket.Create(0, 0);
            await Assert.That(() => p.AppendValue("string is not a value type"))
                .ThrowsExactly<ArgumentException>()
                .WithMessageContaining("Only value types");
        }

        // -------- GetValueAt: bounds --------

        [Test]
        public async Task GetValueAt_NegativeIndex_Throws()
        {
            var p = XPacket.Create(0, 0);
            p.AppendValue(123);

            await Assert.That(() => p.GetValueAt<int>(-1))
                .ThrowsExactly<ArgumentOutOfRangeException>();
        }

        [Test]
        public async Task GetValueAt_OutOfRangeIndex_Throws()
        {
            var p = XPacket.Create(0, 0);
            p.AppendValue(123);

            await Assert.That(() => p.GetValueAt<int>(1))
                .ThrowsExactly<ArgumentOutOfRangeException>();
        }

        [Test]
        public async Task GetValueAtTyped_NegativeIndex_Throws()
        {
            var p = XPacket.Create(0, 0);
            p.AppendValue(123);

            await Assert.That(() => p.GetValueAt(-1, typeof(int)))
                .ThrowsExactly<ArgumentOutOfRangeException>();
        }

        // -------- Encrypt / Decrypt invariants --------

        [Test]
        public async Task EncryptPacket_Null_ReturnsNull()
        {
            await Assert.That(XPacket.EncryptPacket(null)).IsNull();
        }

        [Test]
        public async Task EncryptDecrypt_Roundtrip_PreservesPacket()
        {
            var p = XPacket.Create(7, 3);
            p.AppendValue(0x12345678);
            p.AppendValue(3.14);

            var encryptedBytes = p.Encrypt().ToPacket();
            var decrypted = XPacket.Parse(encryptedBytes);

            await Assert.That(decrypted).IsNotNull();
            await Assert.That(decrypted.PacketType).IsEqualTo((byte)7);
            await Assert.That(decrypted.PacketSubtype).IsEqualTo((byte)3);
            await Assert.That(decrypted.Fields.Count).IsEqualTo(2);
            await Assert.That(decrypted.GetValueAt<int>(0)).IsEqualTo(0x12345678);
            await Assert.That(decrypted.GetValueAt<double>(1)).IsEqualTo(3.14);
        }
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test XProtocol.Tests/XProtocol.Tests.csproj --filter FullyQualifiedName~XPacketTests`

Expected: 14/14 PASS for `XPacketTests`. Total project: 11 + 14 = 25.

If a test fails: trace the actual vs expected behavior in `XProtocol/XPacket.cs`. Do NOT modify production code. If a test is wrong, fix the test; if production behavior diverges from spec, document and report.

- [ ] **Step 3: Measure coverage progress**

Run: `dotnet test XProtocol.Tests/XProtocol.Tests.csproj --coverage --coverage-output coverage-after-task9.cobertura.xml --coverage-output-format cobertura`

Read line-rate from output XML. Should be visibly higher than `BASELINE_LINE_RATE`.

- [ ] **Step 4: Commit**

```
git add XProtocol.Tests/XPacketTests.cs
git commit -m "test: add XPacket malformed-input and edge-case tests

Covers: Parse null/short/wrong-header/truncated/missing-footer cases,
AppendValue null and reference-type rejection, GetValueAt bounds,
EncryptPacket null tolerance, full Encrypt+Decrypt roundtrip preservation."
```

---

### Task 10: Add XPacketTypeManagerTests.cs — GetTypeFromPacket, GetBytesFor, BuildDescriptors with inheritance

**Files:**
- Create: `XProtocol.Tests/XPacketTypeManagerTests.cs`

- [ ] **Step 1: Create XPacketTypeManagerTests.cs**

Create `XProtocol.Tests/XPacketTypeManagerTests.cs` with:

```csharp
using System;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    public class BaseDtoForInheritance
    {
        public int BaseField;
    }

    public class DerivedDtoForInheritance : BaseDtoForInheritance
    {
        public double DerivedField;
    }

    public class XPacketTypeManagerTests
    {
        [Test]
        public async Task GetTypeFromPacket_RegisteredHandshake_ReturnsHandshake()
        {
            var pkt = XPacket.Create(1, 0);
            var resolved = XPacketTypeManager.GetTypeFromPacket(pkt);
            await Assert.That(resolved).IsEqualTo(XPacketType.Handshake);
        }

        [Test]
        public async Task GetTypeFromPacket_UnknownBytes_ReturnsUnknown()
        {
            var pkt = XPacket.Create(0xFE, 0xFE);
            var resolved = XPacketTypeManager.GetTypeFromPacket(pkt);
            await Assert.That(resolved).IsEqualTo(XPacketType.Unknown);
        }

        [Test]
        public async Task GetType_RegisteredEnumValue_ReturnsBytePair()
        {
            var (type, subtype) = XPacketTypeManager.GetType(XPacketType.Handshake);
            await Assert.That(type).IsEqualTo((byte)1);
            await Assert.That(subtype).IsEqualTo((byte)0);
        }

        [Test]
        public async Task BuildDescriptors_InheritedDto_IncludesBaseAndDerivedFields()
        {
            // Регистрация триггерит BuildDescriptors. Используем уникальный XPacketType.
            XPacketTypeManager.Register<DerivedDtoForInheritance>((XPacketType)150, 150, 0);

            var dto = new DerivedDtoForInheritance
            {
                BaseField = 7,
                DerivedField = 2.5
            };

            var packet = XPacketConverter.Serialize(dto);
            await Assert.That(packet.Fields.Count).IsEqualTo(2);

            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            await Assert.That(parsed).IsNotNull();

            var restored = XPacketConverter.Deserialize<DerivedDtoForInheritance>(parsed);
            await Assert.That(restored.BaseField).IsEqualTo(7);
            await Assert.That(restored.DerivedField).IsEqualTo(2.5);
        }
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test XProtocol.Tests/XProtocol.Tests.csproj --filter FullyQualifiedName~XPacketTypeManagerTests`

Expected: 4/4 PASS.

If `BuildDescriptors_InheritedDto_IncludesBaseAndDerivedFields` fails because of MetadataToken ordering across inherited classes, the production code orders `OrderBy(f => f.MetadataToken)` flat — base-class fields and derived-class fields are sorted together by token. The token order is implementation-defined but stable per build. The test asserts both fields are present and round-trip correctly, NOT the order — that is acceptable.

- [ ] **Step 3: Commit**

```
git add XProtocol.Tests/XPacketTypeManagerTests.cs
git commit -m "test: add XPacketTypeManager descriptor and registry tests

Covers GetTypeFromPacket success and miss paths, GetType byte-pair
return, and BuildDescriptors with inheritance roundtrip."
```

---

### Task 11: Add XPacketConverterTests.cs — null-arg checks

**Files:**
- Create: `XProtocol.Tests/XPacketConverterTests.cs`

- [ ] **Step 1: Create XPacketConverterTests.cs**

Create `XProtocol.Tests/XPacketConverterTests.cs` with:

```csharp
using System;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    public class XPacketConverterTests
    {
        [Test]
        public async Task Serialize_NullObj_ThrowsArgumentNullException()
        {
            await Assert.That(() => XPacketConverter.Serialize<SimpleDto>(null))
                .ThrowsExactly<ArgumentNullException>();
        }

        [Test]
        public async Task Deserialize_NullPacket_ThrowsArgumentNullException()
        {
            await Assert.That(() => XPacketConverter.Deserialize<SimpleDto>(null))
                .ThrowsExactly<ArgumentNullException>();
        }
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test XProtocol.Tests/XProtocol.Tests.csproj --filter FullyQualifiedName~XPacketConverterTests`

Expected: 2/2 PASS.

- [ ] **Step 3: Commit**

```
git add XProtocol.Tests/XPacketConverterTests.cs
git commit -m "test: add XPacketConverter null-argument tests"
```

---

### Task 12: Add RijndaelHandlerTests.cs — roundtrip across sizes, corrupted ciphertext

**Files:**
- Create: `XProtocol.Tests/RijndaelHandlerTests.cs`

- [ ] **Step 1: Create RijndaelHandlerTests.cs**

Create `XProtocol.Tests/RijndaelHandlerTests.cs` with:

```csharp
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace XProtocol.Tests
{
    public class RijndaelHandlerTests
    {
        private const string TestPassphrase = "passphrase-for-tests";

        [Test]
        [Arguments(1)]
        [Arguments(15)]
        [Arguments(16)]
        [Arguments(17)]
        [Arguments(100)]
        [Arguments(1000)]
        public async Task EncryptDecrypt_RoundtripPreservesBytes(int plaintextLength)
        {
            var plaintext = new byte[plaintextLength];
            for (int i = 0; i < plaintextLength; i++)
            {
                plaintext[i] = (byte)(i & 0xFF);
            }

            var encrypted = RijndaelHandler.Encrypt(plaintext, TestPassphrase);
            var decrypted = RijndaelHandler.Decrypt(encrypted, TestPassphrase);

            await Assert.That(decrypted.Length).IsEqualTo(plaintextLength);
            await Assert.That(decrypted.SequenceEqual(plaintext)).IsTrue();
        }

        [Test]
        public async Task XProtocolEncryptor_Roundtrip_PreservesBytes()
        {
            var plaintext = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

            var encrypted = XProtocolEncryptor.Encrypt(plaintext);
            var decrypted = XProtocolEncryptor.Decrypt(encrypted);

            await Assert.That(decrypted.SequenceEqual(plaintext)).IsTrue();
        }

        [Test]
        public async Task Decrypt_CorruptedCiphertext_Throws()
        {
            var plaintext = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF };
            var encrypted = RijndaelHandler.Encrypt(plaintext, TestPassphrase);

            // Flip one byte deep inside ciphertext (after 32-byte salt + 16-byte IV).
            encrypted[50] ^= 0xFF;

            await Assert.That(() => RijndaelHandler.Decrypt(encrypted, TestPassphrase))
                .Throws<CryptographicException>();
        }
    }
}
```

> **Note:** `[Arguments(...)]` is TUnit's data-driven test attribute (analogous to MSTest's `[DataRow]` and xUnit's `[InlineData]`). Each `[Arguments]` produces a separate test case for the same method.

- [ ] **Step 2: Run tests**

Run: `dotnet test XProtocol.Tests/XProtocol.Tests.csproj --filter FullyQualifiedName~RijndaelHandlerTests`

Expected: 8/8 PASS (6 parameterized roundtrip cases + 1 wrapper roundtrip + 1 corruption case).

- [ ] **Step 3: Commit**

```
git add XProtocol.Tests/RijndaelHandlerTests.cs
git commit -m "test: add RijndaelHandler roundtrip and corrupted-ciphertext tests

Parameterized roundtrip across plaintext sizes 1..1000 bytes covers
the CryptoStream.CopyTo fix from earlier. Corrupted-ciphertext case
exercises PKCS7 padding rejection."
```

---

### Task 13: Final coverage measurement, gap analysis, optional gap fillers

**Files:** —

- [ ] **Step 1: Run full coverage**

Run: `dotnet test XProtocol.Tests/XProtocol.Tests.csproj --coverage --coverage-output coverage-final.cobertura.xml --coverage-output-format cobertura`

Expected: all tests pass (11 + 14 + 4 + 2 + 8 = 39).

- [ ] **Step 2: Read final line-rate**

Locate `coverage-final.cobertura.xml` and inspect top-level `<coverage line-rate="X.YYY">` attribute. Target: ≥ 0.90.

- [ ] **Step 3: If line-rate ≥ 0.90 — done**

Skip Step 4. Proceed to Step 5.

- [ ] **Step 4: If line-rate < 0.90 — identify uncovered methods**

In the cobertura XML, find `<method>` entries inside `<class name="XProtocol.…">` packages with `line-rate="0"` or low. Identify the most impactful uncovered method (highest line count uncovered).

For each gap:
1. Look at the method body in `XProtocol/<file>.cs`.
2. Decide whether the gap is reachable from a test:
   - **Reachable**: write a small targeted test, name file `XPacketTests.cs` (extend) or a new file based on namespace, run, and re-measure coverage.
   - **Unreachable** (e.g., `>255 fields` rejection, `>255 bytes per value type`): document in `docs/coverage-gaps.md` (create if needed) with one line per gap explaining why it cannot be exercised. These do not block the 0.90 target if they account for less than 10 percentage points.

If after one round of gap filling line-rate is still below 0.90 and remaining gaps are all unreachable, accept the achieved value and document in the final commit.

- [ ] **Step 5: Final commit (if any new gap-filler tests were added)**

If new tests were added in Step 4:

```
git add <files-added-or-modified>
git commit -m "test: add coverage gap-fillers to reach 90% line coverage

<short summary of which methods were targeted>"
```

If only documentation was added:

```
git add docs/coverage-gaps.md
git commit -m "docs: document unreachable code coverage gaps in XProtocol/"
```

If line-rate was already ≥ 0.90 at Step 2, no commit.

- [ ] **Step 6: Capture the final line-rate value**

Record the achieved line-rate value. Report in the implementation summary.

---

### Task 14: Verify production code is unchanged

**Files:** —

- [ ] **Step 1: Diff production directories vs Phase A start**

Run: `git diff <PHASE_A_BASE_SHA>..HEAD -- XProtocol/`

Where `<PHASE_A_BASE_SHA>` is the commit immediately before Task 7 (the last commit before Phase A's commit). The output should be **empty**.

If there are changes, identify them and either revert them (if accidental) or escalate.

- [ ] **Step 2: Diff Test/ and other consumer directories**

Run: `git diff <PHASE_A_BASE_SHA>..HEAD -- Test/ TCPClient/ TCPServer/ TCPProtocol.sln`

Expected: empty output.

- [ ] **Step 3: Run final smoke test (Test/Program.cs)**

Run: `"" | dotnet run --project Test/Test.csproj`

Expected output: `TestNumber=12345, TestDouble=3,14, TestBoolean=True` (or with locale-appropriate decimal separator).

If broken, escalate — production behavior should not have changed.

- [ ] **Step 4: Final summary**

Report at end of plan execution:
- Total tests: should be 39 (or higher if gap-fillers added in Task 13).
- Final line-rate: ≥ 0.90 expected.
- Number of commits added in this plan: 6 (Phase A 1 + Phase B 4 + optional gap-filler/docs from Task 13 + Task 14 has no commit).
- Any unreachable gaps documented.

---

## Self-Review Checklist (for executor)

After Task 14:

1. ✅ AC1 (build) — `dotnet build TCPProtocol.sln` 0 errors → covered by Task 7 Step 1 and final implicit by Task 13.
2. ✅ AC2 (tests pass) — Tasks 7, 9–12, 13 each verify subsets; Task 13 Step 1 verifies all together.
3. ✅ AC3 (coverage ≥ 0.90) — Task 13 Step 2 + Step 4 gap analysis.
4. ✅ AC4 (XProtocol/ unchanged) — Task 14 Step 1.
5. ✅ AC5 (Test/Program.cs sample works) — Task 14 Step 3.
