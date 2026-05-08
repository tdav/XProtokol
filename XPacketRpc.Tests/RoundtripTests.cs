using XPacketRpc;
using XPacketRpc.Tests.Dtos;

namespace XPacketRpc.Tests;

public class RoundtripTests
{
    private readonly XPacketRpcSerializer s = new();

    [Test]
    public async Task Vector3_roundtrip()
    {
        var input = new Vector3 { X = 1.5f, Y = -2.25f, Z = 3.0f };
        var got = s.Deserialize<Vector3>(s.Serialize(input));

        await Assert.That(got!.X).IsEqualTo(1.5f);
        await Assert.That(got.Y).IsEqualTo(-2.25f);
        await Assert.That(got.Z).IsEqualTo(3.0f);
    }

    [Test]
    public async Task LogEntry_roundtrip()
    {
        var input = new LogEntry
        {
            Timestamp = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.FromHours(3)),
            Level = 4,
            Message = "Hello, мир!",
            TraceId = Guid.Parse("01020304-0506-0708-090A-0B0C0D0E0F10"),
            SpanId = Guid.NewGuid(),
        };
        var got = s.Deserialize<LogEntry>(s.Serialize(input));

        await Assert.That(got!.Timestamp).IsEqualTo(input.Timestamp);
        await Assert.That(got.Level).IsEqualTo(input.Level);
        await Assert.That(got.Message).IsEqualTo(input.Message);
        await Assert.That(got.TraceId).IsEqualTo(input.TraceId);
        await Assert.That(got.SpanId).IsEqualTo(input.SpanId);
    }

    [Test]
    public async Task OrderRequest_roundtrip_with_5_items()
    {
        var input = new OrderRequest
        {
            Id = Guid.NewGuid(),
            CustomerId = 42,
            Items = Enumerable.Range(0, 5).Select(i => new OrderItem
            {
                ProductId = i, Quantity = i + 1, Price = i * 1.5m
            }).ToList()
        };

        var got = s.Deserialize<OrderRequest>(s.Serialize(input));

        await Assert.That(got!.Id).IsEqualTo(input.Id);
        await Assert.That(got.CustomerId).IsEqualTo(42);
        await Assert.That(got.Items.Count).IsEqualTo(5);
        for (int i = 0; i < 5; i++)
        {
            await Assert.That(got.Items[i].ProductId).IsEqualTo(input.Items[i].ProductId);
            await Assert.That(got.Items[i].Price).IsEqualTo(input.Items[i].Price);
        }
    }

    [Test]
    public async Task UserProfile_roundtrip()
    {
        var input = new UserProfile
        {
            Id = Guid.NewGuid(),
            Name = "Anna",
            Address = new Address { Street = "Main", City = "Tashkent", Country = "UZ" },
            Tags = new[] { "vip", "early-adopter" }
        };
        var got = s.Deserialize<UserProfile>(s.Serialize(input));

        await Assert.That(got!.Name).IsEqualTo("Anna");
        await Assert.That(got.Address.City).IsEqualTo("Tashkent");
        await Assert.That(got.Tags).IsEquivalentTo(input.Tags);
    }

    [Test]
    public async Task ChunkPayload_16K_roundtrip()
    {
        var data = new byte[16 * 1024];
        new Random(42).NextBytes(data);
        var input = new ChunkPayload { Id = Guid.NewGuid(), Data = data };

        var got = s.Deserialize<ChunkPayload>(s.Serialize(input));
        await Assert.That(got!.Data).IsEquivalentTo(data);
    }

    [Test]
    public async Task BigDictionary_1000_roundtrip()
    {
        var input = new BigDictionary
        {
            Data = Enumerable.Range(0, 1000).ToDictionary(i => $"key-{i}", i => i)
        };

        var got = s.Deserialize<BigDictionary>(s.Serialize(input));
        await Assert.That(got!.Data.Count).IsEqualTo(1000);
        await Assert.That(got.Data["key-500"]).IsEqualTo(500);
    }

    [Test]
    public async Task DeepNested_roundtrip()
    {
        var input = new DeepNested
        {
            X = 1,
            Inner = new Level2
            {
                X = 2,
                Inner = new Level3
                {
                    X = 3,
                    Inner = new Level4
                    {
                        X = 4,
                        Inner = new Level5 { Value = 99 }
                    }
                }
            }
        };
        var got = s.Deserialize<DeepNested>(s.Serialize(input));

        await Assert.That(got!.Inner.Inner.Inner.Inner.Value).IsEqualTo(99);
        await Assert.That(got.X).IsEqualTo(1);
    }

    [Test]
    public async Task RecordRequest_roundtrip_via_ctor_binding()
    {
        var input = new RecordRequest(
            Guid.NewGuid(), 7, "test", DateTimeOffset.UtcNow, 99.99m)
        {
            Comment = "approved"
        };

        var got = s.Deserialize<RecordRequest>(s.Serialize(input));

        await Assert.That(got).IsEqualTo(input);
    }
}
