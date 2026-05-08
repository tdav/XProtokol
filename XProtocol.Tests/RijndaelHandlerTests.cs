using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace XProtocol.Tests
{
    public class RijndaelHandlerTests
    {
        private const string TestPassphrase = "passphrase-for-tests";

        [Test]
        [Arguments(1)]
        [Arguments(15)]
        [Arguments(16)]
        [Arguments(17)]
        [Arguments(100)]
        [Arguments(1000)]
        public async Task EncryptDecrypt_RoundtripPreservesBytes(int plaintextLength)
        {
            var plaintext = new byte[plaintextLength];
            for (int i = 0; i < plaintextLength; i++)
            {
                plaintext[i] = (byte)(i & 0xFF);
            }

            var encrypted = RijndaelHandler.Encrypt(plaintext, TestPassphrase);
            var decrypted = RijndaelHandler.Decrypt(encrypted, TestPassphrase);

            await Assert.That(decrypted.Length).IsEqualTo(plaintextLength);
            await Assert.That(decrypted.SequenceEqual(plaintext)).IsTrue();
        }

        [Test]
        public async Task XProtocolEncryptor_Roundtrip_PreservesBytes()
        {
            var plaintext = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

            var encrypted = XProtocolEncryptor.Encrypt(plaintext);
            var decrypted = XProtocolEncryptor.Decrypt(encrypted);

            await Assert.That(decrypted.SequenceEqual(plaintext)).IsTrue();
        }

        [Test]
        public async Task Decrypt_CorruptedCiphertext_Throws()
        {
            var plaintext = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF };
            var encrypted = RijndaelHandler.Encrypt(plaintext, TestPassphrase);

            encrypted[50] ^= 0xFF;

            await Assert.That(() => RijndaelHandler.Decrypt(encrypted, TestPassphrase))
                .Throws<CryptographicException>();
        }
    }
}
