using XPacketRpc;

namespace XPacketRpc.Tests.E2E;

public sealed class SmokeDto
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string? Comment { get; init; }
}

public class SmokeRoundtripTests
{
    [Test]
    public async Task Roundtrip_simple_dto()
    {
        var s = new XPacketRpcSerializer();
        var input = new SmokeDto { Id = 7, Name = "Bob", Comment = null };

        var bytes = s.Serialize(input);
        var got = s.Deserialize<SmokeDto>(bytes);

        await Assert.That(got).IsNotNull();
        await Assert.That(got!.Id).IsEqualTo(7);
        await Assert.That(got.Name).IsEqualTo("Bob");
        await Assert.That(got.Comment).IsNull();
    }

    [Test]
    public async Task Roundtrip_with_comment()
    {
        var s = new XPacketRpcSerializer();
        var input = new SmokeDto { Id = 9, Name = "Alice", Comment = "test" };

        var bytes = s.Serialize(input);
        var got = s.Deserialize<SmokeDto>(bytes);

        await Assert.That(got).IsNotNull();
        await Assert.That(got!.Id).IsEqualTo(9);
        await Assert.That(got.Name).IsEqualTo("Alice");
        await Assert.That(got.Comment).IsEqualTo("test");
    }
}
