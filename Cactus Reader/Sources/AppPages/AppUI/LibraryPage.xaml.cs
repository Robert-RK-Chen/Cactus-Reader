using Cactus_Reader.Entities.EpubEntities;
using Cactus_Reader.Sources.AppPages.Reader;
using Cactus_Reader.Sources.ToolKits;
using System;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;

namespace Cactus_Reader.Sources.AppPages.AppUI
{
    public sealed partial class LibraryPage : Page
    {
        public LibraryPage()
        {
            InitializeComponent();
        }

        private void OpenDocumentFile(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            OpenDocumentFile();
        }

        private void OpenDocumentFile(Microsoft.UI.Xaml.Controls.SplitButton sender, Microsoft.UI.Xaml.Controls.SplitButtonClickEventArgs args)
        {
            OpenDocumentFile();
        }

        private async void OpenWebDocument(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            TextBox weblinkBox = new()
            {
                Width = 400,
                PlaceholderText = "https://docs.microsoft.com/zh-cn/",
                VerticalAlignment = Windows.UI.Xaml.VerticalAlignment.Bottom,
                Header = "输入你想阅读的网页，我们将自动为你打开沉浸式阅读器。此功能尚在预览体验阶段，阅读效果视网页内容而定。",
            };

            ContentDialog openWebDocumentDialog = new()
            {
                Title = "Cactus Web Reader (Preview)",
                Content = weblinkBox,
                CloseButtonText = "取消",
                PrimaryButtonText = "确定",
                DefaultButton = ContentDialogButton.Primary
            };
            ContentDialogResult result = await openWebDocumentDialog.ShowAsync();

            while (result == ContentDialogResult.Primary)
            {
                string weblink = weblinkBox.Text;
                // 原子操作：下载网页 → Sgml 转 XML → 提取沉浸式正文
                string contentText = WebReaderService.FetchWebPage(weblink);

                if (contentText.Length > 0)
                {
                    MainPage.mainPage.mainContent.Navigate(typeof(TextFileReadingPage), contentText, new EntranceNavigationTransitionInfo());
                    break;
                }
                result = await openWebDocumentDialog.ShowAsync();
            }
        }

        private async void OpenDocumentFile()
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker
            {
                ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail,
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeFilter.Add(".cts");
            picker.FileTypeFilter.Add(".epub");
            picker.FileTypeFilter.Add(".pdf");
            picker.FileTypeFilter.Add(".rtf");
            picker.FileTypeFilter.Add(".txt");
            StorageFile document = await picker.PickSingleFileAsync();

            if (document != null)
            {
                switch (document.FileType)
                {
                    case ".txt":
                        MainPage.mainPage.mainContent.Navigate(typeof(TextFileReadingPage), document, new EntranceNavigationTransitionInfo());
                        break;
                    case ".epub":
                        BookInfo bookInfo = new BookInfo(document);
                        MainPage.mainPage.mainContent.Navigate(typeof(EpubFileReadingPage), bookInfo, new EntranceNavigationTransitionInfo());
                        break;
                }
            }
            else
            {

            }
        }
    }
}
