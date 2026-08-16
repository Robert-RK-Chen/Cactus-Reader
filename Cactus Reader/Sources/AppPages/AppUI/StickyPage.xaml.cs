using Cactus_Reader.Entities;
using Cactus_Reader.Sources.StickyNotes;
using Cactus_Reader.Sources.ToolKits;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Cactus_Reader.Sources.AppPages.AppUI
{
    public sealed partial class StickyPage : Page
    {
        public static StickyPage stickyPage;

        private readonly ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        private readonly ProfileSyncTool syncTool = ProfileSyncTool.Instance;

        /// <summary>是否处于多选模式。</summary>
        private bool isMultiSelectMode;

        public StickyPage()
        {
            InitializeComponent();
            StickyService.GetStickyTheme();
            stickyPage = this;
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            string UID = localSettings.Values["UID"]?.ToString();
            if (string.IsNullOrEmpty(UID))
            {
                return;
            }

            // 0. 按上次视图列数初始化列宽（ItemsPanelRoot 此时已可用）
            UpdateWrapGridItemWidth();

            // 1. 确保密钥就绪：无条件检查（不依赖"是否有便签数据"）。
            //    vault 三态：无备份→首次使用（生成新密钥）；明文备份（无密码模式）→免密采用；
            //    密码包裹→弹框输入旧密码，或选择"重新开始"（不知道旧密码时放弃旧数据）。
            //    用户取消解锁（返回 false）时不再继续，避免"未解锁"被误判为"孤儿文件"而误删。
            if (!await StickyService.EnsureKeyReadyWithDialogAsync())
            {
                return;
            }

            // 2. 先同步下载云端缺失的便签（文件下载不依赖密钥，与登录全量同步互斥串行），
            //    下载完成后一次性加载显示 —— 卸载重登 / 换设备后进入便签页即看到全部便签
            await syncTool.SyncUserSticky(UID);
            await LoadStickyNotes(UID);

            // 3. 兜底增量刷新（不重建卡片，避免闪烁）
            await UpdateStickyListAsync(UID);
        }

        /// <summary>全量加载本地便签并重建卡片列表（首次进入页面时调用）。</summary>
        private async Task LoadStickyNotes(string UID)
        {
            List<Sticky> stickyList = await StickyService.GetStickyListAsync(UID);
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                StickyQuickViewList.Items.Clear();
                foreach (Sticky sticky in stickyList)
                {
                    StickyQuickViewList.Items.Add(CreateQuickView(sticky));
                }
                UpdateEmptyPlaceholder();
            });

            // 本地有便签文件但一张都没解密成功 → 密钥不匹配（原设备密钥已随卸载/换机丢失），
            // 这些是孤儿文件，内容无法恢复，询问用户是否清理
            if (stickyList.Count == 0 && await StickyService.HasStickyFilesAsync(UID))
            {
                await PromptOrphanStickyCleanupAsync(UID);
            }
        }

        /// <summary>
        /// 孤儿便签清理提示：密钥已随旧设备卸载永久丢失的便签无法解密，
        /// 询问是否删除（本地 + 云端同步清理，不进回收站）；用户选择保留后不再重复提示。
        /// </summary>
        private async Task PromptOrphanStickyCleanupAsync(string UID)
        {
            string promptKey = "orphanStickyPrompted_" + UID;
            if (localSettings.Values.ContainsKey(promptKey))
            {
                return;
            }

            ContentDialog dialog = new ContentDialog
            {
                Title = "无法解密的便签",
                Content = "检测到本地有便签文件但无法解密。这通常是因为原设备的加密密钥已随卸载或更换设备丢失（未设置个人密码且密钥未备份到云端）。这些便签的内容无法恢复。是否删除这些文件？",
                PrimaryButtonText = "删除",
                CloseButtonText = "保留",
                DefaultButton = ContentDialogButton.Close
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await StickyService.DeleteAllUnreadableStickyAsync(UID);
                // 删除后本地无便签文件，重新加载刷新空状态
                await LoadStickyNotes(UID);
            }
            else
            {
                // 用户选择保留：本会话不再重复提示
                localSettings.Values[promptKey] = true;
            }
        }

        /// <summary>
        /// 增量刷新列表：与当前卡片集合对比，顺序与内容一致时零操作（不闪烁）；
        /// 同步产生新增/删除/顺序变化时再重建。同步只下载本地缺失的便签，绝大多数情况零操作。
        /// </summary>
        private async Task UpdateStickyListAsync(string UID)
        {
            List<Sticky> stickyList = await StickyService.GetStickyListAsync(UID);
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // 顺序一致（长度 + 每项 serial 相同）则无需任何改动
                bool sameOrder = StickyQuickViewList.Items.Count == stickyList.Count;
                if (sameOrder)
                {
                    for (int i = 0; i < stickyList.Count; i++)
                    {
                        if (!(StickyQuickViewList.Items[i] is StickyQuickView v) ||
                            v.StickySerial != stickyList[i].StickySerial)
                        {
                            sameOrder = false;
                            break;
                        }
                    }
                }

                if (!sameOrder)
                {
                    StickyQuickViewList.Items.Clear();
                    foreach (Sticky sticky in stickyList)
                    {
                        StickyQuickViewList.Items.Add(CreateQuickView(sticky));
                    }
                }
                UpdateEmptyPlaceholder();
            });
        }

        /// <summary>按便签实体创建卡片（锁定便签显示占位文案；时间格式 yyyy/MM/dd；预览统一三行）。</summary>
        private StickyQuickView CreateQuickView(Sticky sticky)
        {
            return new StickyQuickView
            {
                CreateTimeText = sticky.CreateTime.ToString("yyyy/MM/dd"),
                StickySerial = sticky.StickySerial,
                ThemeKind = sticky.StickyTheme,
                QuickViewText = sticky.IsLock ? StickyQuickView.LockedPreviewText : sticky.QuickViewText,
                IsFavorite = sticky.IsFavorite,
            };
        }

        /// <summary>当前实际列数：用户偏好列数受窗口宽度约束（每列最小约 320px），宽度不足时自动降列，最终到 1 列。</summary>
        private int GetEffectiveColumns()
        {
            double availableWidth = GetWrapGridAvailableWidth();
            if (availableWidth <= 0)
            {
                return StickyViewColumns;
            }

            const double minItemWidth = 320;
            int maxColumnsByWidth = Math.Max(1, (int)(availableWidth / minItemWidth));
            return Math.Min(StickyViewColumns, maxColumnsByWidth);
        }

        /// <summary>根据列表是否为空切换空状态占位提示。</summary>
        private void UpdateEmptyPlaceholder()
        {
            EmptyPlaceholder.Opacity = StickyQuickViewList.Items.Count == 0 ? 1 : 0;
        }

        /// <summary>当前视图列数（1-4），存 LocalSettings 保持跨会话记忆。</summary>
        private int StickyViewColumns
        {
            get
            {
                object value = localSettings.Values["StickyViewColumns"];
                int columns = value is int i ? i : 1;
                return columns is >= 1 and <= 4 ? columns : 1;
            }
            set { localSettings.Values["StickyViewColumns"] = value; }
        }

        /// <summary>切换视图列数（1-4）：更新偏好设置并重算列宽，卡片列数由 UpdateWrapGridItemWidth 统一应用。</summary>
        private void ChangeViewColumns(object sender, RoutedEventArgs e)
        {
            int columns = int.Parse(((MenuFlyoutItem)sender).Tag.ToString());
            StickyViewColumns = columns;
            UpdateWrapGridItemWidth();
        }

        /// <summary>列表尺寸变化时重新计算列宽（保持每行精确 1/2/3 列）。</summary>
        private void StickyQuickViewList_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateWrapGridItemWidth();
        }

        /// <summary>
        /// 按列数计算 ItemsWrapGrid 的 ItemWidth：列表可用宽度均分给 N 列。
        /// 实际列数由 GetEffectiveColumns 决定（偏好列数受窗口宽度约束，宽度不足时自动降列）。
        /// </summary>
        private void UpdateWrapGridItemWidth()
        {
            if (StickyQuickViewList.ItemsPanelRoot is not ItemsWrapGrid wrapGrid)
            {
                return;
            }

            double availableWidth = GetWrapGridAvailableWidth();
            if (availableWidth <= 0)
            {
                return;
            }

            int columns = GetEffectiveColumns();
            wrapGrid.ItemWidth = Math.Max(availableWidth / columns, 120);
        }

        /// <summary>
        /// ItemsWrapGrid 实际可用宽度 = GridView 内容区宽度 - ItemsWrapGrid 自身左右 Margin。
        /// 列宽与响应式降列都基于此计算，避免 N 列总宽超出实际布局空间导致换行（少一列）。
        /// </summary>
        private double GetWrapGridAvailableWidth()
        {
            double width = StickyQuickViewList.ActualWidth
                - StickyQuickViewList.Padding.Left - StickyQuickViewList.Padding.Right;
            if (StickyQuickViewList.ItemsPanelRoot is ItemsWrapGrid wrapGrid)
            {
                width -= wrapGrid.Margin.Left + wrapGrid.Margin.Right;
            }
            return width;
        }

        /// <summary>
        /// 从列表移除指定卡片并同步空状态（卡片右键 / 编辑窗口删除共用）。
        /// 跨线程安全：编辑窗口（辅助视图）调用时自动调度回主窗口线程。
        /// </summary>
        public static void RemoveQuickView(StickyQuickView view)
        {
            if (stickyPage == null || stickyPage.StickyQuickViewList == null || view == null)
            {
                return;
            }

            void Remove()
            {
                stickyPage.StickyQuickViewList.Items.Remove(view);
                stickyPage.UpdateEmptyPlaceholder();
            }

            CoreDispatcher listDispatcher = stickyPage.StickyQuickViewList.Dispatcher;
            if (listDispatcher.HasThreadAccess)
            {
                Remove();
            }
            else
            {
                // 跨线程调度是 fire-and-forget：卡片移除失败无副作用，无需等待
                _ = listDispatcher.RunAsync(CoreDispatcherPriority.Normal, () => Remove());
            }
        }

        private async void CreateNewSticky(object sender, RoutedEventArgs e)
        {
            string serial = Guid.NewGuid().ToString("D").ToUpper();
            StickyQuickView stickyQuickView = StickyService.CreateNewStickyQuickView(serial);
            StickyQuickViewList.Items.Add(stickyQuickView);
            UpdateEmptyPlaceholder();

            List<object> parameter = new List<object> { "new", stickyQuickView };
            await StickyService.OpenStickyEditWindowAsync(parameter);
        }

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
                StickyQuickViewList.SelectionMode = ListViewSelectionMode.Multiple;
                MultiSelectButton.Label = "取消选择";
                UpdateDeleteButtonVisibility();
            }
        }

        /// <summary>退出多选模式：恢复单选、清空选择、按钮文字还原、隐藏删除按钮。</summary>
        private void ExitMultiSelectMode()
        {
            isMultiSelectMode = false;
            // 必须先清空选择再切换 SelectionMode：None 模式下 SelectedItems 集合已失效，Clear 会抛 COM 异常
            StickyQuickViewList.SelectedItems.Clear();
            StickyQuickViewList.SelectionMode = ListViewSelectionMode.None;
            MultiSelectButton.Label = "选择便签";
            DeleteSelectedButton.Visibility = Visibility.Collapsed;
        }

        /// <summary>选择变化时更新删除按钮可见性（多选模式且有选中项才显示）。</summary>
        private void StickyQuickViewList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateDeleteButtonVisibility();
        }

        /// <summary>删除按钮可见性：多选模式且有选中项时显示。</summary>
        private void UpdateDeleteButtonVisibility()
        {
            DeleteSelectedButton.Visibility = isMultiSelectMode && StickyQuickViewList.SelectedItems.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        /// <summary>删除选中的便签：确认后逐个删除（本地 + 云端），完成后退出多选模式。</summary>
        private async void DeleteSelectedSticky(object sender, RoutedEventArgs e)
        {
            List<StickyQuickView> selected = StickyQuickViewList.SelectedItems
                .OfType<StickyQuickView>()
                .ToList();
            if (selected.Count == 0)
            {
                return;
            }

            ContentDialog dialog = new ContentDialog
            {
                Title = "删除便签",
                Content = $"确定要删除选中的 {selected.Count} 张便签吗？此操作不可撤销。",
                PrimaryButtonText = "删除",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            string UID = localSettings.Values["UID"]?.ToString();
            foreach (StickyQuickView view in selected)
            {
                // 本地删除 + 云端删除（同步关闭时仅删本地；无本地文件/网络异常均安全返回）
                await StickyService.DeleteStickyAsync(UID, view.StickySerial);
                RemoveQuickView(view);
            }

            ExitMultiSelectMode();
        }
    }
}
