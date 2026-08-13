using Cactus_Reader.Entities.EpubEntities;
using Cactus_Reader.Sources.AppPages.AppUI;
using Cactus_Reader.Sources.StickyNotes;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Cactus_Reader.Sources.AppPages.Reader
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class EpubFileReadingPage : Page
    {
        public ObservableCollection<Chapter> Chapters { get; private set; }
        BookInfo bookInfo = null;
        private string currentFont = "MiSans";
        private double currentFontSize = 18;
        // WebView2 虚拟主机映射：把书籍解压目录映射为 epub.local，
        // 章节用 https://epub.local/ 加载（WebView2 不支持 ms-appdata:/// 协议）
        private const string EpubVirtualHost = "epub.local";
        private string contentFolderPath;
        // WebView 内部导航（点击章节内超链接）时同步左侧目录，此标志防止循环触发
        private bool syncingChapterSelection;

        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        readonly ThemeColorBrushTool brushTool = ThemeColorBrushTool.Instance;

        public EpubFileReadingPage()
        {
            Chapters = new ObservableCollection<Chapter>();
            this.InitializeComponent();
            if (localSettings.Values["StickyTheme"] == null) { localSettings.Values["StickyTheme"] = "GingkoYellow"; }
            if (localSettings.Values["font"] != null) { currentFont = localSettings.Values["font"].ToString(); }
            if (localSettings.Values["fontSize"] != null) { currentFontSize = (double)localSettings.Values["fontSize"]; }

            var titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            // Hide default title bar.
            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = true;
            UpdateTitleBarLayout(coreTitleBar);

            // Set XAML element as a draggable region.
            Window.Current.SetTitleBar(appTitleBar);

            // Register a handler for when the size of the overlaid caption control changes.
            // For example, when the app moves to a screen with a different DPI.
            coreTitleBar.LayoutMetricsChanged += CoreTitleBarLayoutMetricsChanged;

            DataTransferManager dataTransferManager = DataTransferManager.GetForCurrentView();
            dataTransferManager.DataRequested += DataTransferManagerDataRequested;

            // WebView2 内部导航（章节内超链接/上一页下一页）时同步左侧目录选中项
            PivotItemWebView.NavigationStarting += OnWebViewNavigationStarting;
        }

        /// <summary>窗口大小变化时，将阅读区域宽度设为视口一半并居中。</summary>
        private void OnPageSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (PivotItemWebView != null)
            {
                PivotItemWebView.Width = e.NewSize.Width / 2.0;
            }
        }

        /// <summary>
        /// WebView 内部导航到章节时同步左侧目录选中项。
        /// 通过 URL 路径匹配 Chapters 中对应 URI；仅当导航由 WebView 内部发起（点击链接/翻页）时生效。
        /// </summary>
        private void OnWebViewNavigationStarting(WebView2 sender, CoreWebView2NavigationStartingEventArgs args)
        {
            if (syncingChapterSelection) return;

            try
            {
                string path = new Uri(args.Uri).AbsolutePath.TrimStart('/');
                if (path.Length == 0) return;

                for (int i = 0; i < Chapters.Count; i++)
                {
                    string chapterPath = Chapters[i].Uri.AbsolutePath.TrimStart('/');
                    if (string.Equals(path, chapterPath, StringComparison.OrdinalIgnoreCase))
                    {
                        if (ChapterPivot.SelectedIndex != i)
                        {
                            syncingChapterSelection = true;
                            ChapterPivot.SelectedIndex = i;
                            syncingChapterSelection = false;
                        }
                        break;
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private void DataTransferManagerDataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            var chapter = ChapterPivot.SelectedItem as Chapter;
            DataRequest request = args.Request;
            if (chapter?.BookFile != null)
            {
                request.Data.SetStorageItems(new List<IStorageItem> { chapter.BookFile });
                request.Data.Properties.Title = "Cactus Reader";
                request.Data.Properties.Description = "分享 EPUB 章节";
            }
            else
            {
                request.FailWithDisplayText("当前没有可分享的章节。");
            }
        }

        private void ShareNearBy(object sender, RoutedEventArgs e)
        {
            DataTransferManager.ShowShareUI();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            bookInfo = e.Parameter as BookInfo;
            try
            {
                Chapters.Clear();
                await OpenBook(bookInfo.BookFile);
                if (Chapters.Count > 0)
                {
                    ChapterPivot.SelectedIndex = bookInfo.Chapter;
                }
            }
            catch (Exception ex)
            {
                // 解析失败时明确提示用户，避免目录/内容静默空白无法排查
                System.Diagnostics.Debug.WriteLine("OpenBook 失败: " + ex);
                try
                {
                    ContentDialog dialog = new ContentDialog
                    {
                        Title = "无法打开电子书",
                        Content = "解析 EPUB 时出错：\n" + ex.Message,
                        CloseButtonText = "确定"
                    };
                    await dialog.ShowAsync();
                }
                catch (Exception)
                {
                }
            }
        }

        private void CoreTitleBarLayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            UpdateTitleBarLayout(sender);
        }

        private void UpdateTitleBarLayout(CoreApplicationViewTitleBar coreTitleBar)
        {
            // 为窗口控制按钮（最小化/最大化/关闭）在右侧预留空间，
            // 避免 CommandBar 末尾按钮与其重叠
            appTitleBar.Padding = new Thickness(0, 0, coreTitleBar.SystemOverlayRightInset, 0);
        }

        private async Task OpenBook(StorageFile bookFile)
        {
            // 1. 复制书籍到临时目录并解压
            var bookname = bookInfo.BookFile.Name;
            var tempFolder = ApplicationData.Current.TemporaryFolder;
            var tempFile = await bookFile.CopyAsync(tempFolder, bookname, NameCollisionOption.ReplaceExisting);
            var tempSubFolder = await tempFolder.CreateFolderAsync(bookFile.DisplayName, CreationCollisionOption.ReplaceExisting);
            await Task.Run(() =>
            {
                System.IO.Compression.ZipFile.ExtractToDirectory(tempFile.Path, tempSubFolder.Path);
            });

            // 2. container.xml → OPF 相对路径（正则提取，兼容不规范的 XML 声明/实体）
            var metaFolder = await tempSubFolder.GetFolderAsync("META-INF");
            var metaFile = await metaFolder.GetFileAsync("container.xml");
            var containerText = await FileIO.ReadTextAsync(metaFile);
            var contentFilePath = ExtractAttribute(containerText, "rootfile", "full-path");
            if (string.IsNullOrEmpty(contentFilePath))
            {
                throw new InvalidDataException("container.xml 中缺少 rootfile full-path，无法定位 OPF 文件。");
            }

            // 3. 定位 OPF 所在目录并解析 manifest
            var contentFolder = await GetContentFolder(contentFilePath, tempSubFolder);
            var contentFileName = StripPathFromContentFilePath(contentFilePath);
            var opfText = await FileIO.ReadTextAsync(await contentFolder.GetFileAsync(contentFileName));
            var items = ParseManifestItems(opfText);
            if (items.Count == 0)
            {
                throw new InvalidDataException("OPF 中未找到任何 item，无法解析章节。");
            }

            // 记录内容目录完整路径，供 WebView2 虚拟主机映射使用
            contentFolderPath = contentFolder.Path;

            // 4. 解析章节来源：以 OPF spine 定义阅读顺序（完整），NCX/nav 仅提供章节名
            var chapterEntries = await ResolveChapterEntriesAsync(contentFolder, opfText, items);
            if (chapterEntries.Count == 0)
            {
                throw new InvalidDataException("未能从目录/清单中解析出任何章节。");
            }

            // 5. 并行注入阅读样式（大幅加快多章节书籍打开速度），完成后按目录顺序加入列表。
            // 按文件路径去重（NCX 目录中同一章节可能被多次引用），
            // 并使用 Task.Run + 同步 File API（独占锁），避免并发读写同一文件抛"另一个程序正在使用"。
            var style = BuildBodyStyle();
            var overlayCss = BuildOverlayCss();
            var distinctEntries = chapterEntries
                .GroupBy(entry => entry.file.Path)
                .Select(group => group.First())
                .ToList();

            var chapterData = await Task.WhenAll(distinctEntries.Select(async entry =>
            {
                var chapterFile = entry.file;
                string bookFileText = await Task.Run(() => File.ReadAllText(chapterFile.Path));
                bookFileText = InjectBodyStyle(bookFileText, style);
                bookFileText = InjectGlobalStyleCss(bookFileText, overlayCss);
                await Task.Run(() => File.WriteAllText(chapterFile.Path, bookFileText));

                string name = string.IsNullOrEmpty(entry.name) ? Path.GetFileNameWithoutExtension(chapterFile.Name) : entry.name;
                return (name, entry.uri, chapterFile);
            }));

            uint index = 1;
            foreach (var data in chapterData)
            {
                // 目录显示自然数顺序页码（1、2、3…），按 spine 阅读顺序递增
                Chapters.Add(new Chapter(index.ToString(), data.uri, data.chapterFile));
                index++;
            }

            // 提前初始化 WebView2，缩短首次点击章节的等待时间
            if (PivotItemWebView.CoreWebView2 == null)
            {
                await PivotItemWebView.EnsureCoreWebView2Async();
            }
        }

        private async Task<IStorageFolder> GetContentFolder(string contentFilePath, StorageFolder tempSubFolder)
        {
            var contentFolder = tempSubFolder;
            if (contentFilePath.Contains("/"))
            {
                var parts = contentFilePath.Split('/');
                for (var i = 0; i < parts.Length - 1; i++)
                {
                    if (parts[i].Length == 0) continue; // 忽略前导/连续斜杠（不规范 EPUB）
                    contentFolder = await contentFolder.GetFolderAsync(parts[i]);
                }
            }
            return contentFolder;
        }

        private string StripPathFromContentFilePath(string contentFilePath)
        {
            var stripped = contentFilePath;
            if (contentFilePath.Contains("/"))
            {
                var parts = contentFilePath.Split('/');
                stripped = parts[parts.Length - 1];
            }
            return stripped;
        }

        // ---------------- 解析辅助（正则解析，兼容不规范 XML） ----------------

        private static readonly Regex ItemTagRegex = new Regex(@"<item\b[^>]*>", RegexOptions.IgnoreCase);
        private static readonly Regex NavLinkRegex = new Regex(@"<a\b[^>]*href\s*=\s*[""']([^""']+)[""'][^>]*>(.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex BodyOpenTagRegex = new Regex(@"<body\b[^>]*>", RegexOptions.IgnoreCase);
        private static readonly Regex StyleAttrRegex = new Regex(@"style\s*=\s*[""'][^""']*[""']", RegexOptions.IgnoreCase);

        /// <summary>从 XML 片段中提取指定元素的属性值（正则，兼容大小写与命名空间前缀）。</summary>
        private static string ExtractAttribute(string xml, string elementName, string attrName)
        {
            var m = Regex.Match(xml,
                "<" + elementName + @"\b[^>]*" + attrName + @"\s*=\s*[""']([^""']+)[""']",
                RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : null;
        }

        private static string MatchAttribute(string tag, string attrName)
        {
            var m = Regex.Match(tag, attrName + @"\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>解析 OPF manifest：返回 (id, href, media-type, properties) 列表。</summary>
        private static List<(string id, string href, string mediaType, string properties)> ParseManifestItems(string opfText)
        {
            var list = new List<(string, string, string, string)>();
            foreach (Match m in ItemTagRegex.Matches(opfText))
            {
                string tag = m.Value;
                string href = MatchAttribute(tag, "href");
                if (string.IsNullOrEmpty(href)) continue;
                list.Add((MatchAttribute(tag, "id") ?? "", href,
                    MatchAttribute(tag, "media-type") ?? "", MatchAttribute(tag, "properties") ?? ""));
            }
            return list;
        }

        /// <summary>定位目录文件：优先 NCX（EPUB2），其次 properties=nav 的导航文档（EPUB3）。</summary>
        private static string FindTocHref(List<(string id, string href, string mediaType, string properties)> items)
        {
            foreach (var it in items)
            {
                if (it.href.EndsWith(".ncx", StringComparison.OrdinalIgnoreCase)) return it.href;
            }
            foreach (var it in items)
            {
                if (it.properties.IndexOf("nav", StringComparison.OrdinalIgnoreCase) >= 0) return it.href;
            }
            return null;
        }

        /// <summary>
        /// 解析章节条目：返回 (虚拟主机 URI, 章节名, 章节文件)。
        ///
        /// 以 OPF spine（&lt;itemref&gt; 顺序）为唯一阅读顺序来源——这是 EPUB 规范定义的阅读顺序，
        /// 即使用户书籍的 NCX 目录不完整（例如只含封面），也能得到完整章节列表；
        /// NCX/nav 仅用于为章节提供显示名（匹配不上时用文件名）。
        /// 兼容：spine 缺失时回退 manifest 全部 HTML 项；NCX 中 href 支持 ../ 与 URL 编码。
        /// </summary>
        private async Task<List<(Uri uri, string name, StorageFile file)>> ResolveChapterEntriesAsync(
            IStorageFolder contentFolder, string opfText,
            List<(string id, string href, string mediaType, string properties)> items)
        {
            var result = new List<(Uri, string, StorageFile)>();

            // 1. spine 阅读顺序（idref 列表，按阅读顺序）
            var spineRefs = ParseSpineItemRefs(opfText);

            // 2. NCX/nav 章节名映射：href(规范化) → 名称
            var nameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string tocHref = FindTocHref(items);
            if (tocHref != null)
            {
                try
                {
                    var toc = await ResolveTocFileAsync(contentFolder, tocHref);
                    string tocRelDir = tocHref.Contains("/")
                        ? tocHref.Substring(0, tocHref.LastIndexOf('/')) + "/"
                        : "";
                    string text = await FileIO.ReadTextAsync(toc.file);
                    BuildTocNameMap(nameMap, text, toc.isNcx, tocRelDir);
                }
                catch (Exception)
                {
                    // 目录文件缺失/损坏不影响正文解析，仅失去章节名
                }
            }

            // 3. 生成章节：spine 优先，缺失时回退 manifest 全部 HTML 项
            var refs = spineRefs.Count > 0 ? spineRefs : null;
            if (refs != null)
            {
                // spine itemref 引用 manifest item 的 id → 映射到 href
                var hrefById = items
                    .Where(it => it.id.Length > 0)
                    .ToDictionary(it => it.id, it => it.href, StringComparer.OrdinalIgnoreCase);
                foreach (string idref in refs)
                {
                    if (!hrefById.TryGetValue(idref, out string href)) continue;
                    string name = nameMap.TryGetValue(NormalizeRelativePath(href), out string mapped)
                        ? mapped
                        : Path.GetFileNameWithoutExtension(href);
                    var chapter = await BuildChapterAsync(contentFolder, href, name);
                    if (chapter != null) result.Add(chapter.Value);
                }
            }

            if (result.Count == 0)
            {
                // 无 spine 或全部解析失败：回退 manifest 全部 HTML 项（按清单顺序）
                foreach (var it in items)
                {
                    if (IsHtmlItem(it))
                    {
                        string name = nameMap.TryGetValue(NormalizeRelativePath(it.href), out string mapped)
                            ? mapped
                            : Path.GetFileNameWithoutExtension(it.href);
                        var chapter = await BuildChapterAsync(contentFolder, it.href, name);
                        if (chapter != null) result.Add(chapter.Value);
                    }
                }
            }
            return result;
        }

        /// <summary>解析 OPF spine：按顺序返回 itemref 的 idref 列表。</summary>
        private static List<string> ParseSpineItemRefs(string opfText)
        {
            var result = new List<string>();
            var m = Regex.Match(opfText, @"<spine\b[^>]*>(.*?)</spine>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!m.Success) return result;

            foreach (Match im in Regex.Matches(m.Groups[1].Value,
                @"<itemref\b[^>]*idref\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase))
            {
                result.Add(im.Groups[1].Value);
            }
            return result;
        }

        /// <summary>
        /// 构建 NCX/nav 章节名映射：key 为规范化相对路径（相对 OPF 目录），value 为显示名。
        /// NCX src 相对 NCX 所在目录，需拼接 tocRelDir 后规范化，才能与 spine href 对齐。
        /// </summary>
        private static void BuildTocNameMap(Dictionary<string, string> nameMap, string tocText, bool isNcx, string tocRelDir)
        {
            if (isNcx)
            {
                // 逐个 navPoint 提取（text 名称 + content src），支持嵌套 navPoint
                var navPointRegex = new Regex(@"<navPoint\b[^>]*>.*?</navPoint>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                foreach (Match np in navPointRegex.Matches(tocText))
                {
                    var textM = Regex.Match(np.Value, @"<text[^>]*>(.*?)</text>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    var srcM = Regex.Match(np.Value, @"<content\b[^>]*src\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                    if (!textM.Success || !srcM.Success) continue;

                    string name = WebUtility.HtmlDecode(textM.Groups[1].Value.Trim());
                    string key = NormalizeRelativePath(tocRelDir + StripParametersOffFilePath(srcM.Groups[1].Value));
                    if (name.Length > 0 && key.Length > 0 && !nameMap.ContainsKey(key))
                    {
                        nameMap[key] = name;
                    }
                }
            }
            else
            {
                foreach (Match m in NavLinkRegex.Matches(tocText))
                {
                    string name = WebUtility.HtmlDecode(m.Groups[2].Value.Trim());
                    string key = NormalizeRelativePath(tocRelDir + StripParametersOffFilePath(m.Groups[1].Value));
                    if (name.Length > 0 && key.Length > 0 && !nameMap.ContainsKey(key))
                    {
                        nameMap[key] = name;
                    }
                }
            }
        }

        /// <summary>
        /// 构造章节条目：解析相对路径（去 #/?、URL 解码、规范化 ../ 与 ./），
        /// 生成 https://epub.local/ 虚拟主机 URI 并定位实际文件。
        /// 解析失败返回 null（单个章节缺失不中断整本书）。
        /// </summary>
        private async Task<(Uri uri, string name, StorageFile file)?> BuildChapterAsync(
            IStorageFolder contentFolder, string rawPath, string fallbackName = null)
        {
            try
            {
                string cleaned = StripParametersOffFilePath(rawPath);
                string decoded = Uri.UnescapeDataString(cleaned);
                string rel = NormalizeRelativePath(decoded);
                if (rel.Length == 0) return null;

                // UWP 的 GetFileAsync 不支持带子目录的路径，必须逐级导航
                StorageFile file = await GetFileByRelativePathAsync(contentFolder, rel);
                Uri uri = new Uri("https://" + EpubVirtualHost + "/" + rel);
                string name = fallbackName ?? Path.GetFileNameWithoutExtension(rel);
                return (uri, name, file);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>按多级相对路径定位文件（逐级 GetFolderAsync + GetFileAsync，UWP 不支持单次带目录路径）。</summary>
        private static async Task<StorageFile> GetFileByRelativePathAsync(IStorageFolder root, string relPath)
        {
            var parts = relPath.Split('/');
            IStorageFolder folder = root;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                folder = await folder.GetFolderAsync(parts[i]);
            }
            return await folder.GetFileAsync(parts[parts.Length - 1]);
        }

        /// <summary>规范化相对路径：移除 ./ 段、按 ../ 逐级回退，返回 / 分隔的相对路径。</summary>
        private static string NormalizeRelativePath(string path)
        {
            var segments = new List<string>();
            foreach (var seg in path.Split('/'))
            {
                if (seg.Length == 0 || seg == ".") continue;
                if (seg == "..")
                {
                    if (segments.Count > 0) segments.RemoveAt(segments.Count - 1);
                    continue;
                }
                segments.Add(seg);
            }
            return string.Join("/", segments);
        }

        private async Task<(StorageFile file, bool isNcx)> ResolveTocFileAsync(IStorageFolder contentFolder, string tocHref)
        {
            string decoded = Uri.UnescapeDataString(tocHref);
            string dir = decoded.Contains("/") ? decoded.Substring(0, decoded.LastIndexOf('/')) : "";
            string name = decoded.Contains("/") ? decoded.Substring(decoded.LastIndexOf('/') + 1) : decoded;

            var folder = contentFolder;
            if (dir.Length > 0)
            {
                foreach (var p in dir.Split('/'))
                {
                    folder = await folder.GetFolderAsync(p);
                }
            }
            var file = await folder.GetFileAsync(name);
            return (file, name.EndsWith(".ncx", StringComparison.OrdinalIgnoreCase));
        }

        private static string StripParametersOffFilePath(string chapterFilePath)
        {
            if (chapterFilePath.Contains("#"))
            {
                chapterFilePath = chapterFilePath.Substring(0, chapterFilePath.IndexOf('#'));
            }
            if (chapterFilePath.Contains("?"))
            {
                chapterFilePath = chapterFilePath.Substring(0, chapterFilePath.IndexOf('?'));
            }
            return chapterFilePath;
        }

        private static bool IsHtmlItem((string id, string href, string mediaType, string properties) it)
        {
            if (it.mediaType.IndexOf("xhtml", StringComparison.OrdinalIgnoreCase) >= 0
                || it.mediaType.IndexOf("html", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
            return it.href.EndsWith(".xhtml", StringComparison.OrdinalIgnoreCase)
                || it.href.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                || it.href.EndsWith(".htm", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 安全注入 body 样式：定位第一个 &lt;body&gt; 开标签（不区分大小写），
        /// 保留原有属性，仅追加/替换 style；无 body 标签时整体包裹。
        /// </summary>
        private static string InjectBodyStyle(string html, string style)
        {
            var m = BodyOpenTagRegex.Match(html);
            if (!m.Success)
            {
                return "<body style=\"" + style + "\">" + html + "</body>";
            }

            string openTag = m.Value;
            string newTag;
            if (StyleAttrRegex.IsMatch(openTag))
            {
                newTag = StyleAttrRegex.Replace(openTag, "style=\"" + style + "\"");
            }
            else
            {
                newTag = openTag.Substring(0, openTag.Length - 1) + " style=\"" + style + "\">";
            }
            return html.Substring(0, m.Index) + newTag + html.Substring(m.Index + m.Length);
        }

        /// <summary>
        /// 在章节 HTML 中注入全局 CSS 覆盖规则：
        /// 插入到 &lt;/head&gt; 之前（无 head 时插到 body 开标签之后），
        /// 用于统一书内所有元素的字号，压过超大字号/内联样式。
        /// </summary>
        private static string InjectGlobalStyleCss(string html, string css)
        {
            string styleTag = "<style type=\"text/css\">" + css + "</style>";

            int headEnd = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
            if (headEnd >= 0)
            {
                return html.Substring(0, headEnd) + styleTag + html.Substring(headEnd);
            }

            var m = BodyOpenTagRegex.Match(html);
            if (m.Success)
            {
                return html.Substring(0, m.Index + m.Length) + styleTag + html.Substring(m.Index + m.Length);
            }

            return html + styleTag;
        }

        /// <summary>
        /// 全局字号覆盖规则：强制所有正文元素使用统一字号与行高（!important 压过书内样式）。
        /// 含标题（h1-h6）、段落、列表、表格、代码块（pre/code）等，避免书内超大字号破坏排版。
        /// 英文/数字使用 Times New Roman，代码使用 JetBrainsMono，正文使用用户选择的中文字体。
        /// </summary>
        private string BuildOverlayCss()
        {
            int size = (int)currentFontSize;
            return $"body, p, div, span, li, ul, ol, td, th, blockquote, dd, dt, section, article, h1, h2, h3, h4, h5, h6, font, big, small {{ font-size: {size}px !important; line-height: 2 !important; }}"
                + $" pre, code, kbd, samp, tt {{ font-size: {size}px !important; line-height: 1.6 !important; font-family: 'JetBrains Mono Medium', 'MiSans Medium', monospace !important; }}"
                + $" body, p, div, span, li, td, th, dd, dt, section, article, blockquote {{ font-family: '{currentFont}', 'Times New Roman', serif !important; }}";
        }

        /// <summary>根据当前字体构建正文渲染样式（与 TXT 阅读器风格一致，英文 Times New Roman）。</summary>
        private string BuildBodyStyle()
        {
            int size = (int)currentFontSize;
            return $"font-family: '{currentFont}', 'Times New Roman', serif; line-height: 2; font-size: {size}px; margin: 36px; letter-spacing: 2px; background-color: #fbf7f0; font-weight: SemiBold;";
        }

        private void ListViewSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // WebView 内部导航同步选中项时，跳过（章节已在 WebView 中加载，避免重复导航）
            if (syncingChapterSelection)
            {
                return;
            }

            var pivot = sender as ListView;
            var chapter = pivot.SelectedItem as Chapter;

            if (chapter?.BookFile == null) return;

            // 加载章节：将书籍内容目录映射为虚拟主机（https://epub.local/），
            // 章节内相对 CSS/图片资源可正常解析（WebView2 不支持 ms-appdata:///）
            if (PivotItemWebView.CoreWebView2 == null)
            {
                PivotItemWebView.EnsureCoreWebView2Async().AsTask().ContinueWith(t =>
                {
                    // 用户可能已离开页面，此时再导航会抛异常
                    if (PivotItemWebView.CoreWebView2 != null)
                    {
                        EnsureVirtualHostAndNavigate(chapter);
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }
            else
            {
                EnsureVirtualHostAndNavigate(chapter);
            }
        }

        private void EnsureVirtualHostAndNavigate(Chapter chapter)
        {
            try
            {
                if (PivotItemWebView.CoreWebView2 == null || string.IsNullOrEmpty(contentFolderPath))
                {
                    // 虚拟主机未就绪：回退到本地文件路径加载
                    PivotItemWebView.Source = new Uri(chapter.BookFile.Path);
                    return;
                }

                PivotItemWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    EpubVirtualHost,
                    contentFolderPath,
                    CoreWebView2HostResourceAccessKind.Allow);
                PivotItemWebView.Source = chapter.Uri;
            }
            catch (Exception)
            {
                // 映射失败时回退到本地文件路径加载
                PivotItemWebView.Source = new Uri(chapter.BookFile.Path);
            }
        }

        private void BackMainPage(object sender, RoutedEventArgs e)
        {
            // CoreWebView2 可能尚未初始化完成（如打开后立即返回），需空值保护
            if (PivotItemWebView.CoreWebView2 != null)
            {
                try
                {
                    PivotItemWebView.CoreWebView2.Stop();
                }
                catch (Exception)
                {
                }
            }
            try
            {
                PivotItemWebView.Close();
            }
            catch (Exception)
            {
            }
            mainContent.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
        }

        private void PrevPage(object sender, RoutedEventArgs e)
        {
            if (ChapterPivot.SelectedIndex > 0)
            {
                ChapterPivot.SelectedItem = ChapterPivot.Items[ChapterPivot.SelectedIndex - 1];
            }
        }

        private void NextPage(object sender, RoutedEventArgs e)
        {
            if (ChapterPivot.SelectedIndex < Chapters.Count - 1)
            {
                ChapterPivot.SelectedItem = ChapterPivot.Items[ChapterPivot.SelectedIndex + 1];
            }
        }

        /// <summary>
        /// 切换正文渲染字体：更新当前字体，重写所有章节的 body 样式与全局 CSS 并刷新当前章节。
        /// </summary>
        private async void ChangeFont(object sender, RoutedEventArgs e)
        {
            string font = ((MenuFlyoutItem)sender).Tag.ToString();
            currentFont = font;
            localSettings.Values["font"] = font;

            string style = BuildBodyStyle();
            string overlayCss = BuildOverlayCss();
            foreach (var chapter in Chapters)
            {
                try
                {
                    // 同步 File API（独占锁），避免与 WebView2 渲染读取同一文件冲突
                    string text = await Task.Run(() => File.ReadAllText(chapter.BookFile.Path));
                    text = InjectBodyStyle(text, style);
                    text = InjectGlobalStyleCss(text, overlayCss);
                    await Task.Run(() => File.WriteAllText(chapter.BookFile.Path, text));
                }
                catch (Exception)
                {
                    // 单个章节写入失败不中断整体字体切换
                }
            }

            var current = ChapterPivot.SelectedItem as Chapter;
            if (current != null)
            {
                PivotItemWebView.Source = current.Uri;
            }
        }

        private async void CreateNewSticky(object sender, RoutedEventArgs e)
        {
            List<object> parameter = new List<object>();
            string serial = Guid.NewGuid().ToString("D").ToUpper();
            string UID = localSettings.Values["UID"].ToString();
            string theme = localSettings.Values["StickyTheme"].ToString();

            StickyQuickView stickyQuickView = new StickyQuickView
            {
                CreateTimeText = DateTime.Now.ToShortDateString(),
                StickySerial = serial,
                ThemeKind = theme,
                TitleBackground = brushTool.GetThemeColorBrush(theme, false).TitleBrush,
                Background = brushTool.GetThemeColorBrush(theme, false).BackgroundBrush,
            };

            parameter.Add("new");
            parameter.Add(stickyQuickView);

            // 打开新便签界面
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
            bool viewShown = await ApplicationViewSwitcher.TryShowAsStandaloneAsync(newViewId);
        }
    }
}
