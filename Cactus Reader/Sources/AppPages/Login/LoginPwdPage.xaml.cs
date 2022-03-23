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

namespace Cactus_Reader.Sources.AppPages.Login
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class LoginPwdPage : Page
    {
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        readonly ProfileSyncTool syncTool = ProfileSyncTool.Instance;
        readonly MailCodeSender codeSender = MailCodeSender.Instance;
        User currentUser = null;

        public LoginPwdPage()
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
            contentFrame.Navigate(typeof(LoginAccountPage), currentUser, new SlideNavigationTransitionInfo()
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
        private void SendLoginCode(object sender, RoutedEventArgs e)
        {
            try
            {
                // 多线程发送验证码防止应用卡顿
                Task.Factory.StartNew(() =>
                {
                    codeSender.SendVerifyCode(currentUser.Email, "login");
                });

                contentFrame.Navigate(typeof(LoginCodePage), currentUser, new SlideNavigationTransitionInfo()
                {
                    Effect = SlideNavigationTransitionEffect.FromRight
                });
            }
            catch (Exception)
            {
                alertMsg.Text = "未连接，请检查网络开关是否已打开。";
                alertMsg.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// 用户使用 Windows Hello 登录，这一登录过程如下：
        /// 首先判断用户设备是否能使用 Windows Hello，然后调用 Windows Hello
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void WindowsHelloLogin(object sender, RoutedEventArgs e)
        {
            Object oCurrentUID = localSettings.Values["email"];
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
                        bool isSuccessful = await MicrosoftPassportHelper.CreatePassportKeyAsync(currentUser.UID, currentUser.Name);

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

        private void SendResetCode(object sender, RoutedEventArgs e)
        {
            try
            {
                Task.Factory.StartNew(() =>
                {
                    codeSender.SendVerifyCode(currentUser.Email, "reset");
                });

                contentFrame.Navigate(typeof(ForgetPassword), currentUser, new SlideNavigationTransitionInfo()
                {
                    Effect = SlideNavigationTransitionEffect.FromRight
                });
            }
            catch (Exception)
            {
                alertMsg.Text = "未连接，请检查网络开关是否已打开。";
                alertMsg.Visibility = Visibility.Visible;
            }

        }

        private void Login(object sender, RoutedEventArgs e)
        {
            try
            {
                string password = HashDirectory.GetEncryptedPassword(userPwdInput.Password);

                if (userPwdInput.Password.Length == 0)
                {
                    alertMsg.Text = "请在此输入你的帐户密码。";
                }
                else if (!string.Equals(password, currentUser.Password))
                {
                    alertMsg.Text = "Cactus 帐户或密码不正确。";
                }
                else
                {
                    syncTool.LoadCurrentUser(currentUser);
                    StartPage.startPage.mainContent.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
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
