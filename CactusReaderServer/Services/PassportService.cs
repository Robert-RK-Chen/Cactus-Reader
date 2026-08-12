using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace CactusReaderService.Services
{
    /// <summary>
    /// Windows Hello 挑战-响应验证（服务端）。
    /// 挑战一次性使用：生成时入内存字典（默认 5 分钟过期），验证后即删除（防重放）。
    /// 签名验证：用 userkey 表注册的 RSA 公钥验证 challenge 的签名（SHA256 + PKCS#1 v1.5）。
    /// </summary>
    public class PassportService
    {
        private static readonly TimeSpan ChallengeTtl = TimeSpan.FromMinutes(5);
        private readonly ConcurrentDictionary<string, DateTime> _challenges = new ConcurrentDictionary<string, DateTime>();

        /// <summary>生成 32 字节随机挑战，返回 Base64。</summary>
        public string CreateChallenge()
        {
            byte[] challenge = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(challenge);
            }
            string encoded = Convert.ToBase64String(challenge);
            _challenges[encoded] = DateTime.UtcNow;
            CleanupExpired();
            return encoded;
        }

        /// <summary>
        /// 验证签名。storedPublicKeyBase64 为该用户/设备注册的公钥。
        /// 无论验证成败，挑战都会被消耗（一次性）。
        /// </summary>
        public bool VerifySignature(string challengeBase64, string signatureBase64, string storedPublicKeyBase64)
        {
            if (string.IsNullOrEmpty(challengeBase64) ||
                string.IsNullOrEmpty(signatureBase64) ||
                string.IsNullOrEmpty(storedPublicKeyBase64))
            {
                return false;
            }

            // 挑战必须存在且未过期
            if (!_challenges.TryGetValue(challengeBase64, out DateTime created))
            {
                return false;
            }
            if (DateTime.UtcNow - created > ChallengeTtl)
            {
                _challenges.TryRemove(challengeBase64, out _);
                return false;
            }

            try
            {
                byte[] challenge = Convert.FromBase64String(challengeBase64);
                byte[] signature = Convert.FromBase64String(signatureBase64);
                byte[] publicKey = Convert.FromBase64String(storedPublicKeyBase64);

                using (var rsa = RSA.Create())
                {
                    try
                    {
                        rsa.ImportSubjectPublicKeyInfo(publicKey, out _);
                    }
                    catch (CryptographicException)
                    {
                        // 兼容裸 PKCS#1 公钥格式
                        rsa.ImportRSAPublicKey(publicKey, out _);
                    }

                    bool valid = rsa.VerifyData(challenge, signature,
                        HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                    // 一次性：验证后消耗挑战
                    _challenges.TryRemove(challengeBase64, out _);
                    return valid;
                }
            }
            catch (Exception)
            {
                _challenges.TryRemove(challengeBase64, out _);
                return false;
            }
        }

        private void CleanupExpired()
        {
            DateTime now = DateTime.UtcNow;
            foreach (var pair in _challenges)
            {
                if (now - pair.Value > ChallengeTtl)
                {
                    _challenges.TryRemove(pair.Key, out _);
                }
            }
        }
    }
}
