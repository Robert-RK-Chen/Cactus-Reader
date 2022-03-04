using System;
using Windows.Security.Credentials;

namespace Cactus_Reader.Sources.WindowsHello
{
    public class PassportDevice
    {
        public Guid DeviceId { get; set; }

        public byte[] PublicKey { get; set; }

        public KeyCredentialAttestationResult KeyAttestationResult { get; set; }
    }
}
