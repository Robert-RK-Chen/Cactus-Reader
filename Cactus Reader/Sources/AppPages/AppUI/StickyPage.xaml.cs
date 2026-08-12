using Cactus_Reader.Entities;
using Cactus_Reader.Sources.StickyNotes;
using Cactus_Reader.Sources.ToolKits;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
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
        private readonly EncryptStickyTool encryptStickyTool = EncryptStickyTool.Instance;
        private readonly ProfileSyncTool syncTool = ProfileSyncTool.Instance;

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

            string UID = localSettings.Values["UID"].ToString();

            // 0. 确保便签密钥可用（换设备时需输入个人密码解锁）
            await EnsureKeyReadyAsync();

            // 1. 先加载本地已有便签，保证页面快速呈现
            await LoadStickyNotes(UID);

            // 2. 从服务器同步本地缺失的便签，完成后刷新列表
            await syncTool.SyncUserSticky(UID);
            await LoadStickyNotes(UID);
        }

        /// <summary>
        /// 确保便签密钥可用：本机无密钥且服务端有密码包裹密钥时（换设备场景），
        /// 弹出密码输入框解锁。用户取消或解锁失败则返回 false。
        /// </summary>
        private async Task<bool> EnsureKeyReadyAsync()
        {
            if (await encryptStickyTool.EnsureStickyKeyReadyAsync())
            {
                return true;
            }

            while (true)
            {
                PasswordBox passwordBox = new PasswordBox
                {
                    Width = 360,
                    PlaceholderText = "请输入个人密码",
                    VerticalAlignment = VerticalAlignment.Bottom,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Header = "检测到该账号在其他设备上设置了个人密码，请输入密码以解锁你的便签本。",
                };
                ContentDialog dialog = new ContentDialog
                {
                    Title = "解锁便签本",
                    Content = passwordBox,
                    CloseButtonText = "取消",
                    PrimaryButtonText = "确定",
                    DefaultButton = ContentDialogButton.Primary
                };
                ContentDialogResult result = await dialog.ShowAsync();

                if (result != ContentDialogResult.Primary)
                {
                    return false; // 用户取消解锁
                }

                if (await encryptStickyTool.UnlockWithPasswordAsync(passwordBox.Password))
                {
                    return true;
                }

                ContentDialog errorDialog = new ContentDialog
                {
                    Title = "密码错误",
                    Content = "个人密码不正确，请重试。",
                    CloseButtonText = "确定"
                };
                await errorDialog.ShowAsync();
            }
        }

        /// <summary>
        /// 遍历本地便签文件夹，重建便签列表。
        /// </summary>
        private async Task LoadStickyNotes(string UID)
        {
            StorageFolder stickyFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(UID, CreationCollisionOption.OpenIfExists);
            stickyFolder = await stickyFolder.CreateFolderAsync("Sticky", CreationCollisionOption.OpenIfExists);
            IReadOnlyList<StorageFile> fileList = await stickyFolder.GetFilesAsync();

            if (fileList.Count > 0)
            {
                localSettings.Values["EmptyPlaceholderOpacity"] = 0;
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    StickyQuickViewList.Items.Clear();
                    foreach (StorageFile file in fileList)
                    {
                        try
                        {
                            string stickyText = encryptStickyTool.DecryptStickyText(File.ReadAllText(file.Path));
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
                        catch (Exception)
                        {
                            // 密钥未解锁或数据损坏：跳过该便签，不中断列表加载
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
