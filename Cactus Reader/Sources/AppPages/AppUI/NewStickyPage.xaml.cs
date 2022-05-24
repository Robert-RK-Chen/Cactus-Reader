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
        private readonly AESEncryptTool aesEncryptTool = AESEncryptTool.Instance;
        private readonly MD5EncryptTool md5EncryptTool = MD5EncryptTool.Instance;
        private readonly ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        private readonly ThemeColorBrushTool brushTool = ThemeColorBrushTool.Instance;

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
                    string stickyText = aesEncryptTool.DecryptStringFromBytesAes(File.ReadAllText(stickyFile.Path), md5EncryptTool.GetSystemEncryptedKey(), md5EncryptTool.GetSystemEncryptedVector());
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
            SwitchStickyTheme(sticky.StickyTheme);
            StickyEditBox.Document.SetText(TextSetOptions.FormatRtf, sticky.StickyDocument);
            localSettings.Values["isSaved"] = true;
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
            sticky.StickyDocument = (document.TrimEnd());
            sticky.QuickViewText = (quickview.TrimEnd());

            string UID = localSettings.Values["UID"].ToString();
            StorageFolder stickyFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(UID, CreationCollisionOption.OpenIfExists);
            stickyFolder = await stickyFolder.CreateFolderAsync("Sticky", CreationCollisionOption.OpenIfExists);
            StorageFile stickyFile = await stickyFolder.CreateFileAsync(sticky.StickySerial + ".ctsnote", CreationCollisionOption.OpenIfExists);

            string encryptSticky = aesEncryptTool.EncryptStringToBytesAes(JsonConvert.SerializeObject(sticky), md5EncryptTool.GetSystemEncryptedKey(), md5EncryptTool.GetSystemEncryptedVector());
            File.WriteAllText(stickyFile.Path, encryptSticky);
            localSettings.Values["isSaved"] = true;
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
                quickView.ThemeKind = theme;
                quickView.TitleBackground = brushTool.GetThemeColorBrush(theme, false).TitleBrush;
                quickView.Background = brushTool.GetThemeColorBrush(theme, false).BackgroundBrush;
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
            localSettings.Values["isSaved"] = false;
            StickyEditBox.Document.GetText(TextGetOptions.None, out string text);

            await quickView.Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                quickView.QucikViewText = text.TrimEnd();
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
