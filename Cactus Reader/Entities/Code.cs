using FreeSql.DataAnnotations;
using System;

namespace Cactus_Reader.Entities
{
    public class Code
    {
        [Column(IsPrimary = true)]
        public string Email { get; set; }
        
        public string VerifyCode { get; set; }
        
        public DateTime CreateTime { get; set; }
        
        public string CodeType { get; set; }
    }
}
