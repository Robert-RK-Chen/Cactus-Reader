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
        ///   3. 服务端已有明文备份（无密码模式，salt 空 wrappedKey 非空）→ 直接采用（免密恢复）；
        ///   4. 从未备份过 → 生成新随机密钥并缓存。
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

            // 服务端已有密码包裹密钥（EnsureStickyKeyReadyAsync 已加载）→ 需要输入个人密码
            if (!string.IsNullOrEmpty(vaultWrappedKey) && !string.IsNullOrEmpty(vaultSalt))
            {
                throw new VaultKeyRequiredException();
            }

            // 服务端已有明文密钥备份（无密码模式：salt 空、wrappedKey 非空）→ 直接采用
            if (!string.IsNullOrEmpty(vaultWrappedKey) && string.IsNullOrEmpty(vaultSalt) &&
                TryParseKey(vaultWrappedKey, out byte[] plainKey))
            {
                cachedKey = plainKey;
                localSettings.Values[SystemKeySettingName] = vaultWrappedKey;
                return plainKey;
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
        ///
        /// vault 三态：
        ///   wrappedKey 空            → 从未备份（首次使用，可离线生成新密钥）→ true；
        ///   wrappedKey 非空 + salt 空 → 无密码模式明文备份 → 直接采用（免密恢复）→ true；
        ///   wrappedKey 非空 + salt 非空 → 密码包裹 → 需输入个人密码解锁 → false。
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

                // 从未备份 → 首次使用场景，密钥生成交给 GetOrCreateSystemKey
                if (string.IsNullOrEmpty(wrappedKey))
                {
                    return true;
                }

                // 无密码模式明文备份 → 直接采用为本地密钥（免密恢复）
                if (string.IsNullOrEmpty(salt) && TryParseKey(wrappedKey, out _))
                {
                    GetOrCreateSystemKey(); // 内部会采用明文密钥并缓存本机
                    return true;
                }

                // 密码包裹 → 需要输入个人密码解锁
                return false;
            }
            catch (Exception)
            {
                // 网络不可用：本机无密钥时按就绪处理（首次使用可离线生成新密钥）
                return true;
            }
        }

        /// <summary>
        /// 无密码模式密钥备份：本机未设置个人密码时，把便签密钥明文（Base64）备份到服务端 vault
        /// （salt 空 + wrappedKey = Base64(K)），保证卸载 / 换设备后免密恢复便签。
        ///
        /// 必须先加载 vault 再决策：云端已有明文 K 时【采用而非新生成】，否则新设备
        /// 第一次登录会生成新密钥覆盖备份，导致旧便签全部无法解密。
        /// 已设置个人密码（vault 为密码包裹）时跳过，避免覆盖。在登录全量同步时调用。
        /// </summary>
        public async Task BackupPlainKeyIfNeededAsync()
        {
            try
            {
                // 已设置个人密码：vault 必须是密码包裹，不可降级为明文备份
                if (localSettings.Values.ContainsKey("privateKey"))
                {
                    return;
                }

                string uid = localSettings.Values["UID"]?.ToString();
                if (string.IsNullOrEmpty(uid))
                {
                    return;
                }

                // 1. 加载 vault（尚未加载时）；加载失败不阻断（下次同步重试）
                if (vaultSalt == null && vaultWrappedKey == null)
                {
                    try
                    {
                        (bool ok, string salt, string wrappedKey) = await ApiClient.GetVaultKeyAsync(uid);
                        if (!ok)
                        {
                            return;
                        }
                        vaultSalt = salt;
                        vaultWrappedKey = wrappedKey;
                    }
                    catch (Exception)
                    {
                        return;
                    }
                }

                // 2. 密码包裹模式：vault 是 S + 密文密钥，不可被明文覆盖
                if (!string.IsNullOrEmpty(vaultWrappedKey) && !string.IsNullOrEmpty(vaultSalt))
                {
                    return;
                }

                // 3. 明文模式：vault 有明文 K 则采用（GetOrCreateSystemKey 内部处理），无则生成新密钥
                byte[] key = GetOrCreateSystemKey();
                string keyBase64 = Convert.ToBase64String(key);

                if (string.IsNullOrEmpty(vaultSalt) &&
                    string.Equals(vaultWrappedKey, keyBase64, StringComparison.Ordinal))
                {
                    return; // 已是明文备份且一致，无需重复上传
                }

                // 覆盖为明文备份（salt 空 + wrappedKey = 明文密钥）
                if (await ApiClient.SetVaultKeyAsync(uid, "", keyBase64))
                {
                    vaultSalt = "";
                    vaultWrappedKey = keyBase64;
                }
            }
            catch (Exception)
            {
                // 备份失败不阻断主流程：下次同步重试
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

        /// <summary>
        /// 重新开始并设置新密码：放弃旧设备密钥，生成新密钥并用新密码包裹上传 vault。
        /// 之后本机以密码包裹模式运行（与正常设置密码一致，换设备凭新密码恢复）。
        /// 注意：旧便签文件将永久无法解密（密钥已丢失），需配合孤儿文件清理提示使用。
        /// </summary>
        public async Task<bool> RestartWithNewPasswordAsync(string password)
        {
            try
            {
                string uid = localSettings.Values["UID"]?.ToString();
                if (string.IsNullOrEmpty(uid))
                {
                    return false;
                }

                // 1. 生成新密钥并缓存本机（绕过旧 vault 的密码包裹检查，直接落盘本机）
                byte[] key = aesEncryptTool.CreateKey();
                cachedKey = key;
                localSettings.Values[SystemKeySettingName] = Convert.ToBase64String(key);

                // 2. 清除内存中旧 vault 状态，使 SetupVaultAsync 按全新设置处理
                vaultSalt = null;
                vaultWrappedKey = null;

                // 3. 用新密码包裹新密钥上传 vault
                if (!await SetupVaultAsync(password))
                {
                    return false;
                }

                // 4. 缓存密码哈希，本机密码验证（锁定 / 解锁便签）可用
                localSettings.Values["privateKey"] = PasswordHashTool.Instance.HashPassword(password);
                return true;
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

                // 读取-解密-置锁定-加密-写回 整体在后台线程执行，避免阻塞 UI
                await Task.Run(() =>
                {
                    string stickyText = DecryptStickyText(File.ReadAllText(stickyFile.Path));
                    Sticky sticky = JsonConvert.DeserializeObject<Sticky>(stickyText);
                    sticky.IsLock = true;
                    File.WriteAllText(stickyFile.Path, EncryptStickyText(JsonConvert.SerializeObject(sticky)));
                });
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

                // 读取-解密-解除锁定-加密-写回 整体在后台线程执行，避免阻塞 UI
                await Task.Run(() =>
                {
                    string stickyText = DecryptStickyText(File.ReadAllText(stickyFile.Path));
                    Sticky sticky = JsonConvert.DeserializeObject<Sticky>(stickyText);
                    sticky.IsLock = false;
                    File.WriteAllText(stickyFile.Path, EncryptStickyText(JsonConvert.SerializeObject(sticky)));
                });
                return true;
            }
            catch (FileNotFoundException)
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

                // 读取-解密-返回锁定状态 在后台线程执行
                return await Task.Run(() =>
                {
                    string stickyText = DecryptStickyText(File.ReadAllText(stickyFile.Path));
                    Sticky sticky = JsonConvert.DeserializeObject<Sticky>(stickyText);
                    return sticky.IsLock;
                });
            }
            catch (FileNotFoundException)
            {
                return false;
            }
        }

        /// <summary>批量解锁全部便签（关闭个人密码后调用；单个解密失败跳过，不中断其余）。</summary>
        public async Task UnlockAllSticky()
        {
            try
            {
                string UID = localSettings.Values["UID"].ToString();
                StorageFolder stickyFolder = await ApplicationData.Current.LocalFolder.GetFolderAsync(UID);
                stickyFolder = await stickyFolder.GetFolderAsync("Sticky");
                IReadOnlyList<StorageFile> fileList = await stickyFolder.GetFilesAsync();

                // 全部便签的读取-解密-解除锁定-写回 在后台线程执行
                await Task.Run(() =>
                {
                    foreach (StorageFile stickyFile in fileList)
                    {
                        try
                        {
                            string stickyText = DecryptStickyText(File.ReadAllText(stickyFile.Path));
                            Sticky sticky = JsonConvert.DeserializeObject<Sticky>(stickyText);
                            sticky.IsLock = false;
                            File.WriteAllText(stickyFile.Path, EncryptStickyText(JsonConvert.SerializeObject(sticky)));
                        }
                        catch (CryptographicException)
                        {
                            // 单个便签解密失败（密钥不匹配或数据损坏）时跳过，不中断其余便签
                        }
                    }
                });
            }
            catch (FileNotFoundException)
            {
            }
        }
    }
}
