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
        private bool suppressTextChanged;
        private string loadedPlainText = string.Empty;

        public NewStickyPage()
        {
            InitializeComponent();
            localSettings.Values["isSaved"] = false;

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
            quickView = (StickyQuickView)parameter[1];
            sticky = await LoadStickyAsync((string)parameter[0], quickView);

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                SwitchStickyTheme(sticky.StickyTheme);
                suppressTextChanged = true;
                StickyEditBox.Document.SetText(TextSetOptions.FormatRtf, sticky.StickyDocument);
                StickyEditBox.Document.GetText(TextGetOptions.None, out string plain);
                loadedPlainText = plain.TrimEnd();
                localSettings.Values["isSaved"] = true;
                suppressTextChanged = false;
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

            string UID = localSettings.Values["UID"].ToString();
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
            StickyEditBox.Document.GetText(TextGetOptions.FormatRtf, out string document);
            StickyEditBox.Document.GetText(TextGetOptions.None, out string quickview);

            string UID = localSettings.Values["UID"].ToString();
            // 规范化 RTF → 加密落盘 → 上传云端（受同步开关控制）
            await StickyService.SaveStickyAsync(UID, sticky, document, quickview);
        }

        private async void ChangeStickyFont(object sender, RoutedEventArgs e)
        {
            string font = ((MenuFlyoutItem)sender).Text;
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                StickyEditBox.FontFamily = new FontFamily(font);
            });
        }

        private void ChangeStickyTheme(object sender, RoutedEventArgs e)
        {
            string theme = ((MenuFlyoutItem)sender).Tag.ToString();
            SwitchStickyTheme(theme);
            sticky.StickyTheme = theme;
            StickyService.SetStickyTheme(theme);
        }

        private async void SwitchStickyTheme(string theme)
        {
            localSettings.Values["isSaved"] = false;

            StickyTitle.Background = brushTool.GetThemeColorBrush(theme, false).TitleBrush;
            StickyBackground.Background = brushTool.GetThemeColorBrush(theme, false).BackgroundBrush;
            await quickView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // ThemeKind 自动同步画笔，此处显式赋值以覆盖悬停遗留状态
                quickView.ThemeKind = theme;
                quickView.TitleBackground = brushTool.GetThemeColorBrush(theme, false).TitleBrush;
                quickView.ViewBackground = brushTool.GetThemeColorBrush(theme, false).BackgroundBrush;
            });
        }

        private async void DeleteSticky(object sender, RoutedEventArgs e)
        {
            try
            {
                string UID = localSettings.Values["UID"].ToString();
                // 本地删除 + 云端删除（同步关闭时仅删本地）
                await StickyService.DeleteStickyAsync(UID, sticky.StickySerial);

                // 从主窗口列表移除卡片（quickView 与列表同属主窗口线程，一个 Dispatcher 即可）
                await StickyPage.stickyPage.StickyQuickViewList.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    StickyPage.stickyPage.StickyQuickViewList.Items.Remove(quickView);
                    if (StickyPage.stickyPage.StickyQuickViewList.Items.Count == 0)
                    {
                        StickyPage.stickyPage.EmptyPlaceholder.Opacity = 1;
                        localSettings.Values["EmptyPlaceholderOpacity"] = 1;
                    }
                });

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    CoreApplicationView view = CoreApplication.GetCurrentView();
                    view.CoreWindow.Close();
                });
            }
            catch (Exception)
            {
                // 防止 async void 未捕获异常导致视图崩溃
            }
        }

        private async void StickyEditTextChanged(object sender, RoutedEventArgs e)
        {
            StickyEditBox.Document.GetText(TextGetOptions.None, out string text);
            string trimmed = text.TrimEnd();

            // SetText 触发的 TextChanged 延迟到达（标志位已复位），改用内容比较判断是否编辑
            if (suppressTextChanged || string.Equals(trimmed, loadedPlainText, StringComparison.Ordinal))
            {
                return;
            }

            localSettings.Values["isSaved"] = false;

            await quickView.Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                quickView.QucikViewText = trimmed;
            });
        }

        private async void CancelSaveSticky()
        {
            try
            {
                string UID = localSettings.Values["UID"].ToString();
                StorageFolder stickyFolder = await ApplicationData.Current.LocalFolder.GetFolderAsync(UID);
                stickyFolder = await stickyFolder.GetFolderAsync("Sticky");
                StorageFile stickyFile = await stickyFolder.GetFileAsync(sticky.StickySerial + ".ctsnote");
            }
            catch (Exception)
            {
                try
                {
                    await StickyPage.stickyPage.StickyQuickViewList.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    async () =>
                    {
                        await quickView.Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
                        {
                            StickyPage.stickyPage.StickyQuickViewList.Items.Remove(quickView);
                            if (StickyPage.stickyPage.StickyQuickViewList.Items.Count == 0)
                            {
                                StickyPage.stickyPage.EmptyPlaceholder.Opacity = 1;
                                localSettings.Values["EmptyPlaceholderOpacity"] = 1;
                            }
                        });
                    });
                }
                catch (Exception)
                {
                }
            }
        }

        private async void StickyPageCloseRequested(object sender, SystemNavigationCloseRequestedPreviewEventArgs e)
        {
            if (localSettings.Values["isSaved"] is false)
            {
                var deferral = e.GetDeferral();
                ContentDialogResult result = await ShowUnsavedDialogAsync();
                switch (result)
                {
                    case ContentDialogResult.Primary:
                        SaveStickyNote(null, null);
                        break;
                    case ContentDialogResult.Secondary:
                        e.Handled = true;
                        break;
                    case ContentDialogResult.None:
                        await DiscardUnsavedChangesAsync();
                        e.Handled = false;
                        break;
                    default:
                        break;
                }
                deferral.Complete();
            }
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
                quickView.QucikViewText = sticky.QuickViewText;
            });
            CancelSaveSticky();
        }
    }
}
