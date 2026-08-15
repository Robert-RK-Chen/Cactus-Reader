using Cactus_Reader.Entities;
using Cactus_Reader.Sources.AppPages.AppUI;
using Cactus_Reader.Sources.StickyNotes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;

namespace Cactus_Reader.Sources.ToolKits
{
    /// <summary>
    /// 便签原子操作：文件夹定位 / 列表加载 / 单条读写 / 保存 / 删除 / 主题 / RTF 规范化的可复用单元。
    /// 页面只负责渲染与交互，文件与云端的增删改查统一收敛到此服务。
    /// </summary>
    public static class StickyService
    {
        private static readonly EncryptStickyTool encryptStickyTool = EncryptStickyTool.Instance;
        private static readonly ProfileUploadTool uploadTool = ProfileUploadTool.Instance;
        private static readonly ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

        /// <summary>获取（必要时创建）当前用户的便签目录 LocalFolder/{UID}/Sticky。</summary>
        public static async Task<StorageFolder> GetStickyFolderAsync(string uid)
        {
            StorageFolder userFolder = await ApplicationData.Current.LocalFolder
                .CreateFolderAsync(uid, CreationCollisionOption.OpenIfExists);
            return await userFolder.CreateFolderAsync("Sticky", CreationCollisionOption.OpenIfExists);
        }

        // ---------------- 主题 ----------------

        public static string GetStickyTheme()
        {
            object theme = localSettings.Values["StickyTheme"];
            if (theme == null)
            {
                localSettings.Values["StickyTheme"] = "GingkoYellow";
                return "GingkoYellow";
            }
            return theme.ToString();
        }

        public static void SetStickyTheme(string theme)
        {
            localSettings.Values["StickyTheme"] = theme;
        }

        // ---------------- 新建 / 读取 ----------------

        /// <summary>创建新便签实体（未落盘）。</summary>
        public static Sticky CreateSticky(string serial)
        {
            return new Sticky
            {
                IsLock = false,
                CreateTime = DateTime.Now,
                StickyDocument = string.Empty,
                StickyTheme = GetStickyTheme(),
                StickySerial = serial,
                QuickViewText = string.Empty,
            };
        }

        /// <summary>创建新建模式的便签卡片（视图背景用 ViewBackground，模板绑定而非 Control.Background）。</summary>
        public static StickyQuickView CreateNewStickyQuickView(string serial)
        {
            string theme = GetStickyTheme();
            ThemeColorBrush brush = ThemeColorBrushTool.Instance.GetThemeColorBrush(theme, false);
            return new StickyQuickView
            {
                CreateTimeText = DateTime.Now.ToShortDateString(),
                StickySerial = serial,
                ThemeKind = theme,
                TitleBackground = brush.TitleBrush,
                ViewBackground = brush.BackgroundBrush,
            };
        }

        /// <summary>在独立视图（新窗口）中打开便签编辑页，参数为 List&lt;object&gt;（新建/打开模式 + 卡片）。</summary>
        public static async Task OpenStickyEditWindowAsync(List<object> parameter)
        {
            CoreApplicationView newView = CoreApplication.CreateNewView();
            int newViewId = 0;
            await newView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Frame frame = new Frame();
                frame.Navigate(typeof(NewStickyPage), parameter, new DrillInNavigationTransitionInfo());
                Window.Current.Content = frame;
                Window.Current.Activate();
                newViewId = ApplicationView.GetForCurrentView().Id;
            });
            ApplicationView.PreferredLaunchViewSize = new Size(300, 300);
            await ApplicationViewSwitcher.TryShowAsStandaloneAsync(newViewId);
        }

        /// <summary>读取并解密单个便签（文件缺失/解密失败返回 null）。</summary>
        public static async Task<Sticky> LoadStickyAsync(string uid, string serial)
        {
            try
            {
                StorageFolder stickyFolder = await GetStickyFolderAsync(uid);
                StorageFile stickyFile = await stickyFolder.GetFileAsync(serial + ".ctsnote");
                string stickyText = encryptStickyTool.DecryptStickyText(File.ReadAllText(stickyFile.Path));
                return JsonConvert.DeserializeObject<Sticky>(stickyText);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>遍历本地便签文件夹，返回解密成功的便签列表（损坏/未解锁的跳过）。</summary>
        public static async Task<List<Sticky>> GetStickyListAsync(string uid)
        {
            List<Sticky> stickyList = new List<Sticky>();
            StorageFolder stickyFolder = await GetStickyFolderAsync(uid);
            IReadOnlyList<StorageFile> fileList = await stickyFolder.GetFilesAsync();

            foreach (StorageFile file in fileList)
            {
                try
                {
                    string stickyText = encryptStickyTool.DecryptStickyText(File.ReadAllText(file.Path));
                    Sticky sticky = JsonConvert.DeserializeObject<Sticky>(stickyText);
                    if (sticky != null)
                    {
                        stickyList.Add(sticky);
                    }
                }
                catch (Exception)
                {
                    // 密钥未解锁或数据损坏：跳过该便签，不中断列表加载
                }
            }
            return stickyList;
        }

        // ---------------- 保存 / 删除 ----------------

        /// <summary>保存便签：规范化 RTF → 加密落盘 → 上传云端（受同步开关控制）。</summary>
        public static async Task SaveStickyAsync(string uid, Sticky sticky, string rtf, string plainText)
        {
            sticky.StickyDocument = NormalizeRtfForSave(rtf);
            sticky.QuickViewText = plainText.TrimEnd();

            StorageFolder stickyFolder = await GetStickyFolderAsync(uid);
            StorageFile stickyFile = await stickyFolder.CreateFileAsync(
                sticky.StickySerial + ".ctsnote", CreationCollisionOption.OpenIfExists);

            string encrypted = encryptStickyTool.EncryptStickyText(JsonConvert.SerializeObject(sticky));
            File.WriteAllText(stickyFile.Path, encrypted);
            localSettings.Values["isSaved"] = true;

            uploadTool.UploadCactusNotes(stickyFile, uid, stickyFile.Name, "/upload-cactus-notes");
        }

        /// <summary>
        /// 删除便签：本地删除 + 云端删除（同步关闭时仅删本地）。
        /// 新建未保存的便签（本地无文件）从未上传过云端，直接返回，不触发服务端删除。
        /// </summary>
        public static async Task DeleteStickyAsync(string uid, string serial)
        {
            try
            {
                StorageFolder stickyFolder = await GetStickyFolderAsync(uid);

                // 便签只有保存过才有本地文件，也只有保存过才可能上传到服务端；
                // 本地文件不存在 ⇒ 从未保存 ⇒ 服务端没有对应存档，无需（也无法）删除
                StorageFile stickyFile = await stickyFolder.TryGetItemAsync(serial + ".ctsnote") as StorageFile;
                if (stickyFile == null)
                {
                    return;
                }
                await stickyFile.DeleteAsync();

                // 服务端文件名为 {StickySerial}.ctsnote，删除时需带扩展名
                if (ProfileSyncTool.IsSyncEnabled())
                {
                    try
                    {
                        await ApiClient.DeleteNoteAsync(uid, serial + ".ctsnote");
                    }
                    catch (Exception)
                    {
                        // 网络异常时忽略：服务端残留会在下次同步时被拉回，属预期降级行为
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 规范化 RTF 以便保存：RichEditBox 输出的 RTF 始终以段落标记 \par 结尾（文档至少含一个段落），
        /// 若原样保存，SetText 恢复时会额外生成一个空段落，导致每次打开便签末尾多一行空行，
        /// 并因内容不一致触发 TextChanged 被误判为已修改。此处仅移除文档末尾紧跟右大括号前的最后一个 \par，
        /// 用户有意输入的空行（\par\par）会保留。
        /// </summary>
        public static string NormalizeRtfForSave(string rtf)
        {
            rtf = rtf.TrimEnd();
            int closeBrace = rtf.LastIndexOf('}');
            if (closeBrace < 0)
            {
                return rtf;
            }

            string head = rtf.Substring(0, closeBrace).TrimEnd();
            if (head.EndsWith("\\par", StringComparison.Ordinal))
            {
                return head.Substring(0, head.Length - 4) + rtf.Substring(closeBrace);
            }
            return rtf;
        }
    }
}
