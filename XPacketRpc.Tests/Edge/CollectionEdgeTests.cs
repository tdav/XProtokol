using XPacketRpc;

namespace XPacketRpc.Tests.Edge;

public sealed class IntListHolder { public List<int> L { get; init; } = new(); }
public sealed class StringArrayHolder { public string[] A { get; init; } = Array.Empty<string>(); }

public class CollectionEdgeTests
{
    private readonly XPacketRpcSerializer s = new();

    [Test] public async Task Empty_list_roundtrips()
    {
        var got = s.Deserialize<IntListHolder>(s.Serialize(new IntListHolder { L = new() }));
        await Assert.That(got!.L.Count).IsEqualTo(0);
    }

    [Test] public async Task Empty_array_roundtrips()
    {
        var got = s.Deserialize<StringArrayHolder>(s.Serialize(new StringArrayHolder { A = Array.Empty<string>() }));
        await Assert.That(got!.A.Length).IsEqualTo(0);
    }

    [Test] public async Task List_with_varint_size_above_127()
    {
        var input = new IntListHolder { L = Enumerable.Range(0, 500).ToList() };
        var got = s.Deserialize<IntListHolder>(s.Serialize(input));
        await Assert.That(got!.L.Count).IsEqualTo(500);
        await Assert.That(got.L[499]).IsEqualTo(499);
    }
}
