using Cactus_Reader.Sources.ToolKits;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Cactus_Reader.Sources.AppPages.Widget
{
    public sealed partial class GetTroublePage : Page
    {
        private string errorMsg = string.Empty;

        public GetTroublePage()
        {
            this.InitializeComponent();
            // 统一标题栏：透明按钮 + 隐藏系统标题栏 + 可拖拽区域 + 布局/显隐同步
            TitleBarService.Attach(appTitleBar, TitleBarStyle.Standard);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            errorMsg = e.Parameter as string;
            errorMsgText.Text = "错误代码：" + errorMsg;
        }

        private void BackMainPage(object sender, RoutedEventArgs e)
        {
            // 返回承载本页的外层 Frame（MainPage.mainContent），避免在页内嵌套新 MainPage
            Frame hostFrame = Frame;
            if (hostFrame != null && hostFrame.CanGoBack)
            {
                hostFrame.GoBack();
            }
            else if (hostFrame != null)
            {
                hostFrame.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
            }
        }
    }
}
