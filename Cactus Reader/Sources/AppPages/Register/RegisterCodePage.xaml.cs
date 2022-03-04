using Cactus_Reader.Entities;
using Cactus_Reader.Sources.AppPages.Login;
using Cactus_Reader.Sources.ToolKits;
using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Cactus_Reader.Sources.AppPages.Register
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class RegisterCodePage : Page
    {
        readonly IFreeSql freeSql = IFreeSqlService.Instance;
        readonly MailCodeSender codeSender = MailCodeSender.Instance;
        User currentUser = null;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            currentUser = (User)e.Parameter;
            if (null != currentUser)
            {
                userMailBlock.Text = currentUser.Email;
                userMail.Text = currentUser.Email + "，请输入邮件中的代码进行注册。";
            }
        }

        public RegisterCodePage()
        {
            InitializeComponent();
        }

        private void BackPrevPage(object sender, RoutedEventArgs e)
        {
            contentFrame.Navigate(typeof(RegisterMailPage), currentUser, new SlideNavigationTransitionInfo()
            {
                Effect = SlideNavigationTransitionEffect.FromLeft
            });
        }

        private void ContinueRegister(object sender, RoutedEventArgs e)
        {
            string codeInput = verifyCodeInput.Text;
            try
            {
                Code currentCode = freeSql.Select<Code>().Where(code => code.Email == currentUser.Email).ToOne();
                switch (MailCodeVerify.Verify(codeInput, currentCode))
                {
                    case "CODE_INPUT_LENGTH_0":
                        alertMsg.Text = "若要继续，请输入我们刚才发送给你的代码。";
                        break;
                    case "INVALID_MAIL_CODE":
                        alertMsg.Text = "该代码无效，检查该代码并重试。";
                        break;
                    case "VALID_CODE":
                        contentFrame.Navigate(typeof(RegisterUserInfoPage), currentUser, new SlideNavigationTransitionInfo()
                        {
                            Effect = SlideNavigationTransitionEffect.FromRight
                        });
                        break;
                    default:
                        alertMsg.Text = "该代码无效，检查该代码并重试。";
                        break;

                }
                alertMsg.Visibility = Visibility.Visible;
            }
            catch (Exception)
            {
                alertMsg.Text = "未连接，请检查网络开关是否已打开。";
                alertMsg.Visibility = Visibility.Visible;
            }
        }

        private void ClearAlertMsg(object sender, RoutedEventArgs e)
        {
            alertMsg.Visibility = Visibility.Collapsed;
        }

        private void ResendVerifyCode(object sender, RoutedEventArgs e)
        {
            bool sendFlag = codeSender.SendVerifyCode(currentUser.Email, "register");
            if (sendFlag)
            {
                alertMsg.Text = "代码已发送，请注意查收。";
            }
            else
            {
                alertMsg.Text = "代码发送过于频繁，请稍后再试。";
            }
            alertMsg.Visibility = Visibility.Visible;
        }
    }
}