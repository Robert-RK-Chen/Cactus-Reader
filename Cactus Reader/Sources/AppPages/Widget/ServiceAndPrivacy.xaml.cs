using System;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Cactus_Reader.Sources.AppPages
{
    public sealed partial class ServiceAndPrivacy : Page
    {
        public ServiceAndPrivacy()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            try
            {
                StorageFile service = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/服务协议.rtf"));
                StorageFile privacy = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/隐私政策.rtf"));

                IRandomAccessStream serviceStream = await service.OpenAsync(FileAccessMode.Read);
                IRandomAccessStream privacyStream = await privacy.OpenAsync(FileAccessMode.Read);

                // 独立视图线程无 SynchronizationContext，await 后已切到后台线程；
                // RichEditBox.Document 是 UI 线程 COM 对象，跨线程访问抛 RPC_E_WRONG_THREAD，须调度回 UI 线程
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    ServiceTips.Document.LoadFromStream(TextSetOptions.FormatRtf, serviceStream);
                    PrivacyTips.Document.LoadFromStream(TextSetOptions.FormatRtf, privacyStream);
                });
            }
            catch (Exception ex)
            {
                // 防止 async void 未捕获异常导致进程闪退
                System.Diagnostics.Debug.WriteLine("加载服务协议/隐私政策失败: " + ex);
            }
        }
    }
}
