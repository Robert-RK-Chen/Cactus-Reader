using Cactus_Reader.Entities;
using Cactus_Reader.Sources.StickyNotes;
using Cactus_Reader.Sources.ToolKits;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Core.Preview;
using Windows.UI.Text;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Cactus_Reader.Sources.AppPages.AppUI
{
    public sealed partial class NewStickyPage : Page
    {
        private Sticky sticky;
        private StickyQuickView quickView;
        private readonly ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        private readonly ThemeColorBrushTool brushTool = ThemeColorBrushTool.Instance;

        /// <summary>窗口模式："new" 新建 / "open" 打开已有。</summary>
        private string mode;

        /// <summary>当前内容是否已保存（实例状态，多窗口互不干扰）。</summary>
        private bool isSaved;

        /// <summary>加载完成时的纯文本快照，用于区分真实编辑与 SetText 触发的延迟 TextChanged。</summary>
        private string loadedPlainText = string.Empty;

        public NewStickyPage()
        {
            InitializeComponent();

            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = true;
            ApplicationViewTitleBar titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.ButtonForegroundColor = Colors.Black;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonHoverForegroundColor = Colors.Black;
            titleBar.ButtonHoverBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonPressedBackgroundColor = Colors.Transparent;
            titleBar.ButtonPressedBackgroundColor = Colors.Transparent;
            Window.Current.SetTitleBar(StickyTitle);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            SystemNavigationManagerPreview.GetForCurrentView().CloseRequested -= StickyPageCloseRequested;
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            // 新建走新建流程，打开则读取并解密
            base.OnNavigatedTo(e);
            SystemNavigationManagerPreview.GetForCurrentView().CloseRequested += StickyPageCloseRequested;

            List<object> parameter = (List<object>)e.Parameter;
            mode = (string)parameter[0];
            quickView = (StickyQuickView)parameter[1];
            sticky = await LoadStickyAsync(mode, quickView);

            // 同步主题（内部已分别调度本视图与主窗口线程，任意线程可调用）
            await SwitchStickyThemeAsync(sticky.StickyTheme);

            // 编辑器初始化必须在本视图 UI 线程：辅助视图无 SynchronizationContext，
            // async/await 延续不会回到视图线程，故用非 async 的 RunAsync 显式调度，
            // 且 lambda 内不做任何 await，避免延续线程漂移后再访问 UI 元素
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                StickyEditBox.Document.SetText(TextSetOptions.FormatRtf, sticky.StickyDocument);
                StickyEditBox.Document.GetText(TextGetOptions.None, out string plain);
                loadedPlainText = plain.TrimEnd();
                isSaved = true;
            });
        }

        /// <summary>读取卡片序列号：StickySerial 是 DP，须在 quickView 所属线程（主窗口）上读取。</summary>
        private static async Task<string> GetStickySerialAsync(StickyQuickView stickyQuickView)
        {
            string serial = string.Empty;
            await stickyQuickView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                serial = stickyQuickView.StickySerial;
            });
            return serial;
        }

        /// <summary>加载便签：新建模式创建空便签；打开模式读取并解密（失败时回退为新便签）。</summary>
        private async Task<Sticky> LoadStickyAsync(string mode, StickyQuickView stickyQuickView)
        {
            string serial = await GetStickySerialAsync(stickyQuickView);

            if (mode == "new")
            {
                return StickyService.CreateSticky(serial);
            }

            string UID = localSettings.Values["UID"]?.ToString();
            // 读取并解密，文件缺失/解密失败时回退为新便签
            return await StickyService.LoadStickyAsync(UID, serial) ?? StickyService.CreateSticky(serial);
        }

        // 加粗所选文本
        private void BoldSelectText(object sender, RoutedEventArgs e)
        {
            StickyEditBox.Document.Selection.CharacterFormat.Bold = FormatEffect.Toggle;
        }

        // 倾斜所选文本
        private void ItalicSelectText(object sender, RoutedEventArgs e)
        {
            StickyEditBox.Document.Selection.CharacterFormat.Italic = FormatEffect.Toggle;
        }

        // 下划线所选文本
        private void UnderlineSelectText(object sender, RoutedEventArgs e)
        {
            if (StickyEditBox.Document.Selection.CharacterFormat.Underline == UnderlineType.Single)
            {
                StickyEditBox.Document.Selection.CharacterFormat.Underline = UnderlineType.None;
            }
            else
            {
                StickyEditBox.Document.Selection.CharacterFormat.Underline = UnderlineType.Single;
            }
        }

        // 删除线所选文本
        private void DeletelineSelectText(object sender, RoutedEventArgs e)
        {
            StickyEditBox.Document.Selection.CharacterFormat.Strikethrough = FormatEffect.Toggle;
        }

        // 高亮所选文本
        private void HighlightSelectText(object sender, RoutedEventArgs e)
        {
            Button clickedColor = (Button)sender;
            var rectangle = (Windows.UI.Xaml.Shapes.Rectangle)clickedColor.Content;
            var color = ((SolidColorBrush)rectangle.Fill).Color;

            StickyEditBox.Document.Selection.CharacterFormat.BackgroundColor = color;

            HighlightButton.Flyout.Hide();
            StickyEditBox.Focus(FocusState.Keyboard);
        }

        private async void SaveStickyNote(object sender, RoutedEventArgs e)
        {
            await SaveStickyInternalAsync();
        }

        /// <summary>保存便签：规范化 RTF → 加密落盘 → 上传云端（受同步开关控制）。</summary>
        private async Task SaveStickyInternalAsync()
        {
            StickyEditBox.Document.GetText(TextGetOptions.FormatRtf, out string document);
            StickyEditBox.Document.GetText(TextGetOptions.None, out string quickview);

            string UID = localSettings.Values["UID"]?.ToString();
            try
            {
                await StickyService.SaveStickyAsync(UID, sticky, document, quickview);
                isSaved = true;
            }
            catch (EncryptStickyTool.VaultKeyRequiredException)
            {
                // 密钥未就绪（理论上登录/创建前已检查，此处兜底）：提示用户先完成解锁，避免崩溃
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    ContentDialog dialog = new ContentDialog
                    {
                        Title = "无法保存便签",
                        Content = "便签加密密钥尚未解锁，请先回到便签本完成密码验证后再保存。",
                        CloseButtonText = "确定"
                    };
                    await dialog.ShowAsync();
                });
            }
        }

        private async void ChangeStickyFont(object sender, RoutedEventArgs e)
        {
            string font = ((MenuFlyoutItem)sender).Text;
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                StickyEditBox.FontFamily = new FontFamily(font);
            });
        }

        private async void ChangeStickyTheme(object sender, RoutedEventArgs e)
        {
            string theme = ((MenuFlyoutItem)sender).Tag.ToString();
            await SwitchStickyThemeAsync(theme);
            sticky.StickyTheme = theme;
            StickyService.SetStickyTheme(theme);
        }

        /// <summary>
        /// 切换便签主题：本视图元素与主窗口卡片分别用各自的 Dispatcher 调度，
        /// SolidColorBrush 的创建与赋值都在目标 UI 线程内完成。
        /// 辅助视图无 SynchronizationContext，async/await 延续可能在线程池线程，
        /// 因此本方法不假定调用线程，所有 UI 操作（含 brush 创建）均显式 RunAsync。
        /// </summary>
        private async Task SwitchStickyThemeAsync(string theme)
        {
            isSaved = false;

            // 编辑窗口自身元素：brush 创建 + 赋值都在本视图 UI 线程完成
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                ThemeColorBrush brush = brushTool.GetThemeColorBrush(theme, false);
                StickyTitle.Background = brush.TitleBrush;
                StickyBackground.Background = brush.BackgroundBrush;
            });

            // 卡片元素属于主窗口线程：brush 创建 + 赋值都在主窗口线程完成
            await quickView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                ThemeColorBrush cardBrush = brushTool.GetThemeColorBrush(theme, false);
                // ThemeKind 自动同步画笔，此处显式赋值以覆盖悬停遗留状态
                quickView.ThemeKind = theme;
                quickView.TitleBackground = cardBrush.TitleBrush;
                quickView.ViewBackground = cardBrush.BackgroundBrush;
            });
        }

        private async void DeleteSticky(object sender, RoutedEventArgs e)
        {
            try
            {
                string UID = localSettings.Values["UID"]?.ToString();
                // 本地删除 + 云端删除（同步关闭时仅删本地）
                await StickyService.DeleteStickyAsync(UID, sticky.StickySerial);

                // 从主窗口列表移除卡片（RemoveQuickView 内部自动调度回主窗口线程）
                StickyPage.RemoveQuickView(quickView);

                // 关闭编辑窗口（CoreWindow.Close 必须在本视图线程调用）
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    CoreApplication.GetCurrentView().CoreWindow.Close();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("删除便签失败：" + ex.Message);
            }
        }

        private async void StickyEditTextChanged(object sender, RoutedEventArgs e)
        {
            StickyEditBox.Document.GetText(TextGetOptions.None, out string text);
            string trimmed = text.TrimEnd();

            // SetText 触发的 TextChanged 延迟到达，用内容比较判断是否为真实编辑
            if (string.Equals(trimmed, loadedPlainText, StringComparison.Ordinal))
            {
                return;
            }

            isSaved = false;

            await quickView.Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                quickView.QuickViewText = trimmed;
            });
        }

        /// <summary>丢弃未保存的编辑：本地无文件（新建且未保存）时移除卡片，已保存过的保留卡片。</summary>
        private async Task CancelSaveStickyAsync()
        {
            try
            {
                string UID = localSettings.Values["UID"]?.ToString();
                StorageFolder stickyFolder = await StickyService.GetStickyFolderAsync(UID);
                StorageFile stickyFile = await stickyFolder.TryGetItemAsync(sticky.StickySerial + ".ctsnote") as StorageFile;
                if (stickyFile != null)
                {
                    return; // 已保存过，保留卡片
                }
            }
            catch (Exception)
            {
                return; // 目录异常时保守保留卡片
            }

            // 新建未保存：从列表移除卡片（跨线程自动调度）
            StickyPage.RemoveQuickView(quickView);
        }

        private async void StickyPageCloseRequested(object sender, SystemNavigationCloseRequestedPreviewEventArgs e)
        {
            // 新建便签且未输入任何内容：直接丢弃并删除卡片，无需确认（空便签没有保留价值）
            if (mode == "new" && IsEmptyContent())
            {
                var deferral = e.GetDeferral();
                try
                {
                    await DiscardUnsavedChangesAsync();
                }
                finally
                {
                    deferral.Complete();
                }
                return;
            }

            if (!isSaved)
            {
                var deferral = e.GetDeferral();
                try
                {
                    ContentDialogResult result = await ShowUnsavedDialogAsync();
                    switch (result)
                    {
                        case ContentDialogResult.Primary:
                            // 保存完成后 deferral.Complete()，e.Handled 保持 false → 窗口正常关闭
                            await SaveStickyInternalAsync();
                            break;
                        case ContentDialogResult.Secondary:
                            e.Handled = true; // 返回编辑
                            break;
                        case ContentDialogResult.None:
                            await DiscardUnsavedChangesAsync();
                            break;
                    }
                }
                finally
                {
                    deferral.Complete();
                }
            }
        }

        /// <summary>编辑区是否为空（无任何文字）。须在本视图 UI 线程调用（关闭事件即在此线程触发）。</summary>
        private bool IsEmptyContent()
        {
            StickyEditBox.Document.GetText(TextGetOptions.None, out string text);
            return string.IsNullOrWhiteSpace(text.TrimEnd());
        }

        /// <summary>弹出"便签内容暂未保存"确认对话框。</summary>
        private async Task<ContentDialogResult> ShowUnsavedDialogAsync()
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = "便签内容暂未保存",
                Content = "是否保存便签中编辑的内容？",
                CloseButtonText = "丢弃",
                PrimaryButtonText = "保存",
                SecondaryButtonText = "返回",
                DefaultButton = ContentDialogButton.Primary
            };
            return await dialog.ShowAsync();
        }

        /// <summary>丢弃未保存的编辑：恢复卡片预览文本并取消保存状态。</summary>
        private async Task DiscardUnsavedChangesAsync()
        {
            await quickView.Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                quickView.QuickViewText = sticky.QuickViewText;
            });
            await CancelSaveStickyAsync();
        }
    }
}
