using Cactus_Reader.Entities;
using Cactus_Reader.Sources.StickyNotes;
using Cactus_Reader.Sources.ToolKits;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
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
        private readonly ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        private readonly ThemeColorBrushTool brushTool = ThemeColorBrushTool.Instance;
        private readonly AESEncryptTool aesEncryptTool = AESEncryptTool.Instance;
        private readonly MD5EncryptTool md5EncryptTool = MD5EncryptTool.Instance;

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
                        string stickyText = aesEncryptTool.DecryptStringFromBytesAes(File.ReadAllText(file.Path), md5EncryptTool.GetSystemEncryptedKey(), md5EncryptTool.GetSystemEncryptedVector());
                        Sticky sticky = JsonConvert.DeserializeObject<Sticky>(stickyText);

                        if (sticky.IsLock == false)
                        {
                            StickyQuickViewList.Items.Add(new StickyQuickView
                            {
                                CreateTimeText = sticky.CreateTime.ToShortDateString(),
                                StickySerial = sticky.StickySerial,
                                ThemeKind = sticky.StickyTheme,
                                QucikViewText = sticky.QuickViewText,
                            });
                        }
                        else
                        {
                            StickyQuickViewList.Items.Add(new StickyQuickView
                            {
                                CreateTimeText = sticky.CreateTime.ToShortDateString(),
                                StickySerial = sticky.StickySerial,
                                ThemeKind = sticky.StickyTheme,
                                QucikViewText = "🔒 该便签已被锁定。",
                            });
                        }
                    }
                });
            }
            else
            {
                localSettings.Values["EmptyPlaceholderOpacity"] = 1;
            }
            EmptyPlaceholder.Opacity = (int)localSettings.Values["EmptyPlaceholderOpacity"];
        }

        private async void CreateNewSticky(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            List<object> parameter = new List<object>();
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

            parameter.Add("new");
            parameter.Add(stickyQuickView);

            // 打开新便签界面
            CoreApplicationView newView = CoreApplication.CreateNewView();
            int newViewId = 0;
            await newView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Frame frame = new Frame();
                frame.Navigate(typeof(NewStickyPage), parameter, new DrillInNavigationTransitionInfo());
                Window.Current.Content = frame;
                Window.Current.Activate();
                newViewId = ApplicationView.GetForCurrentView().Id;
            });
            ApplicationView.PreferredLaunchViewSize = new Size(300, 300);
            bool viewShown = await ApplicationViewSwitcher.TryShowAsStandaloneAsync(newViewId);
        }
    }
}