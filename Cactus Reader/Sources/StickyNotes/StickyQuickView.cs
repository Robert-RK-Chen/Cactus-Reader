using Cactus_Reader.Sources.AppPages.AppUI;
using Cactus_Reader.Sources.ToolKits;
using Cactus_Reader.Sources.WindowsHello;
using System;
using System.Collections.Generic;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

// The Templated Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234235

namespace Cactus_Reader.Sources.StickyNotes
{
    public sealed class StickyQuickView : Control
    {
        private readonly ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        private readonly ThemeColorBrushTool brushTool = ThemeColorBrushTool.Instance;
        private readonly EncryptStickyTool encryptStickyTool = EncryptStickyTool.Instance;
        private readonly InformationVerify informationVerify = InformationVerify.Instance;
        private readonly MicrosoftPassportHelper microsoftPassportHelper = MicrosoftPassportHelper.Instance;

        public StickyQuickView()
        {
            DefaultStyleKey = typeof(StickyQuickView);
        }

        #region DependencyProperties

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register("TitleBackground",
            typeof(SolidColorBrush), typeof(StickyQuickView), new PropertyMetadata(new SolidColorBrush(Color.FromArgb(255, 255, 242, 171))));
        public SolidColorBrush TitleBackground
        {
            get { return (SolidColorBrush)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public static readonly DependencyProperty QuickViewProperty = DependencyProperty.Register("ViewBackground",
            typeof(SolidColorBrush), typeof(StickyQuickView), new PropertyMetadata(new SolidColorBrush(Color.FromArgb(255, 255, 247, 209))));
        public SolidColorBrush ViewBackground
        {
            get { return (SolidColorBrush)GetValue(QuickViewProperty); }
            set { SetValue(QuickViewProperty, value); }
        }

        public static readonly DependencyProperty CreateTimeProperty = DependencyProperty.Register("CreateTimeText",
            typeof(string), typeof(StickyQuickView), new PropertyMetadata(string.Empty));

        public string CreateTimeText
        {
            get { return (string)GetValue(CreateTimeProperty); }
            set { SetValue(CreateTimeProperty, value); }
        }

        public static readonly DependencyProperty QucikViewProperty = DependencyProperty.Register("QucikViewText",
            typeof(string), typeof(StickyQuickView), new PropertyMetadata(string.Empty));

        public string QucikViewText
        {
            get { return (string)GetValue(QucikViewProperty); }
            set { SetValue(QucikViewProperty, value); }
        }

        public static readonly DependencyProperty ThemeKindProperty = DependencyProperty.Register("ThemeKind",
    typeof(string), typeof(StickyQuickView), new PropertyMetadata("GingkoYellow"));

        public string ThemeKind
        {
            get { return (string)GetValue(ThemeKindProperty); }
            set { SetValue(ThemeKindProperty, value); }
        }

        public static readonly DependencyProperty StickySerialProperties = DependencyProperty.Register("StickySerial",
typeof(string), typeof(StickyQuickView), new PropertyMetadata(Guid.Empty));

        public string StickySerial
        {
            get { return (string)GetValue(StickySerialProperties); }
            set { SetValue(StickySerialProperties, value); }
        }

        #endregion

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            MenuFlyoutItem addItem = GetTemplateChild("OpenSticky") as MenuFlyoutItem;
            MenuFlyoutItem shareItem = GetTemplateChild("ShareSticky") as MenuFlyoutItem;
            MenuFlyoutItem lockItem = GetTemplateChild("LockSticky") as MenuFlyoutItem;
            MenuFlyoutItem unlockItem = GetTemplateChild("UnlockSticky") as MenuFlyoutItem;
            MenuFlyoutItem deleteItem = GetTemplateChild("DeleteSticky") as MenuFlyoutItem;
            addItem.Text = "打开便签";
            shareItem.Text = "分享便签";
            lockItem.Text = "锁定便签";
            unlockItem.Text = "解锁便签";
            deleteItem.Text = "删除便签";

            // 解除事件
            Loaded -= QuickViewLoaded;
            PointerPressed -= QuickViewPointEntered;
            PointerExited -= QuickViewPointExited;
            DoubleTapped -= QuickViewDoubleTapped;
            RightTapped -= QuickViewRightTapped;
            addItem.Click -= QuickViewDoubleTapped;
            shareItem.Click -= ShareSticky;
            lockItem.Click -= LockSticky;
            unlockItem.Click -= UnlockSticky;
            deleteItem.Click -= DeleteSticky;

            // 注册事件
            Loaded += QuickViewLoaded;
            PointerEntered += QuickViewPointEntered;
            PointerExited += QuickViewPointExited;
            DoubleTapped += QuickViewDoubleTapped;
            RightTapped += QuickViewRightTapped;
            addItem.Click += QuickViewDoubleTapped;
            shareItem.Click += ShareSticky;
            lockItem.Click += LockSticky;
            unlockItem.Click += UnlockSticky;
            deleteItem.Click += DeleteSticky;
        }

        private void QuickViewLoaded(object sender, RoutedEventArgs e)
        {
            TitleBackground = brushTool.GetThemeColorBrush(ThemeKind, false).TitleBrush;
            ViewBackground = brushTool.GetThemeColorBrush(ThemeKind, false).BackgroundBrush;
            DataTransferManager dataTransferManager = DataTransferManager.GetForCurrentView();
            dataTransferManager.DataRequested += DataTransferManagerDataRequested;
        }

        private void QuickViewPointEntered(object sender, PointerRoutedEventArgs e)
        {
            TitleBackground = brushTool.GetThemeColorBrush(ThemeKind, true).TitleBrush;
            ViewBackground = brushTool.GetThemeColorBrush(ThemeKind, true).BackgroundBrush;
            e.Handled = true;
        }

        private void QuickViewPointExited(object sender, PointerRoutedEventArgs e)
        {
            TitleBackground = brushTool.GetThemeColorBrush(ThemeKind, false).TitleBrush;
            ViewBackground = brushTool.GetThemeColorBrush(ThemeKind, false).BackgroundBrush;
            e.Handled = true;
        }

        private async void QuickViewDoubleTapped(object sender, RoutedEventArgs e)
        {
            string UID = localSettings.Values["UID"].ToString();
            if (await encryptStickyTool.IsStickyLockedAsync(StickySerial))
            {
                PasswordBox passwordBox = new PasswordBox
                {
                    Width = 360,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Header = "若要查看锁定便签本，请输入便签本的密码。",
                };
                ContentDialog openStickyDialog = new ContentDialog
                {
                    Title = "查看便签本",
                    Content = passwordBox,
                    CloseButtonText = "取消",
                    PrimaryButtonText = "确定",
                    SecondaryButtonText = "Windows Hello",
                    DefaultButton = ContentDialogButton.Primary
                };
                ContentDialogResult result = await openStickyDialog.ShowAsync();

                while (result == ContentDialogResult.Primary || result == ContentDialogResult.Secondary)
                {
                    if(result == ContentDialogResult.Primary)
                    {
                        string password = passwordBox.Password;
                        if (informationVerify.CheckPassword(password))
                        {
                            OpenSticky();
                            break;
                        }
                    }
                    else if (result == ContentDialogResult.Secondary)
                    {
                        if ((bool)localSettings.Values["alreadySetWindowsHello"] == true)
                        {
                            if (await microsoftPassportHelper.CreatePassportKeyAsync(UID, (string)localSettings.Values["name"]))
                            {
                                OpenSticky();
                                break;
                            }
                        }
                    }
                    passwordBox.Header = "该密码不正确，请再试一次。";
                    result = await openStickyDialog.ShowAsync();
                }
            }
            else { OpenSticky(); }
        }

        private async void QuickViewRightTapped(object sender, RoutedEventArgs e)
        {
            MenuFlyoutItem lockItem = GetTemplateChild("LockSticky") as MenuFlyoutItem;
            MenuFlyoutItem unlockItem = GetTemplateChild("UnlockSticky") as MenuFlyoutItem;

            if (await encryptStickyTool.IsStickyLockedAsync(StickySerial))
            {
                lockItem.Visibility = Visibility.Collapsed;
                unlockItem.Visibility = Visibility.Visible;
            }
        }

        private async void DeleteSticky(object sender, RoutedEventArgs e)
        {
            StickyPage.stickyPage.StickyQuickViewList.Items.Remove(this);
            if (StickyPage.stickyPage.StickyQuickViewList.Items.Count == 0)
            {
                StickyPage.stickyPage.EmptyPlaceholder.Opacity = 1;
                localSettings.Values["EmptyPlaceholderOpacity"] = 1;
            }

            string UID = localSettings.Values["UID"].ToString();
            StorageFolder stickyFolder = await ApplicationData.Current.LocalFolder.GetFolderAsync(UID);
            stickyFolder = await stickyFolder.GetFolderAsync("Sticky");
            StorageFile stickyFile = await stickyFolder.GetFileAsync(StickySerial + ".ctsnote");
            await stickyFile.DeleteAsync();
        }

        private async void DataTransferManagerDataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            string UID = localSettings.Values["UID"].ToString();
            StorageFolder stickyFolder = await ApplicationData.Current.LocalFolder.GetFolderAsync(UID);
            stickyFolder = await stickyFolder.GetFolderAsync("Sticky");
            StorageFile stickyFile = await stickyFolder.GetFileAsync(StickySerial + ".ctsnote");

            DataRequest request = args.Request;
            request.Data.SetStorageItems(new List<StorageFile> { stickyFile });
            request.Data.SetText("我最近读了一篇好文章，把感想分享给你！");
            request.Data.Properties.Title = localSettings.Values["name"].ToString();
            request.Data.Properties.Description = "Cactus Notes 分享";
        }

        private void ShareSticky(object sender, RoutedEventArgs e)
        {
            DataTransferManager.ShowShareUI();
        }

        private async void LockSticky(object sender, RoutedEventArgs e)
        {
            if (localSettings.Values.Keys.Contains("privateKey"))
            {
                bool isLockSuccess = await encryptStickyTool.LockStickyAsync(StickySerial);
                if (isLockSuccess)
                {
                    QucikViewText = "🔒 该便签已被锁定。";
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
            string UID = localSettings.Values["UID"].ToString();   
            
            PasswordBox passwordBox = new PasswordBox
            {
                Width = 360,
                VerticalAlignment = VerticalAlignment.Bottom,
                VerticalContentAlignment = VerticalAlignment.Center,
                Header = "需要输入密码才能解除锁定的便签本。",
            };
            ContentDialog unlockStickyDialog = new ContentDialog
            {
                Title = "该便签已被锁定",
                Content = passwordBox,
                CloseButtonText = "取消",
                PrimaryButtonText = "确定",
                SecondaryButtonText = "Windows Hello",
                DefaultButton = ContentDialogButton.Primary
            };
            ContentDialogResult result = await unlockStickyDialog.ShowAsync();

            while (result == ContentDialogResult.Primary || result == ContentDialogResult.Secondary)
            {
                if (result == ContentDialogResult.Primary)
                {
                    string password = passwordBox.Password;
                    if (informationVerify.CheckPassword(password))
                    {
                        await encryptStickyTool.UnlockStickyAsync(StickySerial);
                        QucikViewText = "🔓 已解锁，刷新页面查看便签内容。";
                        break;
                    }
                }
                else if (result == ContentDialogResult.Secondary)
                {
                    if ((bool)localSettings.Values["alreadySetWindowsHello"] == true)
                    {
                        if (await microsoftPassportHelper.CreatePassportKeyAsync(UID, (string)localSettings.Values["name"]))
                        {
                            await encryptStickyTool.UnlockStickyAsync(StickySerial);
                            QucikViewText = "🔓 已解锁，刷新页面查看便签内容。";
                            break;
                        }
                    }
                }
                result = await unlockStickyDialog.ShowAsync();
            }
        }

        private async void OpenSticky()
        {
            List<object> parameter = new List<object> { "open", this };
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
            bool viewShown = await ApplicationViewSwitcher.TryShowAsStandaloneAsync(newViewId);
        }
    }
}
