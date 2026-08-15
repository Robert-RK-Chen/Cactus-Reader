using Cactus_Reader.Entities;
using Cactus_Reader.Sources.ToolKits;
using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Cactus_Reader.Sources.AppPages.SignIn
{
    public sealed partial class SignInPwdPage : Page
    {
        User currentUser = null;

        public SignInPwdPage()
        {
            InitializeComponent();
        }

        // 接收上页传入的用户信息
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
                (bool ok, string reason) = await AccountService.SendVerifyCodeAsync(currentUser.Email, "signin");
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

        /// <summary>Windows Hello 登录：设备可用性检查 → 密钥签名挑战 → 服务端验证。</summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void WindowsHelloSignIn(object sender, RoutedEventArgs e)
        {
            try
            {
                ControllerVisibility.ShowProgressBar(statusBar);
                // 真实验证流程：打开已注册密钥 → PIN/生物识别确认 → 服务端挑战签名校验
                (bool ok, string message) = await AccountService.SignInWithWindowsHelloAsync(currentUser);
                ControllerVisibility.HideProgressBar(statusBar);

                if (ok)
                {
                    StartPage.startPage.mainContent.Navigate(typeof(MainPage), null,
                        new DrillInNavigationTransitionInfo());
                }
                else
                {
                    alertMsg.Text = message;
                }
            }
            catch (Exception)
            {
                alertMsg.Text = "未连接，请检查网络开关是否已打开。";
            }
            alertMsg.Visibility = Visibility.Visible;
        }

        private async void SendResetCode(object sender, RoutedEventArgs e)
        {
            try
            {
                ControllerVisibility.ShowProgressBar(statusBar);
                (bool ok, string reason) = await AccountService.SendVerifyCodeAsync(currentUser.Email, "reset");
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
                    bool isValid = await AccountService.VerifyPasswordAsync(currentUser.UID, userPwdInput.Password);
                    ControllerVisibility.HideProgressBar(statusBar);

                    if (isValid)
                    {
                        AccountService.CompleteLogin(currentUser);
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
