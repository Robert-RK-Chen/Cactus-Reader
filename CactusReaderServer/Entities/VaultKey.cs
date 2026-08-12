using FreeSql.DataAnnotations;
using System;

namespace CactusReaderService.Entities
{
    /// <summary>
    /// vaultkey 表实体：用户便签保险箱 —— 存储"密码包裹的便签加密密钥"。
    /// Salt / WrappedKey 均为 Base64 编码文本。服务端零知识：
    /// 只持有盐与密文密钥，无法解出任何便签数据。
    /// </summary>
    public class VaultKey
    {
        [Column(IsPrimary = true)]
        public string UID { get; set; }

        /// <summary>PBKDF2 盐（Base64）</summary>
        public string Salt { get; set; }

        /// <summary>KEK（由个人密码派生）加密后的便签密钥（Base64）</summary>
        public string WrappedKey { get; set; }

        public DateTime UpdateTime { get; set; }
    }
}
