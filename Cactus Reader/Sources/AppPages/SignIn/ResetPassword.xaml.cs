using Cactus_Reader.Entities;
using Cactus_Reader.Sources.AppPages.SignUp;
using Cactus_Reader.Sources.ToolKits;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Cactus_Reader.Sources.AppPages.SignIn
{
    public sealed partial class ResetPassword : Page
    {
        User currentUser = null;

        // 验证码校验通过后签发的一次性重置令牌（服务端 reset-password 校验，防仅凭 UID 重置他人密码）
        string resetToken = null;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            List<object> parameter = (List<object>)e.Parameter;
            currentUser = (User)parameter[0];
            resetToken = (string)parameter[1];
            userMailBlock.Text = currentUser.Email;
        }

        public ResetPassword()
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

        private async void ResetFinish(object sender, RoutedEventArgs e)
        {
            string password = passwordInput.Password;
            string checkPwd = passwordCheck.Password;

            try
            {
                if (password.Length == 0 && checkPwd.Length == 0)
                {
                    alertMsg.Text = "若要继续，请输入一个长度至少为 8 位，并且含有大小写字母、数字或符号组成的密码。";
                }
                else if (AccountService.IsPasswordValid(password) && string.Equals(password, checkPwd))
                {
                    ControllerVisibility.ShowProgressBar(statusBar);
                    // 密码哈希由服务端生成（带盐）；携带一次性重置令牌，服务端校验后才允许改密
                    bool resetOk = await AccountService.ResetPasswordAsync(currentUser.UID, resetToken, password);
                    ControllerVisibility.HideProgressBar(statusBar);

                    if (!resetOk)
                    {
                        alertMsg.Text = "密码重置失败，请稍后再试。";
                    }
                    else
                    {
                        ContentDialog signInDialog = new ContentDialog
                        {
                            Title = "重置密码成功",
                            Content = "你的 Cactus 帐户密码重置完成。请牢记你的帐号与密码。点击确定按钮后，我们将自动为你登录。",
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
