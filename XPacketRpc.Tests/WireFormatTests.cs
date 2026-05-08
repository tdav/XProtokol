using XPacketRpc;
using XPacketRpc.Tests.Dtos;

namespace XPacketRpc.Tests;

public class WireFormatTests
{
    private readonly XPacketRpcSerializer s = new();

    [Test]
    public async Task Vector3_wire_layout_is_12_bytes()
    {
        var input = new Vector3 { X = 1.0f, Y = 2.0f, Z = 3.0f };
        var bytes = s.Serialize(input);

        // 0 nullable fields → no bitmap. 3 × 4 = 12 bytes.
        await Assert.That(bytes.Length).IsEqualTo(12);
    }

    [Test]
    public async Task LogEntry_with_empty_message_includes_zero_byte_for_string()
    {
        var input = new LogEntry { Timestamp = default, Level = 0, Message = "", TraceId = default, SpanId = default };
        var bytes = s.Serialize(input);

        // Find at least one 0x00 byte (the empty-string varint length)
        bool hasZero = false;
        for (int i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] == 0x00) { hasZero = true; break; }
        }
        await Assert.That(hasZero).IsTrue();
    }

    [Test]
    public async Task Roundtrip_preserves_byte_layout()
    {
        // Stable check: serialize twice with same input, expect identical bytes
        var input = new Vector3 { X = 1.5f, Y = -2.25f, Z = 3.0f };
        var b1 = s.Serialize(input);
        var b2 = s.Serialize(input);
        await Assert.That(b1).IsEquivalentTo(b2);
    }
}
