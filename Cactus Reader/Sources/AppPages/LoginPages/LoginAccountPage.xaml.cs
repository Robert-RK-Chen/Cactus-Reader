using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Cactus_Reader.Sources.AppPages
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class LoginAccountPage : Page
    {
        IFreeSql freeSql = (Application.Current as App).freeSql;
        Utilities.User currentUser = null;

        public LoginAccountPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            currentUser = (Utilities.User)e.Parameter;
            if (currentUser != null)
            {
                accountInput.Text = currentUser.userEmail;
            }
        }

        private void ContinueLogin(object sender, RoutedEventArgs e)
        {
            alertMsg.Visibility = Visibility.Collapsed;
            string userEmail = accountInput.Text;

            try
            {
                currentUser = freeSql.Select<Utilities.User>().Where(user => user.userEmail == userEmail).ToOne();
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
            contentFrame.Navigate(typeof(LogonMailPage), null, new SlideNavigationTransitionInfo()
            { Effect = SlideNavigationTransitionEffect.FromRight });
        }

        private void ClearAlertMsg(object sender, RoutedEventArgs e)
        {
            alertMsg.Visibility = Visibility.Collapsed;
        }
    }
}
