using System.Buffers;
using System.Text;
using XPacketRpc.Internal;

namespace XPacketRpc.Tests;

public class WritersVariableTests
{
    private static byte[] Capture(Action<PooledBufferWriter> action)
    {
        using var w = new PooledBufferWriter(ArrayPool<byte>.Shared, 64);
        action(w);
        return w.WrittenSpan.ToArray();
    }

    [Test]
    public async Task WriteString_empty_emits_single_zero_byte()
    {
        var bytes = Capture(w => Writers.WriteString("", w));
        await Assert.That(bytes).IsEquivalentTo(new byte[] { 0x00 });
    }

    [Test]
    public async Task WriteString_ascii_writes_varint_then_utf8()
    {
        var bytes = Capture(w => Writers.WriteString("Bob", w));
        await Assert.That(bytes).IsEquivalentTo(new byte[] { 0x03, 0x42, 0x6F, 0x62 });
    }

    [Test]
    public async Task WriteString_unicode_BMP_writes_correct_byte_count()
    {
        var bytes = Capture(w => Writers.WriteString("Привет", w));
        var utf8 = Encoding.UTF8.GetBytes("Привет");
        await Assert.That(bytes.Length).IsEqualTo(utf8.Length + 1);
        await Assert.That(bytes[0]).IsEqualTo((byte)utf8.Length);
        await Assert.That(bytes.AsSpan(1).ToArray()).IsEquivalentTo(utf8);
    }

    [Test]
    public async Task WriteBytes_empty_emits_zero_length_only()
    {
        var bytes = Capture(w => Writers.WriteBytes(Array.Empty<byte>(), w));
        await Assert.That(bytes).IsEquivalentTo(new byte[] { 0x00 });
    }

    [Test]
    public async Task WriteBytes_writes_varint_then_raw()
    {
        var bytes = Capture(w => Writers.WriteBytes(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, w));
        await Assert.That(bytes).IsEquivalentTo(new byte[] { 0x04, 0xDE, 0xAD, 0xBE, 0xEF });
    }

    [Test]
    public async Task WriteGuid_emits_16_bytes()
    {
        var g = Guid.Parse("01020304-0506-0708-090A-0B0C0D0E0F10");
        var bytes = Capture(w => Writers.WriteGuid(g, w));
        await Assert.That(bytes.Length).IsEqualTo(16);
    }

    [Test]
    public async Task WriteDateTime_emits_ticks_plus_kind()
    {
        var dt = new DateTime(2026, 1, 15, 12, 30, 45, DateTimeKind.Utc);
        var bytes = Capture(w => Writers.WriteDateTime(dt, w));
        await Assert.That(bytes.Length).IsEqualTo(9);
        await Assert.That(bytes[8]).IsEqualTo((byte)DateTimeKind.Utc);
    }

    [Test]
    public async Task WriteDateTimeOffset_emits_ticks_plus_offset_minutes()
    {
        var dto = new DateTimeOffset(2026, 1, 15, 12, 30, 45, TimeSpan.FromMinutes(180));
        var bytes = Capture(w => Writers.WriteDateTimeOffset(dto, w));
        await Assert.That(bytes.Length).IsEqualTo(10);
    }

    [Test]
    public async Task WriteTimeSpan_emits_ticks()
    {
        var bytes = Capture(w => Writers.WriteTimeSpan(TimeSpan.FromSeconds(7), w));
        await Assert.That(bytes.Length).IsEqualTo(8);
    }

    [Test]
    public async Task WriteDecimalLE_emits_16_bytes()
    {
        var bytes = Capture(w => Writers.WriteDecimalLE(123.456m, w));
        await Assert.That(bytes.Length).IsEqualTo(16);
    }

    [Test]
    public async Task WriteDecimalLE_handles_negative()
    {
        var bytes = Capture(w => Writers.WriteDecimalLE(-1m, w));
        await Assert.That(bytes.Length).IsEqualTo(16);
    }
}
