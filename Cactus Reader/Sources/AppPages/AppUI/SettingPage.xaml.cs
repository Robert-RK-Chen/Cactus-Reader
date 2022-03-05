using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Cactus_Reader.Sources.AppPages.AppUI
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class SettingPage : Page
    {
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

        public SettingPage()
        {
            InitializeComponent();
            if (localSettings.Values["fontSize"] == null)
            {
                localSettings.Values["fontSize"] = 14;
            }
            previewText.FontSize = (int)localSettings.Values["fontSize"];
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            string UID = localSettings.Values["UID"].ToString();

            name.Text = localSettings.Values["Name"].ToString();
            email.Text = localSettings.Values["Email"].ToString();
        }

        private void SignOut(object sender, RoutedEventArgs e)
        {
            localSettings.Values["isLogin"] = "false";
            StartPage.startPage.mainContent.Navigate(typeof(StartPage), null, new DrillInNavigationTransitionInfo());
        }

        private void AppThemeComboLoaded(object sender, RoutedEventArgs e)
        {
            if (localSettings.Values["appTheme"] == null)
            {
                localSettings.Values["appTheme"] = "跟随系统设置";
                appThemeCombo.SelectedValue = "跟随系统设置";
            }
            else
            {
                appThemeCombo.SelectedValue = localSettings.Values["appTheme"];
            }
        }

        private void ChangeAppTheme(object sender, SelectionChangedEventArgs e)
        {
            string appTheme = appThemeCombo.SelectedValue.ToString();
            localSettings.Values["appTheme"] = appTheme;
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
            if (localSettings.Values["font"] == null)
            {
                localSettings.Values["font"] = "宋体";
            }
            fontsCombo.SelectedValue = localSettings.Values["font"];
        }

        private void ChangeAppFont(object sender, SelectionChangedEventArgs e)
        {
            string currentFont = fontsCombo.SelectedValue.ToString();
            localSettings.Values["font"] = currentFont;
            previewText.FontFamily = new FontFamily(currentFont);
        }

        private void DeceaseFontSize(object sender, RoutedEventArgs e)
        {
            int currentFontSize = int.Parse(localSettings.Values["fontSize"].ToString());
            if (currentFontSize > 12)
            {
                currentFontSize--;
                localSettings.Values["fontSize"] = currentFontSize;
                previewText.FontSize = currentFontSize;
            }
        }

        private void IncreaseFontSize(object sender, RoutedEventArgs e)
        {
            int currentFontSize = int.Parse(localSettings.Values["fontSize"].ToString());
            if (currentFontSize < 30)
            {
                currentFontSize++;
                localSettings.Values["fontSize"] = currentFontSize;
                previewText.FontSize = currentFontSize;
            }
        }

        private void HideUserImage(object sender, SizeChangedEventArgs e)
        {
            if (Window.Current.Bounds.Width <= 640)
            {
                userProfileImage.Visibility = Visibility.Collapsed;
            }
            else
            {
                userProfileImage.Visibility = Visibility.Visible;
            }
        }
    }
}
