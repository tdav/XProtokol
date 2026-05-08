using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace XProtocol
{
    public static class RijndaelHandler
    {
        private const int Keysize = 256;           // key size in bits (32 bytes)
        private const int IvSize = 128;            // AES block/IV size in bits (16 bytes)
        private const int DerivationIterations = 1000;

        public static byte[] Encrypt(byte[] data, string passPhrase)
        {
            var saltStringBytes = GenerateRandomBytes(Keysize / 8);  // 32 bytes
            var ivStringBytes = GenerateRandomBytes(IvSize / 8);      // 16 bytes
            var keyBytes = Rfc2898DeriveBytes.Pbkdf2(passPhrase, saltStringBytes, DerivationIterations, HashAlgorithmName.SHA256, Keysize / 8);

            using (var symmetricKey = Aes.Create())
            {
                symmetricKey.Mode = CipherMode.CBC;
                symmetricKey.Padding = PaddingMode.PKCS7;
                using (var encryptor = symmetricKey.CreateEncryptor(keyBytes, ivStringBytes))
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                        {
                            cryptoStream.Write(data, 0, data.Length);
                            cryptoStream.FlushFinalBlock();
                            var cipherTextBytes = saltStringBytes;
                            cipherTextBytes = cipherTextBytes.Concat(ivStringBytes).ToArray();
                            cipherTextBytes = cipherTextBytes.Concat(memoryStream.ToArray()).ToArray();
                            memoryStream.Close();
                            cryptoStream.Close();
                            return cipherTextBytes;
                        }
                    }
                }
            }
        }

        public static byte[] Decrypt(byte[] data, string passPhrase)
        {
            var saltStringBytes = data.Take(Keysize / 8).ToArray();                    // 32 bytes
            var ivStringBytes = data.Skip(Keysize / 8).Take(IvSize / 8).ToArray();    // 16 bytes
            var cipherTextBytes = data.Skip(Keysize / 8 + IvSize / 8)
                                      .Take(data.Length - Keysize / 8 - IvSize / 8).ToArray();
            var keyBytes = Rfc2898DeriveBytes.Pbkdf2(passPhrase, saltStringBytes, DerivationIterations, HashAlgorithmName.SHA256, Keysize / 8);

            using (var symmetricKey = Aes.Create())
            {
                symmetricKey.Mode = CipherMode.CBC;
                symmetricKey.Padding = PaddingMode.PKCS7;
                using (var decryptor = symmetricKey.CreateDecryptor(keyBytes, ivStringBytes))
                {
                    using (var memoryStream = new MemoryStream(cipherTextBytes))
                    {
                        using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                        {
                            using (var plainStream = new MemoryStream())
                            {
                                cryptoStream.CopyTo(plainStream);
                                return plainStream.ToArray();
                            }
                        }
                    }
                }
            }
        }

        private static byte[] GenerateRandomBytes(int count)
        {
            return RandomNumberGenerator.GetBytes(count);
        }
    }
}
