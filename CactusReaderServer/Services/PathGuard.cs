using System;
using System.IO;

namespace CactusReaderService.Services
{
    /// <summary>
    /// 路径安全守卫 —— 单一职责：校验来自 HTTP 请求的路径参数，
    /// 防止路径穿越攻击（..\、绝对路径、非法文件名字符等）。
    /// </summary>
    public static class PathGuard
    {
        /// <summary>
        /// 校验路径参数是否为安全的单个路径段。
        /// </summary>
        public static bool IsValidPathPart(string value)
        {
            return !string.IsNullOrEmpty(value)
                && !value.Contains("..")
                && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }
    }
}
