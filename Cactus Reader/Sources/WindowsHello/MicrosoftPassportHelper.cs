using Cactus_Reader.Entities;
using System;
using System.Diagnostics;
using Windows.Security.Credentials;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;

namespace Cactus_Reader.Sources.WindowsHello
{
    public static class MicrosoftPassportHelper
    {
        public static async Task<bool> MicrosoftPassportAvailableCheckAsync()
        {
            bool keyCredentialAvailable = await KeyCredentialManager.IsSupportedAsync();
            if (keyCredentialAvailable == false)
            {
                return false;
            }
            return true;
        }

        public static async Task<bool> CreatePassportKeyAsync(string UID, string Name)
        {
            KeyCredentialRetrievalResult keyCreationResult = await KeyCredentialManager.RequestCreateAsync(Name, KeyCredentialCreationOption.ReplaceExisting);

            switch (keyCreationResult.Status)
            {
                case KeyCredentialStatus.Success:
                    Debug.WriteLine("成功生成 Windows Hello 密钥。");
                    await GetKeyAttestationAsync(UID, keyCreationResult);
                    return true;
                case KeyCredentialStatus.UserCanceled:
                    Debug.WriteLine("用户取消了 Windows Hello 登录。");
                    break;
                case KeyCredentialStatus.NotFound:
                    Debug.WriteLine("Microsoft 帐户未设置，请前往 Windows 设置配置 PIN 码。");
                    break;
                default:
                    break;
            }
            return false;
        }

        public static async void RemovePassportAccountAsync(User user)
        {
            KeyCredentialRetrievalResult keyOpenResult = await KeyCredentialManager.OpenAsync(user.Name);
            if (keyOpenResult.Status == KeyCredentialStatus.Success)
            {
                await AuthService.Instance.PassportRemoveUserAsync(user.UID);
            }
            await KeyCredentialManager.DeleteAsync(user.Name);
        }

        public static async void RemovePassportDevice(User user, string deviceId)
        {
            await AuthService.Instance.PassportRemoveDeviceAsync(user.UID, deviceId);
        }

        private static async Task GetKeyAttestationAsync(string UID, KeyCredentialRetrievalResult keyCreationResult)
        {
            KeyCredential userKey = keyCreationResult.Credential;
            IBuffer publicKey = userKey.RetrievePublicKey();
            KeyCredentialAttestationResult keyAttestationResult = await userKey.GetAttestationAsync();

            if (keyAttestationResult.Status == KeyCredentialAttestationStatus.Success)
            {
                Debug.WriteLine("Successfully made key and attestation");
            }
            else if (keyAttestationResult.Status == KeyCredentialAttestationStatus.TemporaryFailure)
            {
                Debug.WriteLine("Successfully made key but not attestation");
            }
            else if (keyAttestationResult.Status == KeyCredentialAttestationStatus.NotSupported)
            {
                Debug.WriteLine("Key created, but key attestation not supported");
            }

            string deviceId = DeviceHelper.GetDeviceId();
            // 取证数据在 TemporaryFailure/NotSupported 时可能为空，Base64 存储
            byte[] attestation = keyAttestationResult.AttestationBuffer?.ToArray();
            await UpdatePassportDetailsAsync(UID, deviceId, publicKey.ToArray(), attestation);
        }

        public static async Task<bool> UpdatePassportDetailsAsync(string UID, string deviceId, byte[] publicKey, byte[] attestation)
        {
            return await AuthService.Instance.PassportUpdateDetailsAsync(UID, deviceId, publicKey, attestation);
        }

        /// <summary>
        /// 挑战-响应签名：向服务端申请一次性随机挑战，本地 TPM 密钥签名，
        /// 服务端用注册公钥验证签名。
        /// </summary>
        private static async Task<bool> RequestSignAsync(string UID, KeyCredentialRetrievalResult openKeyResult)
        {
            string challengeBase64 = await AuthService.Instance.PassportRequestChallengeAsync();
            IBuffer challengeMessage = CryptographicBuffer.DecodeFromBase64String(challengeBase64);

            KeyCredential userKey = openKeyResult.Credential;
            KeyCredentialOperationResult signResult = await userKey.RequestSignAsync(challengeMessage);

            if (signResult.Status == KeyCredentialStatus.Success)
            {
                return await AuthService.Instance.SendServerSignedChallengeAsync(
                    UID, DeviceHelper.GetDeviceId(), challengeBase64, signResult.Result.ToArray());
            }
            return false;
        }

        /// <summary>
        /// Windows Hello 登录验证（真实流程）：
        /// 打开已注册密钥 → 用户 PIN/生物识别确认 → 服务端挑战签名验证。
        /// 密钥不存在时返回 false（登录不重建密钥）。
        /// </summary>
        public static async Task<bool> GetPassportAuthenticationMessageAsync(User user)
        {
            KeyCredentialRetrievalResult openKeyResult = await KeyCredentialManager.OpenAsync(user.Name);

            if (openKeyResult.Status == KeyCredentialStatus.Success)
            {
                var consentResult = await Windows.Security.Credentials.UI.UserConsentVerifier.RequestVerificationAsync(user.Name);
                if (consentResult != Windows.Security.Credentials.UI.UserConsentVerificationResult.Verified)
                {
                    return false;
                }
                return await RequestSignAsync(user.UID, openKeyResult);
            }
            else if (openKeyResult.Status == KeyCredentialStatus.NotFound)
            {
                Debug.WriteLine("该设备未注册 Windows Hello 密钥，无法登录。");
            }
            return false;
        }
    }
}
