using System;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using XProtocol;

namespace XProtocol.Tests
{
    public class XPacketChunkTests
    {
        [Test]
        public async Task AppendChunks_PayloadShorterThan255_ProducesOneField()
        {
            var p = XPacket.Create(1, 1);
            var payload = new byte[10];
            for (int i = 0; i < payload.Length; i++) payload[i] = (byte)i;

            p.AppendChunks(payload);

            await Assert.That(p.Fields.Count).IsEqualTo(1);
            await Assert.That(p.Fields[0].FieldSize).IsEqualTo((byte)10);
            await Assert.That(p.Fields[0].Contents).IsEquivalentTo(payload);
        }

        [Test]
        public async Task AppendChunks_PayloadExactly255_ProducesOneField()
        {
            var p = XPacket.Create(1, 1);
            var payload = new byte[255];

            p.AppendChunks(payload);

            await Assert.That(p.Fields.Count).IsEqualTo(1);
            await Assert.That(p.Fields[0].FieldSize).IsEqualTo((byte)255);
        }

        [Test]
        public async Task AppendChunks_PayloadAcross255_ProducesTwoFields()
        {
            var p = XPacket.Create(1, 1);
            var payload = new byte[256];

            p.AppendChunks(payload);

            await Assert.That(p.Fields.Count).IsEqualTo(2);
            await Assert.That(p.Fields[0].FieldSize).IsEqualTo((byte)255);
            await Assert.That(p.Fields[1].FieldSize).IsEqualTo((byte)1);
        }

        [Test]
        public async Task AppendChunks_Payload902Bytes_ProducesFourFields()
        {
            var p = XPacket.Create(1, 1);
            var payload = new byte[902]; // 3 * 255 + 137

            p.AppendChunks(payload);

            await Assert.That(p.Fields.Count).IsEqualTo(4);
            await Assert.That(p.Fields[0].FieldSize).IsEqualTo((byte)255);
            await Assert.That(p.Fields[1].FieldSize).IsEqualTo((byte)255);
            await Assert.That(p.Fields[2].FieldSize).IsEqualTo((byte)255);
            await Assert.That(p.Fields[3].FieldSize).IsEqualTo((byte)137);
        }

        [Test]
        public async Task GetRawAt_ReturnsContents()
        {
            var p = XPacket.Create(1, 1);
            var payload = new byte[] { 9, 8, 7 };
            p.AppendChunks(payload);

            var raw = p.GetRawAt(0);

            await Assert.That(raw).IsEquivalentTo(payload);
        }

        [Test]
        public async Task GetRawAt_OutOfRange_Throws()
        {
            var p = XPacket.Create(1, 1);

            await Assert.That(() => p.GetRawAt(0))
                .ThrowsExactly<ArgumentOutOfRangeException>();
        }

        [Test]
        public async Task AppendChunks_EmptyPayload_Throws()
        {
            var p = XPacket.Create(1, 1);

            await Assert.That(() => p.AppendChunks(Array.Empty<byte>()))
                .ThrowsExactly<ArgumentException>();
        }
    }
}
