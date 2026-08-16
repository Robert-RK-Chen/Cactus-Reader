using System;
using System.Security.Cryptography;
using System.Text;

namespace CactusReaderService.Services
{
    /// <summary>
    /// 密码哈希：随机盐（16 字节，Base64）+ PBKDF2-SHA256（600,000 迭代，符合 OWASP 建议）。
    /// 存储格式："Base64(盐):迭代次数:Base64(哈希)"。
    /// 旧的双重 SHA256 无盐/带盐格式不再兼容（数据库由本人用工具按新格式重算填回）。
    /// </summary>
    public class PasswordHashService
    {
        private const int SaltSize = 16;
        private const int HashSizeBytes = 32;
        private const int Iterations = 600_000; // 与客户端便签密钥派生（AESEncryptTool.DeriveKey）一致

        /// <summary>生成带随机盐的密码哈希（"盐:迭代次数:哈希"）。</summary>
        public string CreateHash(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                password, salt, Iterations, HashAlgorithmName.SHA256, HashSizeBytes);
            return Convert.ToBase64String(salt) + ":" + Iterations + ":" + Convert.ToBase64String(hash);
        }

        /// <summary>校验明文密码与存储哈希是否匹配（按存储的盐与迭代次数派生，恒定时间比较防时序侧信道）。</summary>
        public bool Verify(string password, string stored)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(stored)) return false;

            string[] parts = stored.Split(':');
            if (parts.Length != 3 || !int.TryParse(parts[1], out int iterations) || iterations <= 0)
            {
                return false;
            }

            try
            {
                byte[] salt = Convert.FromBase64String(parts[0]);
                byte[] expected = Convert.FromBase64String(parts[2]);
                byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
                    password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
