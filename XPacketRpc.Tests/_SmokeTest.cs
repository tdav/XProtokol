namespace XPacketRpc.Tests;

public class SmokeTest
{
    [Test]
    public async Task Smoke()
    {
        await Assert.That(1 + 1).IsEqualTo(2);
    }
}
