using Newtonsoft.Json;
using System;
using System.Linq;
using System.Runtime.Serialization;

namespace Cactus_Reader.Entities
{
    /// <summary>
    /// 便签数据实体。序列化为 JSON 后经 AES 加密落盘（{UID}/Sticky/{StickySerial}.ctsnote）。
    /// StickyDocument 保存 RTF 全文；QuickViewText 保存纯文本预览（冗余存储，避免每次读 RTF 提取）。
    /// 继承 CollectibleItem，与书本 / 网络文档统一抽象，供资源库与收藏夹共用。
    /// </summary>
    public class Sticky : CollectibleItem
    {
        /// <summary>便签唯一标识（Guid 大写字符串），同时作为文件名。保留旧字段名以兼容历史存档。</summary>
        public string StickySerial { get; set; }

        /// <summary>基类标识映射到 StickySerial，保证新旧 JSON 双写一致（旧文件仅有 StickySerial 也能正确加载）。</summary>
        [JsonIgnore]
        public override string Serial
        {
            get => StickySerial;
            set => StickySerial = value;
        }

        /// <summary>便签正文（RTF 格式）。</summary>
        public string StickyDocument { get; set; }

        /// <summary>纯文本预览（卡片 QuickView 展示）。</summary>
        public string QuickViewText { get; set; }

        /// <summary>主题标识（GingkoYellow / MintGreen / ...）。</summary>
        public string StickyTheme { get; set; }

        /// <summary>是否锁定（锁定后卡片显示提示文案，不显示内容）。</summary>
        public bool IsLock { get; set; }

        /// <summary>反序列化完成后兜底：旧存档无 ItemType / Name 时补齐默认值，标题取第一行内容；锁定便签不暴露内容。</summary>
        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (StickySerial == null)
            {
                StickySerial = Guid.NewGuid().ToString("D").ToUpper();
            }
            ItemType = CollectibleType.Sticky;
            if (IsLock)
            {
                Name = "锁定便签";
                return;
            }
            if (string.IsNullOrEmpty(Name))
            {
                string preview = (QuickViewText ?? string.Empty).Trim();
                if (preview.Length > 0)
                {
                    string firstLine = preview
                        .Split('\n')
                        .Select(line => line.Trim())
                        .FirstOrDefault(line => line.Length > 0) ?? string.Empty;
                    Name = firstLine.Length > 24 ? firstLine.Substring(0, 24) : firstLine;
                }
                if (string.IsNullOrEmpty(Name))
                {
                    Name = "便签";
                }
            }
        }
    }
}
