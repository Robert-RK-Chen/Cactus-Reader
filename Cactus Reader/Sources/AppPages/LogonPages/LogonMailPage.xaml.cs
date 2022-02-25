using Cactus_Reader.Sources.Utilities;
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
    public sealed partial class LogonMailPage : Page
    {
        IFreeSql freeSql = (Application.Current as App).freeSql;
        User currentUser = null;
        public LogonMailPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            currentUser = (User)e.Parameter;
            if (currentUser != null)
            {
                userMailInput.Text = currentUser.userEmail;
            }
        }

        private void BackPrevPage(object sender, RoutedEventArgs e)
        {
            contentFrame.Navigate(typeof(LoginAccountPage), null, new SlideNavigationTransitionInfo()
            { Effect = SlideNavigationTransitionEffect.FromLeft });
        }

        private void ContinueLogon(object sender, RoutedEventArgs e)
        {
            alertMsg.Visibility = Visibility.Collapsed;
            string mailAddress = userMailInput.Text;
            User user = new User();

            try
            {
                if (EmailVerify.IsEmail(mailAddress) is false)
                {
                    alertMsg.Text = "请输入一个有效的电子邮件地址。";
                    alertMsg.Visibility = Visibility.Visible;
                }
                else if (EmailEnabled(mailAddress) is false)
                {
                    alertMsg.Text = "电子邮件地址已被注册，请尝试使用其他电子邮件。";
                    alertMsg.Visibility = Visibility.Visible;
                }
                else
                {
                    user.userEmail = mailAddress;
                    contentFrame.Navigate(typeof(LogonCodePage), user, new SlideNavigationTransitionInfo()
                    { Effect = SlideNavigationTransitionEffect.FromRight });
                }
            }
            catch (Exception)
            {
                alertMsg.Text = "未连接，请查看网络开关是否已打开。";
                alertMsg.Visibility = Visibility.Visible;
            }
        }

        private void LogonButtonEnabled(object sender, RoutedEventArgs e)
        {
            continueButton.IsEnabled = true;
        }

        private void LogonButtonDisabled(object sender, RoutedEventArgs e)
        {
            continueButton.IsEnabled = false;
        }

        private void ClearAlertMsg(object sender, RoutedEventArgs e)
        {
            alertMsg.Visibility = Visibility.Collapsed;
        }

        private bool EmailEnabled(string email)
        {
            return freeSql.Select<User>().Where(user => user.userEmail == email).ToOne() is null;
        }
    }
}
