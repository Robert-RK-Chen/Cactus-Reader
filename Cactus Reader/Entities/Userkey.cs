using System;

namespace Cactus_Reader.Entities
{
    public class Userkey
    {
        public string ID { get; set; }

        public string UID { get; set; }

        public string PublicKey { get; set; }

        public string Attestation { get; set; }

        public string DeviceID { get; set; }

        public DateTime LastLogonTime { get; set; }
    }
}
