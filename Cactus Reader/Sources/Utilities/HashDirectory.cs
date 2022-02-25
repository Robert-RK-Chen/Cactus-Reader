﻿using System.Security.Cryptography;
using System.Text;

namespace Cactus_Reader.Sources.Utilities
{
    internal class HashDirectory
    {
        public static string GetEncryptedPassword(string password)
        {
            SHA256 mySHA256 = SHA256.Create();
            byte[] passwordArray = Encoding.Default.GetBytes(password);
            byte[] hashValue = mySHA256.ComputeHash(mySHA256.ComputeHash(passwordArray));
            StringBuilder encryptPwd = new StringBuilder();
            foreach (byte ch in hashValue)
            {
                encryptPwd.Append(ch.ToString("X2"));
            }
            return encryptPwd.ToString();
        }
    }
}
