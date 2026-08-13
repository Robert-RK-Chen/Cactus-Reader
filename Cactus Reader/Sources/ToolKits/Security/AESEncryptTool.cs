using System;
using System.Security.Cryptography;
using System.Text;

namespace Cactus_Reader.Sources.ToolKits
{
    /// <summary>
    /// 基于 AES-256-GCM 的认证加密工具。
    /// 加密输出格式：Base64(nonce[12] || ciphertext || tag[16])。
    /// nonce 每次加密随机生成，认证标签保证密文完整性，任何篡改都会导致解密失败。
    /// </summary>
    public class AESEncryptTool
    {
        private const int KeySizeBytes = 32;    // AES-256
        private const int NonceSizeBytes = 12;  // GCM 推荐 nonce 长度
        private const int TagSizeBytes = 16;    // GCM 认证标签长度

        private static readonly Lazy<AESEncryptTool> LazyInstance =
            new Lazy<AESEncryptTool>(() => new AESEncryptTool());

        public static AESEncryptTool Instance => LazyInstance.Value;

        private AESEncryptTool()
        {
        }

        /// <summary>
        /// 生成一个安全随机的高强度密钥（AES-256）。
        /// </summary>
        public byte[] CreateKey()
        {
            return RandomNumberGenerator.GetBytes(KeySizeBytes);
        }

        /// <summary>
        /// 使用 PBKDF2（SHA-256）从口令派生 AES-256 密钥，用于密钥不落盘的场景。
        /// </summary>
        public static byte[] DeriveKey(string password, byte[] salt, int iterations = 600_000)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentNullException(nameof(password));
            if (salt == null || salt.Length == 0)
                throw new ArgumentNullException(nameof(salt));

            return Rfc2898DeriveBytes.Pbkdf2(
                password, salt, iterations, HashAlgorithmName.SHA256, KeySizeBytes);
        }

        /// <summary>
        /// 加密字符串，返回 Base64(nonce || ciphertext || tag)。
        /// </summary>
        public string EncryptString(string plainText, byte[] key)
        {
            if (plainText == null)
                throw new ArgumentNullException(nameof(plainText));
            ValidateKey(key);

            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
            byte[] ciphertext = new byte[plainBytes.Length];
            byte[] tag = new byte[TagSizeBytes];

            using (var aes = new AesGcm(key, TagSizeBytes))
            {
                aes.Encrypt(nonce, plainBytes, ciphertext, tag);
            }

            byte[] result = new byte[NonceSizeBytes + ciphertext.Length + tag.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, NonceSizeBytes);
            Buffer.BlockCopy(ciphertext, 0, result, NonceSizeBytes, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, result, NonceSizeBytes + ciphertext.Length, tag.Length);
            return Convert.ToBase64String(result);
        }

        /// <summary>
        /// 解密 Base64(nonce || ciphertext || tag)。认证失败（密钥错误或数据被篡改）时抛出
        /// <see cref="CryptographicException"/>。
        /// </summary>
        public string DecryptString(string encryptedData, byte[] key)
        {
            if (string.IsNullOrEmpty(encryptedData))
                throw new ArgumentNullException(nameof(encryptedData));
            ValidateKey(key);

            byte[] full = Convert.FromBase64String(encryptedData);
            int ciphertextLength = full.Length - NonceSizeBytes - TagSizeBytes;
            if (ciphertextLength < 0)
                throw new CryptographicException("密文长度无效，数据可能已损坏。");

            byte[] nonce = full.AsSpan(0, NonceSizeBytes).ToArray();
            byte[] ciphertext = full.AsSpan(NonceSizeBytes, ciphertextLength).ToArray();
            byte[] tag = full.AsSpan(NonceSizeBytes + ciphertextLength, TagSizeBytes).ToArray();
            byte[] plainBytes = new byte[ciphertextLength];

            using (var aes = new AesGcm(key, TagSizeBytes))
            {
                aes.Decrypt(nonce, ciphertext, tag, plainBytes);
            }

            return Encoding.UTF8.GetString(plainBytes);
        }

        private static void ValidateKey(byte[] key)
        {
            if (key == null || key.Length != KeySizeBytes)
                throw new ArgumentException($"密钥长度必须为 {KeySizeBytes} 字节（AES-256）。", nameof(key));
        }
    }
}
