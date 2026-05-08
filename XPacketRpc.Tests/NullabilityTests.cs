using XPacketRpc;

namespace XPacketRpc.Tests;

public sealed class NullableDto
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string? Comment { get; init; }
    public int? Score { get; init; }
}

public class NullabilityTests
{
    private readonly XPacketRpcSerializer s = new();

    [Test]
    public async Task Required_string_null_throws_on_serialize()
    {
        var input = new NullableDto { Id = 1, Name = null!, Comment = null, Score = null };

        await Assert.That(() => s.Serialize(input))
            .Throws<RpcSerializationException>();
    }

    [Test]
    public async Task Nullable_string_null_roundtrips()
    {
        var input = new NullableDto { Id = 1, Name = "ok", Comment = null, Score = null };
        var got = s.Deserialize<NullableDto>(s.Serialize(input));

        await Assert.That(got!.Comment).IsNull();
        await Assert.That(got.Score).IsNull();
    }

    [Test]
    public async Task Nullable_int_with_value_roundtrips()
    {
        var input = new NullableDto { Id = 1, Name = "ok", Comment = null, Score = 42 };
        var got = s.Deserialize<NullableDto>(s.Serialize(input));

        await Assert.That(got!.Score).IsEqualTo(42);
    }

    [Test]
    public async Task Both_nullable_filled_roundtrips()
    {
        var input = new NullableDto { Id = 1, Name = "ok", Comment = "yes", Score = 7 };
        var got = s.Deserialize<NullableDto>(s.Serialize(input));

        await Assert.That(got!.Comment).IsEqualTo("yes");
        await Assert.That(got.Score).IsEqualTo(7);
    }
}
