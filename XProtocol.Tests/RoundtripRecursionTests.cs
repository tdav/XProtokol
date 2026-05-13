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
    public class RoundtripRecursionTests
    {
        [Test]
        public async Task JaggedIntArray_Roundtrips()
        {
            var dto = new JaggedIntArrayDto
            {
                Rows = new[] { new[] { 1, 2 }, new[] { 3, 4, 5 }, new int[0] }
            };
            var packet = XPacketConverter.Serialize(dto);
            var parsed = XPacket.Parse(packet.ToPacket());
            var back = XPacketConverter.Deserialize<JaggedIntArrayDto>(parsed);

            await Assert.That(back.Rows.Length).IsEqualTo(3);
            await Assert.That(back.Rows[0]).IsEquivalentTo(new[] { 1, 2 });
            await Assert.That(back.Rows[1]).IsEquivalentTo(new[] { 3, 4, 5 });
            await Assert.That(back.Rows[2].Length).IsEqualTo(0);
        }

        [Test]
        public async Task ListOfIntArray_Roundtrips()
        {
            var dto = new ListOfIntArrayDto
            {
                Buckets = new List<int[]> { new[] { 10, 20 }, new[] { 30 } }
            };
            var packet = XPacketConverter.Serialize(dto);
            var parsed = XPacket.Parse(packet.ToPacket());
            var back = XPacketConverter.Deserialize<ListOfIntArrayDto>(parsed);

            await Assert.That(back.Buckets.Count).IsEqualTo(2);
            await Assert.That(back.Buckets[0]).IsEquivalentTo(new[] { 10, 20 });
        }

        [Test]
        public async Task ListOfListOfString_Roundtrips()
        {
            var dto = new ListOfListDto
            {
                Pages = new List<List<string>>
                {
                    new List<string> { "a", "b" },
                    new List<string> { "c" }
                }
            };
            var packet = XPacketConverter.Serialize(dto);
            var parsed = XPacket.Parse(packet.ToPacket());
            var back = XPacketConverter.Deserialize<ListOfListDto>(parsed);

            await Assert.That(back.Pages.Count).IsEqualTo(2);
            await Assert.That(back.Pages[0]).IsEquivalentTo(new[] { "a", "b" });
            await Assert.That(back.Pages[1]).IsEquivalentTo(new[] { "c" });
        }

        [Test]
        public async Task DictOfList_Roundtrips()
        {
            var dto = new DictOfListDto
            {
                Groups = new Dictionary<string, List<int>>
                {
                    { "evens", new List<int> { 2, 4, 6 } },
                    { "odds",  new List<int> { 1, 3, 5 } }
                }
            };
            var packet = XPacketConverter.Serialize(dto);
            var parsed = XPacket.Parse(packet.ToPacket());
            var back = XPacketConverter.Deserialize<DictOfListDto>(parsed);

            await Assert.That(back.Groups.Count).IsEqualTo(2);
            await Assert.That(back.Groups["evens"]).IsEquivalentTo(new[] { 2, 4, 6 });
        }

        [Test]
        public async Task NestedWithCollections_NullMemberInList_NormalizesToDefault()
        {
            var dto = new NestedWithCollectionsDto
            {
                Title = "T",
                Owner = new Person { Name = "A", Age = 1, Home = new Address { Street = "S", Zip = 1 } },
                Members = new List<Person> { null, new Person { Name = "B", Age = 2, Home = null } },
                Locations = new Dictionary<int, Address>()
            };

            var packet = XPacketConverter.Serialize(dto);
            var parsed = XPacket.Parse(packet.ToPacket());
            var back = XPacketConverter.Deserialize<NestedWithCollectionsDto>(parsed);

            await Assert.That(back.Members.Count).IsEqualTo(2);
            await Assert.That(back.Members[0]).IsNotNull();
            await Assert.That(back.Members[0].Name).IsEqualTo("");
            await Assert.That(back.Members[1].Name).IsEqualTo("B");
        }

        [Test]
        public async Task NestedWithCollections_Roundtrips()
        {
            var dto = new NestedWithCollectionsDto
            {
                Title = "Project Apollo",
                Owner = new Person { Name = "Alice", Age = 40, Home = new Address { Street = "S1", Zip = 100 } },
                Members = new List<Person>
                {
                    new Person { Name = "Bob", Age = 30, Home = new Address { Street = "S2", Zip = 200 } },
                    new Person { Name = "Carol", Age = 35, Home = null }
                },
                Locations = new Dictionary<int, Address>
                {
                    { 1, new Address { Street = "HQ", Zip = 1000 } },
                    { 2, new Address { Street = "Branch", Zip = 2000 } }
                }
            };

            var packet = XPacketConverter.Serialize(dto);
            var parsed = XPacket.Parse(packet.ToPacket());
            var back = XPacketConverter.Deserialize<NestedWithCollectionsDto>(parsed);

            await Assert.That(back.Title).IsEqualTo("Project Apollo");
            await Assert.That(back.Owner.Name).IsEqualTo("Alice");
            await Assert.That(back.Members.Count).IsEqualTo(2);
            await Assert.That(back.Members[1].Name).IsEqualTo("Carol");
            await Assert.That(back.Members[1].Home.Street).IsEqualTo("");
            await Assert.That(back.Locations[2].Street).IsEqualTo("Branch");
        }
    }
}
