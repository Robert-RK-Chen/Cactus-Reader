using Cactus_Reader.Entities;
using Cactus_Reader.Sources.StickyNotes;
using System;
using System.Linq;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Cactus_Reader.Sources.ToolKits
{
    /// <summary>
    /// 资源库卡片视觉帮助类：把 ReadingItem 转换为 XAML 可绑定的展示数据
    /// （类型图标字形 / 类型标签 / 封面矩形色 / 最后阅读时间文案）。
    /// 全部为静态纯函数，供 x:Bind 直接调用；SolidColorBrush 在调用（UI）线程新建。
    /// </summary>
    public static class ReadingItemVisual
    {
        /// <summary>Segoe Fluent Icons 字形：电子书 / 文档 / 网络文档 / 便签。</summary>
        public static string GetGlyph(CollectibleType type)
        {
            switch (type)
            {
                case CollectibleType.Book:
                    return "\uE736"; // ReadingMode
                case CollectibleType.WebPage:
                    return "\uE774"; // Globe
                case CollectibleType.Sticky:
                    return "\uE70B"; // QuickNote
                default:
                    return "\uE8A5"; // Document
            }
        }

        /// <summary>类型标签文案。</summary>
        public static string GetTypeText(CollectibleType type)
        {
            switch (type)
            {
                case CollectibleType.Book:
                    return "电子书";
                case CollectibleType.WebPage:
                    return "网络文档";
                case CollectibleType.Sticky:
                    return "便签";
                default:
                    return "文档";
            }
        }

        /// <summary>封面矩形底色（书本缩略图统一使用矩形色块 + 图标替代）。</summary>
        public static SolidColorBrush GetCoverBrush(CollectibleType type)
        {
            Color color;
            switch (type)
            {
                case CollectibleType.Book:
                    color = Color.FromArgb(0xFF, 0xC2, 0x75, 0x45); // 书橙
                    break;
                case CollectibleType.WebPage:
                    color = Color.FromArgb(0xFF, 0x4F, 0x9A, 0x6A); // 网页绿
                    break;
                case CollectibleType.Sticky:
                    color = Color.FromArgb(0xFF, 0xD9, 0xA8, 0x3C); // 便签黄
                    break;
                default:
                    color = Color.FromArgb(0xFF, 0x4A, 0x7F, 0xB5); // 文档蓝
                    break;
            }
            return new SolidColorBrush(color);
        }

        /// <summary>最后阅读时间文案：今天 / 昨天显示时分，今年显示月日，更早显示完整日期。</summary>
        public static string FormatTime(DateTime time)
        {
            if (time == default)
            {
                return string.Empty;
            }
            DateTime now = DateTime.Now;
            if (time.Date == now.Date)
            {
                return "今天 " + time.ToString("HH:mm");
            }
            if (time.Date == now.Date.AddDays(-1))
            {
                return "昨天 " + time.ToString("HH:mm");
            }
            return time.Year == now.Year ? time.ToString("M月d日") : time.ToString("yyyy年M月d日");
        }

        /// <summary>
        /// 回收站卡片便签预览：从 RecycleItem.Payload 反序列化 Sticky 提取纯文本预览
        /// （截断 30 字）；锁定（加密）便签不暴露内容，显示锁定占位文案；非便签条目或无快照返回空字符串。
        /// </summary>
        public static string GetPreview(CollectibleType type, string payload)
        {
            if (type != CollectibleType.Sticky || string.IsNullOrEmpty(payload))
            {
                return string.Empty;
            }
            try
            {
                Sticky sticky = Newtonsoft.Json.JsonConvert.DeserializeObject<Sticky>(payload);
                if (sticky == null)
                {
                    return string.Empty;
                }
                if (sticky.IsLock)
                {
                    return StickyQuickView.LockedPreviewText;
                }
                string preview = sticky.QuickViewText ?? string.Empty;
                if (string.IsNullOrEmpty(preview))
                {
                    return string.Empty;
                }
                return preview.Length > 30 ? preview.Substring(0, 30) + "…" : preview;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>收藏角标可见性：已收藏显示金色星标。</summary>
        public static Visibility FavoriteVisibility(bool isFavorite)
        {
            return isFavorite ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>右键菜单"收藏"项可见性：已收藏时隐藏（显示"取消收藏"）。</summary>
        public static Visibility UnfavoriteVisibility(bool isFavorite)
        {
            return isFavorite ? Visibility.Collapsed : Visibility.Visible;
        }

        /// <summary>收藏夹便签卡片封面底色：取便签主题的标题色（浅色纸条风，供 x:Bind 调用）。</summary>
        public static SolidColorBrush GetStickyCoverBrush(string theme)
        {
            return ThemeColorBrushTool.Instance.GetThemeColorBrush(theme ?? "GingkoYellow", false).TitleBrush;
        }

        /// <summary>
        /// 便签标题：纯文本预览的第一行（去除空白行，截断 24 字），空内容回退"便签"；
        /// 锁定（加密）便签不暴露内容，一律显示"锁定便签"。
        /// </summary>
        public static string GetStickyTitleText(string quickViewText, bool isLock)
        {
            if (isLock)
            {
                return "锁定便签";
            }
            string preview = (quickViewText ?? string.Empty).Trim();
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

        /// <summary>便签预览文案：锁定便签显示占位提示，否则显示内容。</summary>
        public static string GetStickyPreviewText(string quickViewText, bool isLock)
        {
            return isLock ? StickyQuickView.LockedPreviewText : (quickViewText ?? string.Empty);
        }
    }
}
