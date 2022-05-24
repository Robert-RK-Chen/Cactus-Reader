using Cactus_Reader.Sources.AppPages.Widget;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Cactus_Reader.Sources.AppPages.AppUI
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class PluginsPage : Page
    {
        const string EXPERIMENTAL_NAVIGATETO_HERE = "EXPERIMENTAL_NAVIGATETO_HERE";

        public PluginsPage()
        {
            InitializeComponent();
        }

        private void OpenGetTroublePage(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            MainPage.mainPage.mainContent.Navigate(typeof(GetTroublePage), EXPERIMENTAL_NAVIGATETO_HERE, new EntranceNavigationTransitionInfo());
        }
    }
}
