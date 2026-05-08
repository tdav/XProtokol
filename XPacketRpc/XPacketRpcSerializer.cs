using System.Buffers;
using XPacketRpc.Internal;

namespace XPacketRpc;

public sealed class XPacketRpcSerializer : IRpcSerializer
{
    public const string XPacketRpcContentType = "application/x-xprotocol-rpc";

    public string ContentType => XPacketRpcContentType;

    public byte[] Serialize<T>(T value)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));

        using var buffer = new PooledBufferWriter(ArrayPool<byte>.Shared, initialCapacity: 256);
        XPRpc.Write(value, buffer);
        return buffer.WrittenSpan.ToArray();
    }

    public T? Deserialize<T>(ReadOnlyMemory<byte> payload)
        => XPRpc.Read<T>(payload.Span);
}
