using FreeSql.DataAnnotations;

namespace Cactus_Reader.Sources.Utilities
{
    internal class User
    {
        [Column(IsPrimary = true)]
        public string userID { get; set; }
        public string userEmail { get; set; }
        public string userName { get; set; }
        public string phoneNum { get; set; }
        public string password { get; set; }
        public string userProfile { get; set; }
    }
}
