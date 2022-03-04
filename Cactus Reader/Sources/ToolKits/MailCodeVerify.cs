using Cactus_Reader.Entities;
using System;

namespace Cactus_Reader.Sources.ToolKits
{
    public static class MailCodeVerify
    {
        public static string Verify(string codeInput, Code mailCode)
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
    }
}
