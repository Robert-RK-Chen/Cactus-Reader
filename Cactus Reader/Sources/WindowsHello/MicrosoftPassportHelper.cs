using Cactus_Reader.Entities;
using System;
using System.Diagnostics;
using Windows.Storage.Streams;
using System.Threading.Tasks;
using Windows.Security.Credentials;
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
                Debug.WriteLine("Microsoft 帐户未设置，请前往 Windows 设置配置 PIN 码。");
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
                AuthService.Instance.PassportRemoveUser(user.UID);
            }
            await KeyCredentialManager.DeleteAsync(user.Name);
        }

        public static void RemovePassportDevice(User user, string deviceId)
        {
            AuthService.Instance.PassportRemoveDevice(user.UID, deviceId);
        }

        private static async Task GetKeyAttestationAsync(string UID, KeyCredentialRetrievalResult keyCreationResult)
        {
            KeyCredential userKey = keyCreationResult.Credential;
            IBuffer publicKey = userKey.RetrievePublicKey();
            KeyCredentialAttestationResult keyAttestationResult = await userKey.GetAttestationAsync();
            IBuffer keyAttestation = null;
            IBuffer certificateChain = null;
            bool keyAttestationIncluded = false;
            bool keyAttestationCanBeRetrievedLater = false;
            KeyCredentialAttestationStatus keyAttestationRetryType = 0;

            if (keyAttestationResult.Status == KeyCredentialAttestationStatus.Success)
            {
                keyAttestationIncluded = true;
                keyAttestation = keyAttestationResult.AttestationBuffer;
                certificateChain = keyAttestationResult.CertificateChainBuffer;
                Debug.WriteLine("Successfully made key and attestation");
            }
            else if (keyAttestationResult.Status == KeyCredentialAttestationStatus.TemporaryFailure)
            {
                keyAttestationRetryType = KeyCredentialAttestationStatus.TemporaryFailure;
                keyAttestationCanBeRetrievedLater = true;
                Debug.WriteLine("Successfully made key but not attestation");
            }
            else if (keyAttestationResult.Status == KeyCredentialAttestationStatus.NotSupported)
            {
                keyAttestationRetryType = KeyCredentialAttestationStatus.NotSupported;
                keyAttestationCanBeRetrievedLater = false;
                Debug.WriteLine("Key created, but key attestation not supported");
            }

            string deviceId = DeviceHelper.GetDeviceId();
            UpdatePassportDetails(UID, deviceId, publicKey.ToArray(), keyAttestationResult);
        }

        public static bool UpdatePassportDetails(string UID, string deviceId, byte[] publicKey, KeyCredentialAttestationResult keyAttestationResult)
        {
            AuthService.Instance.PassportUpdateDetails(UID, deviceId, publicKey, keyAttestationResult);
            return true;
        }

        private static async Task<bool> RequestSignAsync(string UID, KeyCredentialRetrievalResult openKeyResult)
        {
            IBuffer challengeMessage = AuthService.Instance.PassportRequestChallenge();
            KeyCredential userKey = openKeyResult.Credential;
            KeyCredentialOperationResult signResult = await userKey.RequestSignAsync(challengeMessage);

            if (signResult.Status == KeyCredentialStatus.Success)
            {
                return AuthService.Instance.SendServerSignedChallenge(
                    UID, DeviceHelper.GetDeviceId(), signResult.Result.ToArray());
            }
            else if (signResult.Status == KeyCredentialStatus.UserCanceled)
            {
            }
            else if (signResult.Status == KeyCredentialStatus.NotFound)
            {
            }
            else if (signResult.Status == KeyCredentialStatus.SecurityDeviceLocked)
            {
            }
            else if (signResult.Status == KeyCredentialStatus.UnknownError)
            {
            }
            return false;
        }

        public static async Task<bool> GetPassportAuthenticationMessageAsync(User user)
        {
            KeyCredentialRetrievalResult openKeyResult = await KeyCredentialManager.OpenAsync(user.Name);
            var consentResult = await Windows.Security.Credentials.UI.UserConsentVerifier.RequestVerificationAsync(user.Name);

            if (openKeyResult.Status == KeyCredentialStatus.Success)
            {
                return await RequestSignAsync(user.UID, openKeyResult);
            }
            else if (openKeyResult.Status == KeyCredentialStatus.NotFound)
            {
                if (await CreatePassportKeyAsync(user.UID, user.Name))
                {
                    return await GetPassportAuthenticationMessageAsync(user);
                }
            }
            return false;
        }
    }
}