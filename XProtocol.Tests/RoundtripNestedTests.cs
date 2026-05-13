using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using XProtocol;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    public class RoundtripNestedTests
    {
        [Test]
        public async Task Person_WithAddress_Roundtrips()
        {
            var dto = new Person
            {
                Name = "Alice",
                Age = 30,
                Home = new Address { Street = "Main St", Zip = 12345 }
            };
            var packet = XPacketConverter.Serialize(dto);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var back = XPacketConverter.Deserialize<Person>(parsed);

            await Assert.That(back.Name).IsEqualTo("Alice");
            await Assert.That(back.Age).IsEqualTo(30);
            await Assert.That(back.Home).IsNotNull();
            await Assert.That(back.Home.Street).IsEqualTo("Main St");
            await Assert.That(back.Home.Zip).IsEqualTo(12345);
        }

        [Test]
        public async Task Person_NullAddress_RoundtripsToDefault()
        {
            var dto = new Person { Name = "Bob", Age = 25, Home = null };
            var packet = XPacketConverter.Serialize(dto);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var back = XPacketConverter.Deserialize<Person>(parsed);

            await Assert.That(back.Home).IsNotNull();
            await Assert.That(back.Home.Street).IsEqualTo("");
            await Assert.That(back.Home.Zip).IsEqualTo(0);
        }
    }
}
