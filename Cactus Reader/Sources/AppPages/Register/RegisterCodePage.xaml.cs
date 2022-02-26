using Cactus_Reader.Entities;
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
    public sealed partial class RegisterCodePage : Page
    {
        User currentUser = null;
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            currentUser = (User)e.Parameter;
            if (currentUser != null)
            {
                userMailBlock.Text = currentUser.Email;
                userMail.Text = currentUser.Email + "，请输入邮件中的代码进行登录。";
            }
        }

        public RegisterCodePage()
        {
            this.InitializeComponent();
        }

        private void BackPrevPage(object sender, RoutedEventArgs e)
        {
            contentFrame.Navigate(typeof(RegisterMailPage), currentUser, new SlideNavigationTransitionInfo()
            { Effect = SlideNavigationTransitionEffect.FromLeft });
        }

        private void ContinueLogon(object sender, RoutedEventArgs e)
        {
            contentFrame.Navigate(typeof(RegisterUserInfoPage), currentUser, new SlideNavigationTransitionInfo()
            { Effect = SlideNavigationTransitionEffect.FromRight });
        }
    }
}