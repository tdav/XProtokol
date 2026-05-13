using System.Linq;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using XProtocol;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    public class RoundtripListTests
    {
        [Test]
        public async Task IntList_Roundtrips()
        {
            var dto = new IntListDto { Numbers = new System.Collections.Generic.List<int> { 1, 2, 3 } };
            var packet = XPacketConverter.Serialize(dto);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var back = XPacketConverter.Deserialize<IntListDto>(parsed);

            await Assert.That(back.Numbers).IsEquivalentTo(new[] { 1, 2, 3 });
        }

        [Test]
        public async Task IntList_Null_BecomesEmptyList()
        {
            var dto = new IntListDto { Numbers = null };
            var packet = XPacketConverter.Serialize(dto);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var back = XPacketConverter.Deserialize<IntListDto>(parsed);

            await Assert.That(back.Numbers).IsNotNull();
            await Assert.That(back.Numbers.Count).IsEqualTo(0);
        }

        [Test]
        public async Task StringList_Empty_Roundtrips()
        {
            var dto = new StringListDto { Header = "h", Items = new System.Collections.Generic.List<string>() };
            var packet = XPacketConverter.Serialize(dto);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var back = XPacketConverter.Deserialize<StringListDto>(parsed);

            await Assert.That(back.Header).IsEqualTo("h");
            await Assert.That(back.Items.Count).IsEqualTo(0);
        }

        [Test]
        public async Task StringList_WithMany_Roundtrips()
        {
            var items = Enumerable.Range(0, 50).Select(i => $"item{i}").ToList();
            var dto = new StringListDto { Header = "head", Items = items };
            var packet = XPacketConverter.Serialize(dto);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var back = XPacketConverter.Deserialize<StringListDto>(parsed);

            await Assert.That(back.Items.Count).IsEqualTo(50);
            await Assert.That(back.Items[49]).IsEqualTo("item49");
        }

        [Test]
        public async Task StringList_WithNullElement_NormalizesToEmpty()
        {
            var dto = new StringListDto
            {
                Header = "h",
                Items = new System.Collections.Generic.List<string> { "x", null, "z" }
            };
            var packet = XPacketConverter.Serialize(dto);
            var parsed = XPacket.Parse(packet.ToPacket());
            var back = XPacketConverter.Deserialize<StringListDto>(parsed);

            await Assert.That(back.Items.Count).IsEqualTo(3);
            await Assert.That(back.Items[1]).IsEqualTo("");
        }
    }
}
