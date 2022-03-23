using Cactus_Reader.Entities;
using Cactus_Reader.Sources.StickyNotes;
using Newtonsoft.Json;
using System;
using System.IO;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI;
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
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

        public NewStickyPage()
        {
            InitializeComponent();
            localSettings.Values["isSaved"] = false;
            Window.Current.SetTitleBar(StickyTitle);

            SystemNavigationManagerPreview.GetForCurrentView().CloseRequested += async (sender, e) =>
            {
                if (localSettings.Values["isSaved"] is false)
                {
                    var deferral = e.GetDeferral();
                    ContentDialog dialog = new ContentDialog()
                    {
                        Title = "便签内容暂未保存",
                        Content = "是否保存便签中编辑的内容？",
                        CloseButtonText = "放弃",
                        PrimaryButtonText = "保存",
                        SecondaryButtonText = "取消"
                    };
                    dialog.DefaultButton = ContentDialogButton.Primary;
                    var result = await dialog.ShowAsync();
                    switch (result)
                    {
                        case ContentDialogResult.Primary:
                            SaveStickyNote();
                            break;
                        case ContentDialogResult.Secondary:
                            e.Handled = true;
                            break;
                        case ContentDialogResult.None:
                            e.Handled = false;
                            break;
                        default:
                            break;
                    }
                    deferral.Complete();
                }
            };
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            // 便签界面接受便签序列号参数，序列号的来源为新建便签与打开的便签
            // 拿到序列号后检索便签文件，如果文件存在则读取，不存在则新建
            base.OnNavigatedTo(e);
            string serial = (string)e.Parameter;
            try
            {
                StorageFolder fileFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("Notes", CreationCollisionOption.OpenIfExists);
                StorageFile stickyFile = await fileFolder.GetFileAsync(serial + ".json");
                sticky = JsonConvert.DeserializeObject<Sticky>(File.ReadAllText(stickyFile.Path));
            }
            catch
            {
                string theme = localSettings.Values["StickyTheme"].ToString();
                sticky = new Sticky
                {
                    CreateTime = DateTime.Now,
                    StickyDocument = string.Empty,
                    StickyTheme = theme,
                    StickySerial = (string)e.Parameter,
                    QuickView = string.Empty,
                };
            }
            SwitchStickyTheme(sticky.StickyTheme);
            StickyEditBox.Document.SetText(TextSetOptions.FormatRtf, sticky.StickyDocument);
            localSettings.Values["isSaved"] = true;
        }

        private void BoldSelectText(object sender, RoutedEventArgs e)
        {
            StickyEditBox.Document.Selection.CharacterFormat.Bold = FormatEffect.Toggle;
        }

        private void ItalicSelectText(object sender, RoutedEventArgs e)
        {
            StickyEditBox.Document.Selection.CharacterFormat.Italic = FormatEffect.Toggle;
        }

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

        private void DeletelineSelectText(object sender, RoutedEventArgs e)
        {
            StickyEditBox.Document.Selection.CharacterFormat.Strikethrough = FormatEffect.Toggle;
        }

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
            sticky.StickyDocument = document;
            sticky.QuickView = quickview;

            StorageFolder storageFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("Notes", CreationCollisionOption.OpenIfExists);
            string filePath = storageFolder.Path + "\\" + sticky.StickySerial + ".json";
            File.WriteAllText(filePath, JsonConvert.SerializeObject(sticky));

            localSettings.Values["isSaved"] = true;
        }

        private async void SaveStickyNote()
        {
            StickyEditBox.Document.GetText(TextGetOptions.FormatRtf, out string document);
            StickyEditBox.Document.GetText(TextGetOptions.None, out string quickview);
            sticky.StickyDocument = document;
            sticky.QuickView = quickview;

            StorageFolder storageFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("Notes", CreationCollisionOption.OpenIfExists);
            string filePath = storageFolder.Path + "\\" + sticky.StickySerial + ".json";
            File.WriteAllText(filePath, JsonConvert.SerializeObject(sticky));

            localSettings.Values["isSaved"] = true;
        }

        private async void ChangeStickyFont(object sender, RoutedEventArgs e)
        {
            string font = ((MenuFlyoutItem)sender).Text;
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
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

        private void SwitchStickyTheme(string Theme)
        {
            ThemeColorBrush brush = new ThemeColorBrushTool().GetThemeColorBrush(Theme, false);
            StickyTitle.Background = brush.TitleBrush;
            StickyBackground.Background = brush.BackgroundBrush;
        }

        private async void DeleteSticky(object sender, RoutedEventArgs e)
        {
            NotesPage.notesPage.StickyQuickViewList.Items.Remove(this);
            StorageFolder storageFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("Notes", CreationCollisionOption.OpenIfExists);
            string filePath = storageFolder.Path + "\\" + sticky.StickySerial + ".json";
            File.Delete(filePath);
            CoreApplicationView view = CoreApplication.GetCurrentView();
            view.CoreWindow.Close();
        }

        private void StickyEditTextChanged(object sender, RoutedEventArgs e)
        {
            localSettings.Values["isSaved"] = false;
        }
    }
}
