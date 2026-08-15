using Cactus_Reader.Entities;
using Cactus_Reader.Sources.ToolKits;
using Cactus_Reader.Sources.WindowsHello;
using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Cactus_Reader.Sources.AppPages.SignUp
{
    public sealed partial class SignUpPwdPage : Page
    {
        User currentUser = null;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            currentUser = (User)e.Parameter;
            userMailBlock.Text = currentUser.Email;
        }

        public SignUpPwdPage()
        {
            InitializeComponent();
        }

        private void BackPrevPage(object sender, RoutedEventArgs e)
        {
            contentFrame.Navigate(typeof(SignUpUserInfoPage), currentUser, new SlideNavigationTransitionInfo()
            {
                Effect = SlideNavigationTransitionEffect.FromLeft
            });
        }

        private async void SignUpFinish(object sender, RoutedEventArgs e)
        {
            string password = passwordInput.Password;
            string checkPwd = passwordCheck.Password;

            try
            {
                if (password.Length == 0 && checkPwd.Length == 0)
                {
                    alertMsg.Text = "若要继续，请为你的帐户创建一个密码。";
                }
                else if (AccountService.IsPasswordValid(password) && string.Equals(password, checkPwd))
                {
                    ControllerVisibility.ShowProgressBar(statusBar);
                    // UID 在客户端生成，密码哈希由服务端生成（带盐）
                    bool ok = await AccountService.CompleteSignUpAsync(currentUser, password);
                    ControllerVisibility.HideProgressBar(statusBar);

                    if (!ok)
                    {
                        alertMsg.Text = "注册失败，该邮箱或用户名可能已被注册，请返回修改。";
                    }
                    else
                    {
                        bool isTPMEnabled = await MicrosoftPassportHelper.MicrosoftPassportAvailableCheckAsync();

                        if (isTPMEnabled)
                        {
                            contentFrame.Navigate(typeof(SignUpWindowsHello), currentUser, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
                        }
                        else
                        {
                            ContentDialog signInDialog = new ContentDialog
                            {
                                Title = "欢迎来到 Cactus Reader",
                                Content = "你的 Cactus 帐户已准备就绪！请牢记你的帐号与密码。下次登录时，你可以使用 Cactus 帐户与你的密码组合进行登录。点击确定按钮后，我们将自动为你登录。",
                                PrimaryButtonText = "确定",
                                DefaultButton = ContentDialogButton.Primary
                            };
                            ContentDialogResult result = await signInDialog.ShowAsync();

                            if (ContentDialogResult.Primary == result)
                            {
                                AccountService.CompleteLogin(currentUser);
                                StartPage.startPage.mainContent.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
                            }
                        }
                    }
                }
                else
                {
                    alertMsg.Text = "无效的密码，或两次输入的密码不相同。";
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
    }
}
