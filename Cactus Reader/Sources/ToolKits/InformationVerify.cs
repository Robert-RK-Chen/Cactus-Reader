using System.Text.RegularExpressions;

namespace Cactus_Reader.Sources.ToolKits
{
    public class InformationVerify
    {
        public static bool IsEmail(string input)
        {
            string matchRule = @"\A(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)\Z";

            return Regex.IsMatch(input, matchRule, RegexOptions.IgnoreCase);
        }

        public static bool IsUserName(string input)
        {
            string matchRule = @"^\S[a-zA-Z\s\d]+\S";
            return Regex.IsMatch(input, matchRule, RegexOptions.IgnoreCase);
        }

        public static bool IsPassword(string input)
        {
            string matchRule = @"(?=^.{8,}$)((?=.*\d)|(?=.*\W+))(?![.\n])(?=.*[A-Z])(?=.*[a-z]).*";
            return Regex.IsMatch(input, matchRule, RegexOptions.IgnoreCase);
        }
    }
}
