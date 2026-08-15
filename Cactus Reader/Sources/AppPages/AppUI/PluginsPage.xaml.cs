using Cactus_Reader.Sources.AppPages.Reader;
using Cactus_Reader.Sources.AppPages.Widget;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;

namespace Cactus_Reader.Sources.AppPages.AppUI
{
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

        private void OpenPDFReadingPage(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            MainPage.mainPage.mainContent.Navigate(typeof(PdfFileReadingPage), null, new EntranceNavigationTransitionInfo());
        }
    }
}
