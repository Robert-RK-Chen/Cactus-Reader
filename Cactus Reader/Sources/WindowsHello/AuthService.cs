using Cactus_Reader.Entities;
using Cactus_Reader.Sources.ToolKits;
using System;
using System.Collections.Generic;
using System.Text;
using Windows.Security.Credentials;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;

namespace Cactus_Reader.Sources.WindowsHello
{
    public class AuthService
    {
        private static AuthService instance;
        static readonly IFreeSql freeSql = IFreeSqlService.Instance;

        public static AuthService Instance
        {
            get { return instance ?? (instance = new AuthService()); }
        }

        private AuthService()
        { }

        public string GetUID(string username)
        {
            User currentUser = freeSql.Select<User>().Where(user => user.Name == username).ToOne();
            return currentUser == null ? "" : currentUser.UID;
        }

        public User GetUser(string UID)
        {
            return freeSql.Select<User>().Where(user => user.UID == UID).ToOne();
        }

        public List<User> GetUsersForDevice(string DeviceID)
        {
            return freeSql.Select<User>().LeftJoin("SELECT `user`.UID,`user`.Email,`user`.`Name`, `user`.Mobile, `user`.`Password`, `user`.RegistDate FROM userkey LEFT JOIN `user` ON `user`.UID = userkey.UID WHERE deviceid = ?deviceID", new { deviceID = DeviceID }).ToList();
        }

        public bool PassportRemoveUser(string UID)
        {
            return freeSql.Delete<Userkey>().Where(userkey => userkey.UID == UID).ExecuteAffrows() == 0;
        }

        public bool PassportRemoveDevice(string UID, string DeviceID)
        {
            return freeSql.Delete<Userkey>().Where(userkey => userkey.UID == UID && userkey.DeviceID == DeviceID).ExecuteAffrows() == 0;
        }

        public void PassportUpdateDetails(string UID, string DeviceID, byte[] publicKey,
            KeyCredentialAttestationResult keyAttestationResult)
        {
            StringBuilder encryptPwd = new StringBuilder();
            Userkey currentUserkey = freeSql.Select<Userkey>().Where(userkey => userkey.UID == UID && userkey.DeviceID == DeviceID).ToOne();
            foreach (byte ch in publicKey)
            {
                encryptPwd.Append(ch);
            }

            if (currentUserkey == null)
            {
                currentUserkey = new Userkey
                {
                    ID = Guid.NewGuid().ToString("D").ToUpper()
                };
            }
            currentUserkey.UID = UID;
            currentUserkey.DeviceID = DeviceID;
            currentUserkey.PublicKey = encryptPwd.ToString();
            currentUserkey.Attestation = keyAttestationResult.ToString();
            currentUserkey.LastLogonTime = DateTime.Now;
            freeSql.InsertOrUpdate<Userkey>().SetSource(currentUserkey).ExecuteAffrows();
        }

        public bool ValidateCredentials(string username, string password)
        {
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                string UID = GetUID(username);
                if (UID != string.Empty)
                {
                    User user = GetUser(UID);
                    if (user != null)
                    {
                        if (string.Equals(password, user.Password))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public IBuffer PassportRequestChallenge()
        {
            return CryptographicBuffer.ConvertStringToBinary("ServerChallenge", BinaryStringEncoding.Utf8);
        }

        public bool SendServerSignedChallenge(string UID, string DeviceID, byte[] signedChallenge)
        {
            byte[] userPublicKey = System.Text.Encoding.Default.GetBytes(freeSql.Select<Userkey>().Where(userkey => userkey.UID == UID && userkey.DeviceID == DeviceID).ToOne().PublicKey);
            return true;
        }
    }
}
