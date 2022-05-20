using Cactus_Reader.Sources.AppPages.Reader;
using Sgml;
using System;
using System.IO;
using System.Net;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using System.Xml;
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
                Width = 400,
                PlaceholderText = "https://docs.microsoft.com/zh-cn/",
                VerticalAlignment = Windows.UI.Xaml.VerticalAlignment.Bottom,
                Header = "输入你想阅读的网页，我们将自动为你打开沉浸式阅读器。此功能尚在预览体验阶段，阅读效果视网页内容而定。",
            };

            ContentDialog openWebDocumentDialog = new ContentDialog
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
                string webContent = GetWebClient(weblink);

                if (webContent != "")
                {
                    XmlDocument document = new XmlDocument();
                    StringReader strReader = new StringReader(SgmlTranslate(webContent));
                    document.Load(strReader);

                    string contentText = GetImmersiveText(document);
                    MainPage.mainPage.mainContent.Navigate(typeof(TextFileReadingPage), contentText, new EntranceNavigationTransitionInfo());
                    break;
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

        private string GetWebClient(string url)
        {
            try
            {
                WebClient webClient = new WebClient
                {
                    Encoding = System.Text.Encoding.UTF8
                };
                return webClient.DownloadString(url);
            }
            catch (Exception)
            {
                return "";
            }
        }

        private string GetImmersiveText(XmlDocument document)
        {
            //获取五类节点：<title>、<h1>、<h2>、<h3>与<p>
            XmlNodeList titleNodes = document.GetElementsByTagName("title");
            XmlNodeList pNodes = document.GetElementsByTagName("p");
            XmlNodeList h1Nodes = document.GetElementsByTagName("h1");
            XmlNodeList h2Nodes = document.GetElementsByTagName("h2");
            XmlNodeList h3Nodes = document.GetElementsByTagName("h3");

            string contentText = string.Empty;
            foreach (XmlElement element in titleNodes)
            {
                string text = element.InnerText.TrimStart().TrimEnd();
                if (text.Length > 0)
                {
                    contentText += text + "\n\n";
                }
            }
            foreach (XmlElement element in h1Nodes)
            {
                string text = element.InnerText.TrimStart().TrimEnd();
                if (text.Length > 0)
                {
                    contentText += text + "\n\n";
                }
            }
            foreach (XmlElement element in h2Nodes)
            {
                string text = element.InnerText.TrimStart().TrimEnd();
                if (text.Length > 0)
                {
                    contentText += text + "\n\n";
                }
            }
            foreach (XmlElement element in h3Nodes)
            {
                string text = element.InnerText.TrimStart().TrimEnd();
                if (text.Length > 0)
                {
                    contentText += text + "\n\n";
                }
            }
            foreach (XmlElement element in pNodes)
            {
                string text = element.InnerText.TrimStart().TrimEnd();
                if (text.Length > 0)
                {
                    contentText += text + "\n\n";
                }
            }
            return contentText;
        }

        private string SgmlTranslate(string input)
        {
            var reader = new SgmlReader
            {
                DocType = "HTML",
                WhitespaceHandling = WhitespaceHandling.None,
                CaseFolding = Sgml.CaseFolding.ToLower,
                InputStream = new StringReader(input)
            };

            var output = new StringWriter();
            var writer = new XmlTextWriter(output)
            {
                Formatting = Formatting.Indented
            };
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Whitespace
                  && reader.NodeType != XmlNodeType.Comment)
                    writer.WriteNode(reader, true);
            }
            writer.Close();
            return output.ToString();
        }
    }
}
