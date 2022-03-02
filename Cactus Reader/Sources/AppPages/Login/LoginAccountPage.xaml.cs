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
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            currentUser = (User)e.Parameter;
            if (currentUser != null)
            {
                accountInput.Text = currentUser.email;
            }
        }

        private void ContinueLogin(object sender, RoutedEventArgs e)
        {
            alertMsg.Visibility = Visibility.Collapsed;
            string email = accountInput.Text;

            try
            {
                currentUser = freeSql.Select<User>().Where(user => user.email == email).ToOne();
                if (currentUser != null)
                {
                    contentFrame.Navigate(typeof(LoginPwdPage), currentUser, new SlideNavigationTransitionInfo()
                    { Effect = SlideNavigationTransitionEffect.FromRight });
                }
                else
                {
                    alertMsg.Text = "请输入有效的电子邮件地址或帐户信息。";
                    alertMsg.Visibility = Visibility.Visible;
                }
            }
            catch (Exception)
            {
                alertMsg.Text = "未连接，请检查网络开关是否已打开。";
                alertMsg.Visibility = Visibility.Visible;
            }
        }

        private void CreateAccountPage(object sender, RoutedEventArgs e)
        {
            contentFrame.Navigate(typeof(RegisterMailPage), null, new SlideNavigationTransitionInfo()
            { Effect = SlideNavigationTransitionEffect.FromRight });
        }

        private void ClearAlertMsg(object sender, RoutedEventArgs e)
        {
            alertMsg.Visibility = Visibility.Collapsed;
        }

        private async void SkipLogin(object sender, RoutedEventArgs e)
        {
            ContentDialog skipLoginDialog = new ContentDialog
            {
                Title = "跳过登录并使用有限功能？",
                Content = "登录到 Cactus Reader 帐户，你可以体验文档与阅读进度的同步。",
                CloseButtonText = "继续登录",
                PrimaryButtonText = "跳过登录",
                DefaultButton = ContentDialogButton.Primary
            };

            ContentDialogResult result = await skipLoginDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                localSettings.Values["currentUser"] = "TempUser";
                StartPage.startPage.mainContent.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
            }
        }
    }
}
