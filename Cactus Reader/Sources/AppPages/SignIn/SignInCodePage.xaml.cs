using Cactus_Reader.Entities;
using Cactus_Reader.Sources.ToolKits;
using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Cactus_Reader.Sources.AppPages.SignIn
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class SignInCodePage : Page
    {
        private readonly ProfileSyncTool syncTool = ProfileSyncTool.Instance;
        private readonly MailCodeSender codeSender = MailCodeSender.Instance;
        User currentUser = null;

        public SignInCodePage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            currentUser = (User)e.Parameter;
            if (null != currentUser)
            {
                userMailBlock.Text = currentUser.Email;
                userMail.Text = currentUser.Email + "，请输入邮件中的代码进行登录。";
            }
        }

        private void BackPrevPage(object sender, RoutedEventArgs e)
        {
            contentFrame.Navigate(typeof(SignInPwdPage), currentUser, new SlideNavigationTransitionInfo()
            {
                Effect = SlideNavigationTransitionEffect.FromLeft
            });
        }

        private async void SignIn(object sender, RoutedEventArgs e)
        {
            string codeInput = verifyCodeInput.Text;
            try
            {
                ControllerVisibility.ShowProgressBar(statusBar);
                if (codeInput.Length == 0)
                {
                    alertMsg.Text = "若要继续，请输入我们刚才发送给你的代码。";
                }
                else
                {
                    // 服务端校验（校验即删，防重放）
                    bool isValid = await ApiClient.VerifyCodeAsync(currentUser.Email, "signin", codeInput);
                    if (isValid)
                    {
                        syncTool.LoadCurrentUser(currentUser);
                        StartPage.startPage.mainContent.Navigate(typeof(MainPage), null,
                            new DrillInNavigationTransitionInfo());
                    }
                    else
                    {
                        alertMsg.Text = "该代码无效，检查该代码并重试。";
                    }
                }
            }
            catch (Exception)
            {
                alertMsg.Text = "未连接，请检查网络开关是否已打开。";
            }

            ControllerVisibility.HideProgressBar(statusBar);
            alertMsg.Visibility = Visibility.Visible;
        }

        private void ClearAlertMsg(object sender, RoutedEventArgs e)
        {
            alertMsg.Visibility = Visibility.Collapsed;
        }

        private async void ResendVerifyCode(object sender, RoutedEventArgs e)
        {
            ControllerVisibility.ShowProgressBar(statusBar);
            (bool ok, string reason) = await codeSender.SendVerifyCodeAsync(currentUser.Email, "signin");
            ControllerVisibility.HideProgressBar(statusBar);

            if (ok)
            {
                alertMsg.Text = "代码已发送，请注意查收。";
            }
            else
            {
                alertMsg.Text = reason == "TOO_FREQUENT"
                    ? "代码发送过于频繁，请稍后再试。"
                    : "代码发送失败，请稍后再试。";
            }
            alertMsg.Visibility = Visibility.Visible;
        }
    }
}
