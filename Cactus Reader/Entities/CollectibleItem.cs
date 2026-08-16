using System;

namespace Cactus_Reader.Entities
{
    /// <summary>
    /// 可收藏内容抽象基类：便签、电子书、本地文档、网络文档等统一抽象。
    /// 资源库（阅读记录）与收藏夹共用该契约，后续"收藏任意内容"时
    /// 只需面向 CollectibleItem 编程，无需感知具体子类。
    /// </summary>
    public abstract class CollectibleItem
    {
        /// <summary>全局唯一标识（Guid 大写字符串）。子类可重写以映射既有标识。</summary>
        public virtual string Serial { get; set; }

        /// <summary>显示名称（文件名 / 网页标题 / 便签预览）。</summary>
        public string Name { get; set; }

        /// <summary>内容类型。</summary>
        public CollectibleType ItemType { get; set; }

        /// <summary>创建时间。</summary>
        public DateTime CreateTime { get; set; }

        /// <summary>最后修改 / 最后阅读时间（列表排序基准）。</summary>
        public DateTime UpdateTime { get; set; }

        /// <summary>是否已收藏（收藏夹使用，资源库记录默认为 false）。</summary>
        public bool IsFavorite { get; set; }
    }
}
