using XPacketRpc.Benchmarks.Dtos.MP;
using XPacketRpc.Tests.Dtos;

namespace XPacketRpc.Benchmarks.Dtos;

public static class DtoFactory
{
    public static Vector3 Vector3() => new() { X = 1.5f, Y = 2.25f, Z = 3.0f };

    public static Vector3MP Vector3MP(Vector3 src) => new() { X = src.X, Y = src.Y, Z = src.Z };

    public static LogEntry LogEntry() => new()
    {
        Timestamp = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero),
        Level = 2,
        Message = "User logged in successfully",
        TraceId = new Guid("11111111-1111-1111-1111-111111111111"),
        SpanId = new Guid("22222222-2222-2222-2222-222222222222"),
    };

    public static LogEntryMP LogEntryMP(LogEntry src) => new()
    {
        Timestamp = src.Timestamp,
        Level = src.Level,
        Message = src.Message,
        TraceId = src.TraceId,
        SpanId = src.SpanId,
    };

    public static OrderRequest OrderRequest() => new()
    {
        Id = new Guid("33333333-3333-3333-3333-333333333333"),
        CustomerId = 42,
        Items =
        [
            new() { ProductId = 1, Quantity = 3, Price = 9.99m },
            new() { ProductId = 7, Quantity = 1, Price = 49.50m },
        ],
    };

    public static OrderRequestMP OrderRequestMP(OrderRequest src) => new()
    {
        Id = src.Id,
        CustomerId = src.CustomerId,
        Items = src.Items.Select(i => new OrderItemMP
        {
            ProductId = i.ProductId,
            Quantity = i.Quantity,
            Price = i.Price,
        }).ToList(),
    };

    public static UserProfile UserProfile() => new()
    {
        Id = new Guid("44444444-4444-4444-4444-444444444444"),
        Name = "Alice Smith",
        Address = new() { Street = "123 Main St", City = "Springfield", Country = "US" },
        Tags = ["admin", "user", "beta"],
    };

    public static UserProfileMP UserProfileMP(UserProfile src) => new()
    {
        Id = src.Id,
        Name = src.Name,
        Address = new AddressMP
        {
            Street = src.Address.Street,
            City = src.Address.City,
            Country = src.Address.Country,
        },
        Tags = src.Tags,
    };

    public static ChunkPayload ChunkPayload()
    {
        var data = new byte[256];
        for (var i = 0; i < data.Length; i++) data[i] = (byte)(i & 0xFF);
        return new() { Id = new Guid("55555555-5555-5555-5555-555555555555"), Data = data };
    }

    public static ChunkPayloadMP ChunkPayloadMP(ChunkPayload src) => new()
    {
        Id = src.Id,
        Data = src.Data,
    };
}
