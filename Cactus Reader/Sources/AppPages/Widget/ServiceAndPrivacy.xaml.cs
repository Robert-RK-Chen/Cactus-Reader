using System;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Text;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Cactus_Reader.Sources.AppPages
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class ServiceAndPrivacy : Page
    {
        public ServiceAndPrivacy()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            StorageFile service = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/服务协议.rtf"));
            StorageFile privacy = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/隐私政策.rtf"));

            IRandomAccessStream serviceStream = await service.OpenAsync(FileAccessMode.Read);
            IRandomAccessStream privacyStream = await privacy.OpenAsync(FileAccessMode.Read);

            ServiceTips.Document.LoadFromStream(TextSetOptions.FormatRtf, serviceStream);
            PrivacyTips.Document.LoadFromStream(TextSetOptions.FormatRtf, privacyStream);
        }
    }
}
