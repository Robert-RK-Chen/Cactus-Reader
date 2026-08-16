using Cactus_Reader.Entities;
using Cactus_Reader.Sources.AppPages.AppUI;
using Cactus_Reader.Sources.StickyNotes;
using Cactus_Reader.Sources.WindowsHello;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
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
            DateTime now = DateTime.Now;
            return new Sticky
            {
                IsLock = false,
                CreateTime = now,
                UpdateTime = now,
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
                CreateTimeText = DateTime.Now.ToString("yyyy/MM/dd"),
                StickySerial = serial,
                ThemeKind = theme,
                TitleBackground = brush.TitleBrush,
                ViewBackground = brush.BackgroundBrush,
            };
        }

        /// <summary>
        /// 在独立视图（新窗口）中打开便签编辑页，参数为 List&lt;object&gt;（新建/打开模式 + 卡片）。
        /// 注意：不在此处设置全局视图尺寸（PreferredLaunchViewSize 会影响整个应用），窗口尺寸交给系统默认。
        /// </summary>
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
            await ApplicationViewSwitcher.TryShowAsStandaloneAsync(newViewId);
        }

        /// <summary>读取并解密单个便签（文件缺失/解密失败返回 null）。文件 IO 在后台线程执行。</summary>
        public static async Task<Sticky> LoadStickyAsync(string uid, string serial)
        {
            try
            {
                StorageFolder stickyFolder = await GetStickyFolderAsync(uid);
                // TryGetItemAsync 不抛 FileNotFoundException：文件缺失返回 null
                StorageFile stickyFile = await stickyFolder.TryGetItemAsync(serial + ".ctsnote") as StorageFile;
                if (stickyFile == null)
                {
                    return null;
                }
                string fileText = await Task.Run(() => File.ReadAllText(stickyFile.Path));
                string stickyText = encryptStickyTool.DecryptStickyText(fileText);
                return JsonConvert.DeserializeObject<Sticky>(stickyText);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 遍历本地便签文件夹，返回解密成功的便签列表（损坏/未解锁的跳过），按最后修改时间降序。
        /// 文件 IO 在后台线程执行，避免阻塞 UI 线程。
        /// </summary>
        public static async Task<List<Sticky>> GetStickyListAsync(string uid)
        {
            StorageFolder stickyFolder = await GetStickyFolderAsync(uid);
            IReadOnlyList<StorageFile> fileList = await stickyFolder.GetFilesAsync();
            if (fileList.Count == 0)
            {
                return new List<Sticky>();
            }

            // 逐文件读取 + 解密（后台线程），单个失败跳过不中断
            List<Sticky> stickyList = await Task.Run(() =>
            {
                List<Sticky> result = new List<Sticky>();
                foreach (StorageFile file in fileList)
                {
                    try
                    {
                        string stickyText = encryptStickyTool.DecryptStickyText(File.ReadAllText(file.Path));
                        Sticky sticky = JsonConvert.DeserializeObject<Sticky>(stickyText);
                        if (sticky != null)
                        {
                            result.Add(sticky);
                        }
                    }
                    catch (Exception)
                    {
                        // 密钥未解锁或数据损坏：跳过该便签，不中断列表加载
                    }
                }
                return result;
            });

            return stickyList
                .OrderByDescending(s => s.UpdateTime == default ? s.CreateTime : s.UpdateTime)
                .ToList();
        }

        /// <summary>本地 Sticky 目录是否存在 .ctsnote 文件（用于判断"有文件但全部解密失败"= 孤儿文件）。</summary>
        public static async Task<bool> HasStickyFilesAsync(string uid)
        {
            try
            {
                StorageFolder stickyFolder = await GetStickyFolderAsync(uid);
                IReadOnlyList<StorageFile> files = await stickyFolder.GetFilesAsync();
                return files.Any(f => f.Name.EndsWith(".ctsnote", StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 删除全部无法解密的孤儿便签文件（本地 + 云端，不进回收站 ——
        /// 孤儿文件的密钥已随旧设备卸载永久丢失，回收站也无法恢复）。
        /// 仅在确认密钥不匹配（AuthenticationTagMismatch）后由页面提示用户调用。
        /// </summary>
        public static async Task DeleteAllUnreadableStickyAsync(string uid)
        {
            try
            {
                StorageFolder stickyFolder = await GetStickyFolderAsync(uid);
                IReadOnlyList<StorageFile> files = await stickyFolder.GetFilesAsync();
                foreach (StorageFile file in files)
                {
                    if (!file.Name.EndsWith(".ctsnote", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    try
                    {
                        await file.DeleteAsync();
                    }
                    catch (Exception)
                    {
                        // 文件已删除/占用：忽略
                    }
                    // 云端同步删除，避免下次同步又下载回来
                    if (ProfileSyncTool.IsSyncEnabled())
                    {
                        try
                        {
                            await ApiClient.DeleteNoteAsync(uid, file.Name);
                        }
                        catch (Exception)
                        {
                            // 网络异常：云端残留会在下次同步时被清理
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 孤儿文件清理失败（目录被占用 / 网络异常）：记录日志，下次进入便签页时用户可再次触发
                System.Diagnostics.Debug.WriteLine($"孤儿便签清理失败：UID={uid}, {ex.Message}");
            }
        }

        // ---------------- 保存 / 删除 ----------------

        /// <summary>保存便签：规范化 RTF → 加密落盘（后台线程）→ 上传云端（受同步开关控制）。</summary>
        public static async Task SaveStickyAsync(string uid, Sticky sticky, string rtf, string plainText)
        {
            sticky.StickyDocument = NormalizeRtfForSave(rtf);
            sticky.QuickViewText = plainText.TrimEnd();
            sticky.UpdateTime = DateTime.Now;

            StorageFolder stickyFolder = await GetStickyFolderAsync(uid);
            StorageFile stickyFile = await stickyFolder.CreateFileAsync(
                sticky.StickySerial + ".ctsnote", CreationCollisionOption.OpenIfExists);

            string encrypted = encryptStickyTool.EncryptStickyText(JsonConvert.SerializeObject(sticky));
            await Task.Run(() => File.WriteAllText(stickyFile.Path, encrypted));

            uploadTool.UploadCactusNotes(stickyFile, uid, stickyFile.Name, "/upload-cactus-notes");
        }

        /// <summary>
        /// 仅更新便签元数据（收藏状态等）并落盘：StickyDocument 已是规范化 RTF，不再重新处理，
        /// 避免重复 NormalizeRtfForSave 误伤用户空行。加密写回 + 上传云端（受同步开关控制）。
        /// </summary>
        public static async Task SaveStickyAsync(string uid, Sticky sticky)
        {
            sticky.UpdateTime = DateTime.Now;

            StorageFolder stickyFolder = await GetStickyFolderAsync(uid);
            StorageFile stickyFile = await stickyFolder.CreateFileAsync(
                sticky.StickySerial + ".ctsnote", CreationCollisionOption.OpenIfExists);

            string encrypted = encryptStickyTool.EncryptStickyText(JsonConvert.SerializeObject(sticky));
            await Task.Run(() => File.WriteAllText(stickyFile.Path, encrypted));

            uploadTool.UploadCactusNotes(stickyFile, uid, stickyFile.Name, "/upload-cactus-notes");
        }

        /// <summary>
        /// 便签标题：取纯文本预览（QuickViewText）的第一行（去除空白行，截断 24 字）。
        /// 便签没有独立标题字段，回收站 / 收藏夹均以第一行内容作为标题；内容为空回退"便签"。
        /// 锁定（加密）便签不暴露内容，一律显示"锁定便签"。
        /// </summary>
        public static string GetStickyTitle(Sticky sticky)
        {
            if (sticky == null)
            {
                return "便签";
            }
            if (sticky.IsLock)
            {
                return "锁定便签";
            }
            string preview = (sticky.QuickViewText ?? string.Empty).Trim();
            if (preview.Length == 0)
            {
                return "便签";
            }
            string firstLine = preview
                .Split('\n')
                .Select(line => line.Trim())
                .FirstOrDefault(line => line.Length > 0) ?? "便签";
            return firstLine.Length > 24 ? firstLine.Substring(0, 24) : firstLine;
        }

        /// <summary>
        /// 弹出密码 / Windows Hello 解锁对话框并验证便签访问权限。
        /// 密码错误时循环重试，取消返回 false。验证通过不修改锁定状态（由调用方决定后续操作）。
        /// 便签本卡片（StickyQuickView）与收藏夹（FavoritePage）打开锁定便签共用此验证。
        /// </summary>
        public static async Task<bool> VerifyStickyUnlockAsync(string title, string header)
        {
            string UID = ApplicationData.Current.LocalSettings.Values["UID"]?.ToString();
            PasswordBox passwordBox = new PasswordBox
            {
                Width = 360,
                VerticalAlignment = VerticalAlignment.Bottom,
                VerticalContentAlignment = VerticalAlignment.Center,
                Header = header,
            };
            ContentDialog dialog = new ContentDialog
            {
                Title = title,
                Content = passwordBox,
                CloseButtonText = "取消",
                PrimaryButtonText = "确定",
                SecondaryButtonText = "Windows Hello",
                DefaultButton = ContentDialogButton.Primary
            };

            ContentDialogResult result = await dialog.ShowAsync();
            while (result == ContentDialogResult.Primary || result == ContentDialogResult.Secondary)
            {
                if (result == ContentDialogResult.Primary &&
                    InformationVerify.Instance.CheckPassword(passwordBox.Password))
                {
                    return true;
                }
                if (result == ContentDialogResult.Secondary &&
                    SettingsService.IsWindowsHelloSet() &&
                    await MicrosoftPassportHelper.CreatePassportKeyAsync(UID,
                        (string)ApplicationData.Current.LocalSettings.Values["name"]))
                {
                    return true;
                }
                passwordBox.Header = "该密码不正确，请再试一次。";
                result = await dialog.ShowAsync();
            }
            return false;
        }

        /// <summary>
        /// 确保便签密钥可用（含用户交互）：本机无密钥且服务端有密码包裹时弹出解锁框。
        /// vault 三态：无备份→首次使用（生成新密钥，返回 true 不弹框）；明文备份（无密码模式）→免密采用；
        /// 密码包裹→弹框输入旧密码，或"重新开始"设置新密码（旧便签变孤儿文件）。
        /// 返回 false = 用户取消解锁；true = 密钥已就绪。
        /// 登录后 / 进便签页前 / 创建便签前调用，保证任何入口创建便签都不闪退。
        /// </summary>
        public static async Task<bool> EnsureKeyReadyWithDialogAsync()
        {
            if (await encryptStickyTool.EnsureStickyKeyReadyAsync())
            {
                return true;
            }

            while (true)
            {
                PasswordBox passwordBox = new()
                {
                    Width = 360,
                    PlaceholderText = "个人密码验证",
                    VerticalAlignment = VerticalAlignment.Bottom,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Header = "检测到你的账号在其他设备上设置了个人密码，请输入密码以解锁并同步你的便签本。若你已忘记密码，可选择重新开始（旧便签数据将无法访问）。",
                };
                ContentDialog dialog = new ContentDialog
                {
                    Title = "解锁便签本",
                    Content = passwordBox,
                    CloseButtonText = "取消",
                    PrimaryButtonText = "确定",
                    SecondaryButtonText = "重新开始",
                    DefaultButton = ContentDialogButton.Primary
                };
                ContentDialogResult result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Secondary)
                {
                    // 重新开始：放弃旧密钥，引导设置新密码（需二次确认，旧便签将无法访问）
                    ContentDialog confirm = new ContentDialog
                    {
                        Title = "重新开始",
                        Content = "旧设备创建的便签将无法访问（加密密钥已丢失）。是否继续，并为新的便签本设置个人密码？",
                        PrimaryButtonText = "继续",
                        CloseButtonText = "取消",
                        DefaultButton = ContentDialogButton.Close
                    };
                    if (await confirm.ShowAsync() == ContentDialogResult.Primary &&
                        await TrySetNewPasswordAsync())
                    {
                        return true; // 新密码设置成功，密钥已就绪
                    }
                    continue; // 取消或设置失败：回到解锁框重新选择
                }

                if (result != ContentDialogResult.Primary)
                {
                    return false; // 用户取消解锁
                }

                if (await encryptStickyTool.UnlockWithPasswordAsync(passwordBox.Password))
                {
                    return true;
                }

                ContentDialog errorDialog = new()
                {
                    Title = "密码错误",
                    Content = "个人密码不正确，请重试。",
                    CloseButtonText = "确定"
                };
                await errorDialog.ShowAsync();
            }
        }

        /// <summary>
        /// 设置新密码（重新开始流程）：与设置页面同款交互 —— 单个密码框 + 循环校验至少 6 位。
        /// 校验通过后生成新密钥并切换为密码包裹模式（复用 SetupVaultAsync 核心逻辑）。
        /// 此对话框只提供 确定 / 取消，不再提供"重新开始"，避免循环。
        /// </summary>
        private static async Task<bool> TrySetNewPasswordAsync()
        {
            PasswordBox passwordBox = new()
            {
                Width = 360,
                PlaceholderText = "密码长度至少为 6 位",
                VerticalAlignment = VerticalAlignment.Bottom,
                VerticalContentAlignment = VerticalAlignment.Center,
                Header = "为重新开始的便签本设置新的个人密码，用于跨设备解锁便签。",
            };
            ContentDialog dialog = new ContentDialog
            {
                Title = "设置个人密码",
                Content = passwordBox,
                CloseButtonText = "取消",
                PrimaryButtonText = "确定",
                DefaultButton = ContentDialogButton.Primary
            };

            ContentDialogResult result = await dialog.ShowAsync();
            while (result == ContentDialogResult.Primary)
            {
                string password = passwordBox.Password;
                if (password.Length >= 6)
                {
                    // 生成新密钥 + 新密码包裹上传 vault（与设置页 SetPrivateKeyAsync 同底层逻辑）
                    if (await encryptStickyTool.RestartWithNewPasswordAsync(password))
                    {
                        return true;
                    }
                    await ShowErrorDialogAsync("设置失败（网络异常或服务不可用），请重试。");
                    result = await dialog.ShowAsync();
                    continue;
                }
                // 密码过短：留在对话框重试（与设置页一致）
                result = await dialog.ShowAsync();
            }
            return false; // 取消设置密码
        }

        /// <summary>弹出单按钮提示对话框。</summary>
        private static async Task ShowErrorDialogAsync(string message)
        {
            ContentDialog errorDialog = new()
            {
                Title = "提示",
                Content = message,
                CloseButtonText = "确定"
            };
            await errorDialog.ShowAsync();
        }

        /// <summary>
        /// 删除便签：移入回收站（本地文件移入 Recycle 目录 + 云端 Notes 区移到 Recycle 区）。
        /// 新建未保存的便签（本地无文件）从未上传过云端，直接丢弃不进入回收站。
        /// 卡片右键 / 编辑窗口 / 多选删除三条路径共用本方法。
        /// </summary>
        public static async Task DeleteStickyAsync(string uid, string serial)
        {
            try
            {
                await RecycleService.MoveStickyToRecycleAsync(uid, serial);
            }
            catch (Exception ex)
            {
                // 移入回收站失败（文件被占用 / 网络异常）：记录日志，避免删除静默失败
                System.Diagnostics.Debug.WriteLine($"移入回收站失败：UID={uid}, Serial={serial}, {ex.Message}");
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
