using System.Security.Cryptography;
using System.Text;

namespace Cactus_Reader.Sources.ToolKits
{
    public class MD5EncryptTool
    {
        private static MD5EncryptTool instance;

        public static MD5EncryptTool Instance
        {
            get
            {
                return instance ?? (instance = new MD5EncryptTool());
            }
        }

        public byte[] GetUserEncryptedPassword(string password)
        {
            MD5 md5 = MD5.Create();
            byte[] inputBytes = Encoding.Default.GetBytes(password);
            return md5.ComputeHash(inputBytes);
        }

        public byte[] GetSystemEncryptedKey()
        {
            MD5 md5 = MD5.Create();
            string systemPassword = "s~7Vzj7Q,_zkgdGZQ#aV6H-Q2F.8+tHbFW7pW9gDKr)AkPXm.wdNtUjj6R:hoph3NgnesxCjiB+GrYcQdx,vx=:bXF.mddFcrF:f";
            byte[] inputBytes = Encoding.Default.GetBytes(systemPassword);
            return md5.ComputeHash(inputBytes);
        }

        public byte[] GetSystemEncryptedVector()
        {
            MD5 md5 = MD5.Create();
            string systemPassword = "N0,dooKTk>qnbxWM>Mk>kFGoW=J^Dh:+,!,EA3pt_=FC0:^Ard+G=nF2H)#oMC]+cDhUssyH^amN%~AZ!J5tm_RLyffpug25Nr4b";
            byte[] inputBytes = Encoding.Default.GetBytes(systemPassword);
            return md5.ComputeHash(inputBytes);
        }
    }
}
