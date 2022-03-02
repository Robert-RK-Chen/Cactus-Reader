using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Cactus_Reader.Sources.AppPages.AppUI
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class SettingPage : Page
    {
        ApplicationDataContainer localSettings = null;
        const string appThemeStyle = "appTheme";
        const string appFont = "font";
        const string appFontSize = "fontSize";

        public SettingPage()
        {
            InitializeComponent();
            localSettings = ApplicationData.Current.LocalSettings;
            if (localSettings.Values[appFontSize] == null)
            {
                localSettings.Values[appFontSize] = 14;
            }
            if (localSettings.Values[appFont] == null)
            {
                localSettings.Values[appFont] = "宋体";
            }
            //previewText.FontFamily = new FontFamily(localSettings.Values[appFont].ToString() ?? "宋体");
            //previewText.FontSize = int.Parse(localSettings.Values[appFontSize].ToString());
        }

        private void AppThemeComboLoaded(object sender, RoutedEventArgs e)
        {
            if (localSettings.Values[appThemeStyle] == null)
            {
                localSettings.Values[appThemeStyle] = "跟随系统设置";
                appThemeCombo.SelectedValue = "跟随系统设置";
            }
            else
            {
                appThemeCombo.SelectedValue = localSettings.Values[appThemeStyle];
            }
        }

        private void ChangeAppTheme(object sender, SelectionChangedEventArgs e)
        {
            string appTheme = appThemeCombo.SelectedValue.ToString();
            localSettings.Values[appThemeStyle] = appTheme;
            switch (appTheme)
            {
                case "使用浅色主题":
                    {
                        ((Frame)Window.Current.Content).RequestedTheme = ElementTheme.Light;
                        break;
                    }
                case "使用深色主题":
                    {
                        ((Frame)Window.Current.Content).RequestedTheme = ElementTheme.Dark;
                        break;
                    }
                case "跟随系统主题":
                    {
                        ((Frame)Window.Current.Content).RequestedTheme = ElementTheme.Default;
                        break;
                    }
                default:
                    {
                        ((Frame)Window.Current.Content).RequestedTheme = ElementTheme.Default;
                        break;
                    }
            }
        }

        private void FontComboLoaded(object sender, RoutedEventArgs e)
        {
            if (localSettings.Values[appFont] == null)
            {
                localSettings.Values[appFont] = "宋体";
                fontsCombo.SelectedValue = "宋体";
            }
            else
            {
                fontsCombo.SelectedValue = localSettings.Values[appFont];
            }
        }

        private void ChangeAppFont(object sender, SelectionChangedEventArgs e)
        {
            string currentFont = fontsCombo.SelectedValue.ToString();
            localSettings.Values[appFont] = currentFont;
            previewText.FontFamily = new FontFamily(currentFont);
        }

        private void DeceaseFontSize(object sender, RoutedEventArgs e)
        {
            var test = localSettings.Values[appFontSize];
            int currentFontSize = int.Parse(localSettings.Values[appFontSize].ToString());
            if (currentFontSize > 12)
            {
                currentFontSize--;
                localSettings.Values[appFontSize] = currentFontSize;
                previewText.FontSize = currentFontSize;
            }
        }

        private void IncreaseFontSize(object sender, RoutedEventArgs e)
        {
            int currentFontSize = int.Parse(localSettings.Values[appFontSize].ToString());
            if (currentFontSize < 30)
            {
                currentFontSize++;
                localSettings.Values[appFontSize] = currentFontSize;
                previewText.FontSize = currentFontSize;
            }
        }
    }
}
