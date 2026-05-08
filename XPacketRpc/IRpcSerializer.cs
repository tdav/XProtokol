namespace XPacketRpc;

public interface IRpcSerializer
{
    string ContentType { get; }
    byte[] Serialize<T>(T value);
    T? Deserialize<T>(ReadOnlyMemory<byte> payload);
}
