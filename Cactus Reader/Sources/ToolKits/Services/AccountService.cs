using Cactus_Reader.Entities;
using Cactus_Reader.Sources.WindowsHello;
using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace Cactus_Reader.Sources.ToolKits
{
    /// <summary>
    /// 账户原子操作：登录 / 注册 / 验证码 / 临时用户 / 登出 的可复用单元。
    /// 页面事件处理方法只做输入校验与 UI 反馈，业务逻辑统一收敛到此服务，
    /// 避免同一逻辑在多页重复实现。
    /// </summary>
    public static class AccountService
    {
        private static readonly ProfileSyncTool syncTool = ProfileSyncTool.Instance;
        private static readonly InformationVerify informationVerify = InformationVerify.Instance;
        private static readonly ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

        // ---------------- 验证码 ----------------

        /// <summary>发送验证码（codeType: signin / signup / reset）。ok=false 时 reason 为 TOO_FREQUENT / SEND_FAILED。</summary>
        public static async Task<(bool ok, string reason)> SendVerifyCodeAsync(string email, string codeType)
        {
            return await MailCodeSender.Instance.SendVerifyCodeAsync(email, codeType);
        }

        /// <summary>校验验证码（服务端校验即删，防重放）。</summary>
        public static Task<bool> VerifyCodeAsync(string email, string codeType, string code)
        {
            return ApiClient.VerifyCodeAsync(email, codeType, code);
        }

        // ---------------- 输入校验 ----------------

        /// <summary>邮箱格式 + 可用性校验，返回错误消息（空字符串表示通过）。</summary>
        public static async Task<string> CheckEmailAsync(string email)
        {
            if (!informationVerify.IsEmail(email))
            {
                return "请输入一个有效的电子邮件地址。";
            }
            if (!await informationVerify.EmailEnabledAsync(email))
            {
                return "电子邮件地址已被注册，请尝试使用其他电子邮件。";
            }
            return "";
        }

        /// <summary>用户名校验，返回错误消息（空字符串表示通过）。</summary>
        public static async Task<string> CheckUserNameAsync(string name)
        {
            if (name.Length == 0)
            {
                return "若要继续，请输入一个用户名";
            }
            if (!informationVerify.IsUserName(name))
            {
                return "无效的用户名，有效的用户名仅由非空格起始或结尾的字母、数字与空格组成";
            }
            if (!await informationVerify.UserNameEnabledAsync(name))
            {
                return "用户名称已被注册，请换一个尝试。";
            }
            return "";
        }

        /// <summary>密码强度校验（长度至少 8 位，含大小写字母、数字或符号）。</summary>
        public static bool IsPasswordValid(string password)
        {
            return informationVerify.IsPassword(password);
        }

        // ---------------- 登录 ----------------

        /// <summary>按邮箱查询用户（网络异常抛出）。</summary>
        public static Task<User> GetUserByEmailAsync(string email)
        {
            return ApiClient.GetUserByEmailAsync(email);
        }

        /// <summary>校验密码（服务端带盐哈希比对）。</summary>
        public static Task<bool> VerifyPasswordAsync(string uid, string password)
        {
            return ApiClient.VerifyPasswordAsync(uid, password);
        }

        /// <summary>登录成功统一入口：写入本地会话（isLogin/UID/email/name/mobile/renewDate）。</summary>
        public static void CompleteLogin(User user)
        {
            syncTool.LoadCurrentUser(user);
        }

        /// <summary>
        /// Windows Hello 登录：设备可用性检查 → 当前会话匹配检查 → 密钥签名挑战。
        /// 成功时自动写入本地会话；返回 (是否成功, 失败原因)。
        /// </summary>
        public static async Task<(bool ok, string message)> SignInWithWindowsHelloAsync(User user)
        {
            bool isTPMEnabled = await MicrosoftPassportHelper.MicrosoftPassportAvailableCheckAsync();
            if (!isTPMEnabled)
            {
                return (false, "TPM 安全处理器未打开，或未设置 Windows Hello PIN。");
            }

            object oCurrentUID = localSettings.Values["email"];
            if (null == oCurrentUID || !string.Equals(user.Email, oCurrentUID.ToString()))
            {
                return (false, "若要使用 Windows Hello，请重新登录。");
            }

            bool isSuccessful = await MicrosoftPassportHelper.GetPassportAuthenticationMessageAsync(user);
            if (!isSuccessful)
            {
                return (false, "Windows Hello 验证失败，请再试一次。");
            }

            syncTool.LoadCurrentUser(user);
            return (true, "");
        }

        /// <summary>跳过登录：创建临时用户（仅本机有限功能）。</summary>
        public static void SkipLogin()
        {
            // isLogin 必须存 bool（与 LoadCurrentUser 一致）：App.xaml.cs 用 `is true` 判断，
            // 存 string "true" 会导致重启后跳过登录状态不被识别
            localSettings.Values["isLogin"] = true;
            localSettings.Values["UID"] = "Temp User";
            localSettings.Values["email"] = "你将使用 Cactus Reader 的有限功能";
            localSettings.Values["name"] = "未登录用户";
        }

        /// <summary>退出登录：清除本地会话标记。</summary>
        public static void SignOut()
        {
            localSettings.Values["isLogin"] = false;
        }

        // ---------------- 注册 / 重置密码 ----------------

        /// <summary>完成注册：生成 UID / 注册时间并调用服务端，返回是否成功。</summary>
        public static async Task<bool> CompleteSignUpAsync(User user, string password)
        {
            // UID 在客户端生成，保证页面间传参一致；密码哈希由服务端生成（带盐）
            user.UID = Guid.NewGuid().ToString("D").ToUpper();
            user.RegistDate = DateTime.Now;
            user.Mobile = null;

            (bool ok, _, _) = await ApiClient.SignUpAsync(
                user.Email, user.Name, password, user.Mobile, user.UID);
            return ok;
        }

        /// <summary>重置密码（服务端生成带盐哈希）。</summary>
        public static Task<bool> ResetPasswordAsync(string uid, string password)
        {
            return ApiClient.ResetPasswordAsync(uid, password);
        }
    }
}
