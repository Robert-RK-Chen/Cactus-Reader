using Cactus_Reader.Entities;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Cactus_Reader.Sources.StickyNotes
{
    public class ThemeColorBrushTool
    {
        private ThemeColorBrush colorBrush = new ThemeColorBrush();

        public ThemeColorBrush GetThemeColorBrush(string theme, bool isFoucus)
        {
            if (isFoucus)
            {
                switch (theme)
                {
                    case "GingkoYellow":
                        colorBrush.TitleBrush = (SolidColorBrush)Application.Current.Resources["GingkoYellowTitleFoucus"];
                        colorBrush.BackgroundBrush = (SolidColorBrush)Application.Current.Resources["GingkoYellowBackgroundFoucus"];
                        break;
                    case "MintGreen":
                        colorBrush.TitleBrush = (SolidColorBrush)Application.Current.Resources["MintGreenTitleFoucus"];
                        colorBrush.BackgroundBrush = (SolidColorBrush)Application.Current.Resources["MintGreenBackgroundFoucus"];
                        break;
                    case "BubblePink":
                        colorBrush.TitleBrush = (SolidColorBrush)Application.Current.Resources["BubblePinkTitleFoucus"];
                        colorBrush.BackgroundBrush = (SolidColorBrush)Application.Current.Resources["BubblePinkBackgroundFoucus"];
                        break;
                    case "TaroPurple":
                        colorBrush.TitleBrush = (SolidColorBrush)Application.Current.Resources["TaroPurpleTitleFoucus"];
                        colorBrush.BackgroundBrush = (SolidColorBrush)Application.Current.Resources["TaroPurpleBackgroundFoucus"];
                        break;
                    case "SkyBlue":
                        colorBrush.TitleBrush = (SolidColorBrush)Application.Current.Resources["SkyBlueTitleFoucus"];
                        colorBrush.BackgroundBrush = (SolidColorBrush)Application.Current.Resources["SkyBlueBackgroundFoucus"];
                        break;
                    case "StoneGray":
                        colorBrush.TitleBrush = (SolidColorBrush)Application.Current.Resources["StoneGrayTitleFoucus"];
                        colorBrush.BackgroundBrush = (SolidColorBrush)Application.Current.Resources["StoneGrayBackgroundFoucus"];
                        break;
                }
            }
            else
            {
                switch (theme)
                {
                    case "GingkoYellow":
                        colorBrush.TitleBrush = (SolidColorBrush)Application.Current.Resources["GingkoYellowTitle"];
                        colorBrush.BackgroundBrush = (SolidColorBrush)Application.Current.Resources["GingkoYellowBackground"];
                        break;
                    case "MintGreen":
                        colorBrush.TitleBrush = (SolidColorBrush)Application.Current.Resources["MintGreenTitle"];
                        colorBrush.BackgroundBrush = (SolidColorBrush)Application.Current.Resources["MintGreenBackground"];
                        break;
                    case "BubblePink":
                        colorBrush.TitleBrush = (SolidColorBrush)Application.Current.Resources["BubblePinkTitle"];
                        colorBrush.BackgroundBrush = (SolidColorBrush)Application.Current.Resources["BubblePinkBackground"];
                        break;
                    case "TaroPurple":
                        colorBrush.TitleBrush = (SolidColorBrush)Application.Current.Resources["TaroPurpleTitle"];
                        colorBrush.BackgroundBrush = (SolidColorBrush)Application.Current.Resources["TaroPurpleBackground"];
                        break;
                    case "SkyBlue":
                        colorBrush.TitleBrush = (SolidColorBrush)Application.Current.Resources["SkyBlueTitle"];
                        colorBrush.BackgroundBrush = (SolidColorBrush)Application.Current.Resources["SkyBlueBackground"];
                        break;
                    case "StoneGray":
                        colorBrush.TitleBrush = (SolidColorBrush)Application.Current.Resources["StoneGrayTitle"];
                        colorBrush.BackgroundBrush = (SolidColorBrush)Application.Current.Resources["StoneGrayBackground"];
                        break;
                }
            }
            return colorBrush;
        }
    }
}
