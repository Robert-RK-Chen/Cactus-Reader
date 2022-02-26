using System.Text.RegularExpressions;

namespace Cactus_Reader.Sources.ToolKits
{
    internal class InformationVerify
    {
        public static bool IsEmail(string input)
        {
            return Regex.IsMatch(input, @"\A(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)\Z", RegexOptions.IgnoreCase);
        }

        public static bool IsUserName(string input)
        {
            return Regex.IsMatch(input, @"^\S[a-zA-Z\s\d]+\S", RegexOptions.IgnoreCase);
        }

        public static bool IsPassword(string input)
        {
            return Regex.IsMatch(input, @"(?=^.{8,}$)((?=.*\d)|(?=.*\W+))(?![.\n])(?=.*[A-Z])(?=.*[a-z]).*", RegexOptions.IgnoreCase);
        }
    }
}
