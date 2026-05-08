namespace XPacketRpc;

public sealed class RpcSerializationException : Exception
{
    public RpcSerializationException(string message) : base(message) { }
    public RpcSerializationException(string message, Exception inner) : base(message, inner) { }
}
