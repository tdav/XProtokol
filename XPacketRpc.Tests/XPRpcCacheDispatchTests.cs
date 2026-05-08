using System.Buffers;
using XPacketRpc;
using XPacketRpc.Internal;

namespace XPacketRpc.Tests;

// Verifies the per-T static Cache<T> dispatch added to XPRpc.Write/Read.
// The dictionary fallback is exercised by XPRpcRegistryTests; these tests
// focus on cache-specific semantics: re-registration, per-type isolation,
// and round-trip through the cached delegate.
public class XPRpcCacheDispatchTests
{
    private sealed class CacheProbeA { public int X; }
    private sealed class CacheProbeB { public int Y; }
    private sealed class CacheProbeC { public string S = ""; }

    [Test]
    public async Task Reregister_replaces_writer_delegate()
    {
        int firstCallCount = 0;
        int secondCallCount = 0;

        XPRpc.Register<CacheProbeA>(
            (v, w) => { firstCallCount++; },
            (ref XPRpcReader r) => new CacheProbeA());

        using (var buf = new PooledBufferWriter(ArrayPool<byte>.Shared))
        {
            XPRpc.Write(new CacheProbeA(), buf);
        }
        await Assert.That(firstCallCount).IsEqualTo(1);

        // Replace the registration with a new delegate.
        XPRpc.Register<CacheProbeA>(
            (v, w) => { secondCallCount++; },
            (ref XPRpcReader r) => new CacheProbeA());

        using (var buf = new PooledBufferWriter(ArrayPool<byte>.Shared))
        {
            XPRpc.Write(new CacheProbeA(), buf);
        }
        await Assert.That(secondCallCount).IsEqualTo(1);
        // The old delegate must not be invoked after re-registration.
        await Assert.That(firstCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task Cache_is_isolated_per_closed_generic_type()
    {
        // Registering CacheProbeB must not affect the unregistered CacheProbeC.
        XPRpc.Register<CacheProbeB>(
            (v, w) => Writers.WriteInt32LE(v.Y, w),
            (ref XPRpcReader r) => new CacheProbeB { Y = r.ReadInt32() });

        // CacheProbeC remains unregistered → MissingSerializerException.
        await Assert.That(() =>
        {
            var span = new ReadOnlySpan<byte>(new byte[4]);
            _ = XPRpc.Read<CacheProbeC>(span);
        }).Throws<MissingSerializerException>();

        // CacheProbeB round-trips via cached delegate.
        using var buf = new PooledBufferWriter(ArrayPool<byte>.Shared);
        XPRpc.Write(new CacheProbeB { Y = 0x12345678 }, buf);
        var got = XPRpc.Read<CacheProbeB>(buf.WrittenSpan);
        await Assert.That(got!.Y).IsEqualTo(0x12345678);
    }

    [Test]
    public async Task Read_unregistered_via_cache_throws_MissingSerializer()
    {
        // Sentinel type guaranteed unregistered for this test.
        await Assert.That(() =>
        {
            var span = new ReadOnlySpan<byte>(new byte[1]);
            _ = XPRpc.Read<UnregisteredSentinel>(span);
        }).Throws<MissingSerializerException>();
    }

    [Test]
    public async Task Write_unregistered_via_cache_throws_MissingSerializer()
    {
        using var buf = new PooledBufferWriter(ArrayPool<byte>.Shared);
        await Assert.That(() => XPRpc.Write(new UnregisteredSentinel2(), buf))
            .Throws<MissingSerializerException>();
    }

    private sealed class UnregisteredSentinel { }
    private sealed class UnregisteredSentinel2 { }
}
