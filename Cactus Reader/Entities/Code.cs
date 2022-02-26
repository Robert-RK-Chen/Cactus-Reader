using FreeSql.DataAnnotations;
using System;

namespace Cactus_Reader.Entities
{
    internal class Code
    {
        [Column(IsPrimary = true)]
        public string email { get; set; }
        public string verify_code { get; set; }
        public DateTime create_time { get; set; }
    }
}
