using System;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace XProtocol.Tests
{
    public class RegistrationTests
    {
        [Test]
        public async Task Register_RejectsReferenceTypeField()
        {
            var ex = await Assert.That(() =>
                XPacketTypeManager.Register<BadDtoWithReferenceField>((XPacketType)90, 90, 0))
                .ThrowsExactly<InvalidOperationException>();

            await Assert.That(ex.Message).Contains("only value-type fields");
            await Assert.That(ex.Message).Contains("Bad");
        }

        [Test]
        public async Task Register_RejectsDuplicatePacketType()
        {
            var ex = await Assert.That(() =>
                XPacketTypeManager.Register<XPacketHandshake>(XPacketType.Handshake, 1, 0))
                .ThrowsExactly<InvalidOperationException>();

            await Assert.That(ex.Message).Contains("already registered");
        }

        [Test]
        public async Task GetType_ReturnsRegisteredPair()
        {
            var (type, subtype) = XPacketTypeManager.GetType(XPacketType.Handshake);
            await Assert.That(type).IsEqualTo((byte)1);
            await Assert.That(subtype).IsEqualTo((byte)0);
        }

        [Test]
        public async Task GetType_ThrowsForUnregistered()
        {
            var ex = await Assert.That(() => XPacketTypeManager.GetType((XPacketType)999))
                .ThrowsExactly<InvalidOperationException>();

            await Assert.That(ex.Message).Contains("not registered");
        }
    }
}
