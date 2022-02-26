using Cactus_Reader.Entities;
using Cactus_Reader.Sources.ToolKits;
using System;
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
        readonly IFreeSql freeSql = (Application.Current as App).freeSql;
        User currentUser = null;

        public LoginPwdPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            currentUser = (User)e.Parameter;
            if (currentUser != null)
            {
                userMailBlock.Text = currentUser.Email;
            }
        }

        private void BackPrevPage(object sender, RoutedEventArgs e)
        {
            contentFrame.Navigate(typeof(LoginAccountPage), currentUser, new SlideNavigationTransitionInfo()
            { Effect = SlideNavigationTransitionEffect.FromLeft });
        }

        private void MailCodeLogin(object sender, RoutedEventArgs e)
        {
            contentFrame.Navigate(typeof(LoginCodePage), currentUser, new SlideNavigationTransitionInfo()
            { Effect = SlideNavigationTransitionEffect.FromRight });
        }

        private async void Login(object sender, RoutedEventArgs e)
        {
            alertMsg.Visibility = Visibility.Collapsed;
            string password = HashDirectory.GetEncryptedPassword(userPwdInput.Password);

            if (userPwdInput.Password.Length == 0)
            {
                alertMsg.Text = "请在此输入你的帐户密码。";
                alertMsg.Visibility = Visibility.Visible;
            }
            else if (freeSql.Select<User>().Where(user => user.Password == password).ToOne() is null)
            {
                alertMsg.Text = "Cactus 帐户或密码不正确。";
                alertMsg.Visibility = Visibility.Visible;
            }
            else
            {
                ContentDialog noWifiDialog = new ContentDialog
                {
                    Title = "Cactus 帐户登录",
                    Content = currentUser.Name + "，欢迎回来！",
                    CloseButtonText = "确定"
                };
                ContentDialogResult result = await noWifiDialog.ShowAsync();
            }
        }

        private void ClearAlertMsg(object sender, RoutedEventArgs e)
        {
            alertMsg.Visibility = Visibility.Collapsed;
        }
    }
}
