using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using XProtocol;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    public class RoundtripDictTests
    {
        [Test]
        public async Task IntStringDict_Roundtrips()
        {
            var dto = new IntStringDictDto
            {
                Map = new Dictionary<int, string> { { 1, "one" }, { 2, "two" }, { 3, "three" } }
            };
            var packet = XPacketConverter.Serialize(dto);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var back = XPacketConverter.Deserialize<IntStringDictDto>(parsed);

            await Assert.That(back.Map.Count).IsEqualTo(3);
            await Assert.That(back.Map[1]).IsEqualTo("one");
            await Assert.That(back.Map[3]).IsEqualTo("three");
        }

        [Test]
        public async Task StringIntDict_Empty_Roundtrips()
        {
            var dto = new StringIntDictDto { Map = new Dictionary<string, int>() };
            var packet = XPacketConverter.Serialize(dto);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var back = XPacketConverter.Deserialize<StringIntDictDto>(parsed);

            await Assert.That(back.Map.Count).IsEqualTo(0);
        }

        [Test]
        public async Task StringIntDict_Null_BecomesEmpty()
        {
            var dto = new StringIntDictDto { Map = null };
            var packet = XPacketConverter.Serialize(dto);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var back = XPacketConverter.Deserialize<StringIntDictDto>(parsed);

            await Assert.That(back.Map).IsNotNull();
            await Assert.That(back.Map.Count).IsEqualTo(0);
        }

        [Test]
        public async Task IntStringDict_Many_Roundtrips()
        {
            var src = Enumerable.Range(0, 30).ToDictionary(i => i, i => $"v{i}");
            var dto = new IntStringDictDto { Map = src };
            var packet = XPacketConverter.Serialize(dto);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var back = XPacketConverter.Deserialize<IntStringDictDto>(parsed);

            await Assert.That(back.Map.Count).IsEqualTo(30);
            foreach (var kv in src)
            {
                await Assert.That(back.Map[kv.Key]).IsEqualTo(kv.Value);
            }
        }
    }
}
