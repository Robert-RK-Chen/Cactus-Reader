using Cactus_Reader.Entities.EpubEntities;
using Cactus_Reader.Sources.AppPages.AppUI;
using Cactus_Reader.Sources.StickyNotes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Xml.Dom;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Cactus_Reader.Sources.AppPages.Reader
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class EpubFileReadingPage : Page
    {
        public ObservableCollection<Chapter> Chapters { get; private set; }
        BookInfo bookInfo = null;
        private string readerStyle = "<body style=\"font-family: MiSans; line-height: 2; font-size: 18px; margin: 36px; letter-spacing: 2px; background-color: #fbf7f0; font-weight: SemiBold;\" ";

        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        readonly ThemeColorBrushTool brushTool = ThemeColorBrushTool.Instance;

        public EpubFileReadingPage()
        {
            Chapters = new ObservableCollection<Chapter>();
            this.InitializeComponent();
            if (localSettings.Values["StickyTheme"] == null) { localSettings.Values["StickyTheme"] = "GingkoYellow"; }

            var titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            // Hide default title bar.
            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = true;
            UpdateTitleBarLayout(coreTitleBar);

            // Set XAML element as a draggable region.
            Window.Current.SetTitleBar(appTitleBar);

            // Register a handler for when the size of the overlaid caption control changes.
            // For example, when the app moves to a screen with a different DPI.
            coreTitleBar.LayoutMetricsChanged += CoreTitleBarLayoutMetricsChanged;

            // Register a handler for when the title bar visibility changes.
            // For example, when the title bar is invoked in full screen mode.
            coreTitleBar.IsVisibleChanged += CoreTitleBarIsVisibleChanged;

            DataTransferManager dataTransferManager = DataTransferManager.GetForCurrentView();
            dataTransferManager.DataRequested += DataTransferManagerDataRequested;
        }

        private void DataTransferManagerDataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            var chapter = ChapterPivot.SelectedItem as Chapter;
            DataRequest request = args.Request;
            request.Data.SetWebLink(chapter.Uri);
            request.Data.Properties.Title = "Robert Chen";
            request.Data.Properties.Description = "Cactus Reader";
        }

        private void ShareNearBy(object sender, RoutedEventArgs e)
        {
            DataTransferManager.ShowShareUI();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            bookInfo = e.Parameter as BookInfo;
            try
            {
                Chapters.Clear();
                await OpenBook(bookInfo.BookFile);
                ChapterPivot.SelectedIndex = bookInfo.Chapter;
            }
            catch (Exception)
            {
            }
        }

        private void CoreTitleBarLayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            UpdateTitleBarLayout(sender);
        }

        private void UpdateTitleBarLayout(CoreApplicationViewTitleBar coreTitleBar)
        {
            // Update title bar control size as needed to account for system size changes.
            appTitleBar.Height = coreTitleBar.Height;

            // Ensure the custom title bar does not overlap window caption controls
            Thickness currMargin = appTitleBar.Margin;
            appTitleBar.Margin = new Thickness(currMargin.Left, currMargin.Top, coreTitleBar.SystemOverlayRightInset, currMargin.Bottom);
        }

        private void CoreTitleBarIsVisibleChanged(CoreApplicationViewTitleBar sender, object args)
        {
            if (sender.IsVisible)
            {
                appTitleBar.Visibility = Visibility.Visible;
            }
            else
            {
                appTitleBar.Visibility = Visibility.Collapsed;
            }
        }

        private async Task OpenBook(StorageFile bookFile)
        {
            //copy book to temp
            var bookname = bookInfo.BookFile.Name;
            var tempFolder = ApplicationData.Current.TemporaryFolder;
            var tempFile = await bookFile.CopyAsync(tempFolder, bookname, NameCollisionOption.ReplaceExisting);

            // extract book to a subfolder
            var tempSubFolder = await tempFolder.CreateFolderAsync(bookFile.DisplayName, CreationCollisionOption.ReplaceExisting);
            await Task.Run(() =>
            {
                System.IO.Compression.ZipFile.ExtractToDirectory(tempFile.Path, tempSubFolder.Path);
            });

            // get meta-data container
            var doc = new XmlDocument();
            var metaFolder = await tempSubFolder.GetFolderAsync("META-INF");
            var metaFileText = "";
            var metaFile = await metaFolder.GetFileAsync("container.xml");
            metaFileText = await FileIO.ReadTextAsync(metaFile);
            doc.LoadXml(metaFileText);
            var contentFilePath = doc.SelectSingleNode("//*[local-name()='rootfile']/@*[local-name()='full-path']").NodeValue.ToString();

            var contentFolder = await GetContentFolder(contentFilePath, tempSubFolder);
            contentFilePath = StripPathFromContentFilePath(contentFilePath);

            // get content file
            var contentFileText = "";
            var contentFile = await contentFolder.GetFileAsync(contentFilePath);
            contentFileText = await FileIO.ReadTextAsync(contentFile);
            doc.LoadXml(contentFileText);
            var tocFilePath = string.Empty;
            var contentPaths = doc.SelectNodes("//*[local-name()='item']/@*[local-name()='href']");
            for (uint index = 0; index < contentPaths.Length; index++)
            {
                tocFilePath = contentPaths.Item(index).NodeValue.ToString();
                if (tocFilePath.EndsWith(".ncx")) break;
            }

            // get table-of-contents file text
            var tocFileText = "";
            var tocFile = await contentFolder.GetFileAsync(tocFilePath);
            tocFileText = await FileIO.ReadTextAsync(tocFile);

            // add all chapters from the tocs list to the XAML bindable collection
            doc.LoadXml(UpdateXmlHeader(tocFileText));
            var xpath = "//*[local-name()='content']/@*[local-name()='src']";
            var srcAttributes = doc.SelectNodes(xpath);
            for (uint index = 0; index < srcAttributes.Length; index++)
            {
                var chapterFilePath = srcAttributes.Item(index).NodeValue.ToString();
                chapterFilePath = StripParametersOffFilePath(chapterFilePath);

                // TODO: propogate parameters to the web-view to jump to sections
                var contentFolderShortPath = StripLocalFileStructureFromPath(contentFolder.Path);
                var uri = new Uri($"ms-appdata:///temp/{contentFolderShortPath}/{chapterFilePath}");
                var chapterFile = await StorageFile.GetFileFromApplicationUriAsync(uri);
                string bookFileText = File.ReadAllText(chapterFile.Path);
                bookFileText = bookFileText.Replace("<body", readerStyle);
                File.WriteAllText(chapterFile.Path, bookFileText);
                Chapters.Add(new Chapter((index + 1).ToString(), uri, chapterFile));
            }
        }

        private async Task<IStorageFolder> GetContentFolder(string contentFilePath, StorageFolder tempSubFolder)
        {
            var contentFolder = tempSubFolder;
            if (contentFilePath.Contains("/"))
            {
                var parts = contentFilePath.Split('/');
                for (var i = 0; i < parts.Length - 1; i++)
                {
                    contentFolder = await contentFolder.GetFolderAsync(parts[i]);
                }
            }
            return contentFolder;
        }

        private string StripPathFromContentFilePath(string contentFilePath)
        {
            var stripped = contentFilePath;
            if (contentFilePath.Contains("/"))
            {
                var parts = contentFilePath.Split('/');
                stripped = parts[parts.Length - 1];
            }
            return stripped;
        }

        private string UpdateXmlHeader(string text)
        {
            var xml = text;
            var startIndex = xml.IndexOf("<docTitle");
            xml = "<?xml version='1.0' encoding='utf-8'?><ncx>" + xml.Substring(startIndex);
            return xml;
        }

        private string StripParametersOffFilePath(string chapterFilePath)
        {
            if (!chapterFilePath.Contains("#")) return chapterFilePath;

            return chapterFilePath.Substring(0, chapterFilePath.IndexOf('#'));
        }

        private string StripLocalFileStructureFromPath(string path)
        {
            var startIndex = path.IndexOf("\\TempState\\");
            return path.Substring(startIndex + 11).Replace('\\', '/');
        }

        private void ListViewSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var pivot = sender as ListView;
            var chapter = pivot.SelectedItem as Chapter;

            if (chapter?.BookFile == null) return;

            Uri fileLink = new Uri(chapter.BookFile.Path);
            PivotItemWebView.Source = fileLink;
        }

        private void BackMainPage(object sender, RoutedEventArgs e)
        {
            PivotItemWebView.CoreWebView2.Stop();
            PivotItemWebView.Close();
            mainContent.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
        }

        private void PrevPage(object sender, RoutedEventArgs e)
        {
            if (ChapterPivot.SelectedIndex > 0)
            {
                ChapterPivot.SelectedItem = ChapterPivot.Items[ChapterPivot.SelectedIndex - 1];
            }
        }

        private void NextPage(object sender, RoutedEventArgs e)
        {
            if (ChapterPivot.SelectedIndex < Chapters.Count - 1)
            {
                ChapterPivot.SelectedItem = ChapterPivot.Items[ChapterPivot.SelectedIndex + 1];
            }
        }

        private void ChangeFont(object sender, RoutedEventArgs e)
        {
        }

        private async void CreateNewSticky(object sender, RoutedEventArgs e)
        {
            List<object> parameter = new List<object>();
            string serial = Guid.NewGuid().ToString("D").ToUpper();
            string UID = localSettings.Values["UID"].ToString();
            string theme = localSettings.Values["StickyTheme"].ToString();

            StickyQuickView stickyQuickView = new StickyQuickView
            {
                CreateTimeText = DateTime.Now.ToShortDateString(),
                StickySerial = serial,
                ThemeKind = theme,
                TitleBackground = brushTool.GetThemeColorBrush(theme, false).TitleBrush,
                Background = brushTool.GetThemeColorBrush(theme, false).BackgroundBrush,
            };

            parameter.Add("new");
            parameter.Add(stickyQuickView);

            // 打开新便签界面
            CoreApplicationView newView = CoreApplication.CreateNewView();
            int newViewId = 0;
            await newView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Frame frame = new Frame();
                frame.Navigate(typeof(NewStickyPage), parameter, new DrillInNavigationTransitionInfo());
                Window.Current.Content = frame;
                Window.Current.Activate();
                newViewId = ApplicationView.GetForCurrentView().Id;
            });
            ApplicationView.PreferredLaunchViewSize = new Size(300, 300);
            bool viewShown = await ApplicationViewSwitcher.TryShowAsStandaloneAsync(newViewId);
        }
    }
}
