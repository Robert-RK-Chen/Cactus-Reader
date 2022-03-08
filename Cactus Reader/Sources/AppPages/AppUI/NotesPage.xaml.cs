using Cactus_Reader.Sources.AppPages.Widget;
using System;
using Windows.Foundation;
using Windows.UI.WindowManagement;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Cactus_Reader.Sources.AppPages.AppUI
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class NotesPage : Page
    {
        public NotesPage()
        {
            InitializeComponent();
        }

        private void CreateNewSticky(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            //AppWindow appWindow = await AppWindow.TryCreateAsync();
            //Frame appWindowContentFrame = new Frame();
            //appWindowContentFrame.Navigate(typeof(Sticky));
            //ElementCompositionPreview.SetAppWindowContent(appWindow, appWindowContentFrame);
            //appWindow.RequestSize(new Size(300, 428));
            //await appWindow.TryShowAsync();
        }
    }
}
