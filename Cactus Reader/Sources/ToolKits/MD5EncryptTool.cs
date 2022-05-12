using System.Security.Cryptography;
using System.Text;

namespace Cactus_Reader.Sources.ToolKits
{
    public static class MD5EncryptTool
    {
        public static byte[] GetEncryptedPassword(string password)
        {
            MD5 md5 = MD5.Create();
            byte[] inputBytes = Encoding.Default.GetBytes(password);
            return md5.ComputeHash(inputBytes);
        }
    }
}
