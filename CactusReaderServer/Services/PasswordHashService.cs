using System;
using System.Security.Cryptography;
using System.Text;

namespace CactusReaderService.Services
{
    /// <summary>
    /// 密码哈希：随机盐（16 字节，Base64）+ 双重 SHA256（UTF-8）。
    /// 存储格式："Base64(盐):哈希Hex"。
    /// 修复原客户端实现的两个问题：Encoding.Default 依赖系统区域设置；无盐易受彩虹表攻击。
    /// </summary>
    public class PasswordHashService
    {
        private const int SaltSize = 16;

        /// <summary>生成带随机盐的密码哈希（"盐:哈希"）。</summary>
        public string CreateHash(string password)
        {
            byte[] salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            byte[] hash = ComputeHash(salt, password);
            return Convert.ToBase64String(salt) + ":" + ToHex(hash);
        }

        /// <summary>校验明文密码与存储哈希是否匹配。旧的无盐格式一律视为不匹配。</summary>
        public bool Verify(string password, string stored)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(stored)) return false;

            int sep = stored.IndexOf(':');
            if (sep <= 0) return false; // 无盐旧格式

            try
            {
                byte[] salt = Convert.FromBase64String(stored.Substring(0, sep));
                string storedHash = stored.Substring(sep + 1);
                byte[] hash = ComputeHash(salt, password);
                return ToHex(hash).Equals(storedHash, StringComparison.OrdinalIgnoreCase);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static byte[] ComputeHash(byte[] salt, string password)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] pwdBytes = Encoding.UTF8.GetBytes(password);
                byte[] salted = new byte[salt.Length + pwdBytes.Length];
                Buffer.BlockCopy(salt, 0, salted, 0, salt.Length);
                Buffer.BlockCopy(pwdBytes, 0, salted, salt.Length, pwdBytes.Length);
                return sha256.ComputeHash(sha256.ComputeHash(salted));
            }
        }

        private static string ToHex(byte[] bytes)
        {
            StringBuilder sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                sb.Append(b.ToString("X2"));
            }
            return sb.ToString();
        }
    }
}
