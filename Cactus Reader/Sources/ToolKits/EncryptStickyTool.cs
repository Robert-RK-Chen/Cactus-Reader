using Cactus_Reader.Entities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Windows.Storage;

namespace Cactus_Reader.Sources.ToolKits
{
    /// <summary>
    /// 便签本加密编排：负责便签内容（.ctsnote）的加密读写与锁定状态维护，
    /// 以及便签加密密钥（vault）的跨设备管理。
    ///
    /// 密钥模型（密码包裹密钥，零知识）：
    ///   1. 便签内容使用随机生成的 AES-256 密钥（K）加密；
    ///   2. 设置个人密码时：生成随机盐 S，KEK = PBKDF2(密码, S)，
    ///      将 K 用 KEK 加密后（S + 密文密钥）上传服务端 vault；
    ///   3. 本机 LocalSettings 缓存一份 K —— 首次输入密码后本机免密；
    ///   4. 更换设备：登录 → 拉取 S + 密文密钥 → 输入个人密码解出 K。
    /// 服务端只持有盐与密文密钥，无法解密任何便签数据（零知识）。
    /// </summary>
    public class EncryptStickyTool
    {
        private const string SystemKeySettingName = "stickyEncryptionKey";
        private const int KeySizeBytes = 32;        // AES-256
        private const int VaultSaltSizeBytes = 16;
        private const int VaultIterations = 600_000; // 与 PasswordHashTool 一致，符合 OWASP 建议

        private readonly ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        private readonly AESEncryptTool aesEncryptTool = AESEncryptTool.Instance;
        private static EncryptStickyTool instance;

        // 会话级密钥缓存，避免每次访问 LocalSettings
        private byte[] cachedKey;

        // 服务端 vault 信息（盐 + 密文密钥），由 EnsureStickyKeyReadyAsync 加载
        private string vaultSalt;
        private string vaultWrappedKey;

        public static EncryptStickyTool Instance
        {
            get
            {
                return instance ?? (instance = new EncryptStickyTool());
            }
        }

        /// <summary>密钥未就绪（换设备需输入个人密码解包）时抛出。</summary>
        public class VaultKeyRequiredException : Exception
        {
        }

        // ---------------- 密钥获取与 vault 管理 ----------------

        /// <summary>
        /// 获取便签加密密钥：
        ///   1. 本机已缓存（会话或 LocalSettings）→ 直接返回；
        ///   2. 服务端已有包裹密钥但本机没有（换设备）→ 抛 <see cref="VaultKeyRequiredException"/>，需输入密码解锁；
        ///   3. 从未设置过密码 → 生成新随机密钥并缓存。
        /// </summary>
        private byte[] GetOrCreateSystemKey()
        {
            if (cachedKey != null)
            {
                return cachedKey;
            }

            if (localSettings.Values.TryGetValue(SystemKeySettingName, out object value) &&
                value is string storedBase64 && TryParseKey(storedBase64, out byte[] localKey))
            {
                cachedKey = localKey;
                return localKey;
            }

            // 服务端已有包裹密钥（EnsureStickyKeyReadyAsync 已加载）→ 需要输入个人密码
            if (!string.IsNullOrEmpty(vaultWrappedKey))
            {
                throw new VaultKeyRequiredException();
            }

            // 首次使用：生成新密钥并持久化
            byte[] key = aesEncryptTool.CreateKey();
            cachedKey = key;
            localSettings.Values[SystemKeySettingName] = Convert.ToBase64String(key);
            return key;
        }

        private static bool TryParseKey(string base64, out byte[] key)
        {
            key = null;
            try
            {
                byte[] bytes = Convert.FromBase64String(base64);
                if (bytes.Length == KeySizeBytes)
                {
                    key = bytes;
                    return true;
                }
            }
            catch (FormatException)
            {
            }
            return false;
        }

        /// <summary>
        /// 确保密钥可用。登录后或进入便签页面时调用：
        /// 加载服务端 vault 信息并判断密钥是否就绪。
        /// 返回 true 表示可直接加解密；返回 false 表示需要用户输入个人密码解锁。
        /// </summary>
        public async Task<bool> EnsureStickyKeyReadyAsync()
        {
            // 本机已有密钥则直接可用
            if (cachedKey != null ||
                (localSettings.Values.TryGetValue(SystemKeySettingName, out object value) &&
                 value is string base64 && TryParseKey(base64, out _)))
            {
                return true;
            }

            string uid = localSettings.Values["UID"]?.ToString();
            if (string.IsNullOrEmpty(uid))
            {
                return true; // 未登录，不触发 vault 流程
            }

            try
            {
                (bool ok, string salt, string wrappedKey) = await ApiClient.GetVaultKeyAsync(uid);
                if (!ok)
                {
                    return false;
                }
                vaultSalt = salt;
                vaultWrappedKey = wrappedKey;

                // 服务端无包裹密钥（从未设置密码）→ 首次使用场景，密钥生成交给 GetOrCreateSystemKey
                return string.IsNullOrEmpty(wrappedKey);
            }
            catch (Exception)
            {
                // 网络不可用：本机无密钥时按就绪处理（首次使用可离线生成新密钥）
                return true;
            }
        }

        /// <summary>
        /// 用个人密码解锁便签密钥（换设备场景）。
        /// 成功后将密钥缓存到本机（之后本机免密），并顺带缓存密码哈希供本机密码校验使用。
        /// 密码错误返回 false，不会抛出。
        /// </summary>
        public async Task<bool> UnlockWithPasswordAsync(string password)
        {
            if (string.IsNullOrEmpty(vaultWrappedKey) || string.IsNullOrEmpty(vaultSalt))
            {
                // 尚未加载 vault 信息，先拉取
                string uid = localSettings.Values["UID"]?.ToString();
                if (string.IsNullOrEmpty(uid))
                {
                    return false;
                }
                try
                {
                    (bool ok, string salt, string wrappedKey) = await ApiClient.GetVaultKeyAsync(uid);
                    if (!ok)
                    {
                        return false;
                    }
                    vaultSalt = salt;
                    vaultWrappedKey = wrappedKey;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            if (string.IsNullOrEmpty(vaultWrappedKey) || string.IsNullOrEmpty(vaultSalt))
            {
                return false; // 服务端没有包裹密钥，无需解锁
            }

            try
            {
                byte[] salt = Convert.FromBase64String(vaultSalt);
                byte[] kek = AESEncryptTool.DeriveKey(password, salt, VaultIterations);
                string keyBase64 = aesEncryptTool.DecryptString(vaultWrappedKey, kek); // GCM 认证失败即密码错误
                byte[] key = Convert.FromBase64String(keyBase64);
                if (key.Length != KeySizeBytes)
                {
                    return false;
                }

                cachedKey = key;
                localSettings.Values[SystemKeySettingName] = keyBase64;

                // 顺带缓存密码哈希，保证本机 CheckPassword（关闭密码等）可用
                if (!localSettings.Values.ContainsKey("privateKey"))
                {
                    localSettings.Values["privateKey"] = PasswordHashTool.Instance.HashPassword(password);
                }
                return true;
            }
            catch (Exception)
            {
                return false; // 密码错误或数据损坏
            }
        }

        /// <summary>
        /// 设置/更换个人密码：用新密码重新包裹当前密钥并上传服务端。
        /// 便签数据无需重新加密。设置成功后本地密码哈希由调用方（SettingPage）写入。
        /// </summary>
        public async Task<bool> SetupVaultAsync(string password)
        {
            try
            {
                string uid = localSettings.Values["UID"]?.ToString();
                if (string.IsNullOrEmpty(uid))
                {
                    return false;
                }

                byte[] key = GetOrCreateSystemKey(); // 可能抛 VaultKeyRequiredException（需先解锁）

                byte[] salt = RandomNumberGenerator.GetBytes(VaultSaltSizeBytes);
                byte[] kek = AESEncryptTool.DeriveKey(password, salt, VaultIterations);
                string wrappedKey = aesEncryptTool.EncryptString(Convert.ToBase64String(key), kek);
                string saltBase64 = Convert.ToBase64String(salt);

                if (!await ApiClient.SetVaultKeyAsync(uid, saltBase64, wrappedKey))
                {
                    return false;
                }

                vaultSalt = saltBase64;
                vaultWrappedKey = wrappedKey;
                return true;
            }
            catch (VaultKeyRequiredException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 关闭个人密码：删除服务端包裹密钥。
        /// 本机密钥保留（本机继续可用），但失去跨设备恢复能力。
        /// </summary>
        public async Task<bool> RemoveVaultAsync()
        {
            try
            {
                string uid = localSettings.Values["UID"]?.ToString();
                if (string.IsNullOrEmpty(uid))
                {
                    return false;
                }
                bool ok = await ApiClient.RemoveVaultKeyAsync(uid);
                if (ok)
                {
                    vaultSalt = null;
                    vaultWrappedKey = null;
                }
                return ok;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>是否已有本地密钥缓存（无需输入密码即可加解密）。</summary>
        public bool HasLocalKey
        {
            get
            {
                return cachedKey != null ||
                       (localSettings.Values.TryGetValue(SystemKeySettingName, out object value) &&
                        value is string base64 && TryParseKey(base64, out _));
            }
        }

        // ---------------- 便签内容加解密 ----------------

        /// <summary>加密便签内容文本。</summary>
        public string EncryptStickyText(string plainText)
        {
            return aesEncryptTool.EncryptString(plainText, GetOrCreateSystemKey());
        }

        /// <summary>解密便签内容文本。密钥错误或数据被篡改时抛出 <see cref="CryptographicException"/>。</summary>
        public string DecryptStickyText(string encryptedText)
        {
            return aesEncryptTool.DecryptString(encryptedText, GetOrCreateSystemKey());
        }

        public async Task<bool> LockStickyAsync(string stickySerial)
        {
            try
            {
                string UID = localSettings.Values["UID"].ToString();
                StorageFolder stickyFolder = await ApplicationData.Current.LocalFolder.GetFolderAsync(UID);
                stickyFolder = await stickyFolder.GetFolderAsync("Sticky");
                StorageFile stickyFile = await stickyFolder.GetFileAsync(stickySerial + ".ctsnote");

                string stickyText = DecryptStickyText(File.ReadAllText(stickyFile.Path));

                Sticky sticky = JsonConvert.DeserializeObject<Sticky>(stickyText);
                sticky.IsLock = true;

                string encryptText = EncryptStickyText(JsonConvert.SerializeObject(sticky));
                File.WriteAllText(stickyFile.Path, encryptText);
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
        }

        public async Task<bool> UnlockStickyAsync(string stickySerial)
        {
            try
            {
                string UID = localSettings.Values["UID"].ToString();
                StorageFolder stickyFolder = await ApplicationData.Current.LocalFolder.GetFolderAsync(UID);
                stickyFolder = await stickyFolder.GetFolderAsync("Sticky");
                StorageFile stickyFile = await stickyFolder.GetFileAsync(stickySerial + ".ctsnote");

                string stickyText = DecryptStickyText(File.ReadAllText(stickyFile.Path));

                Sticky sticky = JsonConvert.DeserializeObject<Sticky>(stickyText);
                sticky.IsLock = false;

                string encryptText = EncryptStickyText(JsonConvert.SerializeObject(sticky));
                File.WriteAllText(stickyFile.Path, encryptText);
                return true;
            }
            catch(FileNotFoundException)
            {
                return false;
            }
        }

        public async Task<bool> IsStickyLockedAsync(string stickySerial)
        {
            try
            {
                string UID = localSettings.Values["UID"].ToString();
                StorageFolder stickyFolder = await ApplicationData.Current.LocalFolder.GetFolderAsync(UID);
                stickyFolder = await stickyFolder.GetFolderAsync("Sticky");
                StorageFile stickyFile = await stickyFolder.GetFileAsync(stickySerial + ".ctsnote");

                string stickyText = DecryptStickyText(File.ReadAllText(stickyFile.Path));

                Sticky sticky = JsonConvert.DeserializeObject<Sticky>(stickyText);
                return sticky.IsLock;
            }
            catch(FileNotFoundException)
            {
                return false;
            }
        }

        public async void UnlockAllSticky()
        {
            try
            {
                string UID = localSettings.Values["UID"].ToString();
                StorageFolder stickyFolder = await ApplicationData.Current.LocalFolder.GetFolderAsync(UID);
                stickyFolder = await stickyFolder.GetFolderAsync("Sticky");
                IReadOnlyList<StorageFile> fileList = await stickyFolder.GetFilesAsync();

                foreach(StorageFile stickyFile in fileList)
                {
                    try
                    {
                        string stickyText = DecryptStickyText(File.ReadAllText(stickyFile.Path));

                        Sticky sticky = JsonConvert.DeserializeObject<Sticky>(stickyText);
                        sticky.IsLock = false;

                        string encryptText = EncryptStickyText(JsonConvert.SerializeObject(sticky));
                        File.WriteAllText(stickyFile.Path, encryptText);
                    }
                    catch (CryptographicException)
                    {
                        // 单个便签解密失败（密钥不匹配或数据损坏）时跳过，不中断其余便签
                    }
                }
            }
            catch (FileNotFoundException)
            {
            }
        }
    }
}
