using Cactus_Reader.Entities;
using Cactus_Reader.Sources.ToolKits;
using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Cactus_Reader.Sources.AppPages.SignUp
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class SignUpUserInfoPage : Page
    {
        User currentUser = null;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            currentUser = (User)e.Parameter;
            userMailBlock.Text = currentUser.Email;
        }

        public SignUpUserInfoPage()
        {
            InitializeComponent();
        }

        private void BackPrevPage(object sender, RoutedEventArgs e)
        {
            contentFrame.Navigate(typeof(SignUpCodePage), currentUser, new SlideNavigationTransitionInfo()
            {
                Effect = SlideNavigationTransitionEffect.FromLeft
            });
        }

        private async void ContinueSignUp(object sender, RoutedEventArgs e)
        {
            string userName = userNameInput.Text;

            try
            {
                ControllerVisibility.ShowProgressBar(statusBar);
                // 用户名格式 + 可用性校验，返回错误消息（空字符串=通过）
                string checkMessage = await AccountService.CheckUserNameAsync(userName);
                if (checkMessage.Length > 0)
                {
                    alertMsg.Text = checkMessage;
                }
                else
                {
                    currentUser.Name = userName;
                    contentFrame.Navigate(typeof(SignUpPwdPage), currentUser, new SlideNavigationTransitionInfo()
                    {
                        Effect = SlideNavigationTransitionEffect.FromRight
                    });
                }
            }
            catch(Exception)
            {
                alertMsg.Text = "未连接，请检查网络开关是否已打开。";
            }

            ControllerVisibility.HideProgressBar(statusBar);
            alertMsg.Visibility = Visibility.Visible;
        }

        private void ClearAlertMsg(object sender, RoutedEventArgs e)
        {
            alertMsg.Visibility = Visibility.Collapsed;
        }
    }
}
