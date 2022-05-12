using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Cactus_Reader.Sources.AppPages.Reader
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class WebDocumentReadingPage : Page
    {
        public WebDocumentReadingPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            string weblink = (string)e.Parameter;
            try
            {
                Uri targetUri = new Uri(weblink);
                webview2.Source = targetUri;
            }
            catch (FormatException)
            {
                // Incorrect address entered.
            }
        }

        private void BackMainPage(object sender, RoutedEventArgs e)
        {
            webview2.CoreWebView2.Stop();
            webview2.Close();
            mainContent.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
        }
    }
}
