using System;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Cactus_Reader.Sources.AppPages.AppUI
{
    public sealed partial class AboutInfoPage : Page
    {
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

        public AboutInfoPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            string UID = localSettings.Values["UID"].ToString();

            if (Guid.TryParse(UID, out _))
            {
                email.Text = localSettings.Values["email"].ToString();
                renewTime.Text = localSettings.Values["renewDate"].ToString();
                supportID.Text = UID;
            }
        }

        private async void ReadServiceAndPrivacy(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            CoreApplicationView newView = CoreApplication.CreateNewView();
            int newViewId = 0;
            await newView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Frame frame = new();
                frame.Navigate(typeof(ServiceAndPrivacy), null, new DrillInNavigationTransitionInfo());
                Window.Current.Content = frame;
                Window.Current.Activate();
                newViewId = ApplicationView.GetForCurrentView().Id;
            });
            bool viewShown = await ApplicationViewSwitcher.TryShowAsStandaloneAsync(newViewId);
        }
    }
}
