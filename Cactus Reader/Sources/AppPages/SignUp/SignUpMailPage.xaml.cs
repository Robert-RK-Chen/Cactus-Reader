using Cactus_Reader.Entities;
using Cactus_Reader.Sources.AppPages.SignIn;
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

namespace Cactus_Reader.Sources.AppPages.SignUp
{
    public sealed partial class SignUpMailPage : Page
    {
        User currentUser = null;

        public SignUpMailPage()
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
            contentFrame.Navigate(typeof(SignInAccountPage), null, new SlideNavigationTransitionInfo()
            {
                Effect = SlideNavigationTransitionEffect.FromLeft
            });
        }

        private async void ContinueSignUp(object sender, RoutedEventArgs e)
        {
            User user = new User();
            string mailAddress = userMailInput.Text;

            try
            {
                ControllerVisibility.ShowProgressBar(statusBar);
                // 邮箱格式 + 可用性校验，返回错误消息（空字符串=通过）
                string checkMessage = await AccountService.CheckEmailAsync(mailAddress);
                if (checkMessage.Length > 0)
                {
                    alertMsg.Text = checkMessage;
                }
                else
                {
                    user.Email = mailAddress;
                    // 等待服务端发信结果：成功才进入验证码页
                    (bool ok, string reason) = await AccountService.SendVerifyCodeAsync(user.Email, "signup");

                    if (ok)
                    {
                        contentFrame.Navigate(typeof(SignUpCodePage), user, new SlideNavigationTransitionInfo()
                        {
                            Effect = SlideNavigationTransitionEffect.FromRight
                        });
                    }
                    else if (reason == "TOO_FREQUENT")
                    {
                        alertMsg.Text = "验证码发送过于频繁，请稍后再试。";
                    }
                    else
                    {
                        alertMsg.Text = "验证码发送失败，请检查邮箱地址或稍后再试。";
                    }
                }
            }
            catch (Exception)
            {
                alertMsg.Text = "未连接，请检查网络开关是否已打开。";
            }
            ControllerVisibility.HideProgressBar(statusBar);
            alertMsg.Visibility = Visibility.Visible;
        }

        private void SignUpButtonEnabled(object sender, RoutedEventArgs e)
        {
            continueButton.IsEnabled = true;
        }

        private void SignUpButtonDisabled(object sender, RoutedEventArgs e)
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
