# Debug Session: only value-type fields are supported

## Date
Session captured during active debugging in Visual Studio Community 2026.

## Exception

**Type:** `System.InvalidOperationException`  
**Message:** `TestPacket.TestString: only value-type fields are supported.`  
**Location:** `XProtocol\XPacketTypeManager.cs`, lines 103–104

---

## Call Stack

| Frame | Location | Line |
|-------|----------|------|
| 1 | `XProtocol.XPacketTypeManager.BuildDescriptors` | 103–104 |
| 2 | `XProtocol.XPacketTypeManager.Register<Test.TestPacket>` | 36 |
| 3 | `Test.Program.Main` | 24 |

---

## Root Cause

`TestPacket` contains a `string` field named `TestString`. The `BuildDescriptors` method in `XPacketTypeManager` enforces that **all fields must be value types** (`IsValueType == true`). `string` is a reference type (heap-allocated class), so it fails the guard:

```csharp
if (!f.FieldType.IsValueType)
{
	throw new InvalidOperationException(
		$"{t.Name}.{f.Name}: only value-type fields are supported.");
}
```

### TestPacket fields at time of exception

| Field | Type | Value type? |
|-------|------|-------------|
| `TestNumber` | `int` | ✅ |
| `TestDouble` | `double` | ✅ |
| `TestBoolean` | `bool` | ✅ |
| `TestString` | `string` | ❌ — **causes exception** |

---

## Affected File

**`XProtocol\XPacketTypeManager.cs`** — `BuildDescriptors` (line 87–113)

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

	foreach (var f in sorted)
	{
		if (!f.FieldType.IsValueType)   // <-- string fails here
		{
			throw new InvalidOperationException(
				$"{t.Name}.{f.Name}: only value-type fields are supported.");
		}
	}
	// ...
}
```

---

## Fix Options

### Option 1 — Remove `TestString` (simplest)
Drop the `string` field from `TestPacket`. The `Console.WriteLine` in `Program.cs` line 39 does not print `TestString`, so it is unused in the roundtrip test.

### Option 2 — Extend the serializer to support `string`
Update `BuildDescriptors` to allow `string` as a special case, and update serialization/deserialization logic in `XPacketConverter` / `FieldDescriptor` to handle length-prefixed UTF-8 encoding.

### Option 3 — Use a fixed-size char/byte array struct
Replace `string` with a blittable fixed-size buffer struct if binary layout needs to stay uniform.

---

## Locals at Exception Point

| Variable | Type | Value |
|----------|------|-------|
| `t` | `System.Type` | `TestPacket` |
| `fields` | `List<FieldInfo>` | Count = 4 |
| `sorted` | `FieldInfo[]` | Length = 4 |
| `f` | `FieldInfo` | `TestString` (the offending field) |

---

## Reproduction

```csharp
// Test\Program.cs — line 24
XPacketTypeManager.Register<TestPacket>(XPacketType.GetOrderAllMethod, 1, 1);

// TestPacket definition (triggers the exception on registration)
public class TestPacket
{
	public int TestNumber;
	public double TestDouble;
	public bool TestBoolean;
	public string TestString;  // ❌ reference type — not supported
}
```

---

## Repository

- **Remote:** https://github.com/tdav/XProtokol  
- **Branch:** master  
- **Workspace:** `C:\Works\XProtokol\`
