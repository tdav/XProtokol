namespace XPacketRpc.Tests.Dtos;

public sealed class Vector3
{
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
}

public sealed class LogEntry
{
    public DateTimeOffset Timestamp { get; init; }
    public byte Level { get; init; }
    public string Message { get; init; } = "";
    public Guid TraceId { get; init; }
    public Guid SpanId { get; init; }
}

public sealed class OrderItem
{
    public int ProductId { get; init; }
    public int Quantity { get; init; }
    public decimal Price { get; init; }
}

public sealed class OrderRequest
{
    public Guid Id { get; init; }
    public int CustomerId { get; init; }
    public List<OrderItem> Items { get; init; } = new();
}

public sealed class Address
{
    public string Street { get; init; } = "";
    public string City { get; init; } = "";
    public string Country { get; init; } = "";
}

public sealed class UserProfile
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public Address Address { get; init; } = new();
    public string[] Tags { get; init; } = Array.Empty<string>();
}

public sealed class ChunkPayload
{
    public Guid Id { get; init; }
    public byte[] Data { get; init; } = Array.Empty<byte>();
}

public sealed class BigDictionary
{
    public Dictionary<string, int> Data { get; init; } = new();
}

public sealed class Level5 { public int Value { get; init; } }
public sealed class Level4 { public Level5 Inner { get; init; } = new(); public int X { get; init; } }
public sealed class Level3 { public Level4 Inner { get; init; } = new(); public int X { get; init; } }
public sealed class Level2 { public Level3 Inner { get; init; } = new(); public int X { get; init; } }
public sealed class DeepNested { public Level2 Inner { get; init; } = new(); public int X { get; init; } }

public sealed record class RecordRequest(
    Guid Id, int CustomerId, string Name, DateTimeOffset CreatedAt, decimal Total)
{
    public string? Comment { get; init; }
}
