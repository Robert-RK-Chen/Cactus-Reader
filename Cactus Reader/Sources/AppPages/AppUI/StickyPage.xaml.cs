using Cactus_Reader.Entities;
using Cactus_Reader.Sources.StickyNotes;
using Cactus_Reader.Sources.ToolKits;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Cactus_Reader.Sources.AppPages.AppUI
{
    public sealed partial class StickyPage : Page
    {
        public static StickyPage stickyPage;
        private readonly ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        private readonly EncryptStickyTool encryptStickyTool = EncryptStickyTool.Instance;
        private readonly ProfileSyncTool syncTool = ProfileSyncTool.Instance;

        public StickyPage()
        {
            InitializeComponent();
            StickyService.GetStickyTheme();
            if (localSettings.Values["EmptyPlaceholderOpacity"] == null) { localSettings.Values["EmptyPlaceholderOpacity"] = 1; }
            stickyPage = this;
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            string UID = localSettings.Values["UID"].ToString();

            // 0. 确保便签密钥可用
            await EnsureKeyReadyAsync();

            // 1. 先加载本地已有便签，保证页面快速呈现
            await LoadStickyNotes(UID);

            // 2. 从服务器同步本地缺失的便签，完成后刷新列表
            await syncTool.SyncUserSticky(UID);
            await LoadStickyNotes(UID);
        }

        /// <summary>确保便签密钥可用：本机无密钥且服务端有包裹密钥时弹出密码框解锁。</summary>
        private async Task<bool> EnsureKeyReadyAsync()
        {
            if (await encryptStickyTool.EnsureStickyKeyReadyAsync())
            {
                return true;
            }

            while (true)
            {
                PasswordBox passwordBox = new()
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

                ContentDialog errorDialog = new()
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
            // 原子操作：读取并解密全部本地便签（损坏/未解锁的自动跳过）
            List<Sticky> stickyList = await StickyService.GetStickyListAsync(UID);

            if (stickyList.Count > 0)
            {
                localSettings.Values["EmptyPlaceholderOpacity"] = 0;
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    StickyQuickViewList.Items.Clear();
                    foreach (Sticky sticky in stickyList)
                    {
                        StickyQuickViewList.Items.Add(new StickyQuickView
                        {
                            CreateTimeText = sticky.CreateTime.ToShortDateString(),
                            StickySerial = sticky.StickySerial,
                            ThemeKind = sticky.StickyTheme,
                            QucikViewText = sticky.IsLock ? "🔒 该便签已被锁定。" : sticky.QuickViewText,
                        });
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
            string serial = Guid.NewGuid().ToString("D").ToUpper();
            StickyQuickView stickyQuickView = StickyService.CreateNewStickyQuickView(serial);
            StickyQuickViewList.Items.Add(stickyQuickView);
            EmptyPlaceholder.Opacity = 0;
            localSettings.Values["EmptyPlaceholderOpacity"] = 0;

            List<object> parameter = new List<object> { "new", stickyQuickView };
            await StickyService.OpenStickyEditWindowAsync(parameter);
        }
    }
}
