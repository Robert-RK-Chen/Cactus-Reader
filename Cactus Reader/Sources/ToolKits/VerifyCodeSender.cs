using Cactus_Reader.Entities;
using MailKit.Net.Smtp;
using MimeKit;
using MimeKit.Text;
using System;

namespace Cactus_Reader.Sources.ToolKits
{
    public class VerifyCodeSender
    {
        readonly IFreeSql freeSql = IFreeSqlService.Instance;

        private static VerifyCodeSender instance;

        public static VerifyCodeSender Instance
        {
            get { return instance ?? (instance = new VerifyCodeSender()); }
        }

        private VerifyCodeSender() { }

        public bool SendVerifyCode(string email)
        {
            try
            {
                Code recentCode = freeSql.Select<Code>().Where(code => code.Email == email).ToOne();

                // 用于判断是否发送验证码
                if (recentCode is null || recentCode.CreateTime.AddMinutes(1) < DateTime.Now)
                {
                    // 随机数生成验证码
                    Random rand = new Random();
                    string verifyCode = Convert.ToString(rand.Next(1000000, 9999999));

                    // 更新验证码状态
                    recentCode = new Code
                    {
                        Email = email,
                        VerifyCode = verifyCode,
                        CreateTime = DateTime.Now
                    };
                    freeSql.InsertOrUpdate<Code>().SetSource(recentCode).ExecuteAffrows();

                    //发送验证码邮件
                    return SendEmail(email, verifyCode);
                }
                else
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool SendEmail(string email, string verifyCode)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("Cactus Reader 帐户团队", "cactus-noreply@robertch.cn"));
                message.To.Add(new MailboxAddress(null, email));
                message.Subject = "Cactus 帐户安全代码";

                string mailText = "<table><tbody><tr><td><p>Cactus Reader 帐户</p><p style=\"color: #05A6F0; font-weight: bold; font-size: 26px;\">安全代码</p><p>你正在使用该电子邮件地址注册或登录 Cactus 帐户，请在 5 分钟内使用以下安全代码进行验证。</p><p>安全代码：" + verifyCode + "</p><p>如果你并没有发出注册或登录 Cactus 帐户的请求，请忽略该电子邮件。</p><br/><p>谢谢！</p><p>Cactus Reader 帐户团队</p><br/><span style=\"color: #05A6F0; font-weight: bold; font-size: 26px;\">R.</span><span style=\"color: #FFBA08; font-weight: bold; font-size: 26px;\">C.</span><span style=\"color: #737373; font-weight: bold; font-size: 26px;\">Software Studio</span><p style=\"font-size: 14; \">该邮件由系统自动发出，因此请勿在该邮件上回复任何内容。</p></td></tr></tbody></table>";

                message.Body = new TextPart(TextFormat.Html) { Text = mailText };
                using (var client = new SmtpClient())
                {
                    client.Connect("smtp.office365.com", 587, false);
                    client.Authenticate("cactus-noreply@robertch.cn", "Robert@Cactus126");
                    client.Send(message);
                    client.Disconnect(true);
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
