using System.Buffers;
using System.Text;
using XPacketRpc;
using XPacketRpc.Internal;

namespace XPacketRpc.Tests;

public class XPRpcReaderVariableTests
{
    private static byte[] Encode(Action<PooledBufferWriter> a)
    {
        using var w = new PooledBufferWriter(ArrayPool<byte>.Shared, 64);
        a(w);
        return w.WrittenSpan.ToArray();
    }

    [Test]
    public async Task ReadString_empty()
    {
        var bytes = Encode(w => Writers.WriteString("", w));
        var r = new XPRpcReader(bytes);
        var got = r.ReadString();
        await Assert.That(got).IsEqualTo("");
    }

    [Test]
    public async Task ReadString_ascii()
    {
        var bytes = Encode(w => Writers.WriteString("Bob", w));
        var r = new XPRpcReader(bytes);
        var got = r.ReadString();
        await Assert.That(got).IsEqualTo("Bob");
    }

    [Test]
    public async Task ReadString_unicode_BMP()
    {
        var bytes = Encode(w => Writers.WriteString("Привет, мир!", w));
        var r = new XPRpcReader(bytes);
        var got = r.ReadString();
        await Assert.That(got).IsEqualTo("Привет, мир!");
    }

    [Test]
    public async Task ReadString_unicode_supplementary()
    {
        var s = "😀 emoji";
        var bytes = Encode(w => Writers.WriteString(s, w));
        var r = new XPRpcReader(bytes);
        var got = r.ReadString();
        await Assert.That(got).IsEqualTo(s);
    }

    [Test]
    public async Task ReadBytes_roundtrips()
    {
        var input = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var bytes = Encode(w => Writers.WriteBytes(input, w));
        var r = new XPRpcReader(bytes);
        var got = r.ReadBytes();
        await Assert.That(got).IsEquivalentTo(input);
    }

    [Test]
    public async Task ReadBytes_empty()
    {
        var bytes = Encode(w => Writers.WriteBytes(Array.Empty<byte>(), w));
        var r = new XPRpcReader(bytes);
        var got = r.ReadBytes();
        await Assert.That(got).IsEquivalentTo(Array.Empty<byte>());
    }

    [Test]
    public async Task ReadGuid_roundtrips()
    {
        var g = Guid.Parse("01020304-0506-0708-090A-0B0C0D0E0F10");
        var bytes = Encode(w => Writers.WriteGuid(g, w));
        var r = new XPRpcReader(bytes);
        var got = r.ReadGuid();
        await Assert.That(got).IsEqualTo(g);
    }

    [Test]
    public async Task ReadDateTime_roundtrips_with_kind()
    {
        var dt = new DateTime(2026, 1, 15, 12, 30, 45, DateTimeKind.Utc);
        var bytes = Encode(w => Writers.WriteDateTime(dt, w));
        var r = new XPRpcReader(bytes);
        var got = r.ReadDateTime();
        await Assert.That(got).IsEqualTo(dt);
        await Assert.That(got.Kind).IsEqualTo(DateTimeKind.Utc);
    }

    [Test]
    public async Task ReadDateTimeOffset_roundtrips_with_offset()
    {
        var dto = new DateTimeOffset(2026, 1, 15, 12, 30, 45, TimeSpan.FromMinutes(180));
        var bytes = Encode(w => Writers.WriteDateTimeOffset(dto, w));
        var r = new XPRpcReader(bytes);
        var got = r.ReadDateTimeOffset();
        await Assert.That(got).IsEqualTo(dto);
    }

    [Test]
    public async Task ReadTimeSpan_roundtrips()
    {
        var ts = TimeSpan.FromSeconds(7);
        var bytes = Encode(w => Writers.WriteTimeSpan(ts, w));
        var r = new XPRpcReader(bytes);
        var got = r.ReadTimeSpan();
        await Assert.That(got).IsEqualTo(ts);
    }

    [Test]
    [Arguments("123.456")]
    [Arguments("-1")]
    [Arguments("0")]
    [Arguments("79228162514264337593543950335")]
    [Arguments("-79228162514264337593543950335")]
    public async Task ReadDecimal_roundtrips_signed_and_extremes(string s)
    {
        var d = decimal.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        var bytes = Encode(w => Writers.WriteDecimalLE(d, w));
        var r = new XPRpcReader(bytes);
        var got = r.ReadDecimal();
        await Assert.That(got).IsEqualTo(d);
    }
}
