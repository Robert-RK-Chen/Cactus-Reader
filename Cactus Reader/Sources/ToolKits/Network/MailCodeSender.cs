using System;
using System.Threading.Tasks;

namespace Cactus_Reader.Sources.ToolKits
{
    /// <summary>
    /// 验证码发送。
    /// 2026-08：SMTP 发送逻辑已移至 CactusReaderServer（MailService），
    /// 客户端不再携带 SMTP 凭据，仅请求服务端代为发送。
    /// </summary>
    public class MailCodeSender
    {
        private static MailCodeSender instance;

        public static MailCodeSender Instance
        {
            get
            {
                return instance ?? (instance = new MailCodeSender());
            }
        }

        private MailCodeSender() { }

        /// <summary>
        /// 请求服务端发送验证码。
        /// ok=false 时 reason：TOO_FREQUENT（1 分钟内重复发送）/ SEND_FAILED / INVALID_INPUT / NETWORK_ERROR。
        /// </summary>
        public async Task<(bool ok, string reason)> SendVerifyCodeAsync(string email, string codeType)
        {
            try
            {
                (bool ok, string error) = await ApiClient.SendCodeAsync(email, codeType);
                return (ok, error ?? "");
            }
            catch (Exception)
            {
                return (false, "NETWORK_ERROR");
            }
        }
    }
}
