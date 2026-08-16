using System;
using System.Threading.Tasks;
using Windows.Security.Credentials;
using Windows.Storage;
using Windows.UI.Xaml;

namespace Cactus_Reader.Sources.ToolKits
{
    /// <summary>
    /// 应用设置原子操作：主题 / 字体 / 字号 / 语音 / 同步开关 / 个人密码 的可复用单元。
    /// 集中读写 LocalSettings，页面不再各自散落初始化与取值逻辑。
    /// </summary>
    public static class SettingsService
    {
        private static readonly ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        private static readonly EncryptStickyTool encryptStickyTool = EncryptStickyTool.Instance;
        private static readonly InformationVerify informationVerify = InformationVerify.Instance;

        /// <summary>补全缺失的默认设置项（幂等，可重复调用）。</summary>
        public static void EnsureDefaultSettings()
        {
            SetIfMissing("appThemeIndex", 2);
            SetIfMissing("font", "宋体");
            SetIfMissing("fontSize", 15.0);
            SetIfMissing("voiceIndex", 0);
            SetIfMissing("voiceName", "冰糖");
            SetIfMissing("voiceLang", "Chinese");
            SetIfMissing("styleIndex", 0);
            SetIfMissing("styleName", "");
            SetIfMissing("speed", 1.0);
            SetIfMissing("tune", 1.0);
            SetIfMissing("alreadySetWindowsHello", false);
            SetIfMissing("syncEnabled", true);
        }

        private static void SetIfMissing(string key, object value)
        {
            if (localSettings.Values[key] == null)
            {
                localSettings.Values[key] = value;
            }
        }

        // ---------------- 主题 ----------------

        public static int GetAppThemeIndex()
        {
            return (int)localSettings.Values["appThemeIndex"];
        }

        /// <summary>应用主题：写入设置并切换窗口 RequestedTheme。</summary>
        public static void ApplyAppTheme(int appThemeIndex)
        {
            localSettings.Values["appThemeIndex"] = appThemeIndex;
            ElementTheme theme = appThemeIndex switch
            {
                0 => ElementTheme.Light,
                1 => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };
            (Window.Current.Content as FrameworkElement).RequestedTheme = theme;
        }

        // ---------------- 字体 / 字号 ----------------

        public static string GetAppFont()
        {
            return localSettings.Values["font"].ToString();
        }

        public static void SetAppFont(string font)
        {
            localSettings.Values["font"] = font;
        }

        public static double GetFontSize()
        {
            // 历史版本可能存过 Int32（ChangeFontSize 旧实现），直接 (double) 强转会抛 InvalidCastException
            return Convert.ToDouble(localSettings.Values["fontSize"]);
        }

        public static void SetFontSize(double size)
        {
            localSettings.Values["fontSize"] = size;
        }

        /// <summary>调整字号（12~30，步进 1），返回调整后的值。</summary>
        public static int ChangeFontSize(int delta)
        {
            int current = int.Parse(localSettings.Values["fontSize"].ToString());
            int next = Math.Max(12, Math.Min(30, current + delta));
            // 统一以 double 存储，避免后续读取 (double) 强转失败
            localSettings.Values["fontSize"] = (double)next;
            return next;
        }

        // ---------------- 语音 ----------------

        public static int GetVoiceIndex()
        {
            return (int)localSettings.Values["voiceIndex"];
        }

        public static string GetVoiceName()
        {
            object value = localSettings.Values["voiceName"];
            return value == null ? "冰糖" : value.ToString();
        }

        public static string GetVoiceLang()
        {
            object value = localSettings.Values["voiceLang"];
            return value == null ? "Chinese" : value.ToString();
        }

        public static int GetStyleIndex()
        {
            object value = localSettings.Values["styleIndex"];
            return value == null ? 0 : (int)value;
        }

        public static string GetStyleName()
        {
            object value = localSettings.Values["styleName"];
            return value == null ? "" : value.ToString();
        }

        public static double GetVoiceSpeed()
        {
            return Convert.ToDouble(localSettings.Values["speed"]);
        }

        public static double GetVoiceTune()
        {
            return Convert.ToDouble(localSettings.Values["tune"]);
        }

        /// <summary>按下拉索引选择讲述人，同步 voiceName / voiceLang（MiMo 预置音色）。</summary>
        public static void SetSpeechVoice(int index)
        {
            localSettings.Values["voiceIndex"] = index;
            (string voiceName, string voiceLang) = index switch
            {
                0 => ("冰糖", "Chinese"),
                1 => ("茉莉", "Chinese"),
                2 => ("苏打", "Chinese"),
                3 => ("白桦", "Chinese"),
                4 => ("Mia", "English"),
                5 => ("Chloe", "English"),
                6 => ("Milo", "English"),
                7 => ("Dean", "English"),
                _ => ("冰糖", "Chinese"),
            };
            localSettings.Values["voiceName"] = voiceName;
            localSettings.Values["voiceLang"] = voiceLang;
        }

        /// <summary>按下拉索引选择讲述人风格，同步 styleName（用于 (风格) 标签）。</summary>
        public static void SetSpeechStyle(int index)
        {
            localSettings.Values["styleIndex"] = index;
            string styleName = index switch
            {
                0 => "",
                1 => "温柔",
                2 => "高冷",
                3 => "活泼",
                4 => "严肃",
                5 => "慵懒",
                6 => "俏皮",
                7 => "深沉",
                8 => "干练",
                9 => "凌厉",
                10 => "开心",
                11 => "悲伤",
                12 => "平静",
                13 => "磁性",
                14 => "清亮",
                15 => "甜美",
                16 => "沙哑",
                17 => "御姐音",
                18 => "正太音",
                19 => "大叔音",
                _ => "",
            };
            localSettings.Values["styleName"] = styleName;
        }

        public static void SetSpeechSpeed(double value)
        {
            localSettings.Values["speed"] = value;
        }

        public static void SetSpeechTune(double value)
        {
            localSettings.Values["tune"] = value;
        }

        // ---------------- MiMo API Key（Windows 凭据保险箱） ----------------

        private const string VaultResource = "CactusReader";
        private const string VaultUserName = "MiMoApiKey";

        /// <summary>读取 MiMo API Key；未设置时返回 null。</summary>
        public static string GetMimoApiKey()
        {
            try
            {
                var vault = new PasswordVault();
                PasswordCredential credential = vault.Retrieve(VaultResource, VaultUserName);
                return credential?.Password;
            }
            catch (Exception)
            {
                // 凭据不存在或读取失败均视为未设置
                return null;
            }
        }

        /// <summary>保存或更新 MiMo API Key（空字符串/ null 表示清除）。</summary>
        public static void SetMimoApiKey(string apiKey)
        {
            var vault = new PasswordVault();
            try
            {
                PasswordCredential existing = vault.Retrieve(VaultResource, VaultUserName);
                if (existing != null)
                {
                    vault.Remove(existing);
                }
            }
            catch (Exception)
            {
                // 无既有凭据，忽略
            }

            if (!string.IsNullOrEmpty(apiKey))
            {
                vault.Add(new PasswordCredential(VaultResource, VaultUserName, apiKey));
            }
        }

        // ---------------- 跨设备同步 ----------------

        public static bool GetSyncEnabled()
        {
            object value = localSettings.Values["syncEnabled"];
            if (value == null)
            {
                localSettings.Values["syncEnabled"] = true;
                return true;
            }
            return (bool)value;
        }

        /// <summary>切换同步开关；重新开启时全量上传本地内容覆盖云端（replace_cloud）。</summary>
        public static async Task SetSyncEnabledAsync(bool enabled)
        {
            localSettings.Values["syncEnabled"] = enabled;
            if (enabled)
            {
                string UID = localSettings.Values["UID"].ToString();
                await ProfileSyncTool.Instance.SyncAllLocalContent(UID);
            }
        }

        // ---------------- 个人密码（便签本密钥） ----------------

        public static bool IsPrivateKeySet()
        {
            return localSettings.Values.Keys.Contains("privateKey");
        }

        public static bool VerifyPrivateKey(string password)
        {
            return informationVerify.CheckPassword(password);
        }

        /// <summary>设置个人密码：PBKDF2 加盐哈希存储，并用密码包裹便签密钥上传服务端。</summary>
        /// <returns>密钥云同步是否成功（换设备时可凭密码找回）。</returns>
        public static async Task<bool> SetPrivateKeyAsync(string password)
        {
            localSettings.Values["privateKey"] = PasswordHashTool.Instance.HashPassword(password);
            return await encryptStickyTool.SetupVaultAsync(password);
        }

        /// <summary>关闭个人密码：验证密码后删除服务端包裹密钥并清除本机设置。</summary>
        /// <returns>vaultRemoved=false 表示云端密钥删除失败（本机仍可正常使用）。</returns>
        public static async Task<bool> ClosePrivateKeyAsync(string password)
        {
            if (!VerifyPrivateKey(password))
            {
                return false;
            }
            bool vaultRemoved = await encryptStickyTool.RemoveVaultAsync();
            localSettings.Values.Remove("privateKey");
            localSettings.Values["alreadySetWindowsHello"] = false;
            // 批量解锁全部便签（UnlockAllSticky 内部已在后台线程执行文件 IO）
            await encryptStickyTool.UnlockAllSticky();
            // 关闭密码后降级为无密码模式：重新备份明文密钥，保证换设备仍可免密恢复便签
            await encryptStickyTool.BackupPlainKeyIfNeededAsync();
            return vaultRemoved;
        }

        // ---------------- Windows Hello 开关 ----------------

        public static bool IsWindowsHelloSet()
        {
            // 键可能从未初始化（用户未打开过设置页），缺失视为未设置，避免 (bool)null 抛异常
            object value = localSettings.Values["alreadySetWindowsHello"];
            return value is bool b && b;
        }

        public static void SetWindowsHello(bool value)
        {
            localSettings.Values["alreadySetWindowsHello"] = value;
        }
    }
}
