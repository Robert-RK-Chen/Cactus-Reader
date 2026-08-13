using System;
using System.Security.Cryptography;

namespace Cactus_Reader.Sources.ToolKits
{
    /// <summary>
    /// 基于 PBKDF2-SHA256 + 随机盐的口令哈希工具，用于存储与校验便签本个人密码。
    /// 存储格式：Base64(salt[16] || hash[32])，每次哈希自动生成新盐，同一口令的哈希结果不同。
    /// 校验使用恒定时间比较（FixedTimeEquals），防止计时攻击。
    /// </summary>
    public class PasswordHashTool
    {
        private const int SaltSizeBytes = 16;
        private const int HashSizeBytes = 32;
        // OWASP Password Storage Cheat Sheet 推荐 PBKDF2-HMAC-SHA256 至少 600,000 次迭代
        private const int Iterations = 600_000;

        private static readonly Lazy<PasswordHashTool> LazyInstance =
            new Lazy<PasswordHashTool>(() => new PasswordHashTool());

        public static PasswordHashTool Instance => LazyInstance.Value;

        private PasswordHashTool()
        {
        }

        /// <summary>
        /// 对口令做加盐哈希，返回 Base64(salt || hash)。结果可安全存入 LocalSettings。
        /// </summary>
        public string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentNullException(nameof(password));

            byte[] salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                password, salt, Iterations, HashAlgorithmName.SHA256, HashSizeBytes);

            byte[] combined = new byte[salt.Length + hash.Length];
            Buffer.BlockCopy(salt, 0, combined, 0, salt.Length);
            Buffer.BlockCopy(hash, 0, combined, salt.Length, hash.Length);
            return Convert.ToBase64String(combined);
        }

        /// <summary>
        /// 校验口令与存储哈希是否匹配。格式非法或口令错误时返回 false，不抛出异常。
        /// </summary>
        public bool VerifyPassword(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
                return false;

            try
            {
                byte[] combined = Convert.FromBase64String(storedHash);
                if (combined.Length != SaltSizeBytes + HashSizeBytes)
                    return false;

                byte[] salt = combined.AsSpan(0, SaltSizeBytes).ToArray();
                byte[] expected = combined.AsSpan(SaltSizeBytes, HashSizeBytes).ToArray();
                byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
                    password, salt, Iterations, HashAlgorithmName.SHA256, HashSizeBytes);

                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
