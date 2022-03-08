using Cactus_Reader.Entities;
using Cactus_Reader.Sources.AppPages.Register;
using Cactus_Reader.Sources.ToolKits;
using System;
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
    public sealed partial class LoginAccountPage : Page
    {
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        readonly IFreeSql freeSql = IFreeSqlService.Instance;
        User currentUser = null;

        public LoginAccountPage()
        {
            InitializeComponent();
            Object oUID = localSettings.Values["UID"];
            if (null != oUID && Guid.TryParse(oUID.ToString(), out _))
            {
                accountInput.Text = localSettings.Values["Email"].ToString();
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

        private void ContinueLogin(object sender, RoutedEventArgs e)
        {
            string email = accountInput.Text;
            // 检查网络未连接异常
            try
            {
                currentUser = freeSql.Select<User>().Where(user => user.Email == email).ToOne();

                // 输入的用户帐号是存在的，则要求用户输入密码
                if (null != currentUser)
                {
                    contentFrame.Navigate(typeof(LoginPwdPage), currentUser, new SlideNavigationTransitionInfo()
                    {
                        Effect = SlideNavigationTransitionEffect.FromRight
                    });
                }
                else
                {
                    alertMsg.Text = "请输入有效的电子邮件地址或帐户信息。";
                }
                alertMsg.Visibility = Visibility.Visible;
            }
            catch (Exception)
            {
                alertMsg.Text = "未连接，请检查网络开关是否已打开。";
                alertMsg.Visibility = Visibility.Visible;
            }
        }

        private async void SkipLogin(object sender, RoutedEventArgs e)
        {
            // 询问用户是否跳过登录
            ContentDialog skipLoginDialog = new ContentDialog
            {
                Title = "跳过登录并使用有限功能？",
                Content = "登录到 Cactus Reader 帐户，你可以解锁大部分的 Cactus Reader 功能，并且可以体验文档与阅读进度的同步，还能同步你的设置到任意设备。",
                CloseButtonText = "继续登录",
                PrimaryButtonText = "跳过登录",
                DefaultButton = ContentDialogButton.Primary
            };
            ContentDialogResult result = await skipLoginDialog.ShowAsync();

            // 确认跳过登录则创建临时帐户
            if (result == ContentDialogResult.Primary)
            {
                localSettings.Values["isLogin"] = "true";
                localSettings.Values["UID"] = "Temp User";
                localSettings.Values["Email"] = "你将使用 Cactus Reader 的有限功能";
                localSettings.Values["Name"] = "未登录用户";
                StartPage.startPage.mainContent.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
            }
        }

        private void CreateAccountPage(object sender, RoutedEventArgs e)
        {
            contentFrame.Navigate(typeof(RegisterMailPage), null, new SlideNavigationTransitionInfo()
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
