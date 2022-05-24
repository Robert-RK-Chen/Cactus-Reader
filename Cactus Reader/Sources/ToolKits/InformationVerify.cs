using Cactus_Reader.Entities;
using System;
using System.Text.RegularExpressions;
using Windows.Storage;

namespace Cactus_Reader.Sources.ToolKits
{
    public class InformationVerify
    {

        readonly static IFreeSql freeSql = IFreeSqlService.Instance;
        readonly static ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        private static InformationVerify instance;
        private readonly MD5EncryptTool md5EncryptTool = new MD5EncryptTool();

        public static InformationVerify Instance
        {
            get
            {
                return instance ?? (instance = new InformationVerify());
            }
        }

        public bool IsEmail(string input)
        {
            string matchRule = @"^\w+@[\da-z\.-]+\.([a-z]{2,}|[\u2E80-\u9FFF]{2,3})$";
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

        public string MailCodeVerify(string codeInput, Code mailCode)
        {
            if (codeInput.Length == 0)
            {
                return "CODE_INPUT_LENGTH_0";
            }
            if (codeInput != mailCode.VerifyCode)
            {
                return "INVALID_MAIL_CODE";
            }
            if (mailCode.CreateTime.AddMinutes(5) < DateTime.Now)
            {
                return "INVALID_MAIL_CODE";
            }
            return "VALID_CODE";
        }

        public bool UserNameEnabled(string userName)
        {
            return freeSql.Select<User>().Where(user => user.Name == userName).ToOne() is null;
        }

        public bool EmailEnabled(string email)
        {
            return freeSql.Select<User>().Where(user => user.Email == email).ToOne() is null;
        }

        public bool CheckPassword(string password)
        {
            byte[] privateKey = (byte[])localSettings.Values["privateKey"];
            byte[] calibrateKey = md5EncryptTool.GetUserEncryptedPassword(password);
            return BitConverter.ToString(privateKey) == BitConverter.ToString(calibrateKey);
        }
    }
}
