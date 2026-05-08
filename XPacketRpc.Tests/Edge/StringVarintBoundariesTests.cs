using System.Buffers;
using System.Text;
using XPacketRpc.Internal;

namespace XPacketRpc.Tests.Edge;

// Verifies the single-pass + backpatch path in Writers.WriteString:
// when the actual UTF-8 length encodes to fewer than 5 varint bytes,
// the payload must be shifted left so the wire format stays compact.
// Boundaries chosen at varint-size transitions: 0/1/2/3/4-byte encodings.
public class StringVarintBoundariesTests
{
    private static byte[] Encode(string s)
    {
        using var w = new PooledBufferWriter(ArrayPool<byte>.Shared, 64);
        Writers.WriteString(s, w);
        return w.WrittenSpan.ToArray();
    }

    private static byte[] BuildExpected(string s)
    {
        var payload = Encoding.UTF8.GetBytes(s);
        using var ms = new MemoryStream();
        uint value = (uint)payload.Length;
        while (value >= 0x80)
        {
            ms.WriteByte((byte)(value | 0x80));
            value >>= 7;
        }
        ms.WriteByte((byte)value);
        ms.Write(payload, 0, payload.Length);
        return ms.ToArray();
    }

    [Test]
    public async Task Empty_emits_single_zero_byte()
    {
        var got = Encode("");
        await Assert.That(got).IsEquivalentTo(new byte[] { 0x00 });
    }

    [Test]
    public async Task Length_127_uses_one_varint_byte()
    {
        // 127 = 0x7F, single varint byte; payload then immediately follows.
        var input = new string('a', 127);
        var got = Encode(input);
        await Assert.That(got.Length).IsEqualTo(1 + 127);
        await Assert.That(got[0]).IsEqualTo((byte)0x7F);
    }

    [Test]
    public async Task Length_128_uses_two_varint_bytes()
    {
        // First length needing 2 varint bytes — exercises a 3-byte left shift.
        var input = new string('b', 128);
        var got = Encode(input);
        await Assert.That(got.Length).IsEqualTo(2 + 128);
        await Assert.That(got[0]).IsEqualTo((byte)0x80);
        await Assert.That(got[1]).IsEqualTo((byte)0x01);
    }

    [Test]
    public async Task Length_16383_uses_two_varint_bytes()
    {
        // 16383 = 0x3FFF = max value encodable in 2 varint bytes.
        var input = new string('c', 16383);
        var got = Encode(input);
        await Assert.That(got.Length).IsEqualTo(2 + 16383);
        await Assert.That(got[0]).IsEqualTo((byte)0xFF);
        await Assert.That(got[1]).IsEqualTo((byte)0x7F);
    }

    [Test]
    public async Task Length_16384_uses_three_varint_bytes()
    {
        // First length needing 3 varint bytes — exercises a 2-byte left shift.
        var input = new string('d', 16384);
        var got = Encode(input);
        await Assert.That(got.Length).IsEqualTo(3 + 16384);
        await Assert.That(got[0]).IsEqualTo((byte)0x80);
        await Assert.That(got[1]).IsEqualTo((byte)0x80);
        await Assert.That(got[2]).IsEqualTo((byte)0x01);
    }

    [Test]
    public async Task Backpatch_payload_intact_at_each_boundary()
    {
        // Property test: the backpatched output must equal the canonical
        // varint-then-payload encoding for every boundary length.
        int[] lengths = { 1, 126, 127, 128, 129, 16382, 16383, 16384, 16385 };
        foreach (var len in lengths)
        {
            var input = new string('z', len);
            var got = Encode(input);
            var expected = BuildExpected(input);
            await Assert.That(got).IsEquivalentTo(expected);
        }
    }

    [Test]
    public async Task Multibyte_utf8_uses_actual_byte_count_not_char_count()
    {
        // 100 cyrillic chars = 200 UTF-8 bytes (each codepoint = 2 bytes in UTF-8).
        // Verifies length prefix uses byte count from the encoding pass, not char count.
        var input = new string('я', 100);
        var got = Encode(input);
        var utf8 = Encoding.UTF8.GetBytes(input);
        await Assert.That(utf8.Length).IsEqualTo(200);
        var expected = BuildExpected(input);
        await Assert.That(got).IsEquivalentTo(expected);
    }
}
