using FreeSql.DataAnnotations;
using System;

namespace CactusReaderService.Entities
{
    /// <summary>
    /// userkey 表实体：Windows Hello 设备密钥注册记录。
    /// PublicKey / Attestation 均为 Base64 编码文本。
    /// </summary>
    public class Userkey
    {
        [Column(IsPrimary = true)]
        public string ID { get; set; }

        public string UID { get; set; }

        public string PublicKey { get; set; }

        public string Attestation { get; set; }

        public string DeviceID { get; set; }

        public DateTime LastSignInTime { get; set; }
    }
}
