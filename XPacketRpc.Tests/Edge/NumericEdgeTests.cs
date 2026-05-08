using XPacketRpc;

namespace XPacketRpc.Tests.Edge;

public enum ByteEnum : byte { A = 0, B = 255 }
public enum LongEnum : long { Min = long.MinValue, Max = long.MaxValue }

public sealed class NumericHolder
{
    public decimal D { get; init; }
    public DateTime Dt { get; init; }
    public DateTimeOffset Dto { get; init; }
    public ByteEnum BE { get; init; }
    public LongEnum LE { get; init; }
}

public class NumericEdgeTests
{
    private readonly XPacketRpcSerializer s = new();

    [Test] public async Task Decimal_min_roundtrips()
    {
        var got = s.Deserialize<NumericHolder>(s.Serialize(new NumericHolder
        {
            D = decimal.MinValue, Dt = DateTime.UnixEpoch, Dto = DateTimeOffset.MinValue,
            BE = ByteEnum.A, LE = LongEnum.Min
        }));
        await Assert.That(got!.D).IsEqualTo(decimal.MinValue);
    }

    [Test] public async Task Decimal_max_roundtrips()
    {
        var got = s.Deserialize<NumericHolder>(s.Serialize(new NumericHolder
        {
            D = decimal.MaxValue, Dt = DateTime.UnixEpoch, Dto = DateTimeOffset.UtcNow,
            BE = ByteEnum.B, LE = LongEnum.Max
        }));
        await Assert.That(got!.D).IsEqualTo(decimal.MaxValue);
        await Assert.That(got.LE).IsEqualTo(LongEnum.Max);
    }

    [Test] public async Task DateTime_min_max_roundtrip()
    {
        var got = s.Deserialize<NumericHolder>(s.Serialize(new NumericHolder
        {
            D = 0m, Dt = DateTime.MinValue, Dto = DateTimeOffset.MaxValue,
            BE = ByteEnum.A, LE = LongEnum.Min
        }));
        await Assert.That(got!.Dt).IsEqualTo(DateTime.MinValue);
        await Assert.That(got.Dto).IsEqualTo(DateTimeOffset.MaxValue);
    }

    [Test] public async Task DateTime_kind_preserved()
    {
        var dt = new DateTime(2026, 5, 8, 12, 0, 0, DateTimeKind.Utc);
        var got = s.Deserialize<NumericHolder>(s.Serialize(new NumericHolder
        {
            D = 0m, Dt = dt, Dto = DateTimeOffset.UtcNow,
            BE = ByteEnum.A, LE = LongEnum.Min
        }));
        await Assert.That(got!.Dt.Kind).IsEqualTo(DateTimeKind.Utc);
    }
}
