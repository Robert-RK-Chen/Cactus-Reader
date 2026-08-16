using Cactus_Reader.Sources.AppPages.AppUI;
using Cactus_Reader.Sources.ToolKits;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Cactus_Reader
{
    public sealed partial class MainPage : Page
    {
        public static MainPage mainPage;

        // 同步提示条仅在进程内首次进入主页面时显示（返回/重新导航不再弹出）
        private static bool hasShownSyncInfo;

        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

        ProfileSyncTool syncTool = ProfileSyncTool.Instance;

        public MainPage()
        {
            InitializeComponent();
            mainPage = this;
            // 登录后同步用户数据：Task.Run 解包（StartNew 返回 Task<Task> 且不等待内部异常）
            _ = Task.Run(() => AsyncUserProfile());

            // 统一标题栏：透明按钮 + 隐藏系统标题栏 + 可拖拽区域 + 布局/显隐/激活同步
            TitleBarService.Attach(appTitleBar, TitleBarStyle.Standard, appTitle);

            // 非首次进入：立即收起同步提示条（XAML 默认 IsOpen=True，此处覆盖）
            if (hasShownSyncInfo)
            {
                syncInfo.IsOpen = false;
            }
            hasShownSyncInfo = true;
        }

        // 导航项：Tag → 页面类型映射
        private readonly List<(string Tag, Type Page)> pages = new List<(string Tag, Type Page)>
        {
            ("library", typeof(LibraryPage)),
            ("favorite", typeof(FavoritePage)),
            ("sticky", typeof(StickyPage)),
            ("plugins", typeof(PluginsPage)),
            ("recycle", typeof(RecyclePage)),
            ("about", typeof(AboutInfoPage)),
        };

        private void NavViewControlLoaded(object sender, RoutedEventArgs e)
        {
            contentFrame.Navigated += OnNavigated;

            // 默认选中首页并导航
            navViewControl.SelectedItem = navViewControl.MenuItems[0];
            NavViewControlNavigate("library", new EntranceNavigationTransitionInfo());

            // 监听窗口级快捷键（Alt+← 返回），与焦点所在元素无关
            Window.Current.CoreWindow.Dispatcher.AcceleratorKeyActivated += CoreDispatcherAcceleratorKeyActivated;

            Window.Current.CoreWindow.PointerPressed += CoreWindowPointerPressed;

            SystemNavigationManager.GetForCurrentView().BackRequested += SystemBackRequested;
        }

        private void NavViewControlItemInvoked(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked == true)
            {
                NavViewControlNavigate("settings", new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
            }
            else if (args.InvokedItemContainer != null)
            {
                var navItemTag = args.InvokedItemContainer.Tag.ToString();
                NavViewControlNavigate(navItemTag, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
            }
        }

        private void NavViewControlNavigate(string navItemTag, NavigationTransitionInfo transitionInfo)
        {
            Type page = null;
            if (navItemTag == "settings")
            {
                page = typeof(SettingPage);
            }
            else
            {
                var item = pages.FirstOrDefault(p => p.Tag.Equals(navItemTag));
                page = item.Page;
            }

            // 避免重复导航：目标页与当前页相同时跳过
            var preNavPageType = contentFrame.CurrentSourcePageType;

            if (page is not null && !Type.Equals(preNavPageType, page))
            {
                contentFrame.Navigate(page, null, transitionInfo);
            }
        }

        private void NavViewControlBackRequested(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewBackRequestedEventArgs args)
        {
            TryGoBack();
        }

        private void CoreDispatcherAcceleratorKeyActivated(CoreDispatcher sender, AcceleratorKeyEventArgs e)
        {
            // Alt+← 返回
            if (e.EventType == CoreAcceleratorKeyEventType.SystemKeyDown
                && e.VirtualKey == VirtualKey.Left
                && e.KeyStatus.IsMenuKeyDown == true
                && !e.Handled)
            {
                e.Handled = TryGoBack();
            }
        }

        private void SystemBackRequested(object sender, BackRequestedEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = TryGoBack();
            }
        }

        private void CoreWindowPointerPressed(CoreWindow sender, PointerEventArgs e)
        {
            // 鼠标侧键（后退键）返回
            if (e.CurrentPoint.Properties.IsXButton1Pressed)
            {
                e.Handled = TryGoBack();
            }
        }

        private bool TryGoBack()
        {
            if (!contentFrame.CanGoBack)
            {
                return false;
            }
            if (navViewControl.IsPaneOpen &&
                (navViewControl.DisplayMode == Microsoft.UI.Xaml.Controls.NavigationViewDisplayMode.Compact ||
                 navViewControl.DisplayMode == Microsoft.UI.Xaml.Controls.NavigationViewDisplayMode.Minimal))
            {
                return false;
            }
            contentFrame.GoBack();
            return true;
        }

        private void OnNavigated(object sender, NavigationEventArgs e)
        {
            navViewControl.IsBackEnabled = contentFrame.CanGoBack;
            var item = pages.FirstOrDefault(p => p.Page == e.SourcePageType);

            if (contentFrame.SourcePageType == typeof(SettingPage))
            {
                navViewControl.SelectedItem = (Microsoft.UI.Xaml.Controls.NavigationViewItem)navViewControl.SettingsItem;
            }
            else if (contentFrame.SourcePageType == typeof(AboutInfoPage))
            {
                navViewControl.SelectedItem = navViewControl.FooterMenuItems
                    .OfType<Microsoft.UI.Xaml.Controls.NavigationViewItem>()
                    .First(n => n.Tag.Equals(item.Tag));
            }
            else if (contentFrame.SourcePageType != null)
            {
                navViewControl.SelectedItem = navViewControl.MenuItems
                    .OfType<Microsoft.UI.Xaml.Controls.NavigationViewItem>()
                    .First(n => n.Tag.Equals(item.Tag));
            }
        }

        private void AutoSuggestBoxTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            // 仅在用户输入时处理（避免程序填充触发）
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
            }
        }

        private void AutoSuggestBoxSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
        }

        private void AutoSuggestBoxQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
        }

        /// <summary>
        /// 登录后同步用户数据：
        /// 1. 先做一次全量合并（双向，云端权威）拉取 / 推送头像、便签、阅读记录、回收站；
        /// 2. 立即验证便签密钥（UI 线程弹解锁框）——旧设备设置过密码时首次登录即验证，
        ///    之后任何入口（便签本 / 阅读页）创建便签都不会因密钥未就绪而闪退；
        /// 3. 收起同步提示框。
        /// </summary>
        private async void AsyncUserProfile()
        {
            // 未登录（Temp User / 设置缺失）时无用户数据可同步
            string UID = localSettings.Values["UID"]?.ToString();
            if (string.IsNullOrEmpty(UID))
            {
                return;
            }

            // 全量合并：开启同步时执行；同步开关关闭时内部自动跳过
            if (ProfileSyncTool.IsSyncEnabled())
            {
                await syncTool.SyncAllLocalContent(UID);
            }

            // 便签密钥验证必须在 UI 线程（ContentDialog 依赖可见窗口），经主窗口 Dispatcher 调度
            await syncInfo.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                await StickyService.EnsureKeyReadyWithDialogAsync();
            });

            // 三秒后收起同步提示框
            await Task.Delay(3200);
            await syncInfo.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                syncInfo.IsOpen = false;
            });
        }
    }
}
