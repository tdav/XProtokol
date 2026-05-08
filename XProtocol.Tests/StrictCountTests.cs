using System;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    public class StrictCountTests
    {
        [Test]
        public async Task Deserialize_FieldCountMismatch_Throws()
        {
            var dto = new SimpleDto { A = 1, B = 2.0, C = true };
            var packet = XPacketConverter.Serialize(dto);

            packet.Fields.RemoveAt(packet.Fields.Count - 1);

            var ex = await Assert.That(() => XPacketConverter.Deserialize<SimpleDto>(packet))
                .ThrowsExactly<InvalidOperationException>();

            await Assert.That(ex.Message).Contains("Field count mismatch");
            await Assert.That(ex.Message).Contains("expected 3");
            await Assert.That(ex.Message).Contains("got 2");
        }
    }
}
