using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    public class RoundtripTests
    {
        [Test]
        public async Task SimpleDto_RoundtripPreservesValues()
        {
            var original = new SimpleDto { A = 42, B = 3.1415, C = true };

            var packet = XPacketConverter.Serialize(original);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            await Assert.That(parsed).IsNotNull();

            var restored = XPacketConverter.Deserialize<SimpleDto>(parsed);

            await Assert.That(restored.A).IsEqualTo(original.A);
            await Assert.That(restored.B).IsEqualTo(original.B);
            await Assert.That(restored.C).IsEqualTo(original.C);
        }

        [Test]
        public async Task SimpleDto_FieldOrderMatchesDeclarationOrder()
        {
            var dto = new SimpleDto { A = 1, B = 2.0, C = false };
            var packet = XPacketConverter.Serialize(dto);

            await Assert.That(packet.Fields.Count).IsEqualTo(3);
            await Assert.That(packet.Fields[0].FieldSize).IsEqualTo((byte)4);
            await Assert.That(packet.Fields[1].FieldSize).IsEqualTo((byte)8);
            await Assert.That((int)packet.Fields[2].FieldSize).IsGreaterThanOrEqualTo(1);
        }

        [Test]
        public async Task EmptyDto_RoundtripProducesZeroFields()
        {
            var original = new EmptyDto();

            var packet = XPacketConverter.Serialize(original);
            await Assert.That(packet.Fields.Count).IsEqualTo(0);

            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            await Assert.That(parsed).IsNotNull();
            await Assert.That(parsed.Fields.Count).IsEqualTo(0);

            var restored = XPacketConverter.Deserialize<EmptyDto>(parsed);
            await Assert.That(restored).IsNotNull();
        }

        [Test]
        public async Task XPacketHandshake_RoundtripPreservesValue()
        {
            var original = new XPacketHandshake { MagicHandshakeNumber = 12345 };

            var packet = XPacketConverter.Serialize(original);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            await Assert.That(parsed).IsNotNull();

            var restored = XPacketConverter.Deserialize<XPacketHandshake>(parsed);
            await Assert.That(restored.MagicHandshakeNumber).IsEqualTo(original.MagicHandshakeNumber);
        }

        [Test]
        public async Task StringDto_RoundtripShortAscii()
        {
            var original = new StringDto { A = 7, S = "hello", B = true };

            var packet = XPacketConverter.Serialize(original);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            await Assert.That(parsed).IsNotNull();

            var restored = XPacketConverter.Deserialize<StringDto>(parsed);

            await Assert.That(restored.A).IsEqualTo(original.A);
            await Assert.That(restored.S).IsEqualTo(original.S);
            await Assert.That(restored.B).IsEqualTo(original.B);
        }
    }
}
