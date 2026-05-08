using MemoryPack;

namespace XPacketRpc.Benchmarks.Dtos.MP;

[MemoryPackable]
public partial class Vector3MP
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
}

[MemoryPackable]
public partial class LogEntryMP
{
    public DateTimeOffset Timestamp { get; set; }
    public byte Level { get; set; }
    public string Message { get; set; } = "";
    public Guid TraceId { get; set; }
    public Guid SpanId { get; set; }
}

[MemoryPackable]
public partial class OrderItemMP
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

[MemoryPackable]
public partial class OrderRequestMP
{
    public Guid Id { get; set; }
    public int CustomerId { get; set; }
    public List<OrderItemMP> Items { get; set; } = new();
}

[MemoryPackable]
public partial class AddressMP
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string Country { get; set; } = "";
}

[MemoryPackable]
public partial class UserProfileMP
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public AddressMP Address { get; set; } = new();
    public string[] Tags { get; set; } = [];
}

[MemoryPackable]
public partial class ChunkPayloadMP
{
    public Guid Id { get; set; }
    public byte[] Data { get; set; } = [];
}
