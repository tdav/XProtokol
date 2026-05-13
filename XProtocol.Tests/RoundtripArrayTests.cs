using System.Linq;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using XProtocol;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    public class RoundtripArrayTests
    {
        [Test]
        public async Task IntArray_Roundtrips()
        {
            var dto = new IntArrayDto { A = 7, Values = new[] { 1, 2, 3, 4, 5 } };
            var packet = XPacketConverter.Serialize(dto);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var back = XPacketConverter.Deserialize<IntArrayDto>(parsed);

            await Assert.That(back.A).IsEqualTo(7);
            await Assert.That(back.Values).IsEquivalentTo(new[] { 1, 2, 3, 4, 5 });
        }

        [Test]
        public async Task IntArray_Null_BecomesEmpty()
        {
            var dto = new IntArrayDto { A = 1, Values = null };
            var packet = XPacketConverter.Serialize(dto);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var back = XPacketConverter.Deserialize<IntArrayDto>(parsed);

            await Assert.That(back.Values).IsNotNull();
            await Assert.That(back.Values.Length).IsEqualTo(0);
        }

        [Test]
        public async Task ByteArray_Large_CrossesChunks()
        {
            var src = Enumerable.Range(0, 1000).Select(i => (byte)(i % 256)).ToArray();
            var dto = new ByteArrayDto { Payload = src };

            var packet = XPacketConverter.Serialize(dto);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var back = XPacketConverter.Deserialize<ByteArrayDto>(parsed);

            await Assert.That(back.Payload.Length).IsEqualTo(1000);
            await Assert.That(back.Payload).IsEquivalentTo(src);
        }

        [Test]
        public async Task StringArray_RoundtripsWithUnicode()
        {
            var dto = new StringArrayDto { Tags = new[] { "ascii", "Привет", "🚀" } };
            var packet = XPacketConverter.Serialize(dto);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var back = XPacketConverter.Deserialize<StringArrayDto>(parsed);

            await Assert.That(back.Tags.Length).IsEqualTo(3);
            await Assert.That(back.Tags[1]).IsEqualTo("Привет");
            await Assert.That(back.Tags[2]).IsEqualTo("🚀");
        }

        [Test]
        public async Task IntArray_Empty_Roundtrips()
        {
            var dto = new IntArrayDto { A = 2, Values = new int[0] };
            var packet = XPacketConverter.Serialize(dto);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var back = XPacketConverter.Deserialize<IntArrayDto>(parsed);

            await Assert.That(back.Values.Length).IsEqualTo(0);
        }
    }
}
