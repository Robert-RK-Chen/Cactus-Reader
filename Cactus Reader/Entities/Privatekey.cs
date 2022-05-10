using FreeSql.DataAnnotations;

namespace Cactus_Reader.Entities
{
    public class Privatekey
    {
        [Column(IsPrimary = true)]
        public string UID { get; set; }
        
        public string Key { get; set; }

        public virtual User UidNavigation { get; set; }
    }
}
