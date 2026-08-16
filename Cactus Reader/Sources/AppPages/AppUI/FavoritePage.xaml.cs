using Cactus_Reader.Entities;
using Cactus_Reader.Entities.EpubEntities;
using Cactus_Reader.Sources.AppPages.Reader;
using Cactus_Reader.Sources.StickyNotes;
using Cactus_Reader.Sources.ToolKits;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Cactus_Reader.Sources.AppPages.AppUI
{
    /// <summary>
    /// 收藏夹页面：展示已收藏（IsFavorite）的阅读记录与便签，为 library.json 与 Sticky 目录的过滤视图。
    /// 点击卡片打开阅读 / 便签编辑窗口；右键菜单提供 打开 / 分享 / 取消收藏 / 删除（进回收站）；
    /// 支持每行 3~5 个视图切换与多选（批量取消收藏 / 删除）。
    /// </summary>
    public sealed partial class FavoritePage : Page
    {
        private readonly ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        private bool isMultiSelectMode;

        // 右键菜单上下文：右键时记录当前卡片项（ReadingItem / Sticky），菜单项 Click 直接使用
        private object contextMenuItem;

        // 分享上下文：DataRequested 一次性订阅，分享面板完成后注销避免事件累积
        private object shareItem;

        public FavoritePage()
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
            await LoadFavoriteList(UID);
        }

        /// <summary>加载全部已收藏内容（阅读记录 + 便签）到列表并同步空状态 / 副标题，按修改时间降序。</summary>
        private async Task LoadFavoriteList(string UID)
        {
            List<ReadingItem> allReadings = await LibraryService.LoadReadingListAsync(UID);
            List<Sticky> allStickies = await StickyService.GetStickyListAsync(UID);

            List<object> favorites = new List<object>();
            favorites.AddRange(allReadings.Where(r => r.IsFavorite));
            favorites.AddRange(allStickies.Where(s => s.IsFavorite));
            favorites = favorites
                .OrderByDescending(o => o is Sticky s
                    ? (s.UpdateTime == default ? s.CreateTime : s.UpdateTime)
                    : ((ReadingItem)o).UpdateTime == default
                        ? ((ReadingItem)o).CreateTime
                        : ((ReadingItem)o).UpdateTime)
                .ToList();

            FavoriteList.Items.Clear();
            foreach (object item in favorites)
            {
                FavoriteList.Items.Add(item);
            }
            UpdateEmptyState();
            SubtitleText.Text = favorites.Count > 0
                ? $"共 {favorites.Count} 个收藏，按最近使用排序。"
                : "你所喜爱的，都在这里。";
        }

        /// <summary>记录数决定布局：无记录显示初始空状态，有记录显示卡片列表。</summary>
        private void UpdateEmptyState()
        {
            bool hasItems = FavoriteList.Items.Count > 0;
            EmptyPlaceholder.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
            FavoriteList.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
        }

        // ---------------- 打开收藏 ----------------

        /// <summary>点击卡片：与右键"打开"共用同一流程（阅读记录走缓存校验导航，便签打开编辑窗口）。</summary>
        private async void FavoriteList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (isMultiSelectMode)
            {
                // 多选模式下点击 = 勾选，不打开
                return;
            }
            await OpenFavoriteItemAsync(e.ClickedItem);
        }

        private async Task OpenFavoriteItemAsync(object item)
        {
            if (item is ReadingItem reading)
            {
                await OpenReadingItemAsync(reading);
            }
            else if (item is Sticky sticky)
            {
                await OpenStickyItemAsync(sticky);
            }
        }

        /// <summary>
        /// 打开一条收藏的阅读记录：校验缓存有效性，失效则提示并从收藏中移除；
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
                await ShowResourceMissingAndRemove(UID, item);
                return;
            }

            item.UpdateTime = DateTime.Now;
            await LibraryService.AddOrUpdateReadingAsync(UID, item);
            NavigateToReading(item, parameter);
            await LoadFavoriteList(UID);
        }

        /// <summary>
        /// 缓存失效：提示"资源不存在"，默认保留收藏（文件仅保存在本机，卸载或换设备后需重新添加）。
        /// 用户主动选择"移出收藏"时才取消收藏（记录仍在资源库，下次打开同样失效）。
        /// </summary>
        private async Task ShowResourceMissingAndRemove(string UID, ReadingItem item)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = "资源不存在",
                Content = $"“{item.Name}”的文件未能找到。文件仅保存在本机，卸载应用或更换设备后需要重新添加文件。是否将其移出收藏夹？",
                PrimaryButtonText = "移出收藏",
                CloseButtonText = "保留收藏",
                DefaultButton = ContentDialogButton.Close
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await LibraryService.SetFavoriteAsync(UID, item.Serial, false);
                await LoadFavoriteList(UID);
            }
        }

        /// <summary>
        /// 打开收藏的便签：锁定便签先验证密码 / Windows Hello（不改写锁定状态），
        /// 验证通过后构造卡片（供编辑窗口读取序列号/主题）并在独立视图打开。
        /// </summary>
        private async Task OpenStickyItemAsync(Sticky sticky)
        {
            // 锁定便签：验证通过才允许打开（与便签本卡片行为一致）
            if (sticky.IsLock)
            {
                bool verified = await StickyService.VerifyStickyUnlockAsync(
                    "查看锁定便签", "若要查看锁定便签，请输入便签本的密码。");
                if (!verified)
                {
                    return;
                }
            }

            StickyQuickView quickView = new StickyQuickView
            {
                CreateTimeText = sticky.CreateTime.ToString("yyyy/MM/dd"),
                StickySerial = sticky.StickySerial,
                ThemeKind = sticky.StickyTheme,
                QuickViewText = sticky.IsLock ? StickyQuickView.LockedPreviewText : sticky.QuickViewText,
                IsFavorite = sticky.IsFavorite,
            };
            List<object> parameter = new List<object> { "open", quickView };
            await StickyService.OpenStickyEditWindowAsync(parameter);
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

        // ---------------- 卡片右键菜单（打开 / 分享 / 取消收藏 / 删除） ----------------

        /// <summary>右键卡片：记录上下文项（菜单项 Click 时使用）。</summary>
        private void FavoriteCardRightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (sender is Grid grid)
            {
                contextMenuItem = grid.DataContext;
            }
        }

        /// <summary>右键菜单"打开"：与点击卡片同一流程。</summary>
        private async void OpenFavoriteMenuClicked(object sender, RoutedEventArgs e)
        {
            if (contextMenuItem != null)
            {
                await OpenFavoriteItemAsync(contextMenuItem);
            }
        }

        /// <summary>右键菜单"取消收藏"：从收藏夹移除（阅读记录仍在资源库，便签仍在便签本）。</summary>
        private async void UnfavoriteMenuClicked(object sender, RoutedEventArgs e)
        {
            string UID = localSettings.Values["UID"]?.ToString();
            if (string.IsNullOrEmpty(UID) || contextMenuItem == null)
            {
                return;
            }

            if (contextMenuItem is ReadingItem reading)
            {
                await LibraryService.SetFavoriteAsync(UID, reading.Serial, false);
            }
            else if (contextMenuItem is Sticky sticky)
            {
                sticky.IsFavorite = false;
                await StickyService.SaveStickyAsync(UID, sticky);
            }
            await LoadFavoriteList(UID);
        }

        /// <summary>右键菜单"删除"：删除内容进回收站并刷新列表。</summary>
        private async void DeleteFavoriteMenuClicked(object sender, RoutedEventArgs e)
        {
            string UID = localSettings.Values["UID"]?.ToString();
            if (string.IsNullOrEmpty(UID) || contextMenuItem == null)
            {
                return;
            }

            if (contextMenuItem is ReadingItem reading)
            {
                await LibraryService.DeleteReadingAsync(UID, reading.Serial);
            }
            else if (contextMenuItem is Sticky sticky)
            {
                await StickyService.DeleteStickyAsync(UID, sticky.StickySerial);
            }
            await LoadFavoriteList(UID);
        }

        /// <summary>右键菜单"分享"：阅读记录分享文件/URL，便签分享 .ctsnote 文件。</summary>
        private void ShareFavoriteMenuClicked(object sender, RoutedEventArgs e)
        {
            if (contextMenuItem == null)
            {
                return;
            }
            shareItem = contextMenuItem;
            DataTransferManager dataTransferManager = DataTransferManager.GetForCurrentView();
            dataTransferManager.DataRequested += ShareFavoriteDataRequested;
            DataTransferManager.ShowShareUI();
        }

        /// <summary>分享数据装配：一次性订阅，面板完成后注销（deferral 异步取文件）。</summary>
        private async void ShareFavoriteDataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            DataRequest request = args.Request;
            DataRequestDeferral deferral = request.GetDeferral();
            try
            {
                string UID = localSettings.Values["UID"]?.ToString();
                if (shareItem is ReadingItem item)
                {
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
                }
                else if (shareItem is Sticky sticky)
                {
                    StorageFolder stickyFolder = await StickyService.GetStickyFolderAsync(UID);
                    StorageFile stickyFile = await stickyFolder.GetFileAsync(sticky.StickySerial + ".ctsnote");
                    request.Data.SetStorageItems(new List<StorageFile> { stickyFile });
                    request.Data.SetText("我最近读了一篇好文章，把感想分享给你！");
                    request.Data.Properties.Title = StickyService.GetStickyTitle(sticky);
                }
                else
                {
                    request.FailWithDisplayText("没有可分享的资源。");
                    return;
                }
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
                sender.DataRequested -= ShareFavoriteDataRequested;
            }
        }

        // ---------------- 多选（取消收藏 / 删除） ----------------

        /// <summary>切换多选模式：进入时开启 GridView 多选，退出时恢复单选并隐藏操作按钮。</summary>
        private void ToggleMultiSelectMode(object sender, RoutedEventArgs e)
        {
            if (isMultiSelectMode)
            {
                ExitMultiSelectMode();
            }
            else
            {
                isMultiSelectMode = true;
                FavoriteList.SelectionMode = ListViewSelectionMode.Multiple;
                MultiSelectButton.Label = "取消选择";
                UpdateActionButtonsVisibility();
            }
        }

        /// <summary>退出多选模式：恢复单选、清空选择、按钮文字还原、隐藏操作按钮。</summary>
        private void ExitMultiSelectMode()
        {
            isMultiSelectMode = false;
            // 必须先清空选择再切换 SelectionMode：None 模式下 SelectedItems 集合已失效，Clear 会抛 COM 异常
            FavoriteList.SelectedItems.Clear();
            FavoriteList.SelectionMode = ListViewSelectionMode.None;
            MultiSelectButton.Label = "选择";
            UnfavoriteSelectedButton.Visibility = Visibility.Collapsed;
        }

        /// <summary>选择变化时更新操作按钮可见性（多选模式且有选中项才显示）。</summary>
        private void FavoriteList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateActionButtonsVisibility();
        }

        private void UpdateActionButtonsVisibility()
        {
            UnfavoriteSelectedButton.Visibility = isMultiSelectMode && FavoriteList.SelectedItems.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        /// <summary>批量取消收藏：选中的内容移出收藏夹（仍保留在资源库 / 便签本）。</summary>
        private async void UnfavoriteSelectedItems(object sender, RoutedEventArgs e)
        {
            List<object> selected = FavoriteList.SelectedItems.Cast<object>().ToList();
            if (selected.Count == 0)
            {
                return;
            }

            string UID = localSettings.Values["UID"]?.ToString();
            if (string.IsNullOrEmpty(UID))
            {
                return;
            }
            foreach (object item in selected)
            {
                if (item is ReadingItem reading)
                {
                    await LibraryService.SetFavoriteAsync(UID, reading.Serial, false);
                }
                else if (item is Sticky sticky)
                {
                    sticky.IsFavorite = false;
                    await StickyService.SaveStickyAsync(UID, sticky);
                }
            }

            await LoadFavoriteList(UID);
            ExitMultiSelectMode();
        }

        // ---------------- 视图切换（每行 3~5 个） ----------------

        /// <summary>当前视图列数（3-5），存 LocalSettings 保持跨会话记忆。</summary>
        private int FavoriteViewColumns
        {
            get
            {
                object value = localSettings.Values["FavoriteViewColumns"];
                int columns = value is int i ? i : 3;
                return columns is >= 3 and <= 5 ? columns : 3;
            }
            set { localSettings.Values["FavoriteViewColumns"] = value; }
        }

        /// <summary>切换视图列数（每行 3 / 4 / 5 个）：更新偏好设置并重算列宽。</summary>
        private void ChangeViewColumns(object sender, RoutedEventArgs e)
        {
            int columns = int.Parse(((MenuFlyoutItem)sender).Tag.ToString());
            FavoriteViewColumns = columns;
            UpdateWrapGridItemWidth();
        }

        /// <summary>列表尺寸变化时重新计算列宽（保持每行精确 3/4/5 列）。</summary>
        private void FavoriteList_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateWrapGridItemWidth();
        }

        /// <summary>按偏好列数计算 ItemsWrapGrid 的 ItemWidth；窗口宽度不足时自动降列（每列最小 160px）。</summary>
        private void UpdateWrapGridItemWidth()
        {
            if (FavoriteList.ItemsPanelRoot is not ItemsWrapGrid wrapGrid)
            {
                return;
            }
            double availableWidth = GetWrapGridAvailableWidth();
            if (availableWidth <= 0)
            {
                return;
            }

            int columns = FavoriteViewColumns;
            const double minItemWidth = 160;
            int maxColumnsByWidth = Math.Max(1, (int)(availableWidth / minItemWidth));
            columns = Math.Min(columns, maxColumnsByWidth);

            wrapGrid.ItemWidth = Math.Max(availableWidth / columns, 160);
        }

        /// <summary>ItemsWrapGrid 实际可用宽度 = GridView 内容区宽度 - ItemsWrapGrid 自身左右 Margin。</summary>
        private double GetWrapGridAvailableWidth()
        {
            double width = FavoriteList.ActualWidth
                - FavoriteList.Padding.Left - FavoriteList.Padding.Right;
            if (FavoriteList.ItemsPanelRoot is ItemsWrapGrid wrapGrid)
            {
                width -= wrapGrid.Margin.Left + wrapGrid.Margin.Right;
            }
            return width;
        }
    }

    /// <summary>
    /// 收藏夹卡片模板选择器：便签（Sticky）走主题色便签卡片，其余（ReadingItem）走阅读卡片。
    /// </summary>
    public class FavoriteTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ReadingTemplate { get; set; }

        public DataTemplate StickyTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            return item is Sticky ? StickyTemplate : ReadingTemplate;
        }
    }
}
