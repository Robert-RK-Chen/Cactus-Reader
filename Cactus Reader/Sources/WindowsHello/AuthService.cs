using Cactus_Reader.Entities;
using Cactus_Reader.Sources.ToolKits;
using System;
using System.Threading.Tasks;

namespace Cactus_Reader.Sources.WindowsHello
{
    /// <summary>
    /// Windows Hello 认证服务。
    /// 2026-08：所有数据库操作（userkey 表等）经 CactusReaderServer API，
    /// 客户端不再直连 MySQL；公钥/取证数据以 Base64 存储；
    /// 挑战-响应为真实签名校验（不再恒返回 true）。
    /// </summary>
    public class AuthService
    {
        private static AuthService instance;

        public static AuthService Instance
        {
            get { return instance ?? (instance = new AuthService()); }
        }

        private AuthService()
        { }

        public async Task<User> GetUserByEmailAsync(string email)
        {
            return await ApiClient.GetUserByEmailAsync(email);
        }

        public async Task<User> GetUserByUidAsync(string uid)
        {
            return await ApiClient.GetUserByUidAsync(uid);
        }

        public async Task<User> GetUserByNameAsync(string name)
        {
            return await ApiClient.GetUserByNameAsync(name);
        }

        /// <summary>删除该用户全部设备密钥（服务端）。</summary>
        public async Task<bool> PassportRemoveUserAsync(string uid)
        {
            return await ApiClient.RemoveUserKeyAsync(uid);
        }

        /// <summary>删除该用户指定设备密钥（服务端）。</summary>
        public async Task<bool> PassportRemoveDeviceAsync(string uid, string deviceId)
        {
            return await ApiClient.RemoveDeviceKeyAsync(uid, deviceId);
        }

        /// <summary>上报密钥：PublicKey / Attestation 均以 Base64 编码存储。</summary>
        public async Task<bool> PassportUpdateDetailsAsync(string uid, string deviceId, byte[] publicKey, byte[] attestation)
        {
            return await ApiClient.UpdateUserKeyAsync(uid, deviceId,
                Convert.ToBase64String(publicKey),
                attestation == null ? "" : Convert.ToBase64String(attestation));
        }

        /// <summary>向服务端申请一次性随机挑战（Base64）。</summary>
        public async Task<string> PassportRequestChallengeAsync()
        {
            return await ApiClient.GetChallengeAsync();
        }

        /// <summary>提交挑战签名，服务端用注册公钥验证（真实校验）。</summary>
        public async Task<bool> SendServerSignedChallengeAsync(string uid, string deviceId, string challengeBase64, byte[] signedChallenge)
        {
            return await ApiClient.VerifySignatureAsync(uid, deviceId, challengeBase64,
                Convert.ToBase64String(signedChallenge));
        }
    }
}
