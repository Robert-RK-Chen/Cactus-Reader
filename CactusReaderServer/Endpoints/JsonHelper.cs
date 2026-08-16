using System.Collections.Generic;
using System.Text;

namespace CactusReaderService.Endpoints
{
    /// <summary>
    /// 文件名清单 JSON 数组序列化（Notes/File 端点共用）。
    /// 保留与旧版逐字节一致的输出格式：手动转义反斜杠与引号，不依赖序列化库。
    /// </summary>
    internal static class JsonHelper
    {
        /// <summary>将字符串列表序列化为 JSON 数组。</summary>
        public static string BuildJsonArray(List<string> values)
        {
            StringBuilder sb = new StringBuilder("[");
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }
                sb.Append('"')
                  .Append(values[i].Replace("\\", "\\\\").Replace("\"", "\\\""))
                  .Append('"');
            }
            sb.Append(']');
            return sb.ToString();
        }
    }
}
