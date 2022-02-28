using Cactus_Reader.Entities;
using Cactus_Reader.Sources.ToolKits;
using System;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Cactus_Reader.Sources.AppPages.Register
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class RegisterPwdPage : Page
    {
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        readonly IFreeSql freeSql = (Application.Current as App).freeSql;
        User currentUser = null;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            currentUser = (User)e.Parameter;
            userMailBlock.Text = currentUser.email;
        }

        public RegisterPwdPage()
        {
            this.InitializeComponent();
        }

        private void BackPrevPage(object sender, RoutedEventArgs e)
        {
            contentFrame.Navigate(typeof(RegisterUserInfoPage), currentUser, new SlideNavigationTransitionInfo()
            { Effect = SlideNavigationTransitionEffect.FromLeft });
        }

        private async void LogonFinish(object sender, RoutedEventArgs e)
        {
            alertMsg.Visibility = Visibility.Collapsed;
            string password = passwordInput.Password;
            try
            {
                if (password.Length == 0)
                {
                    alertMsg.Text = "若要继续，请输入一个长度至少为 8 位，并且含有大小写字母、数字或符号组成的密码。";
                    alertMsg.Visibility = Visibility.Visible;
                }
                else if (!InformationVerify.IsPassword(password))
                {
                    alertMsg.Text = "无效的密码，有效的密码长度至少为 8 位，并且含有大小写字母、数字或符号。";
                    alertMsg.Visibility = Visibility.Visible;
                }
                else
                {
                    currentUser.password = HashDirectory.GetEncryptedPassword(password);
                    currentUser.uid = Guid.NewGuid().ToString("D").ToUpper();
                    currentUser.profile = "CactusRepo\\" + currentUser.uid;
                    freeSql.Insert(currentUser).ExecuteAffrows();

                    ContentDialog signInDialog = new ContentDialog
                    {
                        Title = "欢迎来到 Cactus Reader",
                        Content = "你的 Cactus 帐户已准备就绪！请牢记你的帐号与密码。下次登录时，你可以使用 Cactus 帐户与你的密码组合进行登录。点击确定按钮后，我们将自动为你登录。",
                        PrimaryButtonText = "确定",
                        DefaultButton = ContentDialogButton.Primary
                    };

                    ContentDialogResult result = await signInDialog.ShowAsync();
                    if (result == ContentDialogResult.Primary)
                    {
                        localSettings.Values["currentUser"] = currentUser.uid;
                        StartPage.startPage.mainContent.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
                    }
                }
            }
            catch (Exception)
            {
                alertMsg.Text = "未连接，请检查网络开关是否已打开。";
                alertMsg.Visibility = Visibility.Visible;
            }
        }

        private void ClearAlertMsg(object sender, RoutedEventArgs e)
        {
            alertMsg.Visibility = Visibility.Collapsed;
        }
    }
}
