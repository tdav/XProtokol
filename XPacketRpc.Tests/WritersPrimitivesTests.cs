using System.Buffers;
using XPacketRpc.Internal;

namespace XPacketRpc.Tests;

public class WritersPrimitivesTests
{
    private static byte[] Capture(Action<PooledBufferWriter> action)
    {
        using var w = new PooledBufferWriter(ArrayPool<byte>.Shared, 64);
        action(w);
        return w.WrittenSpan.ToArray();
    }

    [Test]
    public async Task WriteByte_emits_one_byte()
    {
        var bytes = Capture(w => Writers.WriteByte(0xAB, w));
        await Assert.That(bytes).IsEquivalentTo(new byte[] { 0xAB });
    }

    [Test]
    public async Task WriteInt16LE_writes_little_endian()
    {
        var bytes = Capture(w => Writers.WriteInt16LE(unchecked((short)0xCAFE), w));
        await Assert.That(bytes).IsEquivalentTo(new byte[] { 0xFE, 0xCA });
    }

    [Test]
    public async Task WriteInt32LE_writes_little_endian()
    {
        var bytes = Capture(w => Writers.WriteInt32LE(0x12345678, w));
        await Assert.That(bytes).IsEquivalentTo(new byte[] { 0x78, 0x56, 0x34, 0x12 });
    }

    [Test]
    public async Task WriteInt64LE_writes_little_endian()
    {
        var bytes = Capture(w => Writers.WriteInt64LE(0x0123_4567_89AB_CDEF, w));
        await Assert.That(bytes).IsEquivalentTo(new byte[] { 0xEF, 0xCD, 0xAB, 0x89, 0x67, 0x45, 0x23, 0x01 });
    }

    [Test]
    public async Task WriteSingleLE_roundtrips_via_BitConverter()
    {
        var bytes = Capture(w => Writers.WriteSingleLE(3.14f, w));
        var expected = BitConverter.GetBytes(3.14f);
        if (!BitConverter.IsLittleEndian) Array.Reverse(expected);
        await Assert.That(bytes).IsEquivalentTo(expected);
    }

    [Test]
    public async Task WriteDoubleLE_roundtrips_via_BitConverter()
    {
        var bytes = Capture(w => Writers.WriteDoubleLE(2.71828, w));
        var expected = BitConverter.GetBytes(2.71828);
        if (!BitConverter.IsLittleEndian) Array.Reverse(expected);
        await Assert.That(bytes).IsEquivalentTo(expected);
    }

    [Test]
    [Arguments(0u, new byte[] { 0x00 })]
    [Arguments(1u, new byte[] { 0x01 })]
    [Arguments(127u, new byte[] { 0x7F })]
    [Arguments(128u, new byte[] { 0x80, 0x01 })]
    [Arguments(300u, new byte[] { 0xAC, 0x02 })]
    [Arguments(0xFFFFFFFFu, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x0F })]
    public async Task WriteVarUInt32_matches_LEB128(uint value, byte[] expected)
    {
        var bytes = Capture(w => Writers.WriteVarUInt32(value, w));
        await Assert.That(bytes).IsEquivalentTo(expected);
    }

    [Test]
    public async Task ThrowNullRequired_throws_RpcSerializationException()
    {
        var ex = await Assert.That(() => Writers.ThrowNullRequired("Foo"))
            .Throws<RpcSerializationException>();
        await Assert.That(ex!.Message).Contains("Foo");
    }
}
