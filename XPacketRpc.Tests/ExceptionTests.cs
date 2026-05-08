using XPacketRpc;

namespace XPacketRpc.Tests;

public class ExceptionTests
{
    [Test]
    public async Task MissingSerializerException_includes_type_in_message()
    {
        var ex = new MissingSerializerException(typeof(string));

        await Assert.That(ex.MissingType).IsEqualTo(typeof(string));
        await Assert.That(ex.Message).Contains("System.String");
        await Assert.That(ex.Message).Contains("Touch<");
    }

    [Test]
    public async Task RpcSerializationException_carries_message_and_inner()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new RpcSerializationException("payload corrupt", inner);

        await Assert.That(ex.Message).IsEqualTo("payload corrupt");
        await Assert.That(ex.InnerException).IsSameReferenceAs(inner);
    }
}
