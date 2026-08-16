using Cactus_Reader.Entities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

namespace Cactus_Reader.Sources.ToolKits
{
    /// <summary>
    /// 回收站原子操作服务：recycle.json 清单的读写，以及便签 / 阅读记录
    /// "删除进回收站 → 恢复 / 彻底删除" 全流程（本地文件移动 + 云端三区迁移）。
    ///
    /// 本地布局：
    ///   LocalFolder/{UID}/Recycle/recycle.json        回收站清单
    ///   LocalFolder/{UID}/Recycle/{serial}.ctsnote    被删除的便签（保持加密原样移动）
    ///   LocalFolder/{UID}/Recycle/{serial}.txt        被删除的网络文档缓存正文
    ///
    /// 云端布局（服务端 DataRoot/{UID}/）：
    ///   Notes/{serial}   便签在用区；  Recycle/{serial}  删除后暂存区
    ///   Library/{serial} 阅读存档区；  Recycle/{serial}  删除后暂存区
    /// 进回收站 = /move-file 移到 recycle；恢复 = 移回原区；彻底删除 = /delete-*-file。
    /// 同步关闭时仅执行本地操作，云端保持现状（下次同步按本地为准收敛）。
    /// </summary>
    public static class RecycleService
    {
        private const string ListFileName = "recycle.json";

        // ---------------- 目录 / 清单 ----------------

        /// <summary>获取（必要时创建）回收站目录 LocalFolder/{UID}/Recycle。</summary>
        public static async Task<StorageFolder> GetRecycleFolderAsync(string uid)
        {
            StorageFolder userFolder = await ApplicationData.Current.LocalFolder
                .CreateFolderAsync(uid, CreationCollisionOption.OpenIfExists);
            return await userFolder.CreateFolderAsync("Recycle", CreationCollisionOption.OpenIfExists);
        }

        /// <summary>读取回收站清单，按删除时间降序；文件缺失返回空列表。</summary>
        public static async Task<List<RecycleItem>> LoadRecycleListAsync(string uid)
        {
            try
            {
                StorageFolder recycleFolder = await GetRecycleFolderAsync(uid);
                StorageFile listFile = await recycleFolder.TryGetItemAsync(ListFileName) as StorageFile;
                if (listFile == null)
                {
                    return new List<RecycleItem>();
                }
                string json = await Task.Run(() => File.ReadAllText(listFile.Path));
                List<RecycleItem> list = JsonConvert.DeserializeObject<List<RecycleItem>>(json);
                if (list == null)
                {
                    return new List<RecycleItem>();
                }
                return list
                    .Where(r => r != null)
                    .OrderByDescending(r => r.DeleteTime == default ? DateTime.MinValue : r.DeleteTime)
                    .ToList();
            }
            catch (Exception)
            {
                return new List<RecycleItem>();
            }
        }

        /// <summary>整体落盘回收站清单（后台线程原子写）。</summary>
        public static async Task SaveRecycleListAsync(string uid, List<RecycleItem> list)
        {
            StorageFolder recycleFolder = await GetRecycleFolderAsync(uid);
            StorageFile listFile = await recycleFolder.CreateFileAsync(
                ListFileName, CreationCollisionOption.OpenIfExists);
            string json = JsonConvert.SerializeObject(list);
            await Task.Run(() => File.WriteAllText(listFile.Path, json));
        }

        // ---------------- 删除进回收站 ----------------

        /// <summary>
        /// 便签进回收站：本地文件移入 Recycle 目录（保持加密，不做解密再加密），
        /// 记录条目到 recycle.json（Payload 存明文 Sticky JSON 供预览，解密失败为 null），
        /// 云端 Notes 区文件移到 Recycle 区。未保存的便签（本地无文件）直接丢弃不进入回收站。
        /// </summary>
        public static async Task MoveStickyToRecycleAsync(string uid, string serial)
        {
            StorageFolder stickyFolder = await StickyService.GetStickyFolderAsync(uid);
            StorageFile stickyFile = await stickyFolder.TryGetItemAsync(serial + ".ctsnote") as StorageFile;
            if (stickyFile == null)
            {
                return; // 未保存过：服务端无存档，本地无文件，直接丢弃
            }

            // 尝试解密获取展示信息（密钥未就绪时 Payload 为 null，标题兜底"便签"）
            Sticky sticky = await StickyService.LoadStickyAsync(uid, serial);
            string payload = sticky != null ? JsonConvert.SerializeObject(sticky) : null;
            // 标题取便签第一行内容（与收藏夹一致），内容为空才回退"便签"
            string name = sticky != null ? StickyService.GetStickyTitle(sticky) : "便签";

            // 1. 本地：文件移入回收站目录（MoveAsync 跨目录即移动）
            StorageFolder recycleFolder = await GetRecycleFolderAsync(uid);
            await stickyFile.MoveAsync(recycleFolder, stickyFile.Name, NameCollisionOption.ReplaceExisting);

            // 2. 记录条目
            List<RecycleItem> list = await LoadRecycleListAsync(uid);
            list.Add(new RecycleItem
            {
                Serial = Guid.NewGuid().ToString("D").ToUpper(),
                OriginalSerial = serial,
                ItemType = CollectibleType.Sticky,
                Name = name,
                Payload = payload,
                DeleteTime = DateTime.Now,
            });
            await SaveRecycleListAsync(uid, list);

            // 3. 云端：Notes 区 → Recycle 区（同步开启时）
            await TryMoveCloudAsync(uid, serial + ".ctsnote", "notes", "recycle");
        }

        /// <summary>
        /// 阅读记录进回收站：从 library.json 移除，网络文档缓存文件移入 Recycle 目录，
        /// 记录条目到 recycle.json（Payload 存完整 ReadingItem JSON 供恢复），
        /// 云端 Library 区 {serial}.json 移到 Recycle 区。
        /// </summary>
        public static async Task MoveReadingToRecycleAsync(string uid, ReadingItem item)
        {
            // 1. 从资源库清单移除
            List<ReadingItem> readingList = await LibraryService.LoadReadingListAsync(uid);
            readingList.RemoveAll(r => r.Serial == item.Serial);
            await LibraryService.SaveReadingListAsync(uid, readingList);

            // 2. 本地：网络文档缓存正文移入回收站目录（本地文件本身不进回收站，令牌失效即无法恢复文件）
            if (!string.IsNullOrEmpty(item.CacheFile))
            {
                try
                {
                    StorageFolder cacheFolder = await LibraryService.GetCacheFolderAsync(uid);
                    StorageFile cacheFile = await cacheFolder.TryGetItemAsync(item.CacheFile) as StorageFile;
                    if (cacheFile != null)
                    {
                        StorageFolder recycleFolder = await GetRecycleFolderAsync(uid);
                        await cacheFile.MoveAsync(recycleFolder, cacheFile.Name, NameCollisionOption.ReplaceExisting);
                    }
                }
                catch (Exception)
                {
                    // 缓存文件不存在：忽略
                }
            }

            // 3. 记录条目
            List<RecycleItem> list = await LoadRecycleListAsync(uid);
            list.Add(new RecycleItem
            {
                Serial = Guid.NewGuid().ToString("D").ToUpper(),
                OriginalSerial = item.Serial,
                ItemType = item.ItemType,
                Name = item.Name,
                Payload = JsonConvert.SerializeObject(item),
                DeleteTime = DateTime.Now,
            });
            await SaveRecycleListAsync(uid, list);

            // 4. 云端：Library 区 → Recycle 区（同步开启时）
            await TryMoveCloudAsync(uid, item.Serial + ".json", "library", "recycle");
        }

        // ---------------- 恢复 ----------------

        /// <summary>
        /// 恢复单个回收站条目：
        /// 便签 = Recycle 目录文件移回 Sticky 目录 + 云端 Recycle 区移到 Notes 区；
        /// 阅读记录 = Payload 反序列化加回 library.json + 缓存文件移回 + 云端移到 Library 区。
        /// 完成后从回收站清单移除。
        /// </summary>
        public static async Task RestoreItemAsync(string uid, RecycleItem item)
        {
            StorageFolder recycleFolder = await GetRecycleFolderAsync(uid);

            if (item.ItemType == CollectibleType.Sticky)
            {
                // 便签：文件移回 Sticky 目录（保持加密原样）
                StorageFile recycleFile = await recycleFolder.TryGetItemAsync(item.OriginalSerial + ".ctsnote") as StorageFile;
                if (recycleFile != null)
                {
                    StorageFolder stickyFolder = await StickyService.GetStickyFolderAsync(uid);
                    await recycleFile.MoveAsync(stickyFolder, recycleFile.Name, NameCollisionOption.ReplaceExisting);
                }
                await TryMoveCloudAsync(uid, item.OriginalSerial + ".ctsnote", "recycle", "notes");
            }
            else
            {
                // 阅读记录：反序列化加回资源库清单
                ReadingItem reading = !string.IsNullOrEmpty(item.Payload)
                    ? JsonConvert.DeserializeObject<ReadingItem>(item.Payload)
                    : null;
                if (reading != null)
                {
                    List<ReadingItem> readingList = await LibraryService.LoadReadingListAsync(uid);
                    if (readingList.All(r => r.Serial != reading.Serial))
                    {
                        readingList.Add(reading);
                        await LibraryService.SaveReadingListAsync(uid, readingList);
                    }

                    // 网络文档缓存正文移回 Cache 目录
                    if (!string.IsNullOrEmpty(reading.CacheFile))
                    {
                        try
                        {
                            StorageFile recycleCache = await recycleFolder.TryGetItemAsync(reading.CacheFile) as StorageFile;
                            if (recycleCache != null)
                            {
                                StorageFolder cacheFolder = await LibraryService.GetCacheFolderAsync(uid);
                                await recycleCache.MoveAsync(cacheFolder, recycleCache.Name, NameCollisionOption.ReplaceExisting);
                            }
                        }
                        catch (Exception)
                        {
                            // 缓存文件不存在：忽略
                        }
                    }
                }
                await TryMoveCloudAsync(uid, item.OriginalSerial + ".json", "recycle", "library");
            }

            // 从回收站清单移除
            List<RecycleItem> list = await LoadRecycleListAsync(uid);
            list.RemoveAll(r => r.Serial == item.Serial);
            await SaveRecycleListAsync(uid, list);
        }

        /// <summary>批量恢复（多选模式）：逐条恢复，单条失败不中断。</summary>
        public static async Task RestoreItemsAsync(string uid, IEnumerable<RecycleItem> items)
        {
            foreach (RecycleItem item in items)
            {
                try
                {
                    await RestoreItemAsync(uid, item);
                }
                catch (Exception)
                {
                    // 单条恢复失败：跳过继续
                }
            }
        }

        // ---------------- 彻底删除 ----------------

        /// <summary>
        /// 彻底删除单个回收站条目（对话框确认后调用）：
        /// 删除本地 Recycle 目录中的文件（便签 .ctsnote / 网络缓存 .txt），
        /// 从 recycle.json 移除条目，并删除云端 Recycle 区文件；
        /// 阅读记录同时删除云端 Library 区存档（如仍有残留）。
        /// </summary>
        public static async Task PurgeItemAsync(string uid, RecycleItem item)
        {
            StorageFolder recycleFolder = await GetRecycleFolderAsync(uid);

            // 1. 本地：删除回收站目录中的文件
            string localFileName = item.ItemType == CollectibleType.Sticky
                ? item.OriginalSerial + ".ctsnote"
                : ExtractCacheFileName(item.Payload);
            if (!string.IsNullOrEmpty(localFileName))
            {
                try
                {
                    StorageFile recycleFile = await recycleFolder.TryGetItemAsync(localFileName) as StorageFile;
                    if (recycleFile != null)
                    {
                        await recycleFile.DeleteAsync();
                    }
                }
                catch (Exception)
                {
                    // 文件已不存在：忽略
                }
            }

            // 2. 从回收站清单移除
            List<RecycleItem> list = await LoadRecycleListAsync(uid);
            list.RemoveAll(r => r.Serial == item.Serial);
            await SaveRecycleListAsync(uid, list);

            // 3. 云端：删除 Recycle 区文件；阅读记录同时清理 Library 区存档
            if (ProfileSyncTool.IsSyncEnabled())
            {
                string remoteSerial = item.ItemType == CollectibleType.Sticky
                    ? item.OriginalSerial + ".ctsnote"
                    : item.OriginalSerial + ".json";
                await TryDeleteCloudAsync(uid, remoteSerial, "recycle");
                if (item.ItemType != CollectibleType.Sticky)
                {
                    await TryDeleteCloudAsync(uid, remoteSerial, "library");
                }
            }
        }

        /// <summary>批量彻底删除（多选模式）：逐条删除，单条失败不中断。</summary>
        public static async Task PurgeItemsAsync(string uid, IEnumerable<RecycleItem> items)
        {
            foreach (RecycleItem item in items)
            {
                try
                {
                    await PurgeItemAsync(uid, item);
                }
                catch (Exception)
                {
                    // 单条删除失败：跳过继续
                }
            }
        }

        // ---------------- 私有辅助 ----------------

        /// <summary>从阅读记录 Payload 提取缓存文件名（无缓存返回 null）。</summary>
        private static string ExtractCacheFileName(string payload)
        {
            if (string.IsNullOrEmpty(payload))
            {
                return null;
            }
            try
            {
                ReadingItem reading = JsonConvert.DeserializeObject<ReadingItem>(payload);
                return reading?.CacheFile;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>云端跨区移动（同步关闭或网络异常时静默降级）。</summary>
        private static async Task TryMoveCloudAsync(string uid, string serial, string fromSection, string toSection)
        {
            if (!ProfileSyncTool.IsSyncEnabled())
            {
                return;
            }
            try
            {
                await ApiClient.MoveFileAsync(uid, serial, fromSection, toSection);
            }
            catch (Exception)
            {
                // 网络异常：云端残留会在下次同步时按本地收敛
            }
        }

        /// <summary>云端删除（同步关闭或网络异常时静默降级）。</summary>
        private static async Task TryDeleteCloudAsync(string uid, string serial, string section)
        {
            if (!ProfileSyncTool.IsSyncEnabled())
            {
                return;
            }
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
    }
}
