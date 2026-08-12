using Cactus_Reader.Entities;
using Cactus_Reader.Sources.AppPages.SignUp;
using Cactus_Reader.Sources.ToolKits;
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
    public sealed partial class SignInAccountPage : Page
    {
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        User currentUser = null;

        public SignInAccountPage()
        {
            InitializeComponent();
            Object oUID = localSettings.Values["UID"];
            if (null != oUID && Guid.TryParse(oUID.ToString(), out _))
            {
                accountInput.Text = localSettings.Values["email"].ToString();
            }
        }

        // 用于接受其他页面过渡到这一页时传入的用户信息
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            currentUser = (User)e.Parameter;
            if (null != currentUser)
            {
                accountInput.Text = currentUser.Email;
            }
        }

        private async void ContinueSignIn(object sender, RoutedEventArgs e)
        {
            string email = accountInput.Text;

            try
            {
                ControllerVisibility.ShowProgressBar(statusBar);
                currentUser = await ApiClient.GetUserByEmailAsync(email);

                // 输入的用户帐号是存在的，则要求用户输入密码
                if (null != currentUser)
                {
                    contentFrame.Navigate(typeof(SignInPwdPage), currentUser, new SlideNavigationTransitionInfo()
                    {
                        Effect = SlideNavigationTransitionEffect.FromRight
                    });
                }
                else
                {
                    alertMsg.Text = "请输入有效的电子邮件地址或帐户信息。";
                }
            }
            catch (Exception)
            {
                alertMsg.Text = "未连接，请检查网络开关是否已打开。";
            }
            ControllerVisibility.HideProgressBar(statusBar);
            alertMsg.Visibility = Visibility.Visible;
        }

        private async void SkipSignIn(object sender, RoutedEventArgs e)
        {
            // 询问用户是否跳过登录
            ContentDialog skipSignInDialog = new ContentDialog
            {
                Title = "跳过登录并使用有限功能？",
                Content = "登录到 Cactus Reader 帐户，你可以解锁大部分的 Cactus Reader 功能，并且可以体验文档与阅读进度的同步，还能同步你的设置到任意设备。",
                CloseButtonText = "继续登录",
                PrimaryButtonText = "跳过登录",
                DefaultButton = ContentDialogButton.Primary
            };
            ContentDialogResult result = await skipSignInDialog.ShowAsync();

            // 确认跳过登录则创建临时帐户
            if (result == ContentDialogResult.Primary)
            {
                localSettings.Values["isLogin"] = "true";
                localSettings.Values["UID"] = "Temp User";
                localSettings.Values["email"] = "你将使用 Cactus Reader 的有限功能";
                localSettings.Values["name"] = "未登录用户";
                StartPage.startPage.mainContent.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
            }
        }

        private void CreateAccountPage(object sender, RoutedEventArgs e)
        {
            contentFrame.Navigate(typeof(SignUpMailPage), null, new SlideNavigationTransitionInfo()
            {
                Effect = SlideNavigationTransitionEffect.FromRight
            });
        }

        private void ClearAlertMsg(object sender, RoutedEventArgs e)
        {
            alertMsg.Visibility = Visibility.Collapsed;
        }
    }
}
