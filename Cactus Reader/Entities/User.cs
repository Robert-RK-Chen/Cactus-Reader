using FreeSql.DataAnnotations;

namespace Cactus_Reader.Entities
{
    internal class User
    {
        [Column(IsPrimary = true)]
        public string Uid { set; get; }
        public string Email { set; get; }
        public string Name { set; get; }
        public string Mobile { set; get; }
        public string Password { set; get; }
        public string Profile { set; get; }
    }
}
