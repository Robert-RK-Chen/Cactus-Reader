using FreeSql.DataAnnotations;
using System;

namespace CactusReaderService.Entities
{
    /// <summary>
    /// user 表实体（服务端专用，客户端不再直连数据库）。
    /// Password 字段存储格式：Base64(盐) + ":" + 双重 SHA256 哈希（见 PasswordHashService）。
    /// </summary>
    public class User
    {
        [Column(IsPrimary = true)]
        public string UID { get; set; }

        public string Email { get; set; }

        public string Name { get; set; }

        public string Mobile { get; set; }

        public string Password { get; set; }

        public DateTime RegistDate { get; set; }
    }
}
