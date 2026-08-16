using System;

namespace Cactus_Reader.Entities
{
    /// <summary>
    /// 回收站条目实体。被删除的便签 / 阅读记录统一以该实体记录于
    /// LocalFolder/{UID}/Recycle/recycle.json，按删除时间（DeleteTime）降序展示。
    /// 本地文件（便签 .ctsnote / 网络文档缓存 .txt）随条目一并移入 Recycle 目录；
    /// 云端文件（Notes / Library 区）经 /move-file 移入 Recycle 区，恢复 / 彻底删除时反向操作。
    /// </summary>
    public class RecycleItem
    {
        /// <summary>回收站条目唯一标识（Guid 大写字符串）。</summary>
        public string Serial { get; set; }

        /// <summary>原始条目标识（便签 StickySerial / 阅读记录 ReadingItem.Serial），恢复与云端文件定位用。</summary>
        public string OriginalSerial { get; set; }

        /// <summary>内容类型（便签 / 电子书 / 文档 / 网络文档），决定恢复时的去处。</summary>
        public CollectibleType ItemType { get; set; }

        /// <summary>显示名称（便签预览 / 书名 / 网页标题）。</summary>
        public string Name { get; set; }

        /// <summary>
        /// 原始对象 JSON 快照：
        /// 便签 = Sticky 明文 JSON（回收站展示预览用，文件本身保持加密移动）；
        /// 阅读记录 = ReadingItem JSON（恢复时反序列化加回 library.json）。
        /// 解密失败 / 无快照时为 null。
        /// </summary>
        public string Payload { get; set; }

        /// <summary>删除（进入回收站）时间。</summary>
        public DateTime DeleteTime { get; set; }
    }
}
