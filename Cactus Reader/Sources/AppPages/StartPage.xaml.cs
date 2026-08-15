using Cactus_Reader.Sources.AppPages.SignIn;
using Cactus_Reader.Sources.ToolKits;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;

namespace Cactus_Reader.Sources.AppPages
{
    public sealed partial class StartPage : Page
    {
        // 全局 StartPage 实例（登录完成后切换到应用页面）
        public static StartPage startPage;

        public StartPage()
        {
            InitializeComponent();
            startPage = this;

            // 统一标题栏：透明按钮 + 隐藏系统标题栏 + 可拖拽区域 + 布局同步
            TitleBarService.Attach(appTitleBar, TitleBarStyle.Standard, appTitle);
        }

        private void ContinueSignIn(object sender, RoutedEventArgs e)
        {
            contentFrame.Navigate(typeof(SignInAccountPage), null, new SlideNavigationTransitionInfo()
            {
                Effect = SlideNavigationTransitionEffect.FromRight
            });
        }
    }
}
