using Cactus_Reader.Entities;
using Cactus_Reader.Sources.AppPages.Login;
using Cactus_Reader.Sources.ToolKits;
using System;
using System.Threading.Tasks;
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
    public sealed partial class RegisterMailPage : Page
    {
        readonly MailCodeSender codeSender = MailCodeSender.Instance;
        User currentUser = null;

        public RegisterMailPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            currentUser = (User)e.Parameter;
            if (null != currentUser)
            {
                userMailInput.Text = currentUser.Email;
            }
        }

        private void BackPrevPage(object sender, RoutedEventArgs e)
        {
            contentFrame.Navigate(typeof(LoginAccountPage), null, new SlideNavigationTransitionInfo()
            {
                Effect = SlideNavigationTransitionEffect.FromLeft
            });
        }

        private void ContinueRegister(object sender, RoutedEventArgs e)
        {
            string mailAddress = userMailInput.Text;
            User user = new User();
            try
            {
                if (!InformationVerify.IsEmail(mailAddress))
                {
                    alertMsg.Text = "请输入一个有效的电子邮件地址。";
                }
                else if (!InformationVerify.EmailEnabled(mailAddress))
                {
                    alertMsg.Text = "电子邮件地址已被注册，请尝试使用其他电子邮件。";
                }
                else
                {
                    user.Email = mailAddress;
                    Task.Factory.StartNew(() =>
                    {
                        codeSender.SendVerifyCode(user.Email, "register");
                    });

                    contentFrame.Navigate(typeof(RegisterCodePage), user, new SlideNavigationTransitionInfo()
                    {
                        Effect = SlideNavigationTransitionEffect.FromRight
                    });
                }

            }
            catch (Exception)
            {
                alertMsg.Text = "未连接，请检查网络开关是否已打开。";
                alertMsg.Visibility = Visibility.Visible;
            }
            alertMsg.Visibility = Visibility.Visible;
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

        private void OpenServiceWindow(object sender, RoutedEventArgs e)
        {
        }
    }
}
