using Cactus_Reader.Entities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage;
using Windows.Web.Http;

namespace Cactus_Reader.Sources.ToolKits
{
    public class ProfileSyncTool
    {
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        // CactusReaderServer 服务地址（同时承担上传与下载，不再依赖 Tomcat）
        readonly static string SERVER_ADDRESS = "http://127.0.0.1:9527/";

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
                if (!Guid.TryParse(UID, out _))
                {
                    return;
                }

                StorageFolder storageFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(UID, CreationCollisionOption.OpenIfExists);

                // 本地已有头像且 ETag 与服务端一致时跳过下载（ETag 按用户隔离存储）
                string etagKey = "profileImageETag_" + UID;
                string localEtag = localSettings.Values[etagKey]?.ToString();

                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    var request = new System.Net.Http.HttpRequestMessage(
                        System.Net.Http.HttpMethod.Get,
                        new Uri(SERVER_ADDRESS + "download-profile-image?uid=" + UID));
                    if (!string.IsNullOrEmpty(localEtag))
                    {
                        request.Headers.TryAddWithoutValidation("If-None-Match", localEtag);
                    }

                    System.Net.Http.HttpResponseMessage response = await httpClient.SendAsync(request);
                    if (!response.IsSuccessStatusCode)
                    {
                        // 404（服务端无头像）或 304（内容一致）均保持本地现状
                        return;
                    }

                    string remoteEtag = response.Headers.ETag?.Tag;
                    if (!string.IsNullOrEmpty(remoteEtag) && remoteEtag == localEtag)
                    {
                        return; // 兜底：即使服务端未返回 304，ETag 一致也无需重复下载
                    }

                    byte[] bytes = await response.Content.ReadAsByteArrayAsync();
                    StorageFile userImageFile = await storageFolder.CreateFileAsync("ProfilePicture.PNG", CreationCollisionOption.OpenIfExists);
                    await FileIO.WriteBytesAsync(userImageFile, bytes);

                    if (!string.IsNullOrEmpty(remoteEtag))
                    {
                        localSettings.Values[etagKey] = remoteEtag;
                    }
                }
            }
            catch (Exception)
            {
                System.Diagnostics.Debug.Write("未连接，无法同步或无法访问资源。");
            }
        }

        /// <summary>
        /// 可等待的下载任务，供同步便签时逐一下载并等待完成。
        /// </summary>
        private async Task DownloadFileAsync(Uri source, StorageFile file)
        {
            BackgroundDownloader downloader = new BackgroundDownloader();
            DownloadOperation download = downloader.CreateDownload(source, file);
            try
            {
                await download.StartAsync().AsTask();
            }
            catch (Exception ex)
            {
                BackgroundTransferError.GetStatus(ex.HResult);
                System.Diagnostics.Debug.WriteLine("下载错误：" + ex.Message);
            }
        }

        /// <summary>
        /// 同步便签：先从服务器获取该用户的便签清单，再将本地缺失的便签逐个下载。
        /// 下载目标：LocalFolder/{UID}/Sticky/{serial}.ctsnote（与本地新建/保存路径一致）。
        /// 返回后可立即刷新本地便签列表。
        /// </summary>
        public async Task SyncUserSticky(string UID)
        {
            try
            {
                if (!Guid.TryParse(UID, out _))
                {
                    return;
                }

                // 1. 获取服务器便签清单
                List<string> remoteFiles = new List<string>();
                using (HttpClient httpClient = new HttpClient())
                {
                    string listJson = await httpClient.GetStringAsync(new Uri(SERVER_ADDRESS + "notes-list?uid=" + UID));
                    if (!string.IsNullOrEmpty(listJson))
                    {
                        remoteFiles = JsonConvert.DeserializeObject<List<string>>(listJson) ?? new List<string>();
                    }
                }
                if (remoteFiles.Count == 0)
                {
                    return;
                }

                // 2. 获取本地已有便签文件名
                StorageFolder storageFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(UID, CreationCollisionOption.OpenIfExists);
                StorageFolder stickyFolder = await storageFolder.CreateFolderAsync("Sticky", CreationCollisionOption.OpenIfExists);
                IReadOnlyList<StorageFile> localFiles = await stickyFolder.GetFilesAsync();
                HashSet<string> localNames = new HashSet<string>(localFiles.Select(f => f.Name));

                // 3. 逐个下载本地缺失的便签
                foreach (string serial in remoteFiles)
                {
                    if (localNames.Contains(serial))
                    {
                        continue;
                    }

                    Uri source = new Uri(SERVER_ADDRESS + "download-cactus-notes?uid=" + UID + "&serial=" + serial);
                    StorageFile stickyFile = await stickyFolder.CreateFileAsync(serial, CreationCollisionOption.OpenIfExists);
                    await DownloadFileAsync(source, stickyFile);
                }
            }
            catch (Exception)
            {
                System.Diagnostics.Debug.Write("未连接，无法同步便签。");
            }
        }
    }
}
