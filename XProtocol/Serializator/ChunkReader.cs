using System;

namespace XProtocol.Serializator
{
    internal sealed class ChunkReader
    {
        private readonly XPacket packet;
        private int wireIdx;
        private int offsetInChunk;

        public ChunkReader(XPacket packet, int startWireIdx)
        {
            this.packet = packet ?? throw new ArgumentNullException(nameof(packet));
            if (startWireIdx < 0 || startWireIdx > packet.Fields.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(startWireIdx));
            }
            this.wireIdx = startWireIdx;
            this.offsetInChunk = 0;
        }

        public int WireIdx => this.wireIdx;

        public int Available
        {
            get
            {
                int total = 0;
                if (this.wireIdx < this.packet.Fields.Count)
                {
                    total += this.packet.Fields[this.wireIdx].FieldSize - this.offsetInChunk;
                    for (int i = this.wireIdx + 1; i < this.packet.Fields.Count; i++)
                    {
                        total += this.packet.Fields[i].FieldSize;
                    }
                }
                return total;
            }
        }

        public byte ReadByte()
        {
            AdvanceIfChunkExhausted();
            EnsureCanRead(1);
            var b = this.packet.Fields[this.wireIdx].Contents[this.offsetInChunk++];
            AdvanceIfChunkExhausted();
            return b;
        }

        public ushort ReadUInt16LE()
        {
            var lo = ReadByte();
            var hi = ReadByte();
            return (ushort)(lo | (hi << 8));
        }

        public void ReadBytes(byte[] dst, int offset, int count)
        {
            if (dst == null)
            {
                throw new ArgumentNullException(nameof(dst));
            }
            AdvanceIfChunkExhausted();
            EnsureCanRead(count);

            int remaining = count;
            int dstOffset = offset;
            while (remaining > 0)
            {
                AdvanceIfChunkExhausted();
                var field = this.packet.Fields[this.wireIdx];
                int take = Math.Min(remaining, field.FieldSize - this.offsetInChunk);
                Buffer.BlockCopy(field.Contents, this.offsetInChunk, dst, dstOffset, take);
                this.offsetInChunk += take;
                dstOffset += take;
                remaining -= take;
                AdvanceIfChunkExhausted();
            }
        }

        private void EnsureCanRead(int count)
        {
            var avail = Available;
            if (count > avail)
            {
                throw new InvalidOperationException(
                    $"payload truncated: requested {count} bytes, only {avail} remaining (wireIdx={this.wireIdx}, fields={this.packet.Fields.Count}).");
            }
        }

        private void AdvanceIfChunkExhausted()
        {
            while (this.wireIdx < this.packet.Fields.Count
                && this.offsetInChunk >= this.packet.Fields[this.wireIdx].FieldSize)
            {
                this.wireIdx++;
                this.offsetInChunk = 0;
            }
        }
    }
}
