using XPacketRpc;

namespace XPacketRpc.Tests.Edge;

public sealed class StringHolder
{
    public string S { get; init; } = "";
}

public class StringEdgeTests
{
    private readonly XPacketRpcSerializer s = new();

    [Test] public async Task Empty_string_roundtrips()
    {
        var got = s.Deserialize<StringHolder>(s.Serialize(new StringHolder { S = "" }));
        await Assert.That(got!.S).IsEqualTo("");
    }

    [Test] public async Task Cyrillic_BMP_roundtrips()
    {
        var input = "Привет, мир! Тест на кириллицу.";
        var got = s.Deserialize<StringHolder>(s.Serialize(new StringHolder { S = input }));
        await Assert.That(got!.S).IsEqualTo(input);
    }

    [Test] public async Task Emoji_supplementary_roundtrips()
    {
        var input = "Hello 🌍🔥 emoji";
        var got = s.Deserialize<StringHolder>(s.Serialize(new StringHolder { S = input }));
        await Assert.That(got!.S).IsEqualTo(input);
    }

    [Test] public async Task Long_string_uses_multi_byte_varint()
    {
        var input = new string('x', 200);
        var got = s.Deserialize<StringHolder>(s.Serialize(new StringHolder { S = input }));
        await Assert.That(got!.S.Length).IsEqualTo(200);
    }
}
