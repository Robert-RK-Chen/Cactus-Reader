using Cactus_Reader.Sources.AppPages.AppUI;
using Cactus_Reader.Sources.StickyNotes;
using Cactus_Reader.Sources.ToolKits;
using Cactus_Reader.Sources.ToolKits.ViewModels;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Cactus_Reader.Sources.AppPages.Reader
{
    public sealed partial class TextFileReadingPage : Page
    {
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        // 顶部亚克力区域实际高度：正文初始顶部留白，避免被 CommandBar 遮挡
        private double topInset = 80;
        // 亚克力下方呼吸间距
        private const double topSpacing = 24;
        // 文本列宽偏好（窄 600 / 中 900 / 满 1200），与 LocalSettings["passageWidth"] 同步
        private string passageWidthPreference = "normal";

        /// <summary>讲述人设置视图模型（音色 / 风格，供 x:Bind 使用）。</summary>
        public SpeechSettingsViewModel SpeechSettings { get; } = SpeechSettingsViewModel.Instance;

        // 后台播放器（无可见控件，由播放/暂停按钮控制）
        private MediaPlayer speechPlayer;

        public TextFileReadingPage()
        {
            this.InitializeComponent();
            if (localSettings.Values["StickyTheme"] == null) { localSettings.Values["StickyTheme"] = "GingkoYellow"; }
            // 字号与设置页共用 fontSize 键（设置页为全局默认字号）
            if (localSettings.Values["fontSize"] == null) { localSettings.Values["fontSize"] = 20.0; }
            if (localSettings.Values["charSpacing"] == null) { localSettings.Values["charSpacing"] = 20.0; }
            if (localSettings.Values["lineHeight"] == null) { localSettings.Values["lineHeight"] = 2.0; }
            if (localSettings.Values["font"] == null) { localSettings.Values["font"] = "宋体"; }
            if (localSettings.Values["passageWidth"] == null) { localSettings.Values["passageWidth"] = "normal"; }
            // 文本列宽偏好（窄 600 / 中 900 / 满 1200）
            if (localSettings.Values["passageWidth"] != null) { passageWidthPreference = localSettings.Values["passageWidth"].ToString(); }
            if (localSettings.Values["theme"] == null) { localSettings.Values["theme"] = "straw"; }
            if (localSettings.Values["voiceIndex"] == null) { localSettings.Values["voiceIndex"] = 0; }
            if (localSettings.Values["voiceName"] == null) { localSettings.Values["voiceName"] = "冰糖"; }
            if (localSettings.Values["speed"] == null) { localSettings.Values["speed"] = 1.0; }
            if (localSettings.Values["tune"] == null) { localSettings.Values["tune"] = 1.0; }
            localSettings.Values["focusLine"] = 1;

            // 统一标题栏：透明按钮 + 隐藏系统标题栏 + 可拖拽区域 + 右侧系统按钮留白（CommandBar 融合）
            TitleBarService.Attach(appTitleBar, TitleBarStyle.Reader);

            DataTransferManager dataTransferManager = DataTransferManager.GetForCurrentView();
            dataTransferManager.DataRequested += DataTransferManagerDataRequested;
        }

        private void DataTransferManagerDataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            DataRequest request = args.Request;
            request.Data.SetText("我最近读了一篇好文章，分享给你！" + passageBlock.SelectedText);
            request.Data.Properties.Title = "Robert Chen";
            request.Data.Properties.Description = "Cactus Reader";
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // 读取正文：StorageFile 读文件内容，string 直接用
            string text = await LoadTextAsync(e.Parameter);

            RestoreReadingSettings();

            // 按 CommandBar 实际高度设置顶部留白（与 PDF 页一致），初始不被亚克力遮挡
            double actualInset = commandBarHost.ActualHeight;
            if (actualInset > 0) { topInset = actualInset; }
            if (!focusToggleSwitch.IsOn)
            {
                passageBlock.Margin = new Thickness(60, topInset + topSpacing, 60, 60);
            }

            passageBlock.Blocks.Clear();
            Paragraph paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run() { Text = text });
            passageBlock.Blocks.Add(paragraph);
        }

        /// <summary>读取正文：StorageFile 返回文件内容，string 原样返回，其他返回空串。</summary>
        private async Task<string> LoadTextAsync(object document)
        {
            if (document is StorageFile file)
            {
                return await FileIO.ReadTextAsync(file);
            }
            return document as string ?? string.Empty;
        }

        /// <summary>恢复阅读设置：字号/间距/行高/字体/行宽/主题从本地设置回填。</summary>
        private void RestoreReadingSettings()
        {
            fontSizeSlider.Value = Convert.ToDouble(localSettings.Values["fontSize"]);
            charSpacingSlider.Value = Convert.ToDouble(localSettings.Values["charSpacing"]);
            lineHeightSlider.Value = Convert.ToDouble(localSettings.Values["lineHeight"]);
            passageBlock.FontFamily = new FontFamily(localSettings.Values["font"].ToString());
            ApplyPassageWidth();
            ChangeTheme(localSettings.Values["theme"].ToString());
        }

        private void BackMainPage(object sender, RoutedEventArgs e)
        {
            focusToggleSwitch.IsOn = false;
            NavigateBackToMainPage();
        }

        /// <summary>
        /// 返回主页：必须操作承载本页的外层 Frame（MainPage.mainContent），
        /// 而不是本页内部的 mainContent Frame——否则会在阅读页内嵌套新 MainPage，页面逐层叠加。
        /// 有返回栈时 GoBack 回到原 MainPage UI（实例不重建）；无返回栈时兜底导航新 MainPage。
        /// </summary>
        private void NavigateBackToMainPage()
        {
            Frame hostFrame = Frame;
            if (hostFrame != null && hostFrame.CanGoBack)
            {
                hostFrame.GoBack();
            }
            else if (hostFrame != null)
            {
                hostFrame.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
            }
        }

        private async void ReadTextAloud(object sender, RoutedEventArgs e)
        {
            // 直接读取正文模型，避免 SelectAll 造成整篇高亮
            string text = GetPassageText();
            new ToastContentBuilder().AddArgument("action", "viewConversation")
                .AddArgument("conversationId", 9527)
                .AddText("Cactus Reader 讲述人\n")
                .AddText("讲述人准备中，即将为你朗读文本。")
                .Show();
            try
            {
                // 流式合成：返回后立即开始播放，边合成边出声，无需等待整段生成
                MediaStreamSource source = await SpeechService.CreateStreamingSourceAsync(
                    text, SettingsService.GetVoiceName(), SettingsService.GetStyleName(),
                    SettingsService.GetVoiceSpeed(), SettingsService.GetVoiceTune());
                if (source != null)
                {
                    EnsureSpeechPlayer();
                    speechPlayer.Source = MediaSource.CreateFromMediaStreamSource(source);
                    speechPlayer.Play();
                }
                else
                {
                    new ToastContentBuilder().AddArgument("action", "viewConversation")
                        .AddArgument("conversationId", 9528)
                        .AddText("Cactus Reader 讲述人")
                        .AddText("未能生成语音。若要继续，请将设备连接到网络。")
                        .Show();
                }
            }
            catch (Exception)
            {
                new ToastContentBuilder().AddArgument("action", "viewConversation")
                    .AddArgument("conversationId", 9529)
                    .AddText("Cactus Reader 讲述人")
                    .AddText("我们出了点问题。若要使用语音服务，请稍后再试。")
                    .Show();
            }
        }

        /// <summary>提取正文全文：递归遍历 Blocks/Paragraph/Inlines 拼接 Run 文本（避免 SelectAll 高亮）。</summary>
        private string GetPassageText()
        {
            var builder = new StringBuilder();
            foreach (var block in passageBlock.Blocks)
            {
                if (block is Paragraph paragraph)
                {
                    AppendInlineText(paragraph.Inlines, builder);
                }
            }
            return builder.ToString();
        }

        /// <summary>递归拼接 Inline 文本：Run 直接取 Text；Span（Bold/Italic/Hyperlink 等）继续遍历其子 Inlines。</summary>
        private void AppendInlineText(IEnumerable<Inline> inlines, StringBuilder builder)
        {
            foreach (var inline in inlines)
            {
                if (inline is Run run)
                {
                    builder.Append(run.Text);
                }
                else if (inline is Span span)
                {
                    AppendInlineText(span.Inlines, builder);
                }
            }
        }

        /// <summary>懒创建后台播放器并订阅播放状态变化（用于更新播放/暂停按钮）。</summary>
        private void EnsureSpeechPlayer()
        {
            if (speechPlayer == null)
            {
                speechPlayer = new MediaPlayer();
                speechPlayer.PlaybackSession.PlaybackStateChanged += OnSpeechPlaybackStateChanged;
            }
        }

        /// <summary>播放/暂停切换：尚无朗读内容时先触发朗读，否则按当前状态暂停或继续。</summary>
        private void ToggleSpeechPlayback(object sender, RoutedEventArgs e)
        {
            if (speechPlayer == null || speechPlayer.Source == null)
            {
                // 尚无朗读内容：直接触发整段朗读
                ReadTextAloud(sender, e);
                return;
            }

            if (speechPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
            {
                speechPlayer.Pause();
            }
            else
            {
                speechPlayer.Play();
            }
        }

        private void OnSpeechPlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            // 播放状态在播放器线程回调，回到 UI 线程更新按钮
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                bool playing = sender.PlaybackState == MediaPlaybackState.Playing;
                speechPlayPauseIcon.Glyph = playing ? "\uE769" : "\uE768"; // 暂停 / 播放
                speechPlayPauseText.Text = playing ? "暂停朗读" : "播放朗读";
            });
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            // 离开页面时停止并释放播放器，避免后台继续播放/占用资源
            if (speechPlayer != null)
            {
                speechPlayer.PlaybackSession.PlaybackStateChanged -= OnSpeechPlaybackStateChanged;
                speechPlayer.Pause();
                speechPlayer.Source = null;
                speechPlayer.Dispose();
                speechPlayer = null;
                speechPlayPauseIcon.Glyph = "\uE768";
                speechPlayPauseText.Text = "播放朗读";
            }
        }

        /// <summary>应用字号：更新正文与行高并保存设置。</summary>
        private void ApplyFontSize(double fontSize)
        {
            passageBlock.FontSize = fontSize;
            passageBlock.LineHeight = fontSize * lineHeightSlider.Value;
            localSettings.Values["fontSize"] = fontSize;
        }

        private async void ChangeFontSize(object sender,
            Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            await passageBlock.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                ApplyFontSize(fontSizeSlider.Value);
            });
            ChangeFocusLine((int)localSettings.Values["focusLine"]);
        }

        /// <summary>应用文字间距并保存设置。</summary>
        private void ApplyCharSpacing(double charSpacing)
        {
            passageBlock.CharacterSpacing = 10 * (int)charSpacing;
            localSettings.Values["charSpacing"] = charSpacing;
        }

        private async void ChangeCharSpacing(object sender,
            Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            await passageBlock.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                ApplyCharSpacing(charSpacingSlider.Value);
            });
        }

        /// <summary>应用行高并保存设置。</summary>
        private void ApplyLineHeight(double lineHeight)
        {
            passageBlock.LineHeight = passageBlock.FontSize * lineHeight;
            localSettings.Values["lineHeight"] = lineHeight;
        }

        private async void ChangeLineHeight(object sender,
            Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            await passageBlock.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                ApplyLineHeight(lineHeightSlider.Value);
            });
            ChangeFocusLine((int)localSettings.Values["focusLine"]);
        }

        /// <summary>应用正文字体并保存设置。</summary>
        private void ApplyFont(string font)
        {
            passageBlock.FontFamily = new FontFamily(font);
            localSettings.Values["font"] = font;
        }

        private async void ChangeFont(object sender, RoutedEventArgs e)
        {
            string font = ((MenuFlyoutItem)sender).Tag.ToString();
            await passageBlock.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                ApplyFont(font);
            });
        }

        /// <summary>
        /// 按列宽偏好计算正文 MaxWidth：窄 600、中 900、满 1200。
        /// 窗口缩小时，若视口无法容纳偏好列宽，自动升级到更大的列宽（窄→中→满），
        /// 保证正文不被窄列宽限制；窗口重新拉大后回到偏好列宽。
        /// </summary>
        private void ApplyPassageWidth()
        {
            if (passageBlock == null) return;

            // 可用正文宽度 = 视口宽度 - 左右留白；布局未完成时直接按偏好列宽设置，SizeChanged 会再次校正
            double viewportWidth = scrollViewer.ActualWidth;
            double margin = passageBlock.Margin.Left + passageBlock.Margin.Right;
            double available = viewportWidth - margin;
            if (viewportWidth <= 0)
            {
                passageBlock.MaxWidth = MaxWidthOf(passageWidthPreference);
                return;
            }

            string[] order = { "narrow", "normal", "wide" };
            int start = Array.IndexOf(order, passageWidthPreference);
            if (start < 0) { start = 1; passageWidthPreference = "normal"; }

            double maxWidth = MaxWidthOf(passageWidthPreference);
            for (int i = start; i < order.Length; i++)
            {
                double candidate = MaxWidthOf(order[i]);
                if (candidate <= available || i == order.Length - 1)
                {
                    maxWidth = candidate;
                    break;
                }
            }

            // 宽度无变化时不重复赋值，避免多余布局抖动
            if (Math.Abs(passageBlock.MaxWidth - maxWidth) > 0.5)
            {
                passageBlock.MaxWidth = maxWidth;
            }
        }

        /// <summary>列宽偏好对应的正文最大宽度（窄 600、中 900、满 1200）。</summary>
        private static double MaxWidthOf(string preference)
        {
            switch (preference)
            {
                case "narrow": return 600;
                case "wide": return 1200;
                default: return 900;
            }
        }

        /// <summary>文本列样式菜单点击：切换窄/中/满列宽偏好并持久化，立即重排。</summary>
        private void ChangeLineWidth(object sender, RoutedEventArgs e)
        {
            string lineWidth = ((MenuFlyoutItem)sender).Tag.ToString();
            passageWidthPreference = lineWidth;
            // 键名与读取端一致（旧版本误存为 lineWidth）
            localSettings.Values["passageWidth"] = lineWidth;
            ApplyPassageWidth();
        }

        private void ChangeTheme(string theme)
        {
            switch (theme)
            {
                case "pearl":
                    readerMainGrid.Background = new SolidColorBrush(Color.FromArgb(255, 254, 254, 254));
                    passageBlock.Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0));
                    commandBarHost.RequestedTheme = ElementTheme.Light;
                    readerCommandBar.RequestedTheme = ElementTheme.Light;
                    break;
                case "straw":
                    readerMainGrid.Background = new SolidColorBrush(Color.FromArgb(255, 248, 241, 226));
                    passageBlock.Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0));
                    commandBarHost.RequestedTheme = ElementTheme.Light;
                    readerCommandBar.RequestedTheme = ElementTheme.Light;
                    break;
                case "deep":
                    readerMainGrid.Background = new SolidColorBrush(Color.FromArgb(255, 74, 74, 77));
                    passageBlock.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
                    commandBarHost.RequestedTheme = ElementTheme.Dark;
                    readerCommandBar.RequestedTheme = ElementTheme.Dark;
                    break;
                case "midnight":
                    readerMainGrid.Background = new SolidColorBrush(Color.FromArgb(255, 18, 18, 18));
                    passageBlock.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
                    commandBarHost.RequestedTheme = ElementTheme.Dark;
                    readerCommandBar.RequestedTheme = ElementTheme.Dark;
                    break;
                default:
                    readerMainGrid.Background = new SolidColorBrush(Color.FromArgb(255, 248, 241, 226));
                    passageBlock.Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0));
                    commandBarHost.RequestedTheme = ElementTheme.Light;
                    readerCommandBar.RequestedTheme = ElementTheme.Light;
                    break;
            }
        }

        private void ChangeTheme(object sender, RoutedEventArgs e)
        {
            string theme = ((MenuFlyoutItem)sender).Tag.ToString();
            ChangeTheme(theme);
            localSettings.Values["theme"] = theme;
        }

        private void ShareNearBy(object sender, RoutedEventArgs e)
        {
            DataTransferManager.ShowShareUI();
        }

        /// <summary>切换专注模式：开启时启用行数按钮并显示焦点遮罩，关闭时恢复原状。</summary>
        private void SetFocusMode(bool isFocus)
        {
            if (isFocus)
            {
                oneLineButton.IsEnabled = true;
                threeLinesButton.IsEnabled = true;
                fiveLinesButton.IsEnabled = true;
                ChangeFocusLine((int)localSettings.Values["focusLine"]);
                focusRecTop.Visibility = Visibility.Visible;
                focusRecBottom.Visibility = Visibility.Visible;
                // 滚动到正文起始处（跳过顶部留白），使首行对齐焦点透明带
                scrollViewer.ChangeView(null, topInset + topSpacing, null, true);
            }
            else
            {
                oneLineButton.IsEnabled = false;
                threeLinesButton.IsEnabled = false;
                fiveLinesButton.IsEnabled = false;
                // 恢复顶部留白，避免正文被亚克力遮挡
                passageBlock.Margin = new Thickness(60, topInset + topSpacing, 60, 60);
                focusRecTop.Visibility = Visibility.Collapsed;
                focusRecBottom.Visibility = Visibility.Collapsed;
            }
        }

        private void ChangeFocusMode(object sender, RoutedEventArgs e)
        {
            SetFocusMode(focusToggleSwitch.IsOn == true);
        }

        private void ChangeFocusLine(int lineNum)
        {
            double passageHeight;
            try
            {
                passageHeight = scrollViewer.ActualHeight;
                double lineHeight = passageBlock.LineHeight * lineNum;
                focusRecTop.Height = (passageHeight - lineHeight) / 2;
                focusRecBottom.Height = (passageHeight - lineHeight) / 2;
                if (focusToggleSwitch.IsOn == true)
                {
                    // 聚焦模式：顶部留白避开亚克力，首行对齐焦点透明带
                    passageBlock.Margin = new Thickness(60, focusRecTop.Height + topInset + topSpacing, 60, focusRecTop.Height);
                }
            }
            catch (Exception)
            {
            }
        }

        private void ChangeFocusLine(object sender, RoutedEventArgs e)
        {
            int lineNum = int.Parse(((Button)sender).Tag.ToString());
            tipBlock.Text = "专注于阅读 " + lineNum + " 行。";
            ChangeFocusLine(lineNum);
            localSettings.Values["focusLine"] = lineNum;
        }

        /// <summary>按焦点行高滚动：向上/向下滚动一行，返回是否消费了滚轮事件。</summary>
        private bool ScrollByFocusLine(int wheelDelta)
        {
            int lineNum = (int)localSettings.Values["focusLine"];
            double lineHeight = passageBlock.LineHeight * lineNum;
            double verticalOffset = 0.0;

            if (wheelDelta > 0)
            {
                verticalOffset = scrollViewer.VerticalOffset - lineHeight;
            }
            else if (wheelDelta < 0)
            {
                verticalOffset = scrollViewer.VerticalOffset + lineHeight;
            }
            scrollViewer.ChangeView(scrollViewer.HorizontalOffset, verticalOffset, scrollViewer.ZoomFactor);
            return true;
        }

        private void ChangeReadLines(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (!focusToggleSwitch.IsOn)
            {
                return;
            }
            int delta = e.GetCurrentPoint(sender as UIElement).Properties.MouseWheelDelta;
            e.Handled = ScrollByFocusLine(delta);
        }

        private async void CreateNewSticky(object sender, RoutedEventArgs e)
        {
            // 密钥就绪检查：旧设备设置过密码时需先解锁（登录时已弹过，此处兜底防直开窗口）
            if (!await StickyService.EnsureKeyReadyWithDialogAsync())
            {
                return;
            }

            string serial = Guid.NewGuid().ToString("D").ToUpper();
            StickyQuickView stickyQuickView = StickyService.CreateNewStickyQuickView(serial);

            List<object> parameter = new List<object> { "new", stickyQuickView };
            await StickyService.OpenStickyEditWindowAsync(parameter);
        }

        private void ResizeImmersiveReadingMode(object sender, SizeChangedEventArgs e)
        {
            ChangeFocusLine((int)localSettings.Values["focusLine"]);
            ApplyPassageWidth();
        }
    }
}
