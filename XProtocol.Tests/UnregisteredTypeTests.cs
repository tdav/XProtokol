using System;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    public class UnregisteredTypeTests
    {
        [Test]
        public async Task Serialize_UnregisteredType_Throws()
        {
            var dto = new UnregisteredDto { X = 7 };

            var ex = await Assert.That(() => XPacketConverter.Serialize(dto))
                .ThrowsExactly<InvalidOperationException>();

            await Assert.That(ex.Message).Contains(nameof(UnregisteredDto));
            await Assert.That(ex.Message).Contains("not registered");
        }

        [Test]
        public async Task Deserialize_UnregisteredType_Throws()
        {
            var pkt = XPacket.Create(0, 0);

            var ex = await Assert.That(() => XPacketConverter.Deserialize<UnregisteredDto>(pkt))
                .ThrowsExactly<InvalidOperationException>();

            await Assert.That(ex.Message).Contains(nameof(UnregisteredDto));
            await Assert.That(ex.Message).Contains("not registered");
        }
    }
}
