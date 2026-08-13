using Cactus_Reader.Entities;
using Cactus_Reader.Sources.StickyNotes;
using Cactus_Reader.Sources.ToolKits;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
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
        private ProfileUploadTool profileUploadTool = ProfileUploadTool.Instance;
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
                sticky = new Sticky
                {
                    IsLock = false,
                    CreateTime = DateTime.Now,
                    StickyDocument = string.Empty,
                    StickyTheme = localSettings.Values["StickyTheme"].ToString(),
                    StickySerial = serial,
                    QuickViewText = string.Empty,
                };
            }
            else
            {
                try
                {
                    string UID = localSettings.Values["UID"].ToString();
                    StorageFolder stickyFolder = await ApplicationData.Current.LocalFolder.GetFolderAsync(UID);
                    stickyFolder = await stickyFolder.GetFolderAsync("Sticky");
                    StorageFile stickyFile = await stickyFolder.GetFileAsync(serial + ".ctsnote");
                    string stickyText = EncryptStickyTool.Instance.DecryptStickyText(File.ReadAllText(stickyFile.Path));
                    sticky = JsonConvert.DeserializeObject<Sticky>(stickyText);
                }
                catch
                {
                    string theme = localSettings.Values["StickyTheme"].ToString();
                    sticky = new Sticky
                    {
                        IsLock = false,
                        CreateTime = DateTime.Now,
                        StickyDocument = string.Empty,
                        StickyTheme = theme,
                        StickySerial = sticky.StickySerial,
                        QuickViewText = string.Empty,
                    };
                }
            }
            // 本视图为辅助窗口（CoreApplication.CreateNewView 创建），其 UI 线程没有安装
            // SynchronizationContext，async/await 的 continuation 不会自动回到 UI 线程；
            // 因此所有 UI 更新必须显式调度到本视图的 Dispatcher，否则会抛出
            // RPC_E_WRONG_THREAD (0x8001010E) 跨线程访问异常。
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                SwitchStickyTheme(sticky.StickyTheme);
                // 记录加载后的初始纯文本，供 TextChanged 内容比较（SetText 的 TextChanged 是延迟异步触发的，
                // 无法靠标志位可靠抑制，只能用内容是否一致来判断是否真实编辑）
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
            // 去掉 RTF 末尾段落标记，避免恢复时多出一个空段落（每次打开末尾多一行空行）
            sticky.StickyDocument = NormalizeRtfForSave(document);
            sticky.QuickViewText = (quickview.TrimEnd());

            string UID = localSettings.Values["UID"].ToString();
            StorageFolder stickyFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(UID, CreationCollisionOption.OpenIfExists);
            stickyFolder = await stickyFolder.CreateFolderAsync("Sticky", CreationCollisionOption.OpenIfExists);
            StorageFile stickyFile = await stickyFolder.CreateFileAsync(sticky.StickySerial + ".ctsnote", CreationCollisionOption.OpenIfExists);

            string encryptSticky = EncryptStickyTool.Instance.EncryptStickyText(JsonConvert.SerializeObject(sticky));
            File.WriteAllText(stickyFile.Path, encryptSticky);
            localSettings.Values["isSaved"] = true;

            profileUploadTool.UploadCactusNotes(stickyFile, UID, stickyFile.Name, "/upload-cactus-notes");
        }

        /// <summary>
        /// 规范化 RTF 以便保存：RichEditBox 输出的 RTF 始终以段落标记 \par 结尾（文档至少含一个段落），
        /// 若原样保存，SetText 恢复时会额外生成一个空段落，导致每次打开便签末尾多一行空行，
        /// 并因内容不一致触发 TextChanged 被误判为已修改。此处仅移除文档末尾紧跟右大括号前的最后一个 \par，
        /// 用户有意输入的空行（\par\par）会保留。
        /// </summary>
        private static string NormalizeRtfForSave(string rtf)
        {
            rtf = rtf.TrimEnd();
            int closeBrace = rtf.LastIndexOf('}');
            if (closeBrace < 0)
            {
                return rtf;
            }

            string head = rtf.Substring(0, closeBrace).TrimEnd();
            if (head.EndsWith("\\par", StringComparison.Ordinal))
            {
                return head.Substring(0, head.Length - 4) + rtf.Substring(closeBrace);
            }
            return rtf;
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
            localSettings.Values["StickyTheme"] = theme;
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
                StorageFolder stickyFolder = await ApplicationData.Current.LocalFolder.GetFolderAsync(UID);
                stickyFolder = await stickyFolder.CreateFolderAsync("Sticky", CreationCollisionOption.OpenIfExists);
                StorageFile stickyFile = await stickyFolder.CreateFileAsync(sticky.StickySerial + ".ctsnote", CreationCollisionOption.OpenIfExists);
                await stickyFile.DeleteAsync();

                // 同步删除服务端存档，避免下次同步时便签被重新下载（同步关闭时仅删本地，云端残留会在下次开启全量同步时清理）
                if (ProfileSyncTool.IsSyncEnabled())
                {
                    try
                    {
                        // 服务端文件名为 {StickySerial}.ctsnote，删除时需带扩展名
                        await ApiClient.DeleteNoteAsync(UID, sticky.StickySerial + ".ctsnote");
                    }
                    catch (Exception)
                    {
                        // 网络异常时忽略：服务端残留会在下次同步时被拉回，属预期降级行为
                    }
                }
            }
            catch (Exception) { }

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

            CoreApplicationView view = CoreApplication.GetCurrentView();
            view.CoreWindow.Close();
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
                        // 丢弃修改：把便签列表预览恢复为磁盘上保存的内容
                        // （编辑过程中 StickyEditTextChanged 会实时更新 quickView 预览，
                        //   此时未保存，需回滚，避免列表显示未保存的残留内容）
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
