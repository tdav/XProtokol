using System;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    public class ChunkReaderTests
    {
        private static XPacket Pack(params byte[][] chunks)
        {
            var p = XPacket.Create(0, 0);
            foreach (var c in chunks)
            {
                p.Fields.Add(new XPacketField
                {
                    FieldSize = (byte)c.Length,
                    Contents = c
                });
            }
            return p;
        }

        [Test]
        public async Task ReadByte_FromSingleChunk_AdvancesOffset()
        {
            var p = Pack(new byte[] { 0x01, 0x02, 0x03 });
            var r = new ChunkReader(p, 0);

            await Assert.That(r.ReadByte()).IsEqualTo((byte)0x01);
            await Assert.That(r.ReadByte()).IsEqualTo((byte)0x02);
            await Assert.That(r.ReadByte()).IsEqualTo((byte)0x03);
        }

        [Test]
        public async Task ReadByte_CrossesChunkBoundary()
        {
            var p = Pack(new byte[] { 0x01, 0x02 }, new byte[] { 0x03, 0x04 });
            var r = new ChunkReader(p, 0);

            await Assert.That(r.ReadByte()).IsEqualTo((byte)0x01);
            await Assert.That(r.ReadByte()).IsEqualTo((byte)0x02);
            await Assert.That(r.WireIdx).IsEqualTo(1);
            await Assert.That(r.ReadByte()).IsEqualTo((byte)0x03);
            await Assert.That(r.ReadByte()).IsEqualTo((byte)0x04);
        }

        [Test]
        public async Task ReadUInt16LE_AcrossChunkBoundary()
        {
            var p = Pack(new byte[] { 0x01 }, new byte[] { 0x02 });
            var r = new ChunkReader(p, 0);

            await Assert.That(r.ReadUInt16LE()).IsEqualTo((ushort)0x0201);
        }

        [Test]
        public async Task ReadBytes_LargerThanChunk()
        {
            var p = Pack(
                new byte[] { 0x01, 0x02 },
                new byte[] { 0x03, 0x04, 0x05 });
            var r = new ChunkReader(p, 0);
            var buf = new byte[5];
            r.ReadBytes(buf, 0, 5);

            await Assert.That(buf[0]).IsEqualTo((byte)0x01);
            await Assert.That(buf[4]).IsEqualTo((byte)0x05);
            await Assert.That(r.Available).IsEqualTo(0);
        }

        [Test]
        public async Task ReadByte_BeyondEnd_Throws()
        {
            var p = Pack(new byte[] { 0x01 });
            var r = new ChunkReader(p, 0);
            r.ReadByte();

            var ex = await Assert.That(() => r.ReadByte())
                .ThrowsExactly<InvalidOperationException>();
            await Assert.That(ex.Message).Contains("payload truncated");
        }

        [Test]
        public async Task Available_ReflectsRemainingBytes()
        {
            var p = Pack(new byte[] { 1, 2, 3 }, new byte[] { 4, 5 });
            var r = new ChunkReader(p, 0);

            await Assert.That(r.Available).IsEqualTo(5);
            r.ReadByte();
            await Assert.That(r.Available).IsEqualTo(4);
        }

        [Test]
        public async Task StartWireIdx_OffsetsInitialPosition()
        {
            var p = Pack(new byte[] { 1, 2 }, new byte[] { 3, 4 });
            var r = new ChunkReader(p, 1);

            await Assert.That(r.Available).IsEqualTo(2);
            await Assert.That(r.ReadByte()).IsEqualTo((byte)3);
        }
    }
}
