using Cactus_Reader.Entities;
using Cactus_Reader.Sources.AppPages.Login;
using Cactus_Reader.Sources.ToolKits;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
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
        private readonly MailCodeSender codeSender = MailCodeSender.Instance;
        private readonly InformationVerify informationVerify = InformationVerify.Instance;
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

        private async void ContinueRegister(object sender, RoutedEventArgs e)
        {
            User user = new User();
            string mailAddress = userMailInput.Text;

            try
            {
                ControllerVisibility.ShowProgressBar(statusBar);
                bool isMailEnabled = await Task.Factory.StartNew(() => informationVerify.EmailEnabled(mailAddress));

                if (!informationVerify.IsEmail(mailAddress))
                {
                    alertMsg.Text = "请输入一个有效的电子邮件地址。";
                }
                else if (!isMailEnabled)
                {
                    alertMsg.Text = "电子邮件地址已被注册，请尝试使用其他电子邮件。";
                }
                else
                {
                    user.Email = mailAddress;
                    await Task.Factory.StartNew(() => codeSender.SendVerifyCode(user.Email, "register"));
                    contentFrame.Navigate(typeof(RegisterCodePage), user, new SlideNavigationTransitionInfo()
                    {
                        Effect = SlideNavigationTransitionEffect.FromRight
                    });
                }
            }
            catch (Exception)
            {
                alertMsg.Text = "未连接，请检查网络开关是否已打开。";
            }
            ControllerVisibility.HideProgressBar(statusBar);
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

        private async void ReadServiceAndRivacy(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            CoreApplicationView newView = CoreApplication.CreateNewView();
            int newViewId = 0;
            await newView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Frame frame = new Frame();
                frame.Navigate(typeof(ServiceAndPrivacy), null, new DrillInNavigationTransitionInfo());
                Window.Current.Content = frame;
                Window.Current.Activate();
                newViewId = ApplicationView.GetForCurrentView().Id;
            });
            bool viewShown = await ApplicationViewSwitcher.TryShowAsStandaloneAsync(newViewId);
        }
    }
}
