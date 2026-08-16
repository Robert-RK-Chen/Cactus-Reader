using Cactus_Reader.Entities;
using Cactus_Reader.Sources.AppPages.AppUI;
using Cactus_Reader.Sources.ToolKits;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Cactus_Reader.Sources.StickyNotes
{
    /// <summary>
    /// 便签快速预览卡片（TemplatedControl，模板在 Themes/Generic.xaml）。
    /// 事件一律在 OnApplyTemplate 中绑定到模板元素（模板 XAML 不声明事件，避免双重触发）；
    /// DataTransferManager 注册/注销在 Loaded/Unloaded 配对，防止多次导航后事件累积泄漏。
    /// </summary>
    public sealed class StickyQuickView : Control
    {
        /// <summary>锁定便签的卡片预览文案。</summary>
        public const string LockedPreviewText = "🔒 该便签已被锁定。";

        private readonly ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        private readonly ThemeColorBrushTool brushTool = ThemeColorBrushTool.Instance;
        private readonly EncryptStickyTool encryptStickyTool = EncryptStickyTool.Instance;

        // 模板元素缓存（OnApplyTemplate 获取，避免每次 GetTemplateChild）
        private Grid rootGrid;
        private MenuFlyoutItem openItem;
        private MenuFlyoutItem shareItem;
        private MenuFlyoutItem favoriteItem;
        private MenuFlyoutItem unfavoriteItem;
        private MenuFlyoutItem lockItem;
        private MenuFlyoutItem unlockItem;
        private MenuFlyoutItem deleteItem;

        public StickyQuickView()
        {
            DefaultStyleKey = typeof(StickyQuickView);
        }

        #region DependencyProperties

        public static readonly DependencyProperty TitleBackgroundProperty = DependencyProperty.Register(
            nameof(TitleBackground), typeof(SolidColorBrush), typeof(StickyQuickView),
            new PropertyMetadata(null));

        /// <summary>顶部装饰条颜色（模板绑定）。</summary>
        public SolidColorBrush TitleBackground
        {
            get { return (SolidColorBrush)GetValue(TitleBackgroundProperty); }
            set { SetValue(TitleBackgroundProperty, value); }
        }

        public static readonly DependencyProperty ViewBackgroundProperty = DependencyProperty.Register(
            nameof(ViewBackground), typeof(SolidColorBrush), typeof(StickyQuickView),
            new PropertyMetadata(null));

        /// <summary>卡片背景颜色（模板绑定）。</summary>
        public SolidColorBrush ViewBackground
        {
            get { return (SolidColorBrush)GetValue(ViewBackgroundProperty); }
            set { SetValue(ViewBackgroundProperty, value); }
        }

        public static readonly DependencyProperty CreateTimeTextProperty = DependencyProperty.Register(
            nameof(CreateTimeText), typeof(string), typeof(StickyQuickView),
            new PropertyMetadata(string.Empty));

        /// <summary>创建日期文案。</summary>
        public string CreateTimeText
        {
            get { return (string)GetValue(CreateTimeTextProperty); }
            set { SetValue(CreateTimeTextProperty, value); }
        }

        public static readonly DependencyProperty QuickViewTextProperty = DependencyProperty.Register(
            nameof(QuickViewText), typeof(string), typeof(StickyQuickView),
            new PropertyMetadata(string.Empty));

        /// <summary>便签内容预览（纯文本，模板绑定）。</summary>
        public string QuickViewText
        {
            get { return (string)GetValue(QuickViewTextProperty); }
            set { SetValue(QuickViewTextProperty, value); }
        }

        public static readonly DependencyProperty ThemeKindProperty = DependencyProperty.Register(
            nameof(ThemeKind), typeof(string), typeof(StickyQuickView),
            new PropertyMetadata("GingkoYellow", OnThemeKindChanged));

        /// <summary>主题标识，变化时自动同步 TitleBackground/ViewBackground。</summary>
        public string ThemeKind
        {
            get { return (string)GetValue(ThemeKindProperty); }
            set { SetValue(ThemeKindProperty, value); }
        }

        public static readonly DependencyProperty IsFavoriteProperty = DependencyProperty.Register(
            nameof(IsFavorite), typeof(bool), typeof(StickyQuickView),
            new PropertyMetadata(false, OnIsFavoriteChanged));

        /// <summary>是否已收藏，变化时自动同步右上角星标可见性。</summary>
        public bool IsFavorite
        {
            get { return (bool)GetValue(IsFavoriteProperty); }
            set { SetValue(IsFavoriteProperty, value); }
        }

        public static readonly DependencyProperty FavoriteStarVisibilityProperty = DependencyProperty.Register(
            nameof(FavoriteStarVisibility), typeof(Visibility), typeof(StickyQuickView),
            new PropertyMetadata(Visibility.Collapsed));

        /// <summary>收藏星标可见性（模板绑定）。</summary>
        public Visibility FavoriteStarVisibility
        {
            get { return (Visibility)GetValue(FavoriteStarVisibilityProperty); }
            set { SetValue(FavoriteStarVisibilityProperty, value); }
        }

        private static void OnThemeKindChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((StickyQuickView)d).ApplyThemeColors(false);
        }

        private static void OnIsFavoriteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            StickyQuickView view = (StickyQuickView)d;
            view.FavoriteStarVisibility = view.IsFavorite ? Visibility.Visible : Visibility.Collapsed;
        }

        public static readonly DependencyProperty StickySerialProperty = DependencyProperty.Register(
            nameof(StickySerial), typeof(string), typeof(StickyQuickView),
            new PropertyMetadata(string.Empty));

        /// <summary>便签唯一标识（与文件名一致）。</summary>
        public string StickySerial
        {
            get { return (string)GetValue(StickySerialProperty); }
            set { SetValue(StickySerialProperty, value); }
        }

        #endregion

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // 解除旧模板绑定（模板可能被替换，OnApplyTemplate 会多次调用）
            if (rootGrid != null)
            {
                rootGrid.Loaded -= QuickViewLoaded;
                rootGrid.Unloaded -= QuickViewUnloaded;
                rootGrid.PointerEntered -= QuickViewPointEntered;
                rootGrid.PointerExited -= QuickViewPointExited;
                rootGrid.DoubleTapped -= QuickViewDoubleTapped;
                rootGrid.RightTapped -= QuickViewRightTapped;
            }
            if (openItem != null) { openItem.Click -= QuickViewOpenMenuClicked; }
            if (shareItem != null) { shareItem.Click -= ShareSticky; }
            if (favoriteItem != null) { favoriteItem.Click -= ToggleFavorite; }
            if (unfavoriteItem != null) { unfavoriteItem.Click -= ToggleFavorite; }
            if (lockItem != null) { lockItem.Click -= LockSticky; }
            if (unlockItem != null) { unlockItem.Click -= UnlockSticky; }
            if (deleteItem != null) { deleteItem.Click -= DeleteSticky; }

            rootGrid = GetTemplateChild("Root") as Grid;
            openItem = GetTemplateChild("OpenSticky") as MenuFlyoutItem;
            shareItem = GetTemplateChild("ShareSticky") as MenuFlyoutItem;
            favoriteItem = GetTemplateChild("FavoriteSticky") as MenuFlyoutItem;
            unfavoriteItem = GetTemplateChild("UnfavoriteSticky") as MenuFlyoutItem;
            lockItem = GetTemplateChild("LockSticky") as MenuFlyoutItem;
            unlockItem = GetTemplateChild("UnlockSticky") as MenuFlyoutItem;
            deleteItem = GetTemplateChild("DeleteSticky") as MenuFlyoutItem;

            // 注册事件（全部绑定在模板元素上，与模板 XAML 无重复）
            if (rootGrid != null)
            {
                rootGrid.Loaded += QuickViewLoaded;
                rootGrid.Unloaded += QuickViewUnloaded;
                rootGrid.PointerEntered += QuickViewPointEntered;
                rootGrid.PointerExited += QuickViewPointExited;
                rootGrid.DoubleTapped += QuickViewDoubleTapped;
                rootGrid.RightTapped += QuickViewRightTapped;
            }
            if (openItem != null) { openItem.Click += QuickViewOpenMenuClicked; }
            if (shareItem != null) { shareItem.Click += ShareSticky; }
            if (favoriteItem != null) { favoriteItem.Click += ToggleFavorite; }
            if (unfavoriteItem != null) { unfavoriteItem.Click += ToggleFavorite; }
            if (lockItem != null) { lockItem.Click += LockSticky; }
            if (unlockItem != null) { unlockItem.Click += UnlockSticky; }
            if (deleteItem != null) { deleteItem.Click += DeleteSticky; }

            // ThemeKind 元数据默认值不会触发 PropertyChangedCallback，此处兜底应用一次主题色；
            // IsFavorite 同理兜底同步一次星标可见性
            ApplyThemeColors(false);
            FavoriteStarVisibility = IsFavorite ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>按当前 ThemeKind 应用主题色（isFocused 为 true 时用悬停色）。</summary>
        private void ApplyThemeColors(bool isFocused)
        {
            ThemeColorBrush brush = brushTool.GetThemeColorBrush(ThemeKind, isFocused);
            TitleBackground = brush.TitleBrush;
            ViewBackground = brush.BackgroundBrush;
        }

        private void QuickViewLoaded(object sender, RoutedEventArgs e)
        {
            ApplyThemeColors(false);
            // 配对注册：Unloaded 中注销，避免多次导航后 DataRequested 累积
            DataTransferManager.GetForCurrentView().DataRequested += DataTransferManagerDataRequested;
        }

        private void QuickViewUnloaded(object sender, RoutedEventArgs e)
        {
            DataTransferManager.GetForCurrentView().DataRequested -= DataTransferManagerDataRequested;
        }

        private void QuickViewPointEntered(object sender, PointerRoutedEventArgs e)
        {
            ApplyThemeColors(true);
            e.Handled = true;
        }

        private void QuickViewPointExited(object sender, PointerRoutedEventArgs e)
        {
            ApplyThemeColors(false);
            e.Handled = true;
        }

        private async void QuickViewDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            e.Handled = true;
            await OpenStickyWithUnlockCheckAsync();
        }

        /// <summary>菜单项“打开便签”点击：与双击走同一打开流程。</summary>
        private async void QuickViewOpenMenuClicked(object sender, RoutedEventArgs e)
        {
            await OpenStickyWithUnlockCheckAsync();
        }

        /// <summary>打开便签：锁定状态先验证密码/Windows Hello（不改写锁定状态），通过后打开。</summary>
        private async Task OpenStickyWithUnlockCheckAsync()
        {
            if (!await encryptStickyTool.IsStickyLockedAsync(StickySerial))
            {
                OpenSticky();
                return;
            }

            // 锁定便签：验证密码/Windows Hello 后允许打开（不改写锁定状态）
            if (await VerifyUnlockAsync("查看便签本", "若要查看锁定便签本，请输入便签本的密码。"))
            {
                OpenSticky();
            }
        }

        private async void QuickViewRightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            e.Handled = true;
            bool locked = await encryptStickyTool.IsStickyLockedAsync(StickySerial);
            // 每次右键都按当前状态重设菜单项可见性，避免解锁后状态残留
            lockItem.Visibility = locked ? Visibility.Collapsed : Visibility.Visible;
            unlockItem.Visibility = locked ? Visibility.Visible : Visibility.Collapsed;
            // 收藏 / 取消收藏菜单项按当前收藏状态切换
            favoriteItem.Visibility = IsFavorite ? Visibility.Collapsed : Visibility.Visible;
            unfavoriteItem.Visibility = IsFavorite ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 收藏 / 取消收藏：加载便签实体切换 IsFavorite 并落盘（加密写回 + 上传云端），
        /// 成功后同步卡片星标与右键菜单状态。锁定便签不阻止收藏切换（元数据与内容分离）。
        /// </summary>
        private async void ToggleFavorite(object sender, RoutedEventArgs e)
        {
            try
            {
                string UID = localSettings.Values["UID"]?.ToString();
                Sticky sticky = await StickyService.LoadStickyAsync(UID, StickySerial);
                if (sticky == null)
                {
                    return;
                }
                sticky.IsFavorite = !sticky.IsFavorite;
                await StickyService.SaveStickyAsync(UID, sticky);
                IsFavorite = sticky.IsFavorite;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("切换便签收藏失败：" + ex.Message);
            }
        }

        private async void DeleteSticky(object sender, RoutedEventArgs e)
        {
            // 先移除卡片（列表操作），再异步删文件/云端（失败不影响 UI）
            StickyPage.RemoveQuickView(this);
            try
            {
                string UID = localSettings.Values["UID"]?.ToString();
                // 本地删除 + 云端删除（同步关闭时仅删本地；无本地文件/网络异常均安全返回）
                await StickyService.DeleteStickyAsync(UID, StickySerial);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("删除便签失败：" + ex.Message);
            }
        }

        private async void DataTransferManagerDataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            DataRequest request = args.Request;
            DataRequestDeferral deferral = request.GetDeferral();
            try
            {
                string UID = localSettings.Values["UID"]?.ToString();
                StorageFolder stickyFolder = await StickyService.GetStickyFolderAsync(UID);
                StorageFile stickyFile = await stickyFolder.GetFileAsync(StickySerial + ".ctsnote");

                request.Data.SetStorageItems(new List<StorageFile> { stickyFile });
                request.Data.SetText("我最近读了一篇好文章，把感想分享给你！");
                request.Data.Properties.Title = localSettings.Values["name"]?.ToString() ?? string.Empty;
                request.Data.Properties.Description = "Cactus Notes 分享";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("分享便签失败：" + ex.Message);
                request.FailWithDisplayText("便签文件读取失败，无法分享。");
            }
            finally
            {
                deferral.Complete();
            }
        }

        private void ShareSticky(object sender, RoutedEventArgs e)
        {
            DataTransferManager.ShowShareUI();
        }

        private async void LockSticky(object sender, RoutedEventArgs e)
        {
            if (localSettings.Values.Keys.Contains("privateKey"))
            {
                bool success = await encryptStickyTool.LockStickyAsync(StickySerial);
                if (success)
                {
                    QuickViewText = LockedPreviewText;
                }
            }
            else
            {
                ContentDialog contentDialog = new ContentDialog
                {
                    Title = "锁定便签本",
                    Content = "若要锁定你的便签本，你需要先设置个人密码。",
                    PrimaryButtonText = "确定",
                    DefaultButton = ContentDialogButton.Primary
                };
                await contentDialog.ShowAsync();
            }
        }

        private async void UnlockSticky(object sender, RoutedEventArgs e)
        {
            // 验证通过后真正解除锁定并立即刷新预览，无需手动刷新页面
            if (await VerifyUnlockAsync("该便签已被锁定", "需要输入密码才能解除锁定的便签本。"))
            {
                await encryptStickyTool.UnlockStickyAsync(StickySerial);
                await RefreshPreviewAsync();
            }
        }

        /// <summary>
        /// 弹出密码 / Windows Hello 解锁对话框并验证。密码错误时循环重试，取消返回 false。
        /// 验证通过不修改锁定状态（由调用方决定后续操作）。
        /// 委托 StickyService 的统一实现（收藏夹打开锁定便签共用同一验证）。
        /// </summary>
        private Task<bool> VerifyUnlockAsync(string title, string header)
        {
            return StickyService.VerifyStickyUnlockAsync(title, header);
        }

        /// <summary>解锁后重新读取便签内容，刷新卡片预览。</summary>
        private async Task RefreshPreviewAsync()
        {
            try
            {
                string UID = localSettings.Values["UID"]?.ToString();
                Sticky sticky = await StickyService.LoadStickyAsync(UID, StickySerial);
                if (sticky != null && !sticky.IsLock)
                {
                    QuickViewText = sticky.QuickViewText;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("刷新便签预览失败：" + ex.Message);
            }
        }

        private async void OpenSticky()
        {
            List<object> parameter = new List<object> { "open", this };
            await StickyService.OpenStickyEditWindowAsync(parameter);
        }
    }
}
