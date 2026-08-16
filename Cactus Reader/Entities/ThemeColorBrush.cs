using Windows.UI.Xaml.Media;

namespace Cactus_Reader.Entities
{
    /// <summary>便签主题色返回值容器（标题/背景画笔，由 ThemeColorBrushTool 在调用线程新建）。</summary>
    public class ThemeColorBrush
    {
        public SolidColorBrush TitleBrush { get; set; }

        public SolidColorBrush BackgroundBrush { get; set; }
    }
}
