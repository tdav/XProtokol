using XPacketRpc;
using XPacketRpc.Internal;

namespace XPacketRpc.Tests;

public class XPacketRpcSerializerTests
{
    private sealed class TinyDto { public int Id; public string Name = ""; }

    private static void RegisterTinyDto()
    {
        XPRpc.Register<TinyDto>(
            (v, w) =>
            {
                Writers.WriteInt32LE(v.Id, w);
                Writers.WriteString(v.Name, w);
            },
            (ref XPRpcReader r) => new TinyDto { Id = r.ReadInt32(), Name = r.ReadString() });
    }

    [Test]
    public async Task ContentType_is_application_x_xprotocol_rpc()
    {
        var s = new XPacketRpcSerializer();
        await Assert.That(s.ContentType).IsEqualTo("application/x-xprotocol-rpc");
        await Assert.That(XPacketRpcSerializer.XPacketRpcContentType).IsEqualTo("application/x-xprotocol-rpc");
    }

    [Test]
    public async Task Serialize_null_throws_ArgumentNullException()
    {
        var s = new XPacketRpcSerializer();
        await Assert.That(() => s.Serialize<string>(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Roundtrip_via_facade()
    {
        RegisterTinyDto();
        var s = new XPacketRpcSerializer();
        var input = new TinyDto { Id = 42, Name = "Hello" };

        byte[] bytes = s.Serialize(input);
        var got = s.Deserialize<TinyDto>(bytes);

        await Assert.That(got).IsNotNull();
        await Assert.That(got!.Id).IsEqualTo(42);
        await Assert.That(got.Name).IsEqualTo("Hello");
    }

    [Test]
    public async Task Deserialize_unregistered_throws_MissingSerializer()
    {
        var s = new XPacketRpcSerializer();
        // Use a fresh nested type that wasn't registered to avoid cross-test pollution
        await Assert.That(() => s.Deserialize<UnregisteredProbe>(ReadOnlyMemory<byte>.Empty))
            .Throws<MissingSerializerException>();
    }

    private sealed class UnregisteredProbe { public int Y; }
}
