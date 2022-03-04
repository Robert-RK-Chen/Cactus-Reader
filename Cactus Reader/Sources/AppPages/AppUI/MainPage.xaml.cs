using Cactus_Reader.Sources.AppPages.AppUI;
using Cactus_Reader.Sources.ToolKits;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using User = Cactus_Reader.Entities.User;

// https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x804 上介绍了“空白页”项模板

namespace Cactus_Reader
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public static MainPage current;
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        readonly IFreeSql freeSql = IFreeSqlService.Instance;

        public MainPage()
        {
            InitializeComponent();
            Task.Factory.StartNew(() => AsyncUserProfile());

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

            //Register a handler for when the window changes focus
            Window.Current.Activated += CurrentActivated;
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

        // Update the TitleBar based on the inactive/active state of the app
        private void CurrentActivated(object sender, Windows.UI.Core.WindowActivatedEventArgs e)
        {
            SolidColorBrush defaultForegroundBrush = (SolidColorBrush)Application.Current.Resources["TextFillColorPrimaryBrush"];
            SolidColorBrush inactiveForegroundBrush = (SolidColorBrush)Application.Current.Resources["TextFillColorDisabledBrush"];

            if (e.WindowActivationState == Windows.UI.Core.CoreWindowActivationState.Deactivated)
            {
                appTitle.Foreground = inactiveForegroundBrush;
            }
            else
            {
                appTitle.Foreground = defaultForegroundBrush;
            }
        }

        // Update the TitleBar content layout depending on NavigationView DisplayMode
        private void NavViewCtrlDisplayModeChanged(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewDisplayModeChangedEventArgs args)
        {
            const int topIndent = 16;
            const int expandedIndent = 48;
            int minimalIndent = 104;

            // If the back button is not visible, reduce the TitleBar content indent.
            if (navViewControl.IsBackButtonVisible.Equals(Microsoft.UI.Xaml.Controls.NavigationViewBackButtonVisible.Collapsed))
            {
                minimalIndent = 48;
            }

            Thickness currMargin = appTitleBar.Margin;

            // Set the TitleBar margin dependent on NavigationView display mode
            if (sender.PaneDisplayMode == Microsoft.UI.Xaml.Controls.NavigationViewPaneDisplayMode.Top)
            {
                appTitleBar.Margin = new Thickness(topIndent, currMargin.Top, currMargin.Right, currMargin.Bottom);
                contentFrame.Margin = new Thickness(0, 48, 0, 0);
            }
            else if (sender.DisplayMode == Microsoft.UI.Xaml.Controls.NavigationViewDisplayMode.Minimal)
            {
                appTitleBar.Margin = new Thickness(minimalIndent, currMargin.Top, currMargin.Right, currMargin.Bottom);
                contentFrame.Margin = new Thickness(0, 48, 0, 0);
            }
            else
            {
                appTitleBar.Margin = new Thickness(expandedIndent, currMargin.Top, currMargin.Right, currMargin.Bottom);
                contentFrame.Margin = new Thickness(28, 48, 0, 0);
            }
        }

        // List of ValueTuple holding the Navigation Tag and the relative Navigation Page
        private readonly List<(string Tag, Type Page)> pages = new List<(string Tag, Type Page)>
        {
            ("library", typeof(LibraryPage)),
            ("favorite", typeof(FavoritePage)),
            ("notes", typeof(NotesPage)),
            ("plugins", typeof(PluginsPage)),
            ("recycle", typeof(PluginsPage)),
            ("about", typeof(AboutInfoPage))
        };

        private void NavViewControlLoaded(object sender, RoutedEventArgs e)
        {
            // Add handler for ContentFrame navigation.
            contentFrame.Navigated += OnNavigated;

            // NavView doesn't load any page by default, so load home page.
            navViewControl.SelectedItem = navViewControl.MenuItems[0];
            NavViewControlNavigate("library", new EntranceNavigationTransitionInfo());

            // Listen to the window directly so the app responds
            // to accelerator keys regardless of which element has focus.
            Window.Current.CoreWindow.Dispatcher.AcceleratorKeyActivated += CoreDispatcherAcceleratorKeyActivated;

            Window.Current.CoreWindow.PointerPressed += CoreWindowPointerPressed;

            SystemNavigationManager.GetForCurrentView().BackRequested += SystemBackRequested;
        }

        private void NavViewControlItemInvoked(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked == true)
            {
                NavViewControlNavigate("settings", args.RecommendedNavigationTransitionInfo);
            }
            else if (args.InvokedItemContainer != null)
            {
                var navItemTag = args.InvokedItemContainer.Tag.ToString();
                NavViewControlNavigate(navItemTag, args.RecommendedNavigationTransitionInfo);
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
            // Get the page type before navigation so you can prevent duplicate entries in the backstack.
            var preNavPageType = contentFrame.CurrentSourcePageType;

            // Only navigate if the selected page isn't currently loaded.
            if (!(page is null) && !Type.Equals(preNavPageType, page))
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
            // When Alt+Left are pressed navigate back
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
            // Handle mouse back button.
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
                navViewControl.SelectedItem = navViewControl.SelectedItem = navViewControl.FooterMenuItems
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
            // Only get results when it was a user typing,
            // otherwise assume the value got filled in by TextMemberPath
            // or the handler for SuggestionChosen.
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                //Set the ItemsSource to be your filtered dataset
                //sender.ItemsSource = dataset;
            }
        }

        private void AutoSuggestBoxSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            // Set sender.Text. You can use args.SelectedItem to build your text string.
        }

        private void AutoSuggestBoxQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args.ChosenSuggestion != null)
            {
                // User selected an item from the suggestion list, take an action on it here.
            }
            else
            {
                // Use args.QueryText to determine what to do.
            }
        }

        /// <summary>
        /// 同步的类型：用户配置文件；加载时间：登陆完成后；
        /// 在用户的应用端创建一个 UID 的文件夹保存用户数据。
        /// </summary>
        private void AsyncUserProfile()
        {
        }
    }
}
