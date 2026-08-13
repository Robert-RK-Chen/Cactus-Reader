using Cactus_Reader.Sources.AppPages.AppUI;
using Cactus_Reader.Sources.StickyNotes;
using Cactus_Reader.Sources.ToolKits;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Cactus_Reader.Sources.AppPages.Reader
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class TextFileReadingPage : Page
    {
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        readonly ThemeColorBrushTool brushTool = ThemeColorBrushTool.Instance;
        // 顶部亚克力区域（32px 空白 + CommandBar）实际高度：正文初始顶部留白用，
        // 避免内容被 CommandBar 遮住；滚动时内容仍可穿过亚克力显示模糊效果
        private double topInset = 80;
        // 亚克力下方额外呼吸间距：文字不与 CommandBar 紧贴
        private const double topSpacing = 24;

        public TextFileReadingPage()
        {
            this.InitializeComponent();
            if (localSettings.Values["StickyTheme"] == null) { localSettings.Values["StickyTheme"] = "GingkoYellow"; }
            if (localSettings.Values["fontSize"] == null) { localSettings.Values["fontSize"] = 20.0; }
            if (localSettings.Values["charSpacing"] == null) { localSettings.Values["charSpacing"] = 20.0; }
            if (localSettings.Values["lineHeight"] == null) { localSettings.Values["lineHeight"] = 2.0; }
            if (localSettings.Values["font"] == null) { localSettings.Values["font"] = "宋体"; }
            if (localSettings.Values["passageWidth"] == null) { localSettings.Values["passageWidth"] = "normal"; }
            if (localSettings.Values["theme"] == null) { localSettings.Values["theme"] = "straw"; }
            if (localSettings.Values["voiceIndex"] == null) { localSettings.Values["voiceIndex"] = 0; }
            if (localSettings.Values["voiceName"] == null)
            {
                localSettings.Values["voiceName"] = "zh-CN-XiaoxiaoNeural";
            }
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

            string text = string.Empty;
            var document = e.Parameter;
            if (document.GetType() == typeof(StorageFile))
            {
                text = await FileIO.ReadTextAsync((StorageFile)document);
            }
            else if (document.GetType() == typeof(string))
            {
                text = (string)document;
            }

            fontSizeSlider.Value = Convert.ToDouble(localSettings.Values["fontSize"]);
            charSpacingSlider.Value = Convert.ToDouble(localSettings.Values["charSpacing"]);
            lineHeightSlider.Value = Convert.ToDouble(localSettings.Values["lineHeight"]);
            passageBlock.FontFamily = new FontFamily(localSettings.Values["font"].ToString());
            ChangeLineWidth(localSettings.Values["passageWidth"].ToString());
            ChangeTheme(localSettings.Values["theme"].ToString());

            // 按 CommandBar 区域实际高度设置正文顶部留白（与 PDF 页一致），初始不被亚克力遮挡
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

        private void BackMainPage(object sender, RoutedEventArgs e)
        {
            focusToggleSwitch.IsOn = false;
            mainContent.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
        }

        private async void ReadTextAloud(object sender, RoutedEventArgs e)
        {
            passageBlock.SelectAll();
            string text = passageBlock.SelectedText;
            passageBlock.Select(passageBlock.ContentStart, passageBlock.ContentEnd);
            new ToastContentBuilder().AddArgument("action", "viewConversation")
                .AddArgument("conversationId", 9527)
                .AddText("Cactus Reader 讲述人\n")
                .AddText("讲述人准备中，即将为你朗读文本。")
                .Show();
            try
            {
                // 原子操作：合成语音到本地 wav 文件
                StorageFile audioFile = await SpeechService.SynthesizeToFileAsync(text, SettingsService.GetVoiceName());
                if (audioFile != null)
                {
                    await mediaPlayerElement.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        mediaPlayerElement.Source = MediaSource.CreateFromStorageFile(audioFile);
                        mediaPlayerElement.MediaPlayer.Play();
                    });
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

        private async void ChangeFontSize(object sender,
            Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            await passageBlock.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                passageBlock.FontSize = fontSizeSlider.Value;
                passageBlock.LineHeight = fontSizeSlider.Value * lineHeightSlider.Value;
                localSettings.Values["fontSize"] = fontSizeSlider.Value;
            });
            ChangeFocusLine((int)localSettings.Values["focusLine"]);
        }

        private async void ChangeCharSpacing(object sender,
            Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            await passageBlock.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                passageBlock.CharacterSpacing = 10 * (int)charSpacingSlider.Value;
                localSettings.Values["charSpacing"] = charSpacingSlider.Value;
            });
        }

        private async void ChangeLineHeight(object sender,
            Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            await passageBlock.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                passageBlock.LineHeight = passageBlock.FontSize * lineHeightSlider.Value;
                localSettings.Values["lineHeight"] = lineHeightSlider.Value;
            });
            ChangeFocusLine((int)localSettings.Values["focusLine"]);
        }

        private async void ChangeFont(object sender, RoutedEventArgs e)
        {
            string font = ((MenuFlyoutItem)sender).Tag.ToString();
            await passageBlock.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                passageBlock.FontFamily = new FontFamily(font);
                localSettings.Values["font"] = font;
            });
        }

        private void ChangeLineWidth(string lineWidth)
        {
            switch (lineWidth)
            {
                case "narrow":
                    passageBlock.MaxWidth = 600;
                    break;
                case "normal":
                    passageBlock.MaxWidth = 900;
                    break;
                case "wide":
                    passageBlock.MaxWidth = 1200;
                    break;
                default:
                    passageBlock.MaxWidth = 900;
                    break;
            }
        }

        private void ChangeLineWidth(object sender, RoutedEventArgs e)
        {
            string lineWidth = ((MenuFlyoutItem)sender).Tag.ToString();
            ChangeLineWidth(lineWidth);
            localSettings.Values["lineWidth"] = lineWidth;
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

        private void ChangeFocusMode(object sender, RoutedEventArgs e)
        {
            if (focusToggleSwitch.IsOn == true)
            {
                oneLineButton.IsEnabled = true;
                threeLinesButton.IsEnabled = true;
                fiveLinesButton.IsEnabled = true;
                ChangeFocusLine((int)localSettings.Values["focusLine"]);
                focusRecTop.Visibility = Visibility.Visible;
                focusRecBottom.Visibility = Visibility.Visible;
                // 滚动到文字起始处（跳过 topInset + topSpacing 顶部留白），
                // 使第一行文字立即对齐焦点透明带，而不是前面一大片空白
                scrollViewer.ChangeView(null, topInset + topSpacing, null, true);
            }
            else
            {
                oneLineButton.IsEnabled = false;
                threeLinesButton.IsEnabled = false;
                fiveLinesButton.IsEnabled = false;
                // 顶部留白叠加 topInset + 呼吸间距，避免退出聚焦模式后正文回到被亚克力遮挡的位置
                passageBlock.Margin = new Thickness(60, topInset + topSpacing, 60, 60);
                focusRecTop.Visibility = Visibility.Collapsed;
                focusRecBottom.Visibility = Visibility.Collapsed;
            }
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
                    // 聚焦模式下顶部留白避开亚克力区域并加呼吸间距；
                    // ScrollViewer 已滚动到 topInset+topSpacing，第一行文字正好对齐焦点透明带中央
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

        private void LoadSpeechVoice(object sender, RoutedEventArgs e)
        {
            speechTuneCombo.SelectedIndex = (int)localSettings.Values["voiceIndex"];
            tuneTip.Visibility = Visibility.Collapsed;
        }

        private void ChangeSpeechTune(object sender, SelectionChangedEventArgs e)
        {
            tuneTip.Visibility = Visibility.Visible;
            localSettings.Values["voiceIndex"] = speechTuneCombo.SelectedIndex;
            switch (speechTuneCombo.SelectedIndex)
            {
                case 0:
                    localSettings.Values["voiceName"] = "zh-CN-XiaoxiaoNeural";
                    break;
                case 1:
                    localSettings.Values["voiceName"] = "zh-CN-YunxiNeural";
                    break;
                case 2:
                    localSettings.Values["voiceName"] = "zh-CN-XiaoxuanNeural";
                    break;
                case 3:
                    localSettings.Values["voiceName"] = "zh-CN-YunyangNeural";
                    break;
                case 4:
                    localSettings.Values["voiceName"] = "en-US-AshleyNeural";
                    break;
                case 5:
                    localSettings.Values["voiceName"] = "en-US-JennyNeural";
                    break;
                case 6:
                    localSettings.Values["voiceName"] = "en-US-BrandonNeural";
                    break;
                case 7:
                    localSettings.Values["voiceName"] = "en-US-ChristopherNeural";
                    break;
                default:
                    localSettings.Values["voiceName"] = "zh-CN-XiaoxiaoNeural";
                    break;
            }
        }

        private void ChangeReadLines(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (focusToggleSwitch.IsOn)
            {
                e.Handled = true;
                int lineNum = (int)localSettings.Values["focusLine"];
                double lineHeight = passageBlock.LineHeight * lineNum;
                double verticalOffset = 0.0;

                int delta = e.GetCurrentPoint(sender as UIElement).Properties.MouseWheelDelta;
                if (delta > 0)
                {
                    verticalOffset = scrollViewer.VerticalOffset - lineHeight;
                }
                else if (delta < 0)
                {
                    verticalOffset = scrollViewer.VerticalOffset + lineHeight;
                }
                scrollViewer.ChangeView(scrollViewer.HorizontalOffset, verticalOffset, scrollViewer.ZoomFactor);
            }
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

        private void ResizeImmersiveReadingMode(object sender, SizeChangedEventArgs e)
        {
            ChangeFocusLine((int)localSettings.Values["focusLine"]);
        }
    }
}
