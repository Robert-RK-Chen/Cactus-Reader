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
                localSettings.Values["Email"] = currentUser.Email;
                localSettings.Values["Name"] = currentUser.Name;
                localSettings.Values["Mobile"] = currentUser.Mobile;
                localSettings.Values["RegistDate"] = currentUser.RegistDate.ToString("yyyy' 年 'MM' 月 'dd' 日'");
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async void SyncUserImage(string UID)
        {
            try
            {
                if (Guid.TryParse(UID, out _))
                {
                    StorageFolder storageFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(UID, CreationCollisionOption.OpenIfExists);
                    Uri source = new Uri(SERVER_ADDRESS + UID + "/ProfilePicture.PNG");
                    StorageFile userImageFile = await storageFolder.CreateFileAsync("ProfilePicture.PNG", CreationCollisionOption.OpenIfExists);
                    BackgroundDownloader downloader = new BackgroundDownloader();
                    DownloadOperation download = downloader.CreateDownload(source, userImageFile);
                    await download.StartAsync();
                    System.Diagnostics.Debug.WriteLine("同步完成！");
                }
            }
            catch (Exception)
            {
                System.Diagnostics.Debug.Write("未连接，无法同步或无法访问资源。");
            }
        }
    }
}
