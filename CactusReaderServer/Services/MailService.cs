using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace CactusReaderService.Services
{
    /// <summary>
    /// 验证码邮件发送（Microsoft Graph API，应用注册 + 客户端凭据流）。
    /// 凭据（TenantId/ClientId/ClientSecret）仅存在于服务端 appsettings.json，客户端不再携带。
    ///
    /// Azure 应用注册要求：
    ///   - 应用程序权限（Application permission）：Mail.Send
    ///   - 发件邮箱为组织内邮箱（Exchange Online 许可）
    ///   - 若报 ErrorAccessDenied，需 Exchange Online 管理员执行：
    ///       New-ApplicationAccessPolicy -AppId "CLIENT_ID" -PolicyScopeGroupId <mail-enabled-group>
    ///       -AccessRight RestrictAccess -Description "Graph Mail Send"
    /// </summary>
    public class MailService
    {
        private readonly string _tenantId;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _fromAddress;
        private readonly GraphServiceClient _graphClient;

        public MailService(IConfiguration config)
        {
            var mail = config.GetSection("GraphMail");
            _tenantId = mail["TenantId"] ?? "";
            _clientId = mail["ClientId"] ?? "";
            _clientSecret = mail["ClientSecret"] ?? "";
            _fromAddress = mail["FromAddress"] ?? "";

            if (IsConfigured)
            {
                // 客户端凭据流：应用身份（非用户身份）访问 Graph，发件人是组织内邮箱
                var credential = new ClientSecretCredential(_tenantId, _clientId, _clientSecret);
                _graphClient = new GraphServiceClient(credential, new[] { "https://graph.microsoft.com/.default" });
            }
        }

        /// <summary>
        /// Graph 凭据是否已配置：需为真实 GUID / 域名租户，且不含占位符（YOUR_*）。
        /// 未配置时发信直接失败，避免误报成功，也避免应用因无效凭据启动崩溃。
        /// </summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_tenantId) &&
            !string.IsNullOrWhiteSpace(_clientId) &&
            !string.IsNullOrWhiteSpace(_clientSecret) &&
            !string.IsNullOrWhiteSpace(_fromAddress) &&
            !_tenantId.Contains("YOUR_") &&
            !_clientId.Contains("YOUR_") &&
            !_clientSecret.Contains("YOUR_") &&
            !_fromAddress.Contains("YOUR_") &&
            !_fromAddress.Contains("example.com");

        /// <summary>发送验证码邮件（Graph SendMail，202 Accepted 即成功）。</summary>
        public async Task<bool> SendVerifyCodeMailAsync(string toEmail, string verifyCode)
        {
            if (!IsConfigured) return false;
            try
            {
                var message = new Message
                {
                    Subject = "catom 帐户安全代码",
                    Body = new ItemBody
                    {
                        ContentType = BodyType.Html,
                        Content = BuildMailHtml(verifyCode)
                    },
                    ToRecipients = new List<Recipient>
                    {
                        new Recipient
                        {
                            EmailAddress = new EmailAddress { Address = toEmail }
                        }
                    }
                };

                // 应用权限：发件人为组织内指定邮箱
                await _graphClient.Users[_fromAddress]
                    .SendMail
                    .PostAsync(new Microsoft.Graph.Users.Item.SendMail.SendMailPostRequestBody
                    {
                        Message = message,
                        SaveToSentItems = true
                    });
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>组装验证码邮件 HTML 正文。</summary>
        private static string BuildMailHtml(string verifyCode)
        {
            return
                "<table><tbody><tr><td><p>catom 帐户</p>" +
                "<p style=\"color: #05A6F0; font-weight: bold; font-size: 26px;\">安全代码</p>" +
                "<p>你正在使用该电子邮件地址访问你的 catom 帐户，请在 5 分钟内使用以下安全代码进行验证。</p>" +
                "<p>安全代码：" + verifyCode + "</p>" +
                "<p>如果你并没有发出注册、登录或修改 catom 帐户的请求，请忽略该电子邮件。</p><br/>" +
                "<p>谢谢！</p><p>catom technology 团队</p><br/>" +
                "<span style=\"color: #0088ff; font-weight: bold; font-size: 26px;\">c</span>" +
                "<span style=\"color: #000000; font-weight: bold; font-size: 26px;\">atom </span>" +
                "<span style=\"color: #737373; font-weight: bold; font-size: 26px;\">technology</span>" +
                "<p style=\"font-size: 14; \">该邮件由系统自动发出，因此请勿在该邮件上回复任何内容。</p>" +
                "</td></tr></tbody></table>";
        }
    }
}
