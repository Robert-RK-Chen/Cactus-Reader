using Cactus_Reader.Entities;
using Cactus_Reader.Sources.AppPages.AppUI;
using Cactus_Reader.Sources.StickyNotes;
using System;
using System.IO;
using Windows.ApplicationModel.Core;
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

namespace Cactus_Reader.Sources.ToolKits
{
    public sealed class StickyQuickView : Control
    {
        public StickyQuickView()
        {
            DefaultStyleKey = typeof(StickyQuickView);
        }

        #region 依赖项属性

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
            MenuFlyoutItem deleteItem = GetTemplateChild("DeleteSticky") as MenuFlyoutItem;

            // 解除事件
            Loaded -= QuickViewLoaded;
            PointerPressed -= QuickViewPointEntered;
            PointerExited -= QuickViewPointExited;
            DoubleTapped -= QuickViewDoubleTapped;
            addItem.Click -= QuickViewDoubleTapped;
            deleteItem.Click -= DeleteSticky;

            // 注册事件
            Loaded += QuickViewLoaded;
            PointerEntered += QuickViewPointEntered;
            PointerExited += QuickViewPointExited;
            DoubleTapped += QuickViewDoubleTapped;
            addItem.Click += QuickViewDoubleTapped;
            deleteItem.Click += DeleteSticky;
        }

        private void QuickViewLoaded(object sender, RoutedEventArgs e)
        {
            ThemeColorBrush brush = new ThemeColorBrushTool().GetThemeColorBrush(ThemeKind, false);
            TitleBackground = brush.TitleBrush;
            ViewBackground = brush.BackgroundBrush;
        }

        private void QuickViewPointEntered(object sender, PointerRoutedEventArgs e)
        {
            ThemeColorBrush brush = new ThemeColorBrushTool().GetThemeColorBrush(ThemeKind, true);
            TitleBackground = brush.TitleBrush;
            ViewBackground = brush.BackgroundBrush;
            e.Handled = true;
        }

        private void QuickViewPointExited(object sender, PointerRoutedEventArgs e)
        {
            ThemeColorBrush brush = new ThemeColorBrushTool().GetThemeColorBrush(ThemeKind, false);
            TitleBackground = brush.TitleBrush;
            ViewBackground = brush.BackgroundBrush;
            e.Handled = true;
        }

        private async void QuickViewDoubleTapped(object sender, RoutedEventArgs e)
        {
            string serial = StickySerial;
            CoreApplicationView newView = CoreApplication.CreateNewView();
            int newViewId = 0;
            await newView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Frame frame = new Frame();
                frame.Navigate(typeof(NewStickyPage), serial, new DrillInNavigationTransitionInfo());
                Window.Current.Content = frame;
                Window.Current.Activate();
                newViewId = ApplicationView.GetForCurrentView().Id;
            });
            bool viewShown = await ApplicationViewSwitcher.TryShowAsStandaloneAsync(newViewId);
        }

        private async void DeleteSticky(object sender, RoutedEventArgs e)
        {
            NotesPage.notesPage.StickyQuickViewList.Items.Remove(this);
            StorageFolder storageFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("Notes", CreationCollisionOption.OpenIfExists);
            string filePath = storageFolder.Path + "\\" + StickySerial + ".json";
            File.Delete(filePath);
        }
    }
}
