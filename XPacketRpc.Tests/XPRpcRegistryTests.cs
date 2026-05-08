using System.Buffers;
using XPacketRpc;
using XPacketRpc.Internal;

namespace XPacketRpc.Tests;

public class XPRpcRegistryTests
{
    private sealed class Probe { public int X; }

    [Test]
    public async Task Touch_is_no_op_and_does_not_throw()
    {
        XPRpc.Touch<Probe>();
        await Assert.That(true).IsTrue();
    }

    [Test]
    public async Task Write_unregistered_throws_MissingSerializer()
    {
        using var buf = new PooledBufferWriter(ArrayPool<byte>.Shared);
        // Probe is unregistered — but Register() in earlier test could pollute static state.
        // Use a different sentinel-type to ensure unregistered.
        await Assert.That(() => XPRpc.Write(new Sentinel1(), buf))
            .Throws<MissingSerializerException>();
    }

    [Test]
    public async Task Read_unregistered_throws_MissingSerializer()
    {
        await Assert.That(() =>
        {
            var span = new ReadOnlySpan<byte>(new byte[1]);
            _ = XPRpc.Read<Sentinel2>(span);
        }).Throws<MissingSerializerException>();
    }

    [Test]
    public async Task Register_then_Write_invokes_delegate()
    {
        bool called = false;
        XPRpc.Register<Sentinel3>(
            (v, w) => { called = true; },
            (ref XPRpcReader r) => new Sentinel3());

        using var buf = new PooledBufferWriter(ArrayPool<byte>.Shared);
        XPRpc.Write(new Sentinel3(), buf);

        await Assert.That(called).IsTrue();
    }

    private sealed class Sentinel1 { }
    private sealed class Sentinel2 { }
    private sealed class Sentinel3 { }
}
