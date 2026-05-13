using System;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    public class MarshalHelpersTests
    {
        [Test]
        public async Task ToBytes_Int32_ProducesFourBytesLE()
        {
            var bytes = MarshalHelpers.ToBytes(0x01020304, typeof(int));

            await Assert.That(bytes.Length).IsEqualTo(4);
            await Assert.That(bytes[0]).IsEqualTo((byte)0x04);
            await Assert.That(bytes[1]).IsEqualTo((byte)0x03);
            await Assert.That(bytes[2]).IsEqualTo((byte)0x02);
            await Assert.That(bytes[3]).IsEqualTo((byte)0x01);
        }

        [Test]
        public async Task FromBytes_Int32_Roundtrips()
        {
            var bytes = MarshalHelpers.ToBytes(42, typeof(int));
            var back = (int)MarshalHelpers.FromBytes(bytes, typeof(int));

            await Assert.That(back).IsEqualTo(42);
        }

        [Test]
        public async Task ToBytes_Guid_Roundtrips()
        {
            var g = Guid.NewGuid();
            var bytes = MarshalHelpers.ToBytes(g, typeof(Guid));
            var back = (Guid)MarshalHelpers.FromBytes(bytes, typeof(Guid));

            await Assert.That(back).IsEqualTo(g);
        }
    }
}
