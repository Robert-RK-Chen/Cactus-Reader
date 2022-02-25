using Cactus_Reader.Sources.Utilities;
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
    public sealed partial class LogonPwdPage : Page
    {
        User user = null;
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            user = (User)e.Parameter;
        }
        public LogonPwdPage()
        {
            this.InitializeComponent();
        }

        private void BackPrevPage(object sender, RoutedEventArgs e)
        {
            contentFrame.Navigate(typeof(LogonUserInfoPage), user, new SlideNavigationTransitionInfo()
            { Effect = SlideNavigationTransitionEffect.FromLeft });
        }

        private void LogonFinish(object sender, RoutedEventArgs e)
        {
        }
    }
}
