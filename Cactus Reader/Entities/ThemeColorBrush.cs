using Windows.UI.Xaml.Media;

namespace Cactus_Reader.Entities
{
    public class ThemeColorBrush
    {
        private static ThemeColorBrush instance;

        public static ThemeColorBrush Instance
        {
            get { return instance ?? (instance = new ThemeColorBrush()); }
        }

        public SolidColorBrush TitleBrush { get; set; }

        public SolidColorBrush BackgroundBrush { get; set; }
    }
}
