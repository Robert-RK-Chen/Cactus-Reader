using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CactusReaderService.Services
{
    /// <summary>
    /// 文件存储服务 —— 单一职责：用户数据的磁盘原子操作。
    /// 不依赖任何 HTTP 类型，端点层负责将 HTTP 请求翻译为对本服务的调用。
    ///
    /// 磁盘布局：
    ///   {DataRoot}/{UID}/ProfilePicture.PNG        用户头像
    ///   {DataRoot}/{UID}/Notes/{serial}            用户便签（在用）
    ///   {DataRoot}/{UID}/Library/{serial}          阅读内容存档（在用）
    ///   {DataRoot}/{UID}/Recycle/{serial}          回收站内容（删除后暂存）
    /// </summary>
    public class FileStorageService
    {
        private readonly string _dataRoot;

        public FileStorageService(string dataRoot)
        {
            _dataRoot = dataRoot ?? throw new ArgumentNullException(nameof(dataRoot));
        }

        public string DataRoot => _dataRoot;

        /// <summary>
        /// 确保数据根目录存在。
        /// </summary>
        public void EnsureRoot()
        {
            Directory.CreateDirectory(_dataRoot);
        }

        // ---------------- 头像 ----------------

        /// <summary>
        /// 保存用户头像（覆盖同名文件）。
        /// </summary>
        public async Task SaveProfileImageAsync(string uid, Stream content, CancellationToken cancellationToken = default)
        {
            string directory = Path.Combine(_dataRoot, uid);
            Directory.CreateDirectory(directory);

            string filePath = Path.Combine(directory, "ProfilePicture.PNG");
            await WriteFileAsync(filePath, content, cancellationToken);
        }

        /// <summary>
        /// 读取用户头像；文件不存在时返回 false。
        /// </summary>
        public bool TryReadProfileImage(string uid, out byte[] bytes)
        {
            return TryReadFile(Path.Combine(_dataRoot, uid, "ProfilePicture.PNG"), out bytes);
        }

        /// <summary>
        /// 获取头像文件物理路径（供 ETag / 304 协商使用）；文件不存在时返回 false。
        /// </summary>
        public bool TryGetProfileImagePath(string uid, out string path)
        {
            path = Path.Combine(_dataRoot, uid, "ProfilePicture.PNG");
            return File.Exists(path);
        }

        // ---------------- 便签 ----------------

        /// <summary>
        /// 保存用户便签文件（覆盖同名文件）。
        /// </summary>
        public async Task SaveNoteAsync(string uid, string serial, Stream content, CancellationToken cancellationToken = default)
        {
            string directory = Path.Combine(_dataRoot, uid, "Notes");
            Directory.CreateDirectory(directory);

            string filePath = Path.Combine(directory, serial);
            await WriteFileAsync(filePath, content, cancellationToken);
        }

        /// <summary>
        /// 读取用户便签文件；文件不存在时返回 false。
        /// </summary>
        public bool TryReadNote(string uid, string serial, out byte[] bytes)
        {
            return TryReadFile(Path.Combine(_dataRoot, uid, "Notes", serial), out bytes);
        }

        /// <summary>
        /// 删除用户便签文件；返回文件是否存在。
        /// </summary>
        public bool DeleteNote(string uid, string serial)
        {
            string filePath = Path.Combine(_dataRoot, uid, "Notes", serial);
            if (!File.Exists(filePath))
            {
                return false;
            }
            File.Delete(filePath);
            return true;
        }

        /// <summary>
        /// 列出指定用户的便签文件名清单（不含目录）。目录不存在时返回空列表。
        /// </summary>
        public List<string> ListNotes(string uid)
        {
            return ListFiles(uid, "notes");
        }

        // ---------------- 通用存储区（library / recycle） ----------------

        /// <summary>
        /// 存储区目录名映射。notes / library / recycle 三区共用同一套增删查逻辑。
        /// </summary>
        private static string GetSectionDirectory(string section)
        {
            switch (section)
            {
                case "notes": return "Notes";
                case "library": return "Library";
                case "recycle": return "Recycle";
                default: throw new ArgumentException($"未知存储区: {section}");
            }
        }

        /// <summary>
        /// 保存指定存储区的用户文件（覆盖同名文件）。
        /// </summary>
        public async Task SaveFileAsync(string uid, string serial, string section, Stream content, CancellationToken cancellationToken = default)
        {
            string directory = Path.Combine(_dataRoot, uid, GetSectionDirectory(section));
            Directory.CreateDirectory(directory);

            string filePath = Path.Combine(directory, serial);
            await WriteFileAsync(filePath, content, cancellationToken);
        }

        /// <summary>
        /// 读取指定存储区的用户文件；文件不存在时返回 false。
        /// </summary>
        public bool TryReadFile(string uid, string serial, string section, out byte[] bytes)
        {
            return TryReadFile(Path.Combine(_dataRoot, uid, GetSectionDirectory(section), serial), out bytes);
        }

        /// <summary>
        /// 删除指定存储区的用户文件；返回文件是否存在。
        /// </summary>
        public bool DeleteFile(string uid, string serial, string section)
        {
            string filePath = Path.Combine(_dataRoot, uid, GetSectionDirectory(section), serial);
            if (!File.Exists(filePath))
            {
                return false;
            }
            File.Delete(filePath);
            return true;
        }

        /// <summary>
        /// 列出指定存储区的用户文件名清单（不含目录）。目录不存在时返回空列表。
        /// </summary>
        public List<string> ListFiles(string uid, string section)
        {
            List<string> files = new List<string>();
            string sectionPath = Path.Combine(_dataRoot, uid, GetSectionDirectory(section));
            if (Directory.Exists(sectionPath))
            {
                foreach (string file in Directory.GetFiles(sectionPath))
                {
                    files.Add(Path.GetFileName(file));
                }
            }
            return files;
        }

        /// <summary>
        /// 跨区移动文件（notes / library / recycle 之间）。源文件不存在返回 false；
        /// 目标已存在同名文件时先覆盖删除。删除进回收站 / 从回收站恢复均走此操作。
        /// </summary>
        public bool MoveFile(string uid, string serial, string fromSection, string toSection)
        {
            string source = Path.Combine(_dataRoot, uid, GetSectionDirectory(fromSection), serial);
            if (!File.Exists(source))
            {
                return false;
            }

            string targetDirectory = Path.Combine(_dataRoot, uid, GetSectionDirectory(toSection));
            Directory.CreateDirectory(targetDirectory);
            string target = Path.Combine(targetDirectory, serial);
            if (File.Exists(target))
            {
                File.Delete(target);
            }

            File.Move(source, target);
            return true;
        }

        // ---------------- 私有原子操作 ----------------

        /// <summary>
        /// 覆盖写入文件：先删除已存在的同名文件，再异步从流复制内容。
        /// 必须异步读取请求体流（Kestrel），同步 CopyTo 会读到 0 字节。
        /// </summary>
        private static async Task WriteFileAsync(string filePath, Stream content, CancellationToken cancellationToken)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            using (FileStream fileStream = File.OpenWrite(filePath))
            {
                await content.CopyToAsync(fileStream, cancellationToken);
            }
        }

        /// <summary>
        /// 将整个文件读入内存；文件不存在时返回 false。
        /// </summary>
        private static bool TryReadFile(string filePath, out byte[] bytes)
        {
            bytes = null;
            if (!File.Exists(filePath))
            {
                return false;
            }

            bytes = File.ReadAllBytes(filePath);
            return true;
        }
    }
}
