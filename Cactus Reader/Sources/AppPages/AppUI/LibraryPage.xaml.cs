using Cactus_Reader.Sources.AppPages.Reader;
using System;
using System.Reflection.Metadata;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media.Animation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Cactus_Reader.Sources.AppPages.AppUI
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
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
            TextBox weblinkBox = new TextBox
            {
                Width = 360,
                PlaceholderText = "https://docs.microsoft.com/zh-cn/",
                VerticalAlignment = Windows.UI.Xaml.VerticalAlignment.Bottom,
                Header = "网络文档地址：",
            };

            ContentDialog openWebDocumentDialog = new ContentDialog
            {
                Title = "阅读网络上的文档",
                Content = weblinkBox,
                CloseButtonText = "取消",
                PrimaryButtonText = "确定",
                DefaultButton = ContentDialogButton.Primary
            };
            ContentDialogResult result = await openWebDocumentDialog.ShowAsync();

            while (result == ContentDialogResult.Primary)
            {
                string weblink = weblinkBox.Text;
                if (weblink.StartsWith("https://") || weblink.StartsWith("http://"))
                {
                    MainPage.mainPage.mainContent.Navigate(typeof(WebDocumentReadingPage), weblink, new EntranceNavigationTransitionInfo());
                    break;
                }
                else
                {
                    result = await openWebDocumentDialog.ShowAsync();
                }
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
                }
            }
            else
            {

            }
        }
    }
}
