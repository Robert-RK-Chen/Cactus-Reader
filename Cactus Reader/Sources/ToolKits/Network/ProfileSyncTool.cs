using Cactus_Reader.Entities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Networking.BackgroundTransfer;
using Windows.Security.Cryptography;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Web.Http;

namespace Cactus_Reader.Sources.ToolKits
{
    public class ProfileSyncTool
    {
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        // CactusReaderServer 服务地址（与 ApiClient 共用单一常量，末尾带 / 便于拼接路径）
        private const string SERVER_ADDRESS = ApiClient.BaseUrl + "/";

        private static ProfileSyncTool instance;

        public static ProfileSyncTool Instance
        {
            get
            {
                return instance ?? (instance = new ProfileSyncTool());
            }
        }

        /// <summary>
        /// 同步互斥锁：登录全量同步（SyncAllLocalContent）与进便签页增量同步（SyncUserSticky）
        /// 可能同时下载同一批文件，并发写同一 StorageFile 会锁冲突失败；串行化保证稳定。
        /// </summary>
        private static readonly System.Threading.SemaphoreSlim syncLock = new System.Threading.SemaphoreSlim(1, 1);

        private ProfileSyncTool() { }

        /// <summary>
        /// 跨设备同步开关：默认开启；关闭时不执行任何上传/下载，仅维持本地内容。
        /// </summary>
        public static bool IsSyncEnabled()
        {
            ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;
            object value = settings.Values["syncEnabled"];
            if (value == null)
            {
                settings.Values["syncEnabled"] = true;
                return true;
            }
            return (bool)value;
        }

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
            // 进度上报 + 启动/附加任务
            Progress<DownloadOperation> progressReporter = new Progress<DownloadOperation>(OnProgressHandler);
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
            await syncLock.WaitAsync();
            try
            {
                if (!IsSyncEnabled())
                {
                    return;
                }

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
                System.Diagnostics.Debug.WriteLine("未连接，无法同步或无法访问资源。");
            }
            finally
            {
                syncLock.Release();
            }
        }

        /// <summary>
        /// 可等待的下载任务，供同步便签时逐一下载并等待完成。
        /// 使用 HttpClient 字节流下载（与头像下载同实现，可靠支持本地回环地址；
        /// BackgroundDownloader 对 localhost 不稳定，卸载重登后首次同步常因它失败）。
        /// 返回是否下载成功；失败时调用方应清理残留的空文件，避免下次同步误判为"已有"。
        /// </summary>
        private async Task<bool> DownloadFileAsync(Uri source, StorageFile file)
        {
            try
            {
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    using (System.Net.Http.HttpResponseMessage response = await httpClient.GetAsync(source))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            return false;
                        }
                        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
                        await FileIO.WriteBytesAsync(file, bytes);
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                System.Diagnostics.Debug.WriteLine("下载失败：" + source);
                return false;
            }
        }

        /// <summary>下载失败时清理残留文件（空文件/半截文件会令后续同步误判为已存在而跳过）。</summary>
        private static async Task DeleteIfExistsAsync(StorageFile file)
        {
            try
            {
                if (file != null)
                {
                    await file.DeleteAsync();
                }
            }
            catch (Exception)
            {
                // 文件已删除/占用：忽略
            }
        }

        /// <summary>
        /// 获取服务端便签文件名清单（同步开关关闭 / UID 非法 / 网络异常时返回空列表）。
        /// 供同步与"是否存在便签数据"判断复用。
        /// </summary>
        public async Task<List<string>> GetRemoteStickyListAsync(string UID)
        {
            List<string> remoteFiles = new List<string>();
            if (!IsSyncEnabled() || !Guid.TryParse(UID, out _))
            {
                return remoteFiles;
            }

            try
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    string listJson = await httpClient.GetStringAsync(new Uri(SERVER_ADDRESS + "notes-list?uid=" + UID));
                    if (!string.IsNullOrEmpty(listJson))
                    {
                        remoteFiles = JsonConvert.DeserializeObject<List<string>>(listJson) ?? new List<string>();
                    }
                }
            }
            catch (Exception)
            {
                // 网络异常：按无远端数据处理
            }
            return remoteFiles;
        }

        /// <summary>
        /// 同步便签：先从服务器获取该用户的便签清单，再将本地缺失的便签逐个下载。
        /// 下载目标：LocalFolder/{UID}/Sticky/{serial}.ctsnote（与本地新建/保存路径一致）。
        /// 本地回收站中已有的便签（已删除进回收站）不下载，避免关闭同步期间的删除被"复活"。
        /// 与全量同步互斥（串行锁），下载失败清理残留空文件。返回后可立即刷新本地便签列表。
        /// </summary>
        public async Task SyncUserSticky(string UID)
        {
            await syncLock.WaitAsync();
            try
            {
                if (!IsSyncEnabled())
                {
                    return;
                }

                if (!Guid.TryParse(UID, out _))
                {
                    return;
                }

                // 1. 获取服务器便签清单
                List<string> remoteFiles = await GetRemoteStickyListAsync(UID);
                if (remoteFiles.Count == 0)
                {
                    return;
                }

                // 2. 获取本地已有便签文件名
                StorageFolder storageFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(UID, CreationCollisionOption.OpenIfExists);
                StorageFolder stickyFolder = await storageFolder.CreateFolderAsync("Sticky", CreationCollisionOption.OpenIfExists);
                IReadOnlyList<StorageFile> localFiles = await stickyFolder.GetFilesAsync();
                HashSet<string> localNames = new HashSet<string>(localFiles.Select(f => f.Name));

                // 3. 本地回收站中已删除的便签文件名（跳过下载，防止删除被复活）
                List<RecycleItem> recycleItems = await RecycleService.LoadRecycleListAsync(UID);
                HashSet<string> deletedNames = new HashSet<string>(
                    recycleItems.Where(r => r.ItemType == CollectibleType.Sticky)
                                .Select(r => r.OriginalSerial + ".ctsnote"));

                // 4. 逐个下载本地缺失的便签
                foreach (string serial in remoteFiles)
                {
                    if (localNames.Contains(serial) || deletedNames.Contains(serial))
                    {
                        continue;
                    }

                    Uri source = new Uri(SERVER_ADDRESS + "download-cactus-notes?uid=" + UID + "&serial=" + serial);
                    StorageFile stickyFile = await stickyFolder.CreateFileAsync(serial, CreationCollisionOption.OpenIfExists);
                    if (!await DownloadFileAsync(source, stickyFile))
                    {
                        // 下载失败：清理空文件，避免下次同步误判为"已有"而永久跳过
                        await DeleteIfExistsAsync(stickyFile);
                    }
                }
            }
            catch (Exception)
            {
                System.Diagnostics.Debug.WriteLine("未连接，无法同步便签。");
            }
            finally
            {
                syncLock.Release();
            }
        }

        /// <summary>
        /// 全量同步（双向合并，云端权威）：重新开启跨设备同步时调用。
        ///
        /// 与旧版 replace_cloud（以本地为准、删除云端多余）不同，合并规则保证：
        ///   1. 绝不因"本地缺失"删除云端数据 —— 关闭同步期间本地删除的内容，
        ///      云端副本保留，再次开启时从云端下载恢复；
        ///   2. 本地已删除（回收站有条目）的云端文件 move 到云端回收站区，尊重删除意图；
        ///   3. 本地有云端没有的（关闭期间新增/修改）上传到云端；
        ///   4. 换设备：云端全部内容（便签 / 阅读记录 / 回收站）拉回本地。
        ///
        /// 覆盖范围：头像 / 便签 / 阅读记录 / 回收站。
        /// </summary>
        public async Task SyncAllLocalContent(string UID)
        {
            await syncLock.WaitAsync();
            try
            {
                if (!Guid.TryParse(UID, out _))
                {
                    return;
                }

                StorageFolder storageFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(UID, CreationCollisionOption.OpenIfExists);

                // 0. 无密码模式密钥备份：未设置个人密码时把便签密钥明文备份上云，
                //    保证卸载 / 换设备后免密恢复便签（登录与重开同步必触发）
                await EncryptStickyTool.Instance.BackupPlainKeyIfNeededAsync();

                // 1. 头像：本地有则上传；本地无则从云端下载（换设备恢复）
                await MergeAvatarAsync(UID, storageFolder);

                // 2. 便签：双向合并（下载云端缺失 / 上传本地新增 / 已删除的 move 到云端回收站）
                await MergeStickyAsync(UID, storageFolder);

                // 3. 阅读记录：双向合并（下载云端记录 / 上传本地新增 / 已删除的 move 到云端回收站）
                await MergeLibraryAsync(UID);

                // 4. 回收站：清单与文件双向合并（换设备恢复条目与文件 / 彻底删除同步云端）
                await MergeRecycleAsync(UID, storageFolder);
            }
            catch (Exception)
            {
                System.Diagnostics.Debug.WriteLine("未连接，无法全量同步。");
            }
            finally
            {
                syncLock.Release();
            }
        }

        /// <summary>头像合并：本地有 → 上传并清除 ETag 缓存；本地无 → 从云端下载（有则恢复）。</summary>
        private async Task MergeAvatarAsync(string UID, StorageFolder storageFolder)
        {
            // TryGetItemAsync 不抛 FileNotFoundException：卸载重装后本地无头像属正常场景，避免异常提示
            StorageFile imageFile = await storageFolder.TryGetItemAsync("ProfilePicture.PNG") as StorageFile;
            if (imageFile != null)
            {
                try
                {
                    await UploadRawFileAsync(imageFile, UID, "/upload-profile-image", null);
                    localSettings.Values.Remove("profileImageETag_" + UID);
                }
                catch (Exception)
                {
                    // 上传失败：忽略
                }
            }
            else
            {
                // 本地无头像：尝试从云端下载
                try
                {
                    using (var httpClient = new System.Net.Http.HttpClient())
                    {
                        using (System.Net.Http.HttpResponseMessage response =
                            await httpClient.GetAsync(new Uri(SERVER_ADDRESS + "download-profile-image?uid=" + UID)))
                        {
                            if (response.IsSuccessStatusCode)
                            {
                                byte[] bytes = await response.Content.ReadAsByteArrayAsync();
                                StorageFile userImageFile = await storageFolder.CreateFileAsync(
                                    "ProfilePicture.PNG", CreationCollisionOption.OpenIfExists);
                                await FileIO.WriteBytesAsync(userImageFile, bytes);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // 云端无头像 / 网络异常：忽略
                }
            }
        }

        /// <summary>
        /// 便签合并：
        /// 云端有、本地无 → 若本地回收站有条目（已删除）则 move 到云端回收站，否则下载恢复；
        /// 本地有（含两边都有）→ 上传本地覆盖云端（关闭期间本地修改以本地为准）。
        /// </summary>
        private async Task MergeStickyAsync(string UID, StorageFolder storageFolder)
        {
            StorageFolder stickyFolder = await storageFolder.CreateFolderAsync("Sticky", CreationCollisionOption.OpenIfExists);
            IReadOnlyList<StorageFile> localFiles = await stickyFolder.GetFilesAsync();
            HashSet<string> localNames = new HashSet<string>(localFiles.Select(f => f.Name));

            // 本地回收站中已删除的便签文件名（云端 Notes 残留应 move 到回收站而非下载复活）
            List<RecycleItem> recycleItems = await RecycleService.LoadRecycleListAsync(UID);
            HashSet<string> deletedNames = new HashSet<string>(
                recycleItems.Where(r => r.ItemType == CollectibleType.Sticky)
                            .Select(r => r.OriginalSerial + ".ctsnote"));

            List<string> remoteFiles = await GetRemoteStickyListAsync(UID);
            HashSet<string> remoteNames = new HashSet<string>(remoteFiles);

            // 1. 云端有、本地无
            foreach (string serial in remoteFiles)
            {
                if (localNames.Contains(serial))
                {
                    continue;
                }
                if (deletedNames.Contains(serial))
                {
                    // 本地已删除进回收站 → 云端 Notes 残留移到 Recycle 区（尊重删除意图）
                    await TryMoveCloudAsync(UID, serial, "notes", "recycle");
                    continue;
                }
                // 换设备 / 关闭期间误删 → 从云端下载恢复
                try
                {
                    Uri source = new Uri(SERVER_ADDRESS + "download-cactus-notes?uid=" + UID + "&serial=" + serial);
                    StorageFile stickyFile = await stickyFolder.CreateFileAsync(serial, CreationCollisionOption.OpenIfExists);
                    if (!await DownloadFileAsync(source, stickyFile))
                    {
                        // 下载失败：清理空文件，避免下次同步误判为"已有"而永久跳过
                        await DeleteIfExistsAsync(stickyFile);
                    }
                }
                catch (Exception)
                {
                    // 单条下载失败：跳过继续
                }
            }

            // 2. 本地有（含云端已有同名）→ 上传覆盖，保证关闭期间本地修改同步到云端
            foreach (StorageFile file in localFiles)
            {
                await UploadRawFileAsync(file, UID, "/upload-cactus-notes", file.Name);
            }
        }

        /// <summary>
        /// 阅读记录合并：
        /// 云端 Library 有、本地无 → 若本地回收站有条目（已删除）则 move 到云端回收站，否则下载合并进 library.json；
        /// 本地有（含两边都有）→ 上传覆盖云端（关闭期间本地进度/收藏修改以本地为准）。
        /// </summary>
        private async Task MergeLibraryAsync(string UID)
        {
            List<ReadingItem> localReadings = await LibraryService.LoadReadingListAsync(UID);
            HashSet<string> localSerials = new HashSet<string>(localReadings.Select(r => r.Serial));

            // 本地回收站中已删除的阅读记录 Serial（云端 Library 残留应 move 到回收站）
            List<RecycleItem> recycleItems = await RecycleService.LoadRecycleListAsync(UID);
            HashSet<string> deletedSerials = new HashSet<string>(
                recycleItems.Where(r => r.ItemType != CollectibleType.Sticky)
                            .Select(r => r.OriginalSerial));

            List<string> remoteFiles = await ApiClient.ListLibraryFilesAsync(UID);

            bool localChanged = false;

            // 1. 云端有、本地无
            foreach (string remoteFile in remoteFiles)
            {
                string serial = remoteFile.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                    ? remoteFile.Substring(0, remoteFile.Length - 5)
                    : remoteFile;
                if (deletedSerials.Contains(serial))
                {
                    // 本地已删除进回收站 → 云端 Library 残留移到 Recycle 区
                    await TryMoveCloudAsync(UID, remoteFile, "library", "recycle");
                    continue;
                }
                if (localSerials.Contains(serial))
                {
                    continue;
                }
                // 换设备 / 关闭期间删除的阅读记录 → 下载合并回本地
                try
                {
                    byte[] bytes = await ApiClient.DownloadLibraryFileAsync(UID, remoteFile);
                    if (bytes.Length == 0)
                    {
                        continue;
                    }
                    ReadingItem remoteReading = JsonConvert.DeserializeObject<ReadingItem>(
                        System.Text.Encoding.UTF8.GetString(bytes));
                    if (remoteReading == null || string.IsNullOrEmpty(remoteReading.Serial))
                    {
                        continue;
                    }
                    localReadings.Add(remoteReading);
                    localSerials.Add(remoteReading.Serial);
                    localChanged = true;
                }
                catch (Exception)
                {
                    // 单条下载失败：跳过继续
                }
            }

            // 2. 本地有（含云端已有）→ 上传覆盖，保证关闭期间本地进度 / 收藏状态同步到云端
            foreach (ReadingItem item in localReadings)
            {
                await TryUploadLibraryAsync(UID, item);
            }

            // 3. 有云端记录合入时写回本地
            if (localChanged)
            {
                await LibraryService.SaveReadingListAsync(UID, localReadings);
            }
        }

        /// <summary>
        /// 回收站合并：
        /// 清单 recycle.json：本地有 → 上传覆盖；本地无云端有 → 下载（换设备恢复条目）；
        /// 云端 Recycle 便签文件：本地有条目但缺文件 → 下载；本地无条目 → 已彻底删除 → 删云端；
        /// 云端 Recycle 阅读记录 .json：本地有条目 → 保留云端；无 → 已彻底删除 → 删云端；
        /// 本地 Recycle 便签文件云端无 → 上传。
        /// </summary>
        private async Task MergeRecycleAsync(string UID, StorageFolder storageFolder)
        {
            StorageFolder recycleFolder = await RecycleService.GetRecycleFolderAsync(UID);

            // 1. 云端文件清单
            List<string> remoteFiles = await ApiClient.ListRecycleFilesAsync(UID);
            HashSet<string> remoteNames = new HashSet<string>(remoteFiles);

            // 2. 本地清单 recycle.json：本地有 → 上传覆盖；本地无且云端有 → 下载（换设备恢复条目）
            StorageFile localListFile = await recycleFolder.TryGetItemAsync("recycle.json") as StorageFile;
            if (localListFile != null)
            {
                await UploadRawFileAsync(localListFile, UID, "/upload-cactus-recycle", "recycle.json");
            }
            else if (remoteNames.Contains("recycle.json"))
            {
                byte[] listBytes = await ApiClient.DownloadRecycleFileAsync(UID, "recycle.json");
                if (listBytes.Length > 0)
                {
                    StorageFile target = await recycleFolder.CreateFileAsync("recycle.json", CreationCollisionOption.OpenIfExists);
                    await FileIO.WriteBytesAsync(target, listBytes);
                }
            }

            // 3. 重新读取本地条目（下载清单后条目可能已变化）
            List<RecycleItem> localItems = await RecycleService.LoadRecycleListAsync(UID);
            HashSet<string> deletedStickySerials = new HashSet<string>(
                localItems.Where(r => r.ItemType == CollectibleType.Sticky).Select(r => r.OriginalSerial));
            HashSet<string> deletedReadingSerials = new HashSet<string>(
                localItems.Where(r => r.ItemType != CollectibleType.Sticky).Select(r => r.OriginalSerial));

            // 4. 本地回收站文件（排除清单）
            IReadOnlyList<StorageFile> localFiles = await recycleFolder.GetFilesAsync();
            HashSet<string> localNames = new HashSet<string>(localFiles.Select(f => f.Name));

            // 5. 云端 Recycle 文件 → 本地
            foreach (string remoteFile in remoteFiles)
            {
                if (remoteFile == "recycle.json")
                {
                    continue;
                }
                if (remoteFile.EndsWith(".ctsnote", StringComparison.OrdinalIgnoreCase))
                {
                    string baseName = remoteFile.Substring(0, remoteFile.Length - 8);
                    if (!deletedStickySerials.Contains(baseName))
                    {
                        // 本地无此便签回收条目 → 已彻底删除 → 删除云端副本
                        await TryDeleteCloudAsync(UID, remoteFile, "recycle");
                    }
                    else if (!localNames.Contains(remoteFile))
                    {
                        // 有条目但本地文件缺失（换设备）→ 下载补全
                        byte[] bytes = await ApiClient.DownloadRecycleFileAsync(UID, remoteFile);
                        if (bytes.Length > 0)
                        {
                            StorageFile target = await recycleFolder.CreateFileAsync(remoteFile, CreationCollisionOption.OpenIfExists);
                            await FileIO.WriteBytesAsync(target, bytes);
                        }
                    }
                }
                else if (remoteFile.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    // 阅读记录存档 .json：本地有条目 → 保留云端；无 → 已彻底删除 → 删云端
                    string baseName = remoteFile.Substring(0, remoteFile.Length - 5);
                    if (!deletedReadingSerials.Contains(baseName))
                    {
                        await TryDeleteCloudAsync(UID, remoteFile, "recycle");
                    }
                }
                // 其他未知文件（如 .txt 缓存残留）：云端无本地对应条目意义，跳过
            }

            // 6. 本地 Recycle 便签文件 → 云端（云端缺失的补上传；缓存 .txt 正文只在本地，不同步）
            foreach (StorageFile file in localFiles)
            {
                if (file.Name == "recycle.json" ||
                    !file.Name.EndsWith(".ctsnote", StringComparison.OrdinalIgnoreCase) ||
                    remoteNames.Contains(file.Name))
                {
                    continue;
                }
                await UploadRawFileAsync(file, UID, "/upload-cactus-recycle", file.Name);
            }
        }

        /// <summary>云端跨区移动（网络异常静默降级）。</summary>
        private static async Task TryMoveCloudAsync(string uid, string serial, string fromSection, string toSection)
        {
            try
            {
                await ApiClient.MoveFileAsync(uid, serial, fromSection, toSection);
            }
            catch (Exception)
            {
                // 网络异常：云端残留会在下次同步时按本地收敛
            }
        }

        /// <summary>上传阅读记录存档到云端 Library 区（网络异常静默降级）。</summary>
        private static async Task TryUploadLibraryAsync(string uid, ReadingItem item)
        {
            try
            {
                byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(item));
                await ApiClient.UploadLibraryFileAsync(uid, item.Serial + ".json", jsonBytes);
            }
            catch (Exception)
            {
                // 网络异常：云端缺档会在下次同步时按本地补齐
            }
        }

        /// <summary>云端删除（网络异常静默降级）。</summary>
        private static async Task TryDeleteCloudAsync(string uid, string serial, string section)
        {
            try
            {
                if (section == "recycle")
                {
                    await ApiClient.DeleteRecycleFileAsync(uid, serial);
                }
                else
                {
                    await ApiClient.DeleteLibraryFileAsync(uid, serial);
                }
            }
            catch (Exception)
            {
                // 网络异常：云端残留会在下次同步时按本地收敛
            }
        }

        /// <summary>
        /// 以原始字节流上传文件（application/octet-stream），Header 携带 UID / Serial。
        /// 服务端直接落盘，全量同步专用（避开 BackgroundUploader 的 multipart 编码）。
        /// </summary>
        private async Task UploadRawFileAsync(StorageFile file, string UID, string method, string serial)
        {
            // 读取 IBuffer 后转换为字节数组
            IBuffer buffer = await FileIO.ReadBufferAsync(file);
            CryptographicBuffer.CopyToByteArray(buffer, out byte[] bytes);
            using (var httpClient = new System.Net.Http.HttpClient())
            using (var content = new System.Net.Http.ByteArrayContent(bytes))
            {
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                using (var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, new Uri(SERVER_ADDRESS + method)))
                {
                    request.Content = content;
                    request.Headers.Add("UID", UID);
                    if (!string.IsNullOrEmpty(serial))
                    {
                        request.Headers.Add("Serial", serial);
                    }
                    using (System.Net.Http.HttpResponseMessage response = await httpClient.SendAsync(request))
                    {
                        response.EnsureSuccessStatusCode();
                    }
                }
            }
        }
    }
}
