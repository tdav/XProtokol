using XPacketRpc.Internal;

namespace XPacketRpc.Tests;

public class Fnv1aTests
{
    [Test]
    [Arguments("", 0x811C9DC5u)]
    [Arguments("a", 0xE40C292Cu)]
    [Arguments("foobar", 0xBF9CF968u)]
    [Arguments("Id", 0x36E8B900u)]
    [Arguments("Name", 0x0FE07306u)]
    [Arguments("Comment", 0x984F9FFEu)]
    [Arguments("Scores", 0x0CF49432u)]
    public async Task Fnv1a_matches_canonical_vectors(string input, uint expected)
    {
        await Assert.That(Fnv1a.Hash(input)).IsEqualTo(expected);
    }

    [Test]
    public async Task Fnv1a_empty_string_returns_offset_basis()
    {
        await Assert.That(Fnv1a.Hash(string.Empty)).IsEqualTo(0x811C9DC5u);
    }

    [Test]
    public async Task Fnv1a_is_deterministic_across_calls()
    {
        var a = Fnv1a.Hash("HelloWorld");
        var b = Fnv1a.Hash("HelloWorld");

        await Assert.That(a).IsEqualTo(b);
    }
}
