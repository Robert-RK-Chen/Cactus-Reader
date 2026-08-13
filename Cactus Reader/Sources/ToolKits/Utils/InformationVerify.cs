using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Storage;

namespace Cactus_Reader.Sources.ToolKits
{
    public class InformationVerify
    {
        readonly static ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        private static InformationVerify instance;

        public static InformationVerify Instance
        {
            get
            {
                return instance ?? (instance = new InformationVerify());
            }
        }

        public bool IsEmail(string input)
        {
            string matchRule = @"^\w+([-+.]\w+)*@[\da-z\.-]+\.([a-z]{2,}|[\u2E80-\u9FFF]{2,3})$";
            return Regex.IsMatch(input, matchRule, RegexOptions.IgnoreCase);
        }

        public bool IsUserName(string input)
        {
            string matchRule = @"^[a-zA-Z0-9_ \u2E80-\u9FFF]{3,20}$";
            return Regex.IsMatch(input, matchRule, RegexOptions.IgnoreCase);
        }

        public bool IsPassword(string input)
        {
            string matchRule = @"(?=^.{8,}$)((?=.*\d)|(?=.*\W+))(?![.\n])(?=.*[A-Z])(?=.*[a-z]).*";
            return Regex.IsMatch(input, matchRule, RegexOptions.IgnoreCase);
        }

        /// <summary>邮箱是否可用（服务端查询，客户端不再直连数据库）。</summary>
        public async Task<bool> EmailEnabledAsync(string email)
        {
            try
            {
                return await ApiClient.CheckEmailAvailableAsync(email);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>用户名是否可用（服务端查询）。</summary>
        public async Task<bool> UserNameEnabledAsync(string userName)
        {
            try
            {
                return await ApiClient.CheckNameAvailableAsync(userName);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool CheckPassword(string password)
        {
            string storedHash = localSettings.Values["privateKey"] as string;
            return !string.IsNullOrEmpty(storedHash) && PasswordHashTool.Instance.VerifyPassword(password, storedHash);
        }
    }
}
