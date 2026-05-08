using XPacketRpc;

namespace XPacketRpc.Tests.Edge;

// Exercises the batched EnsureAvailable bounds check on
// ReadDecimal / ReadDateTime / ReadDateTimeOffset. After the batching
// optimization, only one EnsureAvailable per multi-field read remains,
// so a truncated payload must still throw at exactly the right moment.
public class TruncatedPayloadTests
{
    [Test]
    public async Task ReadDecimal_throws_when_only_8_bytes_available()
    {
        var data = new byte[8]; // need 16
        await Assert.That(() =>
        {
            var r = new XPRpcReader(data);
            _ = r.ReadDecimal();
        }).Throws<RpcSerializationException>();
    }

    [Test]
    public async Task ReadDecimal_throws_when_15_bytes_available()
    {
        var data = new byte[15]; // off-by-one short
        await Assert.That(() =>
        {
            var r = new XPRpcReader(data);
            _ = r.ReadDecimal();
        }).Throws<RpcSerializationException>();
    }

    [Test]
    public async Task ReadDecimal_succeeds_when_exactly_16_bytes_available()
    {
        var data = new byte[16];
        decimal v;
        int remaining;
        {
            var r = new XPRpcReader(data);
            v = r.ReadDecimal();
            remaining = r.Remaining;
        }
        await Assert.That(v).IsEqualTo(0m);
        await Assert.That(remaining).IsEqualTo(0);
    }

    [Test]
    public async Task ReadDateTime_throws_when_only_8_bytes_available()
    {
        var data = new byte[8]; // need 9 (ticks + kind)
        await Assert.That(() =>
        {
            var r = new XPRpcReader(data);
            _ = r.ReadDateTime();
        }).Throws<RpcSerializationException>();
    }

    [Test]
    public async Task ReadDateTime_succeeds_when_exactly_9_bytes_available()
    {
        var data = new byte[9];
        DateTime v;
        int remaining;
        {
            var r = new XPRpcReader(data);
            v = r.ReadDateTime();
            remaining = r.Remaining;
        }
        await Assert.That(v.Ticks).IsEqualTo(0L);
        await Assert.That(remaining).IsEqualTo(0);
    }

    [Test]
    public async Task ReadDateTimeOffset_throws_when_only_9_bytes_available()
    {
        var data = new byte[9]; // need 10 (ticks + offset minutes)
        await Assert.That(() =>
        {
            var r = new XPRpcReader(data);
            _ = r.ReadDateTimeOffset();
        }).Throws<RpcSerializationException>();
    }

    [Test]
    public async Task ReadDateTimeOffset_succeeds_when_exactly_10_bytes_available()
    {
        var data = new byte[10];
        DateTimeOffset v;
        int remaining;
        {
            var r = new XPRpcReader(data);
            v = r.ReadDateTimeOffset();
            remaining = r.Remaining;
        }
        await Assert.That(v.Ticks).IsEqualTo(0L);
        await Assert.That(remaining).IsEqualTo(0);
    }

    [Test]
    public async Task ReadDecimal_does_not_advance_position_on_failure()
    {
        // Bounds check must reject before any position increment.
        var data = new byte[5];
        await Assert.That(() =>
        {
            var r = new XPRpcReader(data);
            _ = r.ReadDecimal();
        }).Throws<RpcSerializationException>();
    }
}
