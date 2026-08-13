using System;
using Windows.ApplicationModel.Core;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Cactus_Reader.Sources.ToolKits
{
    /// <summary>标题栏样式：Standard=高度/外边距+显隐+激活前景；Reader=仅右侧系统按钮留白。</summary>
    public enum TitleBarStyle
    {
        Standard,
        Reader
    }

    /// <summary>
    /// 标题栏原子操作：统一"隐藏系统标题栏 + 透明按钮 + 可拖拽区域 + 布局同步"，
    /// 消除 MainPage / StartPage / 阅读页 / GetTroublePage 中重复实现的六份近似代码。
    /// </summary>
    public static class TitleBarService
    {
        /// <summary>
        /// 挂接标题栏（应在页面构造器中调用一次）。
        /// </summary>
        /// <param name="dragRegion">可拖拽区域（底层透明 Border）。</param>
        /// <param name="style">Standard=普通页；Reader=阅读器页（CommandBar 融合布局）。</param>
        /// <param name="titleText">窗口标题文本（Standard 下随激活状态切换前景色）。</param>
        /// <param name="onVisibilityChanged">标题栏可见性变化回调（如 PDF 页收起工具按钮）。</param>
        public static void Attach(
            FrameworkElement dragRegion,
            TitleBarStyle style,
            TextBlock titleText = null,
            Action<bool> onVisibilityChanged = null)
        {
            var titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            // 主题根：窗口控制按钮（最小化/最大化/关闭）与 App 标题的前景色默认只跟随系统主题，
            // 在"系统浅色 + 应用深色"下会看不清；这里改为跟随应用实际主题，并在主题切换时刷新。
            FrameworkElement themeRoot = Window.Current.Content as FrameworkElement;
            bool isWindowActive = true;

            if (themeRoot != null)
            {
                UpdateTitleBarForeground(titleBar, titleText, themeRoot.ActualTheme, isWindowActive);
                themeRoot.ActualThemeChanged += (s, e) =>
                    UpdateTitleBarForeground(titleBar, titleText, s.ActualTheme, isWindowActive);
            }

            // 隐藏系统标题栏
            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = true;

            // 设置 XAML 元素为可拖拽区域
            Window.Current.SetTitleBar(dragRegion);

            // 初始化布局并随 DPI/标题栏尺寸变化同步
            UpdateLayout(dragRegion, style, coreTitleBar);
            coreTitleBar.LayoutMetricsChanged += (s, args) => UpdateLayout(dragRegion, style, s);

            if (style == TitleBarStyle.Standard)
            {
                coreTitleBar.IsVisibleChanged += (s, args) =>
                {
                    dragRegion.Visibility = s.IsVisible ? Visibility.Visible : Visibility.Collapsed;
                };

                if (titleText != null)
                {
                    // 窗口激活/未激活时切换标题前景透明度
                    Window.Current.Activated += (s, args) =>
                    {
                        isWindowActive = args.WindowActivationState != CoreWindowActivationState.Deactivated;
                        if (themeRoot != null)
                        {
                            UpdateTitleBarForeground(titleBar, titleText, themeRoot.ActualTheme, isWindowActive);
                        }
                    };
                }
            }
            else if (onVisibilityChanged != null)
            {
                coreTitleBar.IsVisibleChanged += (s, args) => onVisibilityChanged(s.IsVisible);
            }
        }

        private static void UpdateLayout(FrameworkElement dragRegion, TitleBarStyle style, CoreApplicationViewTitleBar coreTitleBar)
        {
            if (style == TitleBarStyle.Standard)
            {
                // 同步标题栏高度，并为窗口控制按钮（最小化/最大化/关闭）预留右侧空间
                dragRegion.Height = coreTitleBar.Height;
                Thickness currMargin = dragRegion.Margin;
                dragRegion.Margin = new Thickness(
                    currMargin.Left, currMargin.Top, coreTitleBar.SystemOverlayRightInset, currMargin.Bottom);
            }
            else
            {
                // 阅读器页 CommandBar 与标题栏融合：仅右侧留白，避免与窗口按钮重叠。
                // UWP 中 Border 与 Control 都定义了自己的 Padding 属性（互不继承），需按实际类型设置。
                Thickness padding = new Thickness(0, 0, coreTitleBar.SystemOverlayRightInset, 0);
                if (dragRegion is Control control)
                {
                    control.Padding = padding;
                }
                else if (dragRegion is Border border)
                {
                    border.Padding = padding;
                }
            }
        }

        /// <summary>
        /// 窗口控制按钮（最小化/最大化/关闭）与 App 标题前景色：
        /// 深色主题用白、浅色主题用黑，未激活窗口时使用 60% 透明度。
        /// 系统默认只跟随系统主题，不跟随应用 RequestedTheme；且资源字典直接取值
        /// 只会拿到默认主题（浅色）的值，因此这里按 ActualTheme 自行决定颜色。
        /// </summary>
        private static void UpdateTitleBarForeground(
            ApplicationViewTitleBar titleBar, TextBlock titleText, ElementTheme actualTheme, bool isActive)
        {
            Color fg = actualTheme == ElementTheme.Dark ? Colors.White : Colors.Black;
            Color inactiveFg = Color.FromArgb(153, fg.R, fg.G, fg.B);

            titleBar.ButtonForegroundColor = fg;
            titleBar.ButtonHoverForegroundColor = fg;
            titleBar.ButtonPressedForegroundColor = fg;
            titleBar.ButtonInactiveForegroundColor = inactiveFg;

            if (titleText != null)
            {
                titleText.Foreground = new SolidColorBrush(isActive ? fg : inactiveFg);
            }
        }
    }
}
