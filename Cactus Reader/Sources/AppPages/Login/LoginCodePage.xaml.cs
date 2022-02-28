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
    public sealed partial class LoginCodePage : Page
    {
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        readonly IFreeSql freeSql = (Application.Current as App).freeSql;
        User currentUser = null;

        public LoginCodePage()
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
                userMail.Text = currentUser.email + "，请输入邮件中的代码进行登录。";
            }
        }

        private void BackPrevPage(object sender, RoutedEventArgs e)
        {
            contentFrame.Navigate(typeof(LoginPwdPage), currentUser, new SlideNavigationTransitionInfo()
            { Effect = SlideNavigationTransitionEffect.FromLeft });
        }

        private void Login(object sender, RoutedEventArgs e)
        {
            alertMsg.Visibility = Visibility.Collapsed;
            string verifyCode = verifyCodeInput.Text;
            try
            {
                Code currentCode = freeSql.Select<Code>().Where(code => code.email == currentUser.email).ToOne();
                if (verifyCode.Length == 0)
                {
                    alertMsg.Text = "若要继续，请输入我们刚才发送给你的代码。";
                    alertMsg.Visibility = Visibility.Visible;
                }
                else if (verifyCode != currentCode.verify_code)
                {
                    alertMsg.Text = "该代码无效，检查该代码并重试。";
                    alertMsg.Visibility = Visibility.Visible;
                }
                else if (currentCode.create_time.AddMinutes(5) < DateTime.Now)
                {
                    alertMsg.Text = "该代码无效，检查该代码并重试。";
                    alertMsg.Visibility = Visibility.Visible;
                }
                else if (currentCode.verify_code == verifyCode)
                {
                    localSettings.Values["currentUser"] = currentUser.uid;
                    StartPage.startPage.mainContent.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
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

        private void ResendVerifyCode(object sender, RoutedEventArgs e)
        {
            Code currentCode = freeSql.Select<Code>().Where(code => code.email == currentUser.email).ToOne();
            bool sendFlag = new VerifyCodeSender().SendVerifyCode(currentUser.email);
            if (sendFlag == true)
            {
                alertMsg.Text = "代码已发送，请注意查收。";
                alertMsg.Visibility = Visibility.Visible;
            }
            else
            {
                alertMsg.Text = "代码发送过于频繁，请稍后再试。";
                alertMsg.Visibility = Visibility.Visible;
            }
        }
    }
}
