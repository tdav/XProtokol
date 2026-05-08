using System;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace XProtocol.Tests
{
    public class XPacketTests
    {
        // -------- Parse: malformed inputs --------

        [Test]
        public async Task Parse_NullInput_ReturnsNull()
        {
            await Assert.That(XPacket.Parse(null)).IsNull();
        }

        [Test]
        public async Task Parse_TooShort_ReturnsNull()
        {
            var bytes = new byte[7];
            await Assert.That(XPacket.Parse(bytes)).IsNull();
        }

        [Test]
        public async Task Parse_WrongHeader_ReturnsNull()
        {
            var bytes = new byte[] { 0xDE, 0xAD, 0xBE, 0x00, 0x00, 0x00, 0xFF, 0x00 };
            await Assert.That(XPacket.Parse(bytes)).IsNull();
        }

        [Test]
        public async Task Parse_PlainHeaderEmptyFields_Succeeds()
        {
            var bytes = new byte[] { 0xAF, 0xAA, 0xAF, 0x00, 0x00, 0x00, 0xFF, 0x00 };
            var p = XPacket.Parse(bytes);
            await Assert.That(p).IsNotNull();
            await Assert.That(p.Fields.Count).IsEqualTo(0);
        }

        [Test]
        public async Task Parse_TruncatedField_ReturnsNull()
        {
            var bytes = new byte[] {
                0xAF, 0xAA, 0xAF, 0x00, 0x00,
                0x01,
                0x0A, 0x01, 0x02, 0x03, 0x04, 0x05,
                0xFF, 0x00
            };
            await Assert.That(XPacket.Parse(bytes)).IsNull();
        }

        [Test]
        public async Task Parse_MissingFooter_ReturnsNull()
        {
            var bytes = new byte[] { 0xAF, 0xAA, 0xAF, 0x00, 0x00, 0x00, 0xAB, 0xCD };
            await Assert.That(XPacket.Parse(bytes)).IsNull();
        }

        [Test]
        public async Task Parse_OneByteField_RoundtripBytes()
        {
            var bytes = new byte[] {
                0xAF, 0xAA, 0xAF, 0x05, 0x06,
                0x01,
                0x02, 0x10, 0x20,
                0xFF, 0x00
            };
            var p = XPacket.Parse(bytes);
            await Assert.That(p).IsNotNull();
            await Assert.That(p.Fields.Count).IsEqualTo(1);
            await Assert.That(p.Fields[0].FieldSize).IsEqualTo((byte)2);
            await Assert.That(p.Fields[0].Contents[0]).IsEqualTo((byte)0x10);
            await Assert.That(p.Fields[0].Contents[1]).IsEqualTo((byte)0x20);
        }

        // -------- AppendValue: edge cases --------

        [Test]
        public async Task AppendValue_Null_ThrowsArgumentNullException()
        {
            var p = XPacket.Create(0, 0);
            await Assert.That(() => p.AppendValue(null))
                .ThrowsExactly<ArgumentNullException>();
        }

        [Test]
        public async Task AppendValue_ReferenceType_ThrowsArgumentException()
        {
            var p = XPacket.Create(0, 0);
            var ex = await Assert.That(() => p.AppendValue("string is not a value type"))
                .ThrowsExactly<ArgumentException>();
            await Assert.That(ex.Message).Contains("Only value types");
        }

        // -------- GetValueAt: bounds --------

        [Test]
        public async Task GetValueAt_NegativeIndex_Throws()
        {
            var p = XPacket.Create(0, 0);
            p.AppendValue(123);

            await Assert.That(() => p.GetValueAt<int>(-1))
                .ThrowsExactly<ArgumentOutOfRangeException>();
        }

        [Test]
        public async Task GetValueAt_OutOfRangeIndex_Throws()
        {
            var p = XPacket.Create(0, 0);
            p.AppendValue(123);

            await Assert.That(() => p.GetValueAt<int>(1))
                .ThrowsExactly<ArgumentOutOfRangeException>();
        }

        [Test]
        public async Task GetValueAtTyped_NegativeIndex_Throws()
        {
            var p = XPacket.Create(0, 0);
            p.AppendValue(123);

            await Assert.That(() => p.GetValueAt(-1, typeof(int)))
                .ThrowsExactly<ArgumentOutOfRangeException>();
        }

        // -------- Encrypt / Decrypt invariants --------

        [Test]
        public async Task EncryptPacket_Null_ReturnsNull()
        {
            await Assert.That(XPacket.EncryptPacket(null)).IsNull();
        }

        [Test]
        public async Task EncryptDecrypt_Roundtrip_PreservesPacket()
        {
            var p = XPacket.Create(7, 3);
            p.AppendValue(0x12345678);
            p.AppendValue(3.14);

            var encryptedBytes = p.Encrypt().ToPacket();
            var decrypted = XPacket.Parse(encryptedBytes);

            await Assert.That(decrypted).IsNotNull();
            await Assert.That(decrypted.PacketType).IsEqualTo((byte)7);
            await Assert.That(decrypted.PacketSubtype).IsEqualTo((byte)3);
            await Assert.That(decrypted.Fields.Count).IsEqualTo(2);
            await Assert.That(decrypted.GetValueAt<int>(0)).IsEqualTo(0x12345678);
            await Assert.That(decrypted.GetValueAt<double>(1)).IsEqualTo(3.14);
        }
    }
}
