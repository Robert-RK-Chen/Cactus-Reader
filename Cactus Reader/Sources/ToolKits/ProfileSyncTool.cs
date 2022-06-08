using Cactus_Reader.Entities;
using System;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage;

namespace Cactus_Reader.Sources.ToolKits
{
    public class ProfileSyncTool
    {
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        readonly static string SERVER_ADDRESS = "http://106.54.173.192/cactus-reader-repo/";

        private static ProfileSyncTool instance;

        public static ProfileSyncTool Instance
        {
            get
            {
                return instance ?? (instance = new ProfileSyncTool());
            }
        }

        private ProfileSyncTool() { }

        public bool LoadCurrentUser(User currentUser)
        {
            try
            {
                localSettings.Values["isLogin"] = true;
                localSettings.Values["UID"] = currentUser.UID;
                localSettings.Values["email"] = currentUser.Email;
                localSettings.Values["name"] = currentUser.Name;
                localSettings.Values["mobile"] = currentUser.Mobile;
                localSettings.Values["renewDate"] = currentUser.RegistDate.AddYears(1).ToString("yyyy' 年 'MM' 月 'dd' 日'");
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async void SetDownload(DownloadOperation opr, bool starting)
        {
            // 当上传进度更新时能收到报告
            Progress<DownloadOperation> progressReporter = new Progress<DownloadOperation>(OnProgressHandler);
            // 启动或附加任务
            try
            {
                if (starting)
                {
                    await opr.StartAsync().AsTask(progressReporter);
                }
                else
                {
                    await opr.AttachAsync().AsTask(progressReporter);
                }
            }
            catch (Exception ex)
            {
                var state = BackgroundTransferError.GetStatus(ex.HResult);
                System.Diagnostics.Debug.WriteLine("错误：" + state);
            }
        }

        private void OnProgressHandler(DownloadOperation p)
        {
            BackgroundDownloadProgress progress = p.Progress;
            switch (progress.Status)
            {
                case BackgroundTransferStatus.Canceled:
                    System.Diagnostics.Debug.WriteLine("任务已取消。");
                    break;
                case BackgroundTransferStatus.Completed:
                    System.Diagnostics.Debug.WriteLine("任务已完成。");
                    break;
                case BackgroundTransferStatus.Error:
                    System.Diagnostics.Debug.WriteLine("发生了错误。");
                    break;
                case BackgroundTransferStatus.Running:
                    System.Diagnostics.Debug.WriteLine("任务执行中。");
                    break;
            }
        }

        public async void SyncUserImage(string UID)
        {
            try
            {
                if (Guid.TryParse(UID, out _))
                {
                    // 获取服务器资源路径
                    Uri source = new Uri(SERVER_ADDRESS + UID + "/ProfilePicture.PNG");

                    // 本地保存位置
                    StorageFolder storageFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(UID, CreationCollisionOption.OpenIfExists);
                    StorageFile userImageFile = await storageFolder.CreateFileAsync("ProfilePicture.PNG", CreationCollisionOption.OpenIfExists);

                    // 下载服务器资源
                    BackgroundDownloader downloader = new BackgroundDownloader();
                    DownloadOperation download = downloader.CreateDownload(source, userImageFile);
                    SetDownload(download, true);
                }
            }
            catch (Exception)
            {
                System.Diagnostics.Debug.Write("未连接，无法同步或无法访问资源。");
            }
        }

        public async void SyncUserSticky(string UID)
        {
            try
            {
                if (Guid.TryParse(UID, out _))
                {
                    // 获取服务器资源路径
                    Uri source = new Uri(SERVER_ADDRESS + UID + "/ProfilePicture.PNG");

                    // 本地保存位置
                    StorageFolder storageFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(UID, CreationCollisionOption.OpenIfExists);
                    StorageFile userImageFile = await storageFolder.CreateFileAsync("ProfilePicture.PNG", CreationCollisionOption.OpenIfExists);

                    // 下载服务器资源
                    BackgroundDownloader downloader = new BackgroundDownloader();
                    DownloadOperation download = downloader.CreateDownload(source, userImageFile);
                    SetDownload(download, true);
                }
            }
            catch(Exception)
            {
                
            }
        }
    }
}