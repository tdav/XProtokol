using XPacketRpc;

namespace XPacketRpc.Tests;

public class XPRpcReaderPrimitivesTests
{
    [Test]
    public async Task ReadByte_returns_value_and_advances()
    {
        // XPRpcReader is a ref struct — instances cannot live across an await
        // boundary, so capture all observed values before awaiting any assertion.
        var r = new XPRpcReader(new byte[] { 0xAB, 0xCD });
        var value = r.ReadByte();
        var position = r.Position;
        var remaining = r.Remaining;

        await Assert.That(value).IsEqualTo((byte)0xAB);
        await Assert.That(position).IsEqualTo(1);
        await Assert.That(remaining).IsEqualTo(1);
    }

    [Test]
    public async Task ReadInt16_reads_little_endian()
    {
        var r = new XPRpcReader(new byte[] { 0xFE, 0xCA });
        var value = r.ReadInt16();
        await Assert.That(value).IsEqualTo(unchecked((short)0xCAFE));
    }

    [Test]
    public async Task ReadInt32_reads_little_endian()
    {
        var r = new XPRpcReader(new byte[] { 0x78, 0x56, 0x34, 0x12 });
        var value = r.ReadInt32();
        await Assert.That(value).IsEqualTo(0x12345678);
    }

    [Test]
    public async Task ReadInt64_reads_little_endian()
    {
        var r = new XPRpcReader(new byte[] { 0xEF, 0xCD, 0xAB, 0x89, 0x67, 0x45, 0x23, 0x01 });
        var value = r.ReadInt64();
        await Assert.That(value).IsEqualTo(0x0123_4567_89AB_CDEF);
    }

    [Test]
    public async Task ReadSingle_roundtrips_float()
    {
        var w = BitConverter.GetBytes(3.14f);
        if (!BitConverter.IsLittleEndian) Array.Reverse(w);

        var r = new XPRpcReader(w);
        var value = r.ReadSingle();
        await Assert.That(value).IsEqualTo(3.14f);
    }

    [Test]
    public async Task ReadDouble_roundtrips_double()
    {
        var w = BitConverter.GetBytes(2.71828);
        if (!BitConverter.IsLittleEndian) Array.Reverse(w);

        var r = new XPRpcReader(w);
        var value = r.ReadDouble();
        await Assert.That(value).IsEqualTo(2.71828);
    }

    [Test]
    [Arguments(new byte[] { 0x00 }, 0u)]
    [Arguments(new byte[] { 0x01 }, 1u)]
    [Arguments(new byte[] { 0x7F }, 127u)]
    [Arguments(new byte[] { 0x80, 0x01 }, 128u)]
    [Arguments(new byte[] { 0xAC, 0x02 }, 300u)]
    [Arguments(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x0F }, 0xFFFFFFFFu)]
    public async Task ReadVarUInt32_decodes_LEB128(byte[] input, uint expected)
    {
        var r = new XPRpcReader(input);
        var value = r.ReadVarUInt32();
        await Assert.That(value).IsEqualTo(expected);
    }

    [Test]
    public async Task ReadByte_past_end_throws()
    {
        // XPRpcReader is a ref struct — it cannot be captured by a lambda closure.
        // Construct the reader inside the lambda to avoid the capture restriction.
        await Assert.That(() =>
        {
            var r = new XPRpcReader(Array.Empty<byte>());
            r.ReadByte();
        }).Throws<RpcSerializationException>();
    }

    [Test]
    public async Task ReadVarUInt32_overlong_throws()
    {
        // 6+ continuation bytes — invalid for uint32
        var input = new byte[] { 0x80, 0x80, 0x80, 0x80, 0x80, 0x01 };
        await Assert.That(() =>
        {
            var r = new XPRpcReader(input);
            r.ReadVarUInt32();
        }).Throws<RpcSerializationException>();
    }
}
