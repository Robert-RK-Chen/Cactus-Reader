using Cactus_Reader.Sources.ToolKits;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Cactus_Reader.Sources.AppPages.Reader
{
    public class CalligraphicPen : InkToolbarCustomPen
    {
        public CalligraphicPen()
        {
        }

        protected override InkDrawingAttributes CreateInkDrawingAttributesCore(Brush brush, double strokeWidth)
        {

            InkDrawingAttributes inkDrawingAttributes = new InkDrawingAttributes();
            inkDrawingAttributes.PenTip = PenTipShape.Circle;
            inkDrawingAttributes.IgnorePressure = false;
            SolidColorBrush solidColorBrush = (SolidColorBrush)brush;

            if (solidColorBrush != null)
            {
                inkDrawingAttributes.Color = solidColorBrush.Color;
            }

            inkDrawingAttributes.Size = new Size(strokeWidth, 2.0f * strokeWidth);
            inkDrawingAttributes.PenTipTransform = System.Numerics.Matrix3x2.CreateRotation((float)(Math.PI * 45 / 180));

            return inkDrawingAttributes;
        }
    }

    public sealed partial class PdfFileReadingPage : Page
    {
        private Polyline lasso;
        private Rect boundingRect;
        private bool isBoundRect;

        public PdfFileReadingPage()
        {
            this.InitializeComponent();
            inkCanvas.InkPresenter.InputDeviceTypes = CoreInputDeviceTypes.Pen;
            Loaded += OnPageLoaded;
            pageImage.ImageOpened += OnPageImageOpened;

            var titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            // 统一标题栏：透明按钮 + 隐藏系统标题栏 + 可拖拽区域 + 右侧系统按钮留白（CommandBar 融合）
            // 标题栏不可见（全屏等）时收起固定工具栏按钮状态
            TitleBarService.Attach(appTitleBar, TitleBarStyle.Reader, null,
                isVisible => { if (!isVisible) toggleButton.IsChecked = false; });
        }

        /// <summary>
        /// 页面加载完成后，按顶部 CommandBar 区域实际高度设置内容顶部留白，
        /// 保证图片初始时不被亚克力区域遮挡；滚动时内容仍可穿过亚克力显示模糊效果。
        /// </summary>
        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            double topInset = commandBarArea.ActualHeight;
            if (topInset <= 0) { topInset = 80; }
            contentGrid.Padding = new Thickness(0, topInset, 0, 60);
        }

        /// <summary>
        /// 占位图片解码完成后，按图片实际像素尺寸设置画布与图片大小，
        /// 使画布/墨迹坐标与内容大小一致。
        /// </summary>
        private void OnPageImageOpened(object sender, RoutedEventArgs e)
        {
            if (pageImage.Source is BitmapImage bitmap)
            {
                ResizeCanvasToImage(bitmap.PixelWidth, bitmap.PixelHeight);
            }
        }

        private void ResizeCanvasToImage(double width, double height)
        {
            selectionCanvas.Width = width;
            selectionCanvas.Height = height;
            pageImage.Width = width;
            pageImage.Height = height;
        }

        /// <summary>
        /// 供后续 PDF 渲染接入：将指定图片（如 PDF 某页渲染结果）设为画布内容，
        /// 并按图片实际尺寸调整画布大小。
        /// </summary>
        private async Task SetPageImageAsync(StorageFile file)
        {
            BitmapImage bitmap = new BitmapImage();
            using (var stream = await file.OpenAsync(FileAccessMode.Read))
            {
                await bitmap.SetSourceAsync(stream);
            }

            pageImage.Source = bitmap;
            ResizeCanvasToImage(bitmap.PixelWidth, bitmap.PixelHeight);
        }

        private void BackMainPage(object sender, RoutedEventArgs e)
        {
            mainContent.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
        }

        private void InkToolClick(object sender, RoutedEventArgs e)
        {
            if (mainGrid.Children.Contains(inkToolBar))
            {
                toggleButton.IsChecked = false;
                inkCanvas.InkPresenter.InputDeviceTypes &= ~CoreInputDeviceTypes.Touch;
                inkCanvas.InkPresenter.InputDeviceTypes &= ~CoreInputDeviceTypes.Mouse;
                mainGrid.Children.Remove(inkToolBar);
            }
            else
            {
                mainGrid.Children.Add(inkToolBar);
            }
        }


        private void ToggleCustomClick(object sender, RoutedEventArgs e)
        {
            if (toggleButton.IsChecked == true)
            {
                inkCanvas.InkPresenter.InputDeviceTypes |= CoreInputDeviceTypes.Touch;
                inkCanvas.InkPresenter.InputDeviceTypes |= CoreInputDeviceTypes.Mouse;
            }
            else
            {
                inkCanvas.InkPresenter.InputDeviceTypes &= ~CoreInputDeviceTypes.Touch;
                inkCanvas.InkPresenter.InputDeviceTypes &= ~CoreInputDeviceTypes.Mouse;
            }
        }

        private void ToolButtonLassoClick(object sender, RoutedEventArgs e)
        {
            // By default, pen barrel button or right mouse button is processed for inking
            // Set the configuration to instead allow processing these input on the UI thread
            inkCanvas.InkPresenter.InputProcessingConfiguration.RightDragAction = InkInputRightDragAction.LeaveUnprocessed;

            inkCanvas.InkPresenter.UnprocessedInput.PointerPressed += UnprocessedInputPointerPressed;
            inkCanvas.InkPresenter.UnprocessedInput.PointerMoved += UnprocessedInputPointerMoved;
            inkCanvas.InkPresenter.UnprocessedInput.PointerReleased += UnprocessedInputPointerReleased;
        }

        private void UnprocessedInputPointerPressed(InkUnprocessedInput sender, PointerEventArgs args)
        {
            lasso = new Polyline()
            {
                Stroke = new SolidColorBrush(Windows.UI.Colors.Blue),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection() { 5, 2 },
            };

            lasso.Points.Add(args.CurrentPoint.RawPosition);
            //selectionCanvas.Children.Add(lasso);
            isBoundRect = true;
        }

        private void UnprocessedInputPointerMoved(InkUnprocessedInput sender, PointerEventArgs args)
        {
            if (isBoundRect)
            {
                lasso.Points.Add(args.CurrentPoint.RawPosition);
            }
        }

        private void UnprocessedInputPointerReleased(InkUnprocessedInput sender, PointerEventArgs args)
        {
            lasso.Points.Add(args.CurrentPoint.RawPosition);

            boundingRect = inkCanvas.InkPresenter.StrokeContainer.SelectWithPolyLine(lasso.Points);
            isBoundRect = false;
            DrawBoundingRect();
        }

        private void DrawBoundingRect()
        {
            // selectionCanvas.Children.Clear();

            if (boundingRect.Width <= 0 || boundingRect.Height <= 0)
            {
                return;
            }

            var rectangle = new Rectangle()
            {
                Stroke = new SolidColorBrush(Colors.Blue),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection() { 5, 2 },
                Width = boundingRect.Width,
                Height = boundingRect.Height
            };

            Canvas.SetLeft(rectangle, boundingRect.X);
            Canvas.SetTop(rectangle, boundingRect.Y);

            // selectionCanvas.Children.Add(rectangle);
        }

        private void UpdateScaleMulti(object sender, ScrollViewerViewChangedEventArgs e)
        {
            float scale = canvasContainer.ZoomFactor;
            ScaleMulti.Text = (int)(scale * 100) + "%";
        }
    }
}
