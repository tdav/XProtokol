using System;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    public class XPacketConverterTests
    {
        [Test]
        public async Task Serialize_NullObj_ThrowsArgumentNullException()
        {
            await Assert.That(() => XPacketConverter.Serialize<SimpleDto>(null))
                .ThrowsExactly<ArgumentNullException>();
        }

        [Test]
        public async Task Deserialize_NullPacket_ThrowsArgumentNullException()
        {
            await Assert.That(() => XPacketConverter.Deserialize<SimpleDto>(null))
                .ThrowsExactly<ArgumentNullException>();
        }
    }
}
