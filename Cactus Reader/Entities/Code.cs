using FreeSql.DataAnnotations;
using System;

namespace Cactus_Reader.Entities
{
    internal class Code
    {
        [Column(IsPrimary = true)]
        public string Email { get; set; }
        public string VerifyCode { get; set; }
        public DateTime CreatTime { get; set; }
    }
}
