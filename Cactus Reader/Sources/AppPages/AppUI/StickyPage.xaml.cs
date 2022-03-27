using Cactus_Reader.Entities;
using Cactus_Reader.Sources.StickyNotes;
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
    public sealed partial class StickyPage : Page
    {
        public static StickyPage stickyPage;
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        readonly ThemeColorBrushTool brushTool = ThemeColorBrushTool.Instance;

        public StickyPage()
        {
            InitializeComponent();
            if (localSettings.Values["StickyTheme"] == null) { localSettings.Values["StickyTheme"] = "GingkoYellow"; }
            if (localSettings.Values["EmptyPlaceholderOpacity"] == null) { localSettings.Values["EmptyPlaceholderOpacity"] = 1; }
            stickyPage = this;
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // 遍历便签文件夹，加入所有的便签
            string UID = localSettings.Values["UID"].ToString();
            StorageFolder stickyFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(UID, CreationCollisionOption.OpenIfExists);
            stickyFolder = await stickyFolder.CreateFolderAsync("Sticky", CreationCollisionOption.OpenIfExists);
            IReadOnlyList<StorageFile> fileList = await stickyFolder.GetFilesAsync();

            if (fileList.Count > 0)
            {
                localSettings.Values["EmptyPlaceholderOpacity"] = 0;
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
                            QucikViewText = sticky.QuickViewText,
                        });
                    }
                });
            }
            else
            {
                localSettings.Values["EmptyPlaceholderOpacity"] = 1;
            }
            EmptyPlaceholder.Opacity = int.Parse(localSettings.Values["EmptyPlaceholderOpacity"].ToString());
        }

        private async void CreateNewSticky(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            string serial = Guid.NewGuid().ToString("D").ToUpper();
            string UID = localSettings.Values["UID"].ToString();
            string theme = localSettings.Values["StickyTheme"].ToString();
            EmptyPlaceholder.Opacity = 0;
            localSettings.Values["EmptyPlaceholderOpacity"] = 0;

            StickyQuickView stickyQuickView = new StickyQuickView
            {
                CreateTimeText = DateTime.Now.ToShortDateString(),
                StickySerial = serial,
                ThemeKind = theme,
                TitleBackground = brushTool.GetThemeColorBrush(theme, false).TitleBrush,
                Background = brushTool.GetThemeColorBrush(theme, false).BackgroundBrush,
            };
            StickyQuickViewList.Items.Add(stickyQuickView);

            // 打开新便签界面
            CoreApplicationView newView = CoreApplication.CreateNewView();
            int newViewId = 0;
            await newView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Frame frame = new Frame();
                frame.Navigate(typeof(NewStickyPage), stickyQuickView, new DrillInNavigationTransitionInfo());
                Window.Current.Content = frame;
                Window.Current.Activate();
                newViewId = ApplicationView.GetForCurrentView().Id;
            });
            bool viewShown = await ApplicationViewSwitcher.TryShowAsStandaloneAsync(newViewId);
        }
    }
}