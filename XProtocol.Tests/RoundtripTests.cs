using System;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    public class RoundtripTests
    {
        [Test]
        public async Task SimpleDto_RoundtripPreservesValues()
        {
            var original = new SimpleDto { A = 42, B = 3.1415, C = true };

            var packet = XPacketConverter.Serialize(original);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            await Assert.That(parsed).IsNotNull();

            var restored = XPacketConverter.Deserialize<SimpleDto>(parsed);

            await Assert.That(restored.A).IsEqualTo(original.A);
            await Assert.That(restored.B).IsEqualTo(original.B);
            await Assert.That(restored.C).IsEqualTo(original.C);
        }

        [Test]
        public async Task SimpleDto_FieldOrderMatchesDeclarationOrder()
        {
            var dto = new SimpleDto { A = 1, B = 2.0, C = false };
            var packet = XPacketConverter.Serialize(dto);

            await Assert.That(packet.Fields.Count).IsEqualTo(3);
            await Assert.That(packet.Fields[0].FieldSize).IsEqualTo((byte)4);
            await Assert.That(packet.Fields[1].FieldSize).IsEqualTo((byte)8);
            await Assert.That((int)packet.Fields[2].FieldSize).IsGreaterThanOrEqualTo(1);
        }

        [Test]
        public async Task EmptyDto_RoundtripProducesZeroFields()
        {
            var original = new EmptyDto();

            var packet = XPacketConverter.Serialize(original);
            await Assert.That(packet.Fields.Count).IsEqualTo(0);

            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            await Assert.That(parsed).IsNotNull();
            await Assert.That(parsed.Fields.Count).IsEqualTo(0);

            var restored = XPacketConverter.Deserialize<EmptyDto>(parsed);
            await Assert.That(restored).IsNotNull();
        }

        [Test]
        public async Task XPacketHandshake_RoundtripPreservesValue()
        {
            var original = new XPacketHandshake { MagicHandshakeNumber = 12345 };

            var packet = XPacketConverter.Serialize(original);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            await Assert.That(parsed).IsNotNull();

            var restored = XPacketConverter.Deserialize<XPacketHandshake>(parsed);
            await Assert.That(restored.MagicHandshakeNumber).IsEqualTo(original.MagicHandshakeNumber);
        }

        [Test]
        public async Task StringDto_RoundtripShortAscii()
        {
            var original = new StringDto { A = 7, S = "hello", B = true };

            var packet = XPacketConverter.Serialize(original);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            await Assert.That(parsed).IsNotNull();

            var restored = XPacketConverter.Deserialize<StringDto>(parsed);

            await Assert.That(restored.A).IsEqualTo(original.A);
            await Assert.That(restored.S).IsEqualTo(original.S);
            await Assert.That(restored.B).IsEqualTo(original.B);
        }

        [Test]
        public async Task StringDto_Roundtrip_EmptyString()
        {
            var original = new StringDto { A = 1, S = "", B = false };
            var packet = XPacketConverter.Serialize(original);

            await Assert.That(packet.Fields.Count).IsEqualTo(3);
            await Assert.That(packet.Fields[1].FieldSize).IsEqualTo((byte)2);

            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var restored = XPacketConverter.Deserialize<StringDto>(parsed);

            await Assert.That(restored.S).IsEqualTo("");
        }

        [Test]
        public async Task StringDto_Roundtrip_NullString_NormalizesToEmpty()
        {
            var original = new StringDto { A = 1, S = null, B = true };
            var packet = XPacketConverter.Serialize(original);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);

            var restored = XPacketConverter.Deserialize<StringDto>(parsed);

            await Assert.That(restored.S).IsEqualTo("");
        }

        [Test]
        public async Task StringDto_Roundtrip_253ByteAscii_SingleChunk()
        {
            var s = new string('a', 253);
            var original = new StringDto { A = 2, S = s, B = false };
            var packet = XPacketConverter.Serialize(original);

            await Assert.That(packet.Fields.Count).IsEqualTo(3);
            await Assert.That(packet.Fields[1].FieldSize).IsEqualTo((byte)255);

            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var restored = XPacketConverter.Deserialize<StringDto>(parsed);

            await Assert.That(restored.S).IsEqualTo(s);
        }

        [Test]
        public async Task StringDto_Roundtrip_254ByteAscii_TwoChunks()
        {
            var s = new string('a', 254);
            var original = new StringDto { A = 3, S = s, B = false };
            var packet = XPacketConverter.Serialize(original);

            await Assert.That(packet.Fields.Count).IsEqualTo(4);
            await Assert.That(packet.Fields[1].FieldSize).IsEqualTo((byte)255);
            await Assert.That(packet.Fields[2].FieldSize).IsEqualTo((byte)1);

            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var restored = XPacketConverter.Deserialize<StringDto>(parsed);

            await Assert.That(restored.S).IsEqualTo(s);
        }

        [Test]
        public async Task StringDto_Roundtrip_510ByteAscii_ThreeChunks()
        {
            var s = new string('a', 510);
            var original = new StringDto { A = 4, S = s, B = false };
            var packet = XPacketConverter.Serialize(original);

            await Assert.That(packet.Fields.Count).IsEqualTo(5);

            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var restored = XPacketConverter.Deserialize<StringDto>(parsed);

            await Assert.That(restored.S).IsEqualTo(s);
        }

        [Test]
        public async Task StringDto_Roundtrip_900ByteAscii_FourStringChunks()
        {
            var s = new string('x', 900);
            var original = new StringDto { A = 5, S = s, B = true };
            var packet = XPacketConverter.Serialize(original);

            // 1 (int) + ceil((900+2)/255) = 1 + 4 = 5 string-related fields; plus bool = 6 total
            await Assert.That(packet.Fields.Count).IsEqualTo(6);

            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var restored = XPacketConverter.Deserialize<StringDto>(parsed);

            await Assert.That(restored.S).IsEqualTo(s);
            await Assert.That(restored.A).IsEqualTo(5);
            await Assert.That(restored.B).IsEqualTo(true);
        }

        [Test]
        public async Task StringDto_Roundtrip_CyrillicMultiByteUtf8()
        {
            var s = "привет мир";
            var original = new StringDto { A = 6, S = s, B = false };
            var packet = XPacketConverter.Serialize(original);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);

            var restored = XPacketConverter.Deserialize<StringDto>(parsed);

            await Assert.That(restored.S).IsEqualTo(s);
        }

        [Test]
        public async Task StringDto_Roundtrip_EmojiFourByteUtf8()
        {
            var s = "abc " + char.ConvertFromUtf32(0x1F680) + " xyz";
            var original = new StringDto { A = 7, S = s, B = false };
            var packet = XPacketConverter.Serialize(original);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);

            var restored = XPacketConverter.Deserialize<StringDto>(parsed);

            await Assert.That(restored.S).IsEqualTo(s);
        }

        [Test]
        public async Task StringDto_Roundtrip_16000ByteAscii_FitsInWireCap()
        {
            var s = new string('x', 16000);
            var original = new StringDto { A = 8, S = s, B = false };
            var packet = XPacketConverter.Serialize(original);

            // 16002 / 255 = 62.75 → 63 string chunks; plus int + bool = 65 total wire fields (< 255)
            await Assert.That(packet.Fields.Count).IsLessThanOrEqualTo((int)byte.MaxValue);

            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);
            var restored = XPacketConverter.Deserialize<StringDto>(parsed);

            await Assert.That(restored.S).IsEqualTo(s);
        }

        [Test]
        public async Task MultiStringDto_Roundtrip_PreservesBothStrings()
        {
            var original = new MultiStringDto
            {
                First = "alpha",
                Middle = 42,
                Last = new string('y', 300)  // multi-chunk
            };

            var packet = XPacketConverter.Serialize(original);
            var bytes = packet.ToPacket();
            var parsed = XPacket.Parse(bytes);

            var restored = XPacketConverter.Deserialize<MultiStringDto>(parsed);

            await Assert.That(restored.First).IsEqualTo(original.First);
            await Assert.That(restored.Middle).IsEqualTo(original.Middle);
            await Assert.That(restored.Last).IsEqualTo(original.Last);
        }

        [Test]
        public async Task StringDto_Roundtrip_EncryptedPath_900Bytes()
        {
            var s = new string('z', 900);
            var original = new StringDto { A = 9, S = s, B = true };

            var packet = XPacketConverter.Serialize(original);
            var encryptedBytes = packet.Encrypt().ToPacket();
            var parsed = XPacket.Parse(encryptedBytes);
            await Assert.That(parsed).IsNotNull();

            var restored = XPacketConverter.Deserialize<StringDto>(parsed);

            await Assert.That(restored.A).IsEqualTo(original.A);
            await Assert.That(restored.S).IsEqualTo(original.S);
            await Assert.That(restored.B).IsEqualTo(original.B);
        }

        [Test]
        public async Task Serialize_StringOverflow_Throws()
        {
            var s = new string('x', ushort.MaxValue + 1);  // 65536
            var dto = new StringDto { A = 10, S = s, B = false };

            var ex = await Assert.That(() => XPacketConverter.Serialize(dto))
                .ThrowsExactly<InvalidOperationException>();

            await Assert.That(ex.Message).Contains("exceeds 65535");
            await Assert.That(ex.Message).Contains("StringDto");
        }

        [Test]
        public async Task Serialize_TotalWireOverflow_Throws()
        {
            // 65535 bytes → 257 string chunks; + int + bool = 259 wire fields > 255 cap.
            var s = new string('x', ushort.MaxValue);
            var dto = new StringDto { A = 11, S = s, B = false };

            var ex = await Assert.That(() => XPacketConverter.Serialize(dto))
                .ThrowsExactly<InvalidOperationException>();

            await Assert.That(ex.Message).Contains("exceeds 255 wire fields");
            await Assert.That(ex.Message).Contains("StringDto");
        }

        [Test]
        public async Task Deserialize_StringTruncated_Throws()
        {
            var original = new StringDto { A = 12, S = new string('q', 600), B = true };
            var packet = XPacketConverter.Serialize(original);

            // Layout: [int][string_chunk_0][string_chunk_1][string_chunk_2][bool]
            // Remove second-to-last to truncate string mid-payload.
            packet.Fields.RemoveAt(packet.Fields.Count - 2);

            var ex = await Assert.That(() => XPacketConverter.Deserialize<StringDto>(packet))
                .ThrowsExactly<InvalidOperationException>();

            var msg = ex.Message;
            var matched = msg.Contains("string truncated") || msg.Contains("Field count mismatch");
            await Assert.That(matched).IsTrue();
        }

        [Test]
        public async Task Deserialize_StringHeaderTruncated_Throws()
        {
            var original = new StringDto { A = 13, S = "hi", B = false };
            var packet = XPacketConverter.Serialize(original);

            // Replace the string descriptor's first chunk (index 1) with a 1-byte field.
            packet.Fields[1] = new XPacketField
            {
                FieldSize = 1,
                Contents = new byte[] { 0 }
            };

            var ex = await Assert.That(() => XPacketConverter.Deserialize<StringDto>(packet))
                .ThrowsExactly<InvalidOperationException>();

            await Assert.That(ex.Message).Contains("string header truncated");
        }
    }
}
