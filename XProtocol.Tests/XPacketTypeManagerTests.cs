using System;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    public class BaseDtoForInheritance
    {
        public int BaseField;
    }

    public class DerivedDtoForInheritance : BaseDtoForInheritance
    {
        public double DerivedField;
    }

    public class XPacketTypeManagerTests
    {
        [Test]
        public async Task GetTypeFromPacket_RegisteredHandshake_ReturnsHandshake()
        {
            var pkt = XPacket.Create(1, 0);
            var resolved = XPacketTypeManager.GetTypeFromPacket(pkt);
            await Assert.That(resolved).IsEqualTo(XPacketType.Handshake);
        }

        [Test]
        public async Task GetTypeFromPacket_UnknownBytes_ReturnsUnknown()
        {
            var pkt = XPacket.Create(0xFE, 0xFE);
            var resolved = XPacketTypeManager.GetTypeFromPacket(pkt);
            await Assert.That(resolved).IsEqualTo(XPacketType.Unknown);
        }

        [Test]
        public async Task GetType_RegisteredEnumValue_ReturnsBytePair()
        {
            var (type, subtype) = XPacketTypeManager.GetType(XPacketType.Handshake);
            await Assert.That(type).IsEqualTo((byte)1);
            await Assert.That(subtype).IsEqualTo((byte)0);
        }

        [Test]
        public async Task BuildDescriptors_InheritedDto_IncludesBaseAndDerivedFields()
        {
            XPacketTypeManager.Register<DerivedDtoForInheritance>((XPacketType)150, 150, 0);

            var dto = new DerivedDtoForInheritance
            {
                BaseField = 7,
                DerivedField = 2.5
            };

            var packet = XPacketConverter.Serialize(dto);
            await Assert.That(packet.Fields.Count).IsEqualTo(2);

            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            await Assert.That(parsed).IsNotNull();

            var restored = XPacketConverter.Deserialize<DerivedDtoForInheritance>(parsed);
            await Assert.That(restored.BaseField).IsEqualTo(7);
            await Assert.That(restored.DerivedField).IsEqualTo(2.5);
        }
    }
}
