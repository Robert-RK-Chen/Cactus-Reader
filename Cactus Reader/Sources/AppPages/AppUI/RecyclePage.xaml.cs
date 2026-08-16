using Cactus_Reader.Entities;
using Cactus_Reader.Sources.ToolKits;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace Cactus_Reader.Sources.AppPages.AppUI
{
    /// <summary>
    /// 回收站页面：统一暂存被删除的便签与阅读记录。
    /// 顶部三个按钮：恢复（多选模式下批量恢复选中项）/ 多选 / 视图切换（每行 3~5 个）；
    /// 卡片右键菜单：恢复 / 彻底删除（对话框警告，删除本地缓存与云端副本）。
    /// 恢复时将项目放回原来的地方（便签回便签本，阅读记录回资源库）。
    /// </summary>
    public sealed partial class RecyclePage : Page
    {
        private readonly ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        private bool isMultiSelectMode;

        // 右键菜单上下文：右键时记录当前卡片项，菜单项 Click 直接使用（模板内菜单项无可靠 DataContext）
        private RecycleItem contextMenuItem;

        public RecyclePage()
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
            // 从其他页面返回时重置多选状态
            if (isMultiSelectMode)
            {
                ExitMultiSelectMode();
            }
            await LoadRecycleList(UID);
        }

        /// <summary>加载全部回收站条目到列表并同步空状态 / 副标题。</summary>
        private async Task LoadRecycleList(string UID)
        {
            List<RecycleItem> list = await RecycleService.LoadRecycleListAsync(UID);
            RecycleGrid.Items.Clear();
            foreach (RecycleItem item in list)
            {
                RecycleGrid.Items.Add(item);
            }
            UpdateEmptyState();
            SubtitleText.Text = list.Count > 0
                ? $"共 {list.Count} 个项目，按删除时间排序。"
                : "删除的内容会暂存于此。";
        }

        /// <summary>条目数决定布局：无条目显示初始空状态，有条目显示卡片列表。</summary>
        private void UpdateEmptyState()
        {
            bool hasItems = RecycleGrid.Items.Count > 0;
            EmptyPlaceholder.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
            RecycleGrid.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
        }

        // ---------------- 卡片右键菜单（恢复 / 彻底删除） ----------------

        /// <summary>右键卡片：记录上下文项（菜单项 Click 时使用）。</summary>
        private void RecycleCardRightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (sender is Grid grid && grid.DataContext is RecycleItem item)
            {
                contextMenuItem = item;
            }
        }

        /// <summary>右键菜单"恢复"：把项目放回原来的地方（便签回便签本，阅读记录回资源库）。</summary>
        private async void RestoreMenuClicked(object sender, RoutedEventArgs e)
        {
            RecycleItem item = contextMenuItem;
            if (item == null)
            {
                return;
            }
            string UID = localSettings.Values["UID"]?.ToString();
            if (string.IsNullOrEmpty(UID))
            {
                return;
            }
            await RecycleService.RestoreItemAsync(UID, item);
            await LoadRecycleList(UID);
        }

        /// <summary>
        /// 右键菜单"彻底删除"：对话框警告后删除本地缓存与云端副本。
        /// 提示文案随类型区分（便签提示加密文件，阅读记录提示缓存与云端存档）。
        /// </summary>
        private async void PurgeMenuClicked(object sender, RoutedEventArgs e)
        {
            RecycleItem item = contextMenuItem;
            if (item == null)
            {
                return;
            }
            string UID = localSettings.Values["UID"]?.ToString();
            if (string.IsNullOrEmpty(UID))
            {
                return;
            }

            ContentDialog dialog = new ContentDialog
            {
                Title = "彻底删除",
                Content = $"“{item.Name}”将被彻底删除，本地缓存与云端副本将一并移除，此操作不可撤销。",
                PrimaryButtonText = "彻底删除",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            await RecycleService.PurgeItemAsync(UID, item);
            await LoadRecycleList(UID);
        }

        // ---------------- 多选恢复 ----------------

        /// <summary>切换多选模式：进入时开启 GridView 多选，退出时恢复单选并禁用恢复按钮。</summary>
        private void ToggleMultiSelectMode(object sender, RoutedEventArgs e)
        {
            if (isMultiSelectMode)
            {
                ExitMultiSelectMode();
            }
            else
            {
                isMultiSelectMode = true;
                RecycleGrid.SelectionMode = ListViewSelectionMode.Multiple;
                MultiSelectButton.Label = "取消选择";
                UpdateActionButtonsState();
            }
        }

        /// <summary>
        /// 退出多选模式：恢复单选、清空选择、按钮文字还原、隐藏操作按钮。
        /// 注意：Single 选择模式下 SelectedItems 是只读集合，Clear() 会抛 COM 异常，
        /// 因此仅在仍处于 Multiple 模式时清空（单选模式点击恢复后列表已重建，无需清空）。
        /// </summary>
        private void ExitMultiSelectMode()
        {
            isMultiSelectMode = false;
            if (RecycleGrid.SelectionMode == ListViewSelectionMode.Multiple)
            {
                // 多选模式下清空选择是合法的；必须清空后再切回 Single，避免残留选中态
                RecycleGrid.SelectedItems.Clear();
                RecycleGrid.SelectionMode = ListViewSelectionMode.Single;
            }
            MultiSelectButton.Label = "选择";
            RestoreButton.IsEnabled = false;
            DeleteSelectedButton.Visibility = Visibility.Collapsed;
        }

        /// <summary>选择变化时更新按钮状态：恢复按钮有选中项即可用；彻底删除仅多选且有选中项时显示。</summary>
        private void RecycleGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateActionButtonsState();
        }

        private void UpdateActionButtonsState()
        {
            RestoreButton.IsEnabled = RecycleGrid.SelectedItems.Count > 0;
            DeleteSelectedButton.Visibility = isMultiSelectMode && RecycleGrid.SelectedItems.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        /// <summary>恢复选中的回收站条目：逐条放回原处，完成后刷新并退出多选。</summary>
        private async void RestoreSelectedItems(object sender, RoutedEventArgs e)
        {
            List<RecycleItem> selected = RecycleGrid.SelectedItems
                .OfType<RecycleItem>()
                .ToList();
            if (selected.Count == 0)
            {
                return;
            }

            string UID = localSettings.Values["UID"]?.ToString();
            if (string.IsNullOrEmpty(UID))
            {
                return;
            }
            await RecycleService.RestoreItemsAsync(UID, selected);

            await LoadRecycleList(UID);
            ExitMultiSelectMode();
        }

        /// <summary>
        /// 批量彻底删除选中的回收站条目：对话框警告后删除本地缓存与云端副本，
        /// 完成后刷新并退出多选。
        /// </summary>
        private async void DeleteSelectedItems(object sender, RoutedEventArgs e)
        {
            List<RecycleItem> selected = RecycleGrid.SelectedItems
                .OfType<RecycleItem>()
                .ToList();
            if (selected.Count == 0)
            {
                return;
            }

            ContentDialog dialog = new ContentDialog
            {
                Title = "彻底删除",
                Content = $"确定要彻底删除选中的 {selected.Count} 个项目吗？本地缓存与云端副本将一并移除，此操作不可撤销。",
                PrimaryButtonText = "彻底删除",
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
            await RecycleService.PurgeItemsAsync(UID, selected);

            await LoadRecycleList(UID);
            ExitMultiSelectMode();
        }

        // ---------------- 视图切换（每行 3~5 个） ----------------

        /// <summary>当前视图列数（3-5），存 LocalSettings 保持跨会话记忆。</summary>
        private int RecycleViewColumns
        {
            get
            {
                object value = localSettings.Values["RecycleViewColumns"];
                int columns = value is int i ? i : 3;
                return columns is >= 3 and <= 5 ? columns : 3;
            }
            set { localSettings.Values["RecycleViewColumns"] = value; }
        }

        /// <summary>切换视图列数（每行 3 / 4 / 5 个）：更新偏好设置并重算列宽。</summary>
        private void ChangeViewColumns(object sender, RoutedEventArgs e)
        {
            int columns = int.Parse(((MenuFlyoutItem)sender).Tag.ToString());
            RecycleViewColumns = columns;
            UpdateWrapGridItemWidth();
        }

        /// <summary>列表尺寸变化时重新计算列宽（保持每行精确 3/4/5 列）。</summary>
        private void RecycleGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateWrapGridItemWidth();
        }

        /// <summary>按偏好列数计算 ItemsWrapGrid 的 ItemWidth；窗口宽度不足时自动降列（每列最小 160px）。</summary>
        private void UpdateWrapGridItemWidth()
        {
            if (RecycleGrid.ItemsPanelRoot is not ItemsWrapGrid wrapGrid)
            {
                return;
            }
            double availableWidth = GetWrapGridAvailableWidth();
            if (availableWidth <= 0)
            {
                return;
            }

            int columns = RecycleViewColumns;
            const double minItemWidth = 160;
            int maxColumnsByWidth = Math.Max(1, (int)(availableWidth / minItemWidth));
            columns = Math.Min(columns, maxColumnsByWidth);

            wrapGrid.ItemWidth = Math.Max(availableWidth / columns, 160);
        }

        /// <summary>ItemsWrapGrid 实际可用宽度 = GridView 内容区宽度 - ItemsWrapGrid 自身左右 Margin。</summary>
        private double GetWrapGridAvailableWidth()
        {
            double width = RecycleGrid.ActualWidth
                - RecycleGrid.Padding.Left - RecycleGrid.Padding.Right;
            if (RecycleGrid.ItemsPanelRoot is ItemsWrapGrid wrapGrid)
            {
                width -= wrapGrid.Margin.Left + wrapGrid.Margin.Right;
            }
            return width;
        }
    }
}
