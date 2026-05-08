namespace XPacketRpc.Tests;

public class GeneratorSmokeTests
{
    [Test]
    public async Task Generator_emits_marker_type()
    {
        // If generator works, type XPacketRpc.Generated.__GeneratorMarker
        // exists in this assembly (via generated source).
        var marker = Type.GetType("XPacketRpc.Generated.__GeneratorMarker, XPacketRpc.Tests");
        await Assert.That(marker).IsNotNull();
    }
}
