using Cactus_Reader.Entities;
using Cactus_Reader.Sources.ToolKits;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Cactus_Reader.Sources.AppPages.AppUI
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class NotesPage : Page
    {
        public static NotesPage notesPage;
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

        public NotesPage()
        {
            InitializeComponent();
            if (localSettings.Values["StickyTheme"] == null) { localSettings.Values["StickyTheme"] = "GingkoYellow"; }
            notesPage = this;
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // 遍历便签文件夹，加入所有的便签
            StorageFolder fileFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("Notes", CreationCollisionOption.OpenIfExists);
            IReadOnlyList<StorageFile> fileList = await fileFolder.GetFilesAsync();
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                foreach (StorageFile file in fileList)
                {
                    Sticky sticky = JsonConvert.DeserializeObject<Sticky>(File.ReadAllText(file.Path));
                    StickyQuickViewList.Items.Add(new StickyQuickView
                    {
                        CreateTimeText = sticky.CreateTime.ToShortDateString(),
                        StickySerial = sticky.StickySerial,
                        ThemeKind = sticky.StickyTheme,
                        QucikViewText = sticky.QuickView,
                    });
                }
            });
        }

        private async void CreateNewSticky(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            string Serial = Guid.NewGuid().ToString("D").ToUpper();

            CoreApplicationView newView = CoreApplication.CreateNewView();
            int newViewId = 0;
            await newView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Frame frame = new Frame();
                frame.Navigate(typeof(NewStickyPage), Serial, new DrillInNavigationTransitionInfo());
                Window.Current.Content = frame;
                Window.Current.Activate();
                newViewId = ApplicationView.GetForCurrentView().Id;
            });
            bool viewShown = await ApplicationViewSwitcher.TryShowAsStandaloneAsync(newViewId);

            StickyQuickViewList.Items.Add(new StickyQuickView
            {
                CreateTimeText = DateTime.Now.ToShortDateString(),
                StickySerial = Serial,
                ThemeKind = localSettings.Values["StickyTheme"].ToString(),
            });
        }
    }
}
