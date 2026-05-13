using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    public class ShapeCodecTests
    {
        private static XPacket WrapAsPacket(byte[] payload)
        {
            var p = XPacket.Create(0, 0);
            p.AppendChunks(payload);
            return p;
        }

        [Test]
        public async Task WriteArray_Int3Elements_ProducesExpectedBytes()
        {
            var shape = new ArrayShape(typeof(int), new ValueShape(typeof(int)));
            var bytes = ShapeCodec.WriteField(shape, new[] { 1, 2, 3 });

            // [count=3 LE]   [int 1] [int 2] [int 3]
            // [03 00]        [01 00 00 00] [02 00 00 00] [03 00 00 00]
            await Assert.That(bytes.Length).IsEqualTo(2 + 3 * 4);
            await Assert.That(bytes[0]).IsEqualTo((byte)0x03);
            await Assert.That(bytes[1]).IsEqualTo((byte)0x00);
            await Assert.That(bytes[2]).IsEqualTo((byte)0x01);
        }

        [Test]
        public async Task ReadArray_RoundtripsInt()
        {
            var shape = new ArrayShape(typeof(int), new ValueShape(typeof(int)));
            var bytes = ShapeCodec.WriteField(shape, new[] { 10, 20, 30 });
            var reader = new ChunkReader(WrapAsPacket(bytes), 0);

            var back = (int[])ShapeCodec.ReadField(shape, reader);

            await Assert.That(back.Length).IsEqualTo(3);
            await Assert.That(back[0]).IsEqualTo(10);
            await Assert.That(back[2]).IsEqualTo(30);
        }

        [Test]
        public async Task WriteArray_NullValue_TreatedAsEmpty()
        {
            var shape = new ArrayShape(typeof(int), new ValueShape(typeof(int)));
            var bytes = ShapeCodec.WriteField(shape, null);

            await Assert.That(bytes.Length).IsEqualTo(2);
            await Assert.That(bytes[0]).IsEqualTo((byte)0);
            await Assert.That(bytes[1]).IsEqualTo((byte)0);
        }

        [Test]
        public async Task WriteArray_ByteFastPath_ProducesContiguousBytes()
        {
            var shape = new ArrayShape(typeof(byte), new ValueShape(typeof(byte)));
            var bytes = ShapeCodec.WriteField(shape, new byte[] { 0xAA, 0xBB, 0xCC });

            // [03 00] [AA BB CC]
            await Assert.That(bytes.Length).IsEqualTo(2 + 3);
            await Assert.That(bytes[2]).IsEqualTo((byte)0xAA);
        }

        [Test]
        public async Task WriteArray_TooLarge_Throws()
        {
            var shape = new ArrayShape(typeof(byte), new ValueShape(typeof(byte)));
            var big = new byte[ushort.MaxValue + 1];

            var ex = await Assert.That(() => ShapeCodec.WriteField(shape, big))
                .ThrowsExactly<InvalidOperationException>();
            await Assert.That(ex.Message).Contains("exceeds 65535 elements");
        }

        [Test]
        public async Task WriteArray_StringElements_Roundtrips()
        {
            var shape = new ArrayShape(typeof(string), StringShape.Instance);
            var bytes = ShapeCodec.WriteField(shape, new[] { "a", "bb" });
            var reader = new ChunkReader(WrapAsPacket(bytes), 0);

            var back = (string[])ShapeCodec.ReadField(shape, reader);

            await Assert.That(back.Length).IsEqualTo(2);
            await Assert.That(back[0]).IsEqualTo("a");
            await Assert.That(back[1]).IsEqualTo("bb");
        }

        [Test]
        public async Task WriteList_IntElements_RoundtripsViaReader()
        {
            var shape = new ListShape(typeof(int), new ValueShape(typeof(int)));
            var bytes = ShapeCodec.WriteField(shape, new System.Collections.Generic.List<int> { 100, 200, 300 });
            var reader = new ChunkReader(WrapAsPacket(bytes), 0);

            var back = (System.Collections.Generic.List<int>)ShapeCodec.ReadField(shape, reader);

            await Assert.That(back.Count).IsEqualTo(3);
            await Assert.That(back[0]).IsEqualTo(100);
            await Assert.That(back[2]).IsEqualTo(300);
        }

        [Test]
        public async Task WriteList_NullValue_TreatedAsEmpty()
        {
            var shape = new ListShape(typeof(int), new ValueShape(typeof(int)));
            var bytes = ShapeCodec.WriteField(shape, null);

            await Assert.That(bytes.Length).IsEqualTo(2);
            await Assert.That(bytes[0]).IsEqualTo((byte)0);
            await Assert.That(bytes[1]).IsEqualTo((byte)0);
        }

        [Test]
        public async Task WriteList_StringElements_Roundtrips()
        {
            var shape = new ListShape(typeof(string), StringShape.Instance);
            var bytes = ShapeCodec.WriteField(shape, new System.Collections.Generic.List<string> { "x", "yy" });
            var reader = new ChunkReader(WrapAsPacket(bytes), 0);

            var back = (System.Collections.Generic.List<string>)ShapeCodec.ReadField(shape, reader);

            await Assert.That(back).IsEquivalentTo(new[] { "x", "yy" });
        }

        [Test]
        public async Task WriteDict_IntToString_Roundtrips()
        {
            var shape = new DictShape(typeof(int), typeof(string),
                new ValueShape(typeof(int)), StringShape.Instance);

            var dict = new System.Collections.Generic.Dictionary<int, string> { { 1, "one" }, { 2, "two" } };
            var bytes = ShapeCodec.WriteField(shape, dict);
            var reader = new ChunkReader(WrapAsPacket(bytes), 0);

            var back = (System.Collections.Generic.Dictionary<int, string>)ShapeCodec.ReadField(shape, reader);

            await Assert.That(back.Count).IsEqualTo(2);
            await Assert.That(back[1]).IsEqualTo("one");
            await Assert.That(back[2]).IsEqualTo("two");
        }

        [Test]
        public async Task WriteDict_NullValue_TreatedAsEmpty()
        {
            var shape = new DictShape(typeof(int), typeof(int),
                new ValueShape(typeof(int)), new ValueShape(typeof(int)));
            var bytes = ShapeCodec.WriteField(shape, null);

            await Assert.That(bytes.Length).IsEqualTo(2);
            await Assert.That(bytes[0]).IsEqualTo((byte)0);
        }

        [Test]
        public async Task WriteNested_Roundtrips()
        {
            var shape = ShapeResolver.Resolve(typeof(NestedDtoNeedsFields), new HashSet<System.Type>());
            var dto = new NestedDtoNeedsFields { X = 999 };
            var bytes = ShapeCodec.WriteField(shape, dto);
            var reader = new ChunkReader(WrapAsPacket(bytes), 0);

            var back = (NestedDtoNeedsFields)ShapeCodec.ReadField(shape, reader);

            await Assert.That(back.X).IsEqualTo(999);
        }

        [Test]
        public async Task WriteNested_NullInstance_BecomesDefault()
        {
            var shape = ShapeResolver.Resolve(typeof(NestedDtoNeedsFields), new HashSet<System.Type>());
            var bytes = ShapeCodec.WriteField(shape, null);
            var reader = new ChunkReader(WrapAsPacket(bytes), 0);

            var back = (NestedDtoNeedsFields)ShapeCodec.ReadField(shape, reader);

            await Assert.That(back).IsNotNull();
            await Assert.That(back.X).IsEqualTo(0);
        }

        [Test]
        public async Task WriteList_TooManyElements_Throws()
        {
            var shape = new ListShape(typeof(int), new ValueShape(typeof(int)));
            var list = new System.Collections.Generic.List<int>();
            for (int i = 0; i < ushort.MaxValue + 1; i++) list.Add(i);

            var ex = await Assert.That(() => ShapeCodec.WriteField(shape, list))
                .ThrowsExactly<System.InvalidOperationException>();
            await Assert.That(ex.Message).Contains("exceeds 65535 elements");
        }

        [Test]
        public async Task WriteDict_TooManyEntries_Throws()
        {
            var shape = new DictShape(typeof(int), typeof(int),
                new ValueShape(typeof(int)), new ValueShape(typeof(int)));
            var dict = new System.Collections.Generic.Dictionary<int, int>();
            for (int i = 0; i < ushort.MaxValue + 1; i++) dict[i] = i;

            var ex = await Assert.That(() => ShapeCodec.WriteField(shape, dict))
                .ThrowsExactly<System.InvalidOperationException>();
            await Assert.That(ex.Message).Contains("exceeds 65535 elements");
        }

        [Test]
        public async Task ReadArray_PayloadTruncated_Throws()
        {
            var shape = new ArrayShape(typeof(int), new ValueShape(typeof(int)));
            // Forge a payload that claims count=2 but supplies only 4 bytes (one int).
            var payload = new byte[] { 0x02, 0x00, 0x01, 0x00, 0x00, 0x00 };
            var p = XPacket.Create(0, 0);
            p.AppendChunks(payload);
            var reader = new ChunkReader(p, 0);

            var ex = await Assert.That(() => ShapeCodec.ReadField(shape, reader))
                .ThrowsExactly<System.InvalidOperationException>();
            await Assert.That(ex.Message).Contains("payload truncated");
        }
    }
}
