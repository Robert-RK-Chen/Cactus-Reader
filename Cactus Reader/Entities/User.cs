using FreeSql.DataAnnotations;

namespace Cactus_Reader.Entities
{
    internal class User
    {
        [Column(IsPrimary = true)]
        public string uid { set; get; }
        public string email { set; get; }
        public string name { set; get; }
        public string mobile { set; get; }
        public string password { set; get; }
        public string profile { set; get; }
    }
}
