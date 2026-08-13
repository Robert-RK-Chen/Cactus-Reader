using Cactus_Reader.Entities;
using Cactus_Reader.Sources.StickyNotes;
using Cactus_Reader.Sources.ToolKits;
using System;
using System.Collections.Generic;
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

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Cactus_Reader.Sources.AppPages.AppUI
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
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
            // 接收传递的参数，若是新建，走新建流程
            // 若是打开，则先解密
            base.OnNavigatedTo(e);
            SystemNavigationManagerPreview.GetForCurrentView().CloseRequested += StickyPageCloseRequested;

            string serial = string.Empty;
            List<object> parameter = (List<object>)e.Parameter;
            quickView = (StickyQuickView)parameter[1];
            // StickySerial 是 DependencyProperty，必须在 quickView 所属线程（StickyPage 主窗口线程）上读取
            await quickView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                serial = quickView.StickySerial;
            });

            if ((string)parameter[0] == "new")
            {
                sticky = StickyService.CreateSticky(serial);
            }
            else
            {
                string UID = localSettings.Values["UID"].ToString();
                // 原子操作：读取 + 解密；文件缺失/解密失败回退为新便签
                sticky = await StickyService.LoadStickyAsync(UID, serial) ?? StickyService.CreateSticky(serial);
            }

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

        // 加粗所选的便签文本
        private void BoldSelectText(object sender, RoutedEventArgs e)
        {
            StickyEditBox.Document.Selection.CharacterFormat.Bold = FormatEffect.Toggle;
        }

        // 倾斜所选的便签文本
        private void ItalicSelectText(object sender, RoutedEventArgs e)
        {
            StickyEditBox.Document.Selection.CharacterFormat.Italic = FormatEffect.Toggle;
        }

        // 下划线所选的便签文本
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

        // 所选的便签文本增加删除线
        private void DeletelineSelectText(object sender, RoutedEventArgs e)
        {
            StickyEditBox.Document.Selection.CharacterFormat.Strikethrough = FormatEffect.Toggle;
        }

        //高亮所选的便签文本
        private void HighlightSelectText(object sender, RoutedEventArgs e)
        {
            Button clickedColor = (Button)sender;
            var rectangle = (Windows.UI.Xaml.Shapes.Rectangle)clickedColor.Content;
            var color = ((SolidColorBrush)rectangle.Fill).Color;

            StickyEditBox.Document.Selection.CharacterFormat.BackgroundColor = color;

            HighlightButton.Flyout.Hide();
            StickyEditBox.Focus(FocusState.Keyboard);
        }

        // 保存便签
        private async void SaveStickyNote(object sender, RoutedEventArgs e)
        {
            StickyEditBox.Document.GetText(TextGetOptions.FormatRtf, out string document);
            StickyEditBox.Document.GetText(TextGetOptions.None, out string quickview);

            string UID = localSettings.Values["UID"].ToString();
            // 原子操作：规范化 RTF → 加密落盘 → 上传云端（受同步开关控制）
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
                // ThemeKind 变更会自动同步 TitleBackground/ViewBackground，这里显式赋值以覆盖悬停遗留状态
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
                // 原子操作：本地删除 + 云端删除（同步关闭时仅删本地）
                await StickyService.DeleteStickyAsync(UID, sticky.StickySerial);

                // 从主窗口便签列表移除卡片（quickView 与 StickyQuickViewList 同属主窗口，一个 Dispatcher 即可）
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
                // 兜底：防止 async void 未捕获异常导致视图崩溃
            }
        }

        private async void StickyEditTextChanged(object sender, RoutedEventArgs e)
        {
            StickyEditBox.Document.GetText(TextGetOptions.None, out string text);
            string trimmed = text.TrimEnd();

            // 加载便签内容时 SetText 触发的 TextChanged 是延迟异步到达的（标志位已复位），
            // 因此这里用内容比较：与加载时的初始内容一致则视为未编辑，不标记为已修改
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
                ContentDialog dialog = new ContentDialog
                {
                    Title = "便签内容暂未保存",
                    Content = "是否保存便签中编辑的内容？",
                    CloseButtonText = "丢弃",
                    PrimaryButtonText = "保存",
                    SecondaryButtonText = "返回",
                    DefaultButton = ContentDialogButton.Primary
                };
                var result = await dialog.ShowAsync();
                switch (result)
                {
                    case ContentDialogResult.Primary:
                        SaveStickyNote(null, null);
                        break;
                    case ContentDialogResult.Secondary:
                        e.Handled = true;
                        break;
                    case ContentDialogResult.None:
                        await quickView.Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
                        {
                            quickView.QucikViewText = sticky.QuickViewText;
                        });
                        CancelSaveSticky();
                        e.Handled = false;
                        break;
                    default:
                        break;
                }
                deferral.Complete();
            }
        }
    }
}
