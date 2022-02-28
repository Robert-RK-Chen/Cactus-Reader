using Cactus_Reader.Entities;
using Cactus_Reader.Sources.ToolKits;
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
        readonly IFreeSql freeSql = (Application.Current as App).freeSql;
        readonly VerifyCodeSender codeSender = (Application.Current as App).codeSender;
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
                userMailBlock.Text = currentUser.email;
            }
        }

        private void BackPrevPage(object sender, RoutedEventArgs e)
        {
            contentFrame.Navigate(typeof(LoginAccountPage), currentUser, new SlideNavigationTransitionInfo()
            { Effect = SlideNavigationTransitionEffect.FromLeft });
        }

        private async void MailCodeLogin(object sender, RoutedEventArgs e)
        {
            try
            {
                Code currentCode = freeSql.Select<Code>().Where(code => code.email == currentUser.email).ToOne();
                if (currentCode == null || currentCode.create_time.AddMinutes(1) < DateTime.Now)
                {
                    await codeSender.SendVerifyCode(currentUser.email);
                }
                contentFrame.Navigate(typeof(LoginCodePage), currentUser, new SlideNavigationTransitionInfo()
                { Effect = SlideNavigationTransitionEffect.FromRight });
            }
            catch (Exception)
            {
                alertMsg.Text = "未连接，请检查网络开关是否已打开。";
                alertMsg.Visibility = Visibility.Visible;
            }
        }

        private void Login(object sender, RoutedEventArgs e)
        {
            alertMsg.Visibility = Visibility.Collapsed;
            string password = HashDirectory.GetEncryptedPassword(userPwdInput.Password);

            if (userPwdInput.Password.Length == 0)
            {
                alertMsg.Text = "请在此输入你的帐户密码。";
                alertMsg.Visibility = Visibility.Visible;
            }
            else if (freeSql.Select<User>().Where(user => user.password == password).ToOne() == null)
            {
                alertMsg.Text = "Cactus 帐户或密码不正确。";
                alertMsg.Visibility = Visibility.Visible;
            }
            else
            {
                localSettings.Values["currentUser"] = currentUser.uid;
                StartPage.startPage.mainContent.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
            }
        }

        private void ClearAlertMsg(object sender, RoutedEventArgs e)
        {
            alertMsg.Visibility = Visibility.Collapsed;
        }
    }
}
