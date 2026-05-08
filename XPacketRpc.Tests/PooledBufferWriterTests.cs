using System.Buffers;
using XPacketRpc.Internal;

namespace XPacketRpc.Tests;

public class PooledBufferWriterTests
{
    [Test]
    public async Task Empty_writer_has_zero_written()
    {
        using var w = new PooledBufferWriter(ArrayPool<byte>.Shared, initialCapacity: 16);

        await Assert.That(w.WrittenCount).IsEqualTo(0);
        await Assert.That(w.WrittenSpan.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Write_advance_grows_written_span()
    {
        using var w = new PooledBufferWriter(ArrayPool<byte>.Shared, initialCapacity: 16);

        var span = w.GetSpan(4);
        span[0] = 1; span[1] = 2; span[2] = 3; span[3] = 4;
        w.Advance(4);

        await Assert.That(w.WrittenCount).IsEqualTo(4);
        await Assert.That(w.WrittenSpan.ToArray()).IsEquivalentTo(new byte[] { 1, 2, 3, 4 });
    }

    [Test]
    public async Task GetSpan_grows_buffer_when_needed()
    {
        using var w = new PooledBufferWriter(ArrayPool<byte>.Shared, initialCapacity: 4);

        var span = w.GetSpan(64);

        await Assert.That(span.Length).IsGreaterThanOrEqualTo(64);
    }

    [Test]
    public async Task Advance_negative_throws()
    {
        using var w = new PooledBufferWriter(ArrayPool<byte>.Shared);
        await Assert.That(() => w.Advance(-1)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Advance_past_buffer_throws()
    {
        using var w = new PooledBufferWriter(ArrayPool<byte>.Shared, initialCapacity: 4);
        int spanLength = w.GetSpan(4).Length;

        await Assert.That(() => w.Advance(spanLength + 1)).Throws<InvalidOperationException>();
    }
}
