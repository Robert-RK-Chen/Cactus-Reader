using Cactus_Reader.Sources.AppPages.SignIn;
using Cactus_Reader.Sources.ToolKits;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Cactus_Reader.Sources.AppPages
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class StartPage : Page
    {
        // 创建一个全局的 StartPage，用于登录完成将页面切换到应用页面
        public static StartPage startPage;

        public StartPage()
        {
            InitializeComponent();
            startPage = this;

            // 以下是将 Mica 效果扩展到标题栏
            // 统一标题栏：透明按钮 + 隐藏系统标题栏 + 可拖拽区域 + 布局/显隐/激活同步
            TitleBarService.Attach(appTitleBar, TitleBarStyle.Standard, appTitle);
        }

        // 开始用户登陆与注册的过程
        private void ContinueSignIn(object sender, RoutedEventArgs e)
        {
            contentFrame.Navigate(typeof(SignInAccountPage), null, new SlideNavigationTransitionInfo()
            {
                Effect = SlideNavigationTransitionEffect.FromRight
            });
        }
    }
}
