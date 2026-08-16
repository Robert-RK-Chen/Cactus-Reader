namespace Cactus_Reader.Entities
{
    /// <summary>
    /// 资源库阅读记录实体。一条记录代表一次阅读痕迹，序列化为 JSON 存于
    /// LocalFolder/{UID}/Library/library.json，按 UpdateTime（最后阅读时间）降序展示。
    /// 记录内容来源（Source）、阅读类型（ItemType）与最后阅读时间（UpdateTime）。
    /// </summary>
    public class ReadingItem : CollectibleItem
    {
        /// <summary>
        /// 内容来源：
        /// 本地文档 / EPUB = FutureAccessList 令牌（跨会话重新取回 StorageFile）；
        /// 网络文档 = 完整 URL（重新打开时按输入网页的方式重新抓取）。
        /// </summary>
        public string Source { get; set; }

        /// <summary>缓存文件名（Library/Cache 目录下），仅网络文档缓存正文时使用。</summary>
        public string CacheFile { get; set; }

        /// <summary>EPUB 阅读进度：章节索引（恢复阅读位置用）。</summary>
        public int Chapter { get; set; }

        /// <summary>EPUB 阅读进度：章节内位置。</summary>
        public int Position { get; set; }

        /// <summary>原始文件名扩展名（小写，如 .epub / .txt / .pdf），本地文档区分阅读页用。</summary>
        public string Extension { get; set; }
    }
}
