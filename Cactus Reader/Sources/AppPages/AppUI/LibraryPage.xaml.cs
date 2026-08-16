using Cactus_Reader.Entities;
using Cactus_Reader.Entities.EpubEntities;
using Cactus_Reader.Sources.AppPages.Reader;
using Cactus_Reader.Sources.ToolKits;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Cactus_Reader.Sources.AppPages.AppUI
{
    /// <summary>
    /// 资源库页面：统一集中管理阅读历史痕迹（本地文档 / EPUB / 网络文档）。
    /// 有记录时"打开"入口收敛到右上角（与便签本一致），支持每行 3~5 本视图切换与多选删除；
    /// 卡片右键菜单提供打开 / 分享 / 删除；删光记录后自动回到初始空状态布局。
    /// </summary>
    public sealed partial class LibraryPage : Page
    {
        private readonly ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        private bool isMultiSelectMode;

        // 右键菜单上下文：右键时记录当前卡片项，菜单项 Click 直接使用（模板内菜单项无可靠 DataContext）
        private ReadingItem contextMenuItem;

        // 分享上下文：DataRequested 一次性订阅，分享面板完成后注销避免事件累积
        private ReadingItem shareItem;

        public LibraryPage()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            string UID = localSettings.Values["UID"]?.ToString();
            if (string.IsNullOrEmpty(UID))
            {
                return;
            }
            // 从阅读页返回时重置多选状态，保证列表可正常点击
            if (isMultiSelectMode)
            {
                ExitMultiSelectMode();
            }
            await LoadReadingList(UID);
        }

        /// <summary>加载全部阅读记录到列表并同步空状态 / 副标题。</summary>
        private async Task LoadReadingList(string UID)
        {
            List<ReadingItem> list = await LibraryService.LoadReadingListAsync(UID);
            LibraryBookList.Items.Clear();
            foreach (ReadingItem item in list)
            {
                LibraryBookList.Items.Add(item);
            }
            UpdateEmptyState();
            SubtitleText.Text = list.Count > 0
                ? $"共 {list.Count} 个资源，按最近阅读排序。"
                : "所有资源，尽在一处。";
        }

        /// <summary>记录数决定布局：无记录显示初始空状态，有记录显示卡片列表。</summary>
        private void UpdateEmptyState()
        {
            bool hasItems = LibraryBookList.Items.Count > 0;
            EmptyPlaceholder.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
            LibraryBookList.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
        }

        // ---------------- 打开入口 ----------------

        /// <summary>AppBarButton / MenuFlyoutItem 的打开文件入口。</summary>
        private void OpenDocumentFile(object sender, RoutedEventArgs e)
        {
            OpenDocumentFile();
        }

        /// <summary>SplitButton 主按钮（直接打开文件选择器）的入口。</summary>
        private void OpenDocumentFile(Microsoft.UI.Xaml.Controls.SplitButton sender, Microsoft.UI.Xaml.Controls.SplitButtonClickEventArgs args)
        {
            OpenDocumentFile();
        }

        /// <summary>文件选择器：支持 cts / epub / pdf / rtf / txt，选中后登记阅读记录并打开阅读页。</summary>
        private async void OpenDocumentFile()
        {
            var picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeFilter.Add(".cts");
            picker.FileTypeFilter.Add(".epub");
            picker.FileTypeFilter.Add(".pdf");
            picker.FileTypeFilter.Add(".rtf");
            picker.FileTypeFilter.Add(".txt");
            StorageFile document = await picker.PickSingleFileAsync();

            if (document != null)
            {
                await AddReadingAndNavigate(document);
            }
        }

        /// <summary>登记一条本地文件阅读记录（FutureAccessList 令牌跨会话取回）并打开阅读页。</summary>
        private async Task AddReadingAndNavigate(StorageFile file)
        {
            string UID = localSettings.Values["UID"]?.ToString();
            if (string.IsNullOrEmpty(UID))
            {
                return;
            }

            ReadingItem item = new ReadingItem
            {
                Serial = Guid.NewGuid().ToString("D").ToUpper(),
                Name = file.Name,
                ItemType = file.FileType.Equals(".epub", StringComparison.OrdinalIgnoreCase)
                    ? CollectibleType.Book
                    : CollectibleType.Document,
                Extension = file.FileType.ToLowerInvariant(),
                Source = StorageApplicationPermissions.FutureAccessList.Add(file),
                CreateTime = DateTime.Now,
                UpdateTime = DateTime.Now,
            };

            await LibraryService.AddOrUpdateReadingAsync(UID, item);
            NavigateToReading(item, file);
            await LoadReadingList(UID);
        }

        /// <summary>打开网络文档：输入 URL → 抓取沉浸式正文 → 登记记录并缓存 → 打开阅读页。</summary>
        private async void OpenWebDocument(object sender, RoutedEventArgs e)
        {
            TextBox weblinkBox = new()
            {
                Width = 400,
                PlaceholderText = "https://docs.microsoft.com/zh-cn/",
                VerticalAlignment = VerticalAlignment.Bottom,
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
                string contentText = WebReaderService.FetchWebPage(weblink);

                if (contentText.Length > 0)
                {
                    string UID = localSettings.Values["UID"]?.ToString();
                    if (string.IsNullOrEmpty(UID))
                    {
                        break;
                    }

                    ReadingItem item = new ReadingItem
                    {
                        Serial = Guid.NewGuid().ToString("D").ToUpper(),
                        Name = ExtractTitle(contentText, weblink),
                        ItemType = CollectibleType.WebPage,
                        Extension = string.Empty,
                        Source = weblink,
                        CreateTime = DateTime.Now,
                        UpdateTime = DateTime.Now,
                    };

                    // 正文写入本地缓存（下次打开优先离线读取），再登记记录
                    await LibraryService.WriteWebCacheAsync(UID, item, contentText);
                    await LibraryService.AddOrUpdateReadingAsync(UID, item);
                    NavigateToReading(item, contentText);
                    await LoadReadingList(UID);
                    break;
                }
                result = await openWebDocumentDialog.ShowAsync();
            }
        }

        /// <summary>从抓取正文提取标题（首行），失败回退 URL 主机名。</summary>
        private static string ExtractTitle(string contentText, string fallbackUrl)
        {
            string firstLine = contentText.Split('\n')
                .Select(line => line.Trim())
                .FirstOrDefault(line => line.Length > 0) ?? string.Empty;
            if (firstLine.Length > 0)
            {
                return firstLine.Length > 30 ? firstLine.Substring(0, 30) : firstLine;
            }
            try
            {
                return new Uri(fallbackUrl).Host;
            }
            catch (Exception)
            {
                return fallbackUrl;
            }
        }

        // ---------------- 打开阅读记录 ----------------

        /// <summary>点击卡片：与右键"打开"共用同一流程（锁定验证 → 缓存校验 → 更新最后阅读时间 → 导航）。</summary>
        private async void LibraryBookList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (isMultiSelectMode)
            {
                // 多选模式下点击 = 勾选，不打开
                return;
            }
            if (e.ClickedItem is not ReadingItem item)
            {
                return;
            }
            await OpenReadingItemAsync(item);
        }

        /// <summary>
        /// 打开一条阅读记录：校验缓存有效性，失效则提示并删除记录；
        /// 成功则刷新最后阅读时间并导航到阅读页。
        /// </summary>
        private async Task OpenReadingItemAsync(ReadingItem item)
        {
            string UID = localSettings.Values["UID"]?.ToString();
            if (string.IsNullOrEmpty(UID))
            {
                return;
            }

            object parameter = await LibraryService.OpenReadingAsync(UID, item);
            if (parameter == null)
            {
                await ShowResourceMissingAndDelete(UID, item);
                return;
            }

            item.UpdateTime = DateTime.Now;
            await LibraryService.AddOrUpdateReadingAsync(UID, item);
            NavigateToReading(item, parameter);
            await LoadReadingList(UID);
        }

        /// <summary>
        /// 缓存失效：提示"资源不存在"，默认保留记录（文件仅保存在本机，卸载或换设备后需重新添加）。
        /// 用户主动选择"删除记录"时才删除（进回收站）。
        /// </summary>
        private async Task ShowResourceMissingAndDelete(string UID, ReadingItem item)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = "资源不存在",
                Content = $"“{item.Name}”的文件未能找到。文件仅保存在本机，卸载应用或更换设备后需要重新添加文件。是否删除这条阅读记录？",
                PrimaryButtonText = "删除记录",
                CloseButtonText = "保留记录",
                DefaultButton = ContentDialogButton.Close
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await LibraryService.DeleteReadingAsync(UID, item.Serial);
                await LoadReadingList(UID);
            }
        }

        /// <summary>按阅读类型导航到对应阅读页（EPUB / PDF / 文本，网络正文走沉浸式文本阅读器）。</summary>
        private void NavigateToReading(ReadingItem item, object parameter)
        {
            switch (item.ItemType)
            {
                case CollectibleType.Book:
                    if (parameter is StorageFile bookFile)
                    {
                        MainPage.mainPage.mainContent.Navigate(typeof(EpubFileReadingPage),
                            new BookInfo(bookFile, item.Chapter, item.Position),
                            new EntranceNavigationTransitionInfo());
                    }
                    break;

                case CollectibleType.Document:
                    if (string.Equals(item.Extension, ".pdf", StringComparison.OrdinalIgnoreCase))
                    {
                        MainPage.mainPage.mainContent.Navigate(typeof(PdfFileReadingPage), parameter,
                            new EntranceNavigationTransitionInfo());
                    }
                    else
                    {
                        MainPage.mainPage.mainContent.Navigate(typeof(TextFileReadingPage), parameter,
                            new EntranceNavigationTransitionInfo());
                    }
                    break;

                case CollectibleType.WebPage:
                    MainPage.mainPage.mainContent.Navigate(typeof(TextFileReadingPage), parameter,
                        new EntranceNavigationTransitionInfo());
                    break;
            }
        }

        // ---------------- 卡片右键菜单（打开 / 分享 / 删除） ----------------

        /// <summary>右键卡片：记录上下文项，并按当前收藏状态切换"收藏 / 取消收藏"菜单项可见性。</summary>
        private void BookCardRightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (sender is Grid grid && grid.DataContext is ReadingItem item)
            {
                contextMenuItem = item;
                if (grid.ContextFlyout is MenuFlyout flyout)
                {
                    foreach (MenuFlyoutItem menuItem in flyout.Items.OfType<MenuFlyoutItem>())
                    {
                        if (menuItem.Tag is string tag)
                        {
                            if (tag == "favorite")
                            {
                                menuItem.Visibility = item.IsFavorite ? Visibility.Collapsed : Visibility.Visible;
                            }
                            else if (tag == "unfavorite")
                            {
                                menuItem.Visibility = item.IsFavorite ? Visibility.Visible : Visibility.Collapsed;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>右键菜单"收藏 / 取消收藏"：切换收藏状态并刷新列表（角标同步更新）。</summary>
        private async void ToggleFavoriteMenuClicked(object sender, RoutedEventArgs e)
        {
            ReadingItem item = contextMenuItem;
            if (item == null)
            {
                return;
            }
            string UID = localSettings.Values["UID"]?.ToString();
            if (string.IsNullOrEmpty(UID))
            {
                return;
            }
            await LibraryService.SetFavoriteAsync(UID, item.Serial, !item.IsFavorite);
            await LoadReadingList(UID);
        }

        /// <summary>右键菜单"打开"：与点击卡片同一流程。</summary>
        private async void OpenReadingMenuClicked(object sender, RoutedEventArgs e)
        {
            if (contextMenuItem != null)
            {
                await OpenReadingItemAsync(contextMenuItem);
            }
        }

        /// <summary>右键菜单"分享"：本地文件分享 StorageFile，网络文档分享 URL。</summary>
        private void ShareReadingMenuClicked(object sender, RoutedEventArgs e)
        {
            if (contextMenuItem == null)
            {
                return;
            }
            shareItem = contextMenuItem;
            DataTransferManager dataTransferManager = DataTransferManager.GetForCurrentView();
            dataTransferManager.DataRequested += ShareReadingDataRequested;
            DataTransferManager.ShowShareUI();
        }

        /// <summary>分享数据装配：一次性订阅，面板完成后注销（deferral 异步取文件）。</summary>
        private async void ShareReadingDataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            DataRequest request = args.Request;
            DataRequestDeferral deferral = request.GetDeferral();
            try
            {
                ReadingItem item = shareItem;
                if (item == null)
                {
                    request.FailWithDisplayText("没有可分享的资源。");
                    return;
                }

                if (item.ItemType == CollectibleType.WebPage)
                {
                    request.Data.SetText(item.Source);
                }
                else
                {
                    StorageFile file = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(item.Source);
                    request.Data.SetStorageItems(new List<StorageFile> { file });
                }
                request.Data.Properties.Title = item.Name;
                request.Data.Properties.Description = "Cactus Reader 分享";
            }
            catch (Exception)
            {
                request.FailWithDisplayText("资源读取失败，无法分享。");
            }
            finally
            {
                deferral.Complete();
                // 一次性订阅：避免多次分享后 DataRequested 事件累积
                sender.DataRequested -= ShareReadingDataRequested;
            }
        }

        /// <summary>右键菜单"删除"：删除记录（含网络缓存）并刷新列表。</summary>
        private async void DeleteReadingMenuClicked(object sender, RoutedEventArgs e)
        {
            ReadingItem item = contextMenuItem;
            if (item == null)
            {
                return;
            }
            string UID = localSettings.Values["UID"]?.ToString();
            if (string.IsNullOrEmpty(UID))
            {
                return;
            }
            await LibraryService.DeleteReadingAsync(UID, item.Serial);
            await LoadReadingList(UID);
        }

        // ---------------- 多选删除 ----------------

        /// <summary>切换多选模式：进入时开启 GridView 多选，退出时恢复单选并隐藏删除按钮。</summary>
        private void ToggleMultiSelectMode(object sender, RoutedEventArgs e)
        {
            if (isMultiSelectMode)
            {
                ExitMultiSelectMode();
            }
            else
            {
                isMultiSelectMode = true;
                LibraryBookList.SelectionMode = ListViewSelectionMode.Multiple;
                MultiSelectButton.Label = "取消选择";
                UpdateDeleteButtonVisibility();
            }
        }

        /// <summary>退出多选模式：恢复单选、清空选择、按钮文字还原、隐藏删除按钮。</summary>
        private void ExitMultiSelectMode()
        {
            isMultiSelectMode = false;
            // 必须先清空选择再切换 SelectionMode：None 模式下 SelectedItems 集合已失效，Clear 会抛 COM 异常
            LibraryBookList.SelectedItems.Clear();
            LibraryBookList.SelectionMode = ListViewSelectionMode.None;
            MultiSelectButton.Label = "选择";
            DeleteSelectedButton.Visibility = Visibility.Collapsed;
        }

        /// <summary>选择变化时更新删除按钮可见性（多选模式且有选中项才显示）。</summary>
        private void LibraryBookList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateDeleteButtonVisibility();
        }

        private void UpdateDeleteButtonVisibility()
        {
            DeleteSelectedButton.Visibility = isMultiSelectMode && LibraryBookList.SelectedItems.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        /// <summary>删除选中的阅读记录：确认后逐条删除（含网络缓存），删完后刷新并退出多选。</summary>
        private async void DeleteSelectedBooks(object sender, RoutedEventArgs e)
        {
            List<ReadingItem> selected = LibraryBookList.SelectedItems
                .OfType<ReadingItem>()
                .ToList();
            if (selected.Count == 0)
            {
                return;
            }

            ContentDialog dialog = new ContentDialog
            {
                Title = "删除阅读记录",
                Content = $"确定要删除选中的 {selected.Count} 条阅读记录吗？此操作不可撤销。",
                PrimaryButtonText = "删除",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            string UID = localSettings.Values["UID"]?.ToString();
            if (string.IsNullOrEmpty(UID))
            {
                return;
            }
            foreach (ReadingItem item in selected)
            {
                await LibraryService.DeleteReadingAsync(UID, item.Serial);
            }

            await LoadReadingList(UID);
            ExitMultiSelectMode();
        }

        // ---------------- 视图切换（每行 3~5 本） ----------------

        /// <summary>当前视图列数（3-5），存 LocalSettings 保持跨会话记忆。</summary>
        private int LibraryViewColumns
        {
            get
            {
                object value = localSettings.Values["LibraryViewColumns"];
                int columns = value is int i ? i : 3;
                return columns is >= 3 and <= 5 ? columns : 3;
            }
            set { localSettings.Values["LibraryViewColumns"] = value; }
        }

        /// <summary>切换视图列数（每行 3 / 4 / 5 本）：更新偏好设置并重算列宽。</summary>
        private void ChangeViewColumns(object sender, RoutedEventArgs e)
        {
            int columns = int.Parse(((MenuFlyoutItem)sender).Tag.ToString());
            LibraryViewColumns = columns;
            UpdateWrapGridItemWidth();
        }

        /// <summary>列表尺寸变化时重新计算列宽（保持每行精确 3/4/5 列）。</summary>
        private void LibraryBookList_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateWrapGridItemWidth();
        }

        /// <summary>按偏好列数计算 ItemsWrapGrid 的 ItemWidth；窗口宽度不足时自动降列（每列最小 160px）。</summary>
        private void UpdateWrapGridItemWidth()
        {
            if (LibraryBookList.ItemsPanelRoot is not ItemsWrapGrid wrapGrid)
            {
                return;
            }
            double availableWidth = GetWrapGridAvailableWidth();
            if (availableWidth <= 0)
            {
                return;
            }

            int columns = LibraryViewColumns;
            const double minItemWidth = 160;
            int maxColumnsByWidth = Math.Max(1, (int)(availableWidth / minItemWidth));
            columns = Math.Min(columns, maxColumnsByWidth);

            wrapGrid.ItemWidth = Math.Max(availableWidth / columns, 160);
        }

        /// <summary>ItemsWrapGrid 实际可用宽度 = GridView 内容区宽度 - ItemsWrapGrid 自身左右 Margin。</summary>
        private double GetWrapGridAvailableWidth()
        {
            double width = LibraryBookList.ActualWidth
                - LibraryBookList.Padding.Left - LibraryBookList.Padding.Right;
            if (LibraryBookList.ItemsPanelRoot is ItemsWrapGrid wrapGrid)
            {
                width -= wrapGrid.Margin.Left + wrapGrid.Margin.Right;
            }
            return width;
        }
    }
}
