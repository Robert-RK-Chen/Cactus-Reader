namespace Cactus_Reader.Entities
{
    /// <summary>
    /// 可收藏内容类型。资源库阅读记录、收藏夹、便签等统一以该枚举区分内容形态，
    /// 新增阅读类型（音频/视频/PDF 等）时在此扩展即可。
    /// </summary>
    public enum CollectibleType
    {
        /// <summary>电子书（EPUB）。</summary>
        Book,

        /// <summary>本地文档（txt / rtf / pdf 等）。</summary>
        Document,

        /// <summary>网络文档（URL 抓取的沉浸式正文）。</summary>
        WebPage,

        /// <summary>便签（Cactus Notes）。</summary>
        Sticky,
    }
}
