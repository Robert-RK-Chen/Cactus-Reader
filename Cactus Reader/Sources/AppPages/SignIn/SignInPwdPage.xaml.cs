using Cactus_Reader.Entities;
using Cactus_Reader.Sources.ToolKits;
using Cactus_Reader.Sources.WindowsHello;
using System;
using System.Threading.Tasks;
using Windows.Storage;
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
    public sealed partial class SignInPwdPage : Page
    {
        private ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        private readonly ProfileSyncTool syncTool = ProfileSyncTool.Instance;
        private readonly MailCodeSender codeSender = MailCodeSender.Instance;

        User currentUser = null;

        public SignInPwdPage()
        {
            InitializeComponent();
        }

        // 用于接受其他页面过渡到这一页时传入的用户信息
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            currentUser = (User)e.Parameter;
            if (null != currentUser)
            {
                userMailBlock.Text = currentUser.Email;
            }
        }

        private void BackPrevPage(object sender, RoutedEventArgs e)
        {
            contentFrame.Navigate(typeof(SignInAccountPage), currentUser, new SlideNavigationTransitionInfo()
            {
                Effect = SlideNavigationTransitionEffect.FromLeft
            });
        }

        private void ShowMoreLoginWays(object sender, RoutedEventArgs e)
        {
            if (loginWays.Visibility == Visibility.Collapsed)
            {
                loginWays.Visibility = Visibility.Visible;
            }
            else
            {
                loginWays.Visibility = Visibility.Collapsed;
            }
        }

        // 使用邮件验证码登录
        private async void SendLoginCode(object sender, RoutedEventArgs e)
        {
            try
            {
                // 等待服务端发送结果，成功才进入验证码页
                ControllerVisibility.ShowProgressBar(statusBar);
                (bool ok, string reason) = await codeSender.SendVerifyCodeAsync(currentUser.Email, "signin");
                ControllerVisibility.HideProgressBar(statusBar);

                if (ok)
                {
                    contentFrame.Navigate(typeof(SignInCodePage), currentUser, new SlideNavigationTransitionInfo()
                    {
                        Effect = SlideNavigationTransitionEffect.FromRight
                    });
                }
                else if (reason == "TOO_FREQUENT")
                {
                    alertMsg.Text = "验证码发送过于频繁，请稍后再试。";
                }
                else
                {
                    alertMsg.Text = "验证码发送失败，请检查邮箱地址或稍后再试。";
                }
            }
            catch (Exception)
            {
                alertMsg.Text = "未连接，请检查网络开关是否已打开。";
            }
            alertMsg.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 用户使用 Windows Hello 登录，这一登录过程如下：
        /// 首先判断用户设备是否能使用 Windows Hello，然后调用 Windows Hello
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void WindowsHelloSignIn(object sender, RoutedEventArgs e)
        {
            object oCurrentUID = localSettings.Values["email"];
            bool isTPMEnabled = await MicrosoftPassportHelper.MicrosoftPassportAvailableCheckAsync();

            if (isTPMEnabled)
            {
                try
                {
                    if (null == oCurrentUID || !string.Equals(currentUser.Email, oCurrentUID.ToString()))
                    {
                        alertMsg.Text = "若要使用 Windows Hello，请重新登录。";
                    }
                    else
                    {
                        ControllerVisibility.ShowProgressBar(statusBar);
                        // 真实验证流程：打开已注册密钥 → PIN/生物识别确认 → 服务端挑战签名校验
                        bool isSuccessful = await MicrosoftPassportHelper.GetPassportAuthenticationMessageAsync(currentUser);

                        if (isSuccessful)
                        {
                            syncTool.LoadCurrentUser(currentUser);
                            StartPage.startPage.mainContent.Navigate(typeof(MainPage), null,
                                new DrillInNavigationTransitionInfo());
                        }
                        else
                        {
                            alertMsg.Text = "Windows Hello 验证失败，请再试一次。";
                        }
                    }
                }
                catch (Exception)
                {
                    alertMsg.Text = "未连接，请检查网络开关是否已打开。";
                }
            }
            else
            {
                alertMsg.Text = "TPM 安全处理器未打开，或未设置 Windows Hello PIN。";
            }
            ControllerVisibility.HideProgressBar(statusBar);
            alertMsg.Visibility = Visibility.Visible;
        }

        private async void SendResetCode(object sender, RoutedEventArgs e)
        {
            try
            {
                ControllerVisibility.ShowProgressBar(statusBar);
                (bool ok, string reason) = await codeSender.SendVerifyCodeAsync(currentUser.Email, "reset");
                ControllerVisibility.HideProgressBar(statusBar);

                if (ok)
                {
                    contentFrame.Navigate(typeof(ForgetPassword), currentUser, new SlideNavigationTransitionInfo()
                    {
                        Effect = SlideNavigationTransitionEffect.FromRight
                    });
                }
                else if (reason == "TOO_FREQUENT")
                {
                    alertMsg.Text = "验证码发送过于频繁，请稍后再试。";
                }
                else
                {
                    alertMsg.Text = "验证码发送失败，请检查邮箱地址或稍后再试。";
                }
            }
            catch (Exception)
            {
                alertMsg.Text = "未连接，请检查网络开关是否已打开。";
            }
            alertMsg.Visibility = Visibility.Visible;
        }

        private async void SignIn(object sender, RoutedEventArgs e)
        {
            try
            {
                if (userPwdInput.Password.Length == 0)
                {
                    alertMsg.Text = "请在此输入你的帐户密码。";
                }
                else
                {
                    // 密码校验由服务端完成（带盐哈希），客户端不持有哈希
                    ControllerVisibility.ShowProgressBar(statusBar);
                    bool isValid = await ApiClient.VerifyPasswordAsync(currentUser.UID, userPwdInput.Password);
                    ControllerVisibility.HideProgressBar(statusBar);

                    if (isValid)
                    {
                        syncTool.LoadCurrentUser(currentUser);
                        StartPage.startPage.mainContent.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
                    }
                    else
                    {
                        alertMsg.Text = "Cactus 帐户或密码不正确。";
                    }
                }
            }
            catch(Exception)
            {
                alertMsg.Text = "未连接，请检查网络开关是否已打开。";
            }
            alertMsg.Visibility = Visibility.Visible;
        }

        private void ClearAlertMsg(object sender, RoutedEventArgs e)
        {
            alertMsg.Visibility = Visibility.Collapsed;
        }
    }
}
