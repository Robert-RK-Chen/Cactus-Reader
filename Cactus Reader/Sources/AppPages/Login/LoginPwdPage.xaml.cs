using Cactus_Reader.Entities;
using Cactus_Reader.Sources.ToolKits;
using Cactus_Reader.Sources.WindowsHello;
using System;
using System.Diagnostics;
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
        readonly IFreeSql freeSql = IFreeSqlService.Instance;
        readonly VerifyCodeSender codeSender = VerifyCodeSender.Instance;
        User currentUser = null;

        public LoginPwdPage()
        {
            InitializeComponent();
        }

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
            { Effect = SlideNavigationTransitionEffect.FromLeft });
        }

        private void ClearAlertMsg(object sender, RoutedEventArgs e)
        {
            alertMsg.Visibility = Visibility.Collapsed;
        }

        private void ShowMoreLoginWays(object sender, RoutedEventArgs e)
        {
            loginWays.Visibility = loginWays.Visibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SendLoginCode(object sender, RoutedEventArgs e)
        {
            try
            {
                Task.Factory.StartNew(() => { codeSender.SendVerifyCode(currentUser.Email, "login"); });

                contentFrame.Navigate(typeof(LoginCodePage), currentUser, new SlideNavigationTransitionInfo()
                { Effect = SlideNavigationTransitionEffect.FromRight });
            }
            catch (Exception)
            {
                alertMsg.Text = "未连接，请检查网络开关是否已打开。";
                alertMsg.Visibility = Visibility.Visible;
            }
        }

        private async void WindowsHelloLogin(object sender, RoutedEventArgs e)
        {
            User user = freeSql.Select<User>().Where(users => users.Email == currentUser.Email).ToOne();
            if (user.UID == localSettings.Values["currentUser"].ToString())
            {
                bool isSuccessful = await MicrosoftPassportHelper.CreatePassportKeyAsync(user.UID, user.Name);
                if (isSuccessful)
                {
                    localSettings.Values["currentUser"] = currentUser.UID;
                    StartPage.startPage.mainContent.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
                }
                else
                {
                    alertMsg.Text = "Windows Hello 验证失败，请再试一次。";
                }
            }
            else
            {
                alertMsg.Text = "若要使用 Windows Hello，请重新登录。";
            }
        }

        private void SendResetCode(object sender, RoutedEventArgs e)
        {
            try
            {
                Task.Factory.StartNew(() => { codeSender.SendVerifyCode(currentUser.Email, "reset"); });

                contentFrame.Navigate(typeof(ForgetPassword), currentUser, new SlideNavigationTransitionInfo()
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
            else if (freeSql.Select<User>().Where(user => user.Password == password).ToOne() is null)
            {
                alertMsg.Text = "Cactus 帐户或密码不正确。";
                alertMsg.Visibility = Visibility.Visible;
            }
            else
            {
                localSettings.Values["currentUser"] = currentUser.UID;
                StartPage.startPage.mainContent.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
            }
        }
    }
}
