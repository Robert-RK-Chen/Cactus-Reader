using Cactus_Reader.Entities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.AccessCache;

namespace Cactus_Reader.Sources.ToolKits
{
    /// <summary>
    /// 资源库原子操作服务：阅读记录（library.json）的加载 / 去重保存 / 删除，
    /// 以及"重新打开"时的缓存有效性校验与导航参数构造。
    /// 页面只负责渲染与交互，文件与缓存的增删改查统一收敛到此服务。
    /// </summary>
    public static class LibraryService
    {
        /// <summary>网络文档正文缓存的扩展名。</summary>
        private const string CacheExtension = ".txt";

        /// <summary>获取（必要时创建）当前用户的资源库目录 LocalFolder/{UID}/Library。</summary>
        public static async Task<StorageFolder> GetLibraryFolderAsync(string uid)
        {
            StorageFolder userFolder = await ApplicationData.Current.LocalFolder
                .CreateFolderAsync(uid, CreationCollisionOption.OpenIfExists);
            return await userFolder.CreateFolderAsync("Library", CreationCollisionOption.OpenIfExists);
        }

        /// <summary>获取（必要时创建）网络文档正文缓存目录 LocalFolder/{UID}/Library/Cache。</summary>
        public static async Task<StorageFolder> GetCacheFolderAsync(string uid)
        {
            StorageFolder libraryFolder = await GetLibraryFolderAsync(uid);
            return await libraryFolder.CreateFolderAsync("Cache", CreationCollisionOption.OpenIfExists);
        }

        /// <summary>读取全部阅读记录，按最后阅读时间（UpdateTime）降序；文件缺失返回空列表。</summary>
        public static async Task<List<ReadingItem>> LoadReadingListAsync(string uid)
        {
            try
            {
                StorageFolder libraryFolder = await GetLibraryFolderAsync(uid);
                StorageFile listFile = await libraryFolder.TryGetItemAsync("library.json") as StorageFile;
                if (listFile == null)
                {
                    return new List<ReadingItem>();
                }
                string json = await Task.Run(() => File.ReadAllText(listFile.Path));
                List<ReadingItem> list = JsonConvert.DeserializeObject<List<ReadingItem>>(json);
                if (list == null)
                {
                    return new List<ReadingItem>();
                }
                return list
                    .Where(r => r != null)
                    .OrderByDescending(r => r.UpdateTime == default ? r.CreateTime : r.UpdateTime)
                    .ToList();
            }
            catch (Exception)
            {
                return new List<ReadingItem>();
            }
        }

        /// <summary>整体落盘阅读记录（后台线程原子写）。</summary>
        public static async Task SaveReadingListAsync(string uid, List<ReadingItem> list)
        {
            StorageFolder libraryFolder = await GetLibraryFolderAsync(uid);
            StorageFile listFile = await libraryFolder.CreateFileAsync(
                "library.json", CreationCollisionOption.OpenIfExists);
            string json = JsonConvert.SerializeObject(list);
            await Task.Run(() => File.WriteAllText(listFile.Path, json));
        }

        /// <summary>
        /// 新增或更新阅读记录（去重）：
        /// 网络文档按 URL（Source）唯一；本地文档 / 电子书按 文件名 + 扩展名 唯一
        /// （同一文件重读只刷新来源令牌与最后阅读时间，不产生重复记录）。
        /// 保存后同步上传 ReadingItem 存档到云端 Library 区（{serial}.json，同步开关控制）。
        /// </summary>
        public static async Task AddOrUpdateReadingAsync(string uid, ReadingItem item)
        {
            List<ReadingItem> list = await LoadReadingListAsync(uid);
            ReadingItem existing = list.FirstOrDefault(r =>
                r.ItemType == item.ItemType &&
                (r.ItemType == CollectibleType.WebPage
                    ? string.Equals(r.Source, item.Source, StringComparison.OrdinalIgnoreCase)
                    : string.Equals(r.Name, item.Name, StringComparison.OrdinalIgnoreCase)
                      && string.Equals(r.Extension, item.Extension, StringComparison.OrdinalIgnoreCase)));

            if (existing != null)
            {
                existing.Source = item.Source;
                existing.Name = item.Name;
                existing.Extension = item.Extension;
                existing.CacheFile = item.CacheFile;
                existing.Chapter = item.Chapter;
                existing.Position = item.Position;
                existing.UpdateTime = item.UpdateTime;
            }
            else
            {
                list.Add(item);
            }
            await SaveReadingListAsync(uid, list);

            // 云端 Library 区同步：上传阅读记录存档（失败不影响本地）
            await TryUploadLibraryFileAsync(uid, existing ?? item);
        }

        /// <summary>删除阅读记录：移入回收站（本地清单移除 + 缓存移入 Recycle 目录 + 云端 Library 区移到 Recycle 区）。</summary>
        public static async Task DeleteReadingAsync(string uid, string serial)
        {
            List<ReadingItem> list = await LoadReadingListAsync(uid);
            ReadingItem removed = list.FirstOrDefault(r => r.Serial == serial);
            if (removed == null)
            {
                return;
            }
            await RecycleService.MoveReadingToRecycleAsync(uid, removed);
        }

        /// <summary>切换阅读记录收藏状态（收藏夹 = library.json 中 IsFavorite 为 true 的过滤视图），并同步云端存档。</summary>
        public static async Task SetFavoriteAsync(string uid, string serial, bool isFavorite)
        {
            List<ReadingItem> list = await LoadReadingListAsync(uid);
            ReadingItem item = list.FirstOrDefault(r => r.Serial == serial);
            if (item == null)
            {
                return;
            }
            item.IsFavorite = isFavorite;
            await SaveReadingListAsync(uid, list);
            await TryUploadLibraryFileAsync(uid, item);
        }

        /// <summary>上传阅读记录存档到云端 Library 区（同步开启时；失败静默降级）。</summary>
        public static async Task TryUploadLibraryFileAsync(string uid, ReadingItem item)
        {
            if (!ProfileSyncTool.IsSyncEnabled())
            {
                return;
            }
            try
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(item));
                await ApiClient.UploadLibraryFileAsync(uid, item.Serial + ".json", bytes);
            }
            catch (Exception)
            {
                // 网络异常：云端缺档会在下次同步时按本地补齐
            }
        }

        /// <summary>
        /// 打开一条阅读记录，返回阅读页导航参数；缓存失效（本地文件被移动 / 网络抓取失败）返回 null，
        /// 调用方应提示"资源不存在"并删除该记录。
        /// </summary>
        public static async Task<object> OpenReadingAsync(string uid, ReadingItem item)
        {
            switch (item.ItemType)
            {
                case CollectibleType.Book:
                case CollectibleType.Document:
                    // 本地文件经 FutureAccessList 令牌跨会话取回；令牌失效即资源不存在
                    try
                    {
                        return await StorageApplicationPermissions.FutureAccessList.GetFileAsync(item.Source);
                    }
                    catch (Exception)
                    {
                        return null;
                    }

                case CollectibleType.WebPage:
                    // 优先本地缓存正文（离线可用），其次按输入网页的方式重新抓取
                    string cached = await ReadWebCacheAsync(uid, item);
                    if (!string.IsNullOrEmpty(cached))
                    {
                        return cached;
                    }
                    string content = WebReaderService.FetchWebPage(item.Source);
                    if (content.Length > 0)
                    {
                        await WriteWebCacheAsync(uid, item, content);
                        return content;
                    }
                    return null;

                default:
                    return null;
            }
        }

        // ---------------- 网络文档正文缓存 ----------------

        /// <summary>读取网络文档缓存正文；无缓存 / 文件缺失返回空字符串。</summary>
        public static async Task<string> ReadWebCacheAsync(string uid, ReadingItem item)
        {
            if (string.IsNullOrEmpty(item.CacheFile))
            {
                return string.Empty;
            }
            try
            {
                StorageFolder cacheFolder = await GetCacheFolderAsync(uid);
                StorageFile cacheFile = await cacheFolder.GetFileAsync(item.CacheFile);
                return await Task.Run(() => File.ReadAllText(cacheFile.Path));
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>把抓取的网络正文写入缓存目录（{Serial}.txt），并回填 item.CacheFile。</summary>
        public static async Task WriteWebCacheAsync(string uid, ReadingItem item, string content)
        {
            try
            {
                if (string.IsNullOrEmpty(item.CacheFile))
                {
                    item.CacheFile = (item.Serial ?? Guid.NewGuid().ToString("D").ToUpper()) + CacheExtension;
                }
                StorageFolder cacheFolder = await GetCacheFolderAsync(uid);
                StorageFile cacheFile = await cacheFolder.CreateFileAsync(
                    item.CacheFile, CreationCollisionOption.OpenIfExists);
                await Task.Run(() => File.WriteAllText(cacheFile.Path, content));
            }
            catch (Exception)
            {
                // 缓存写失败不影响阅读：正文已返回给阅读页
            }
        }
    }
}
