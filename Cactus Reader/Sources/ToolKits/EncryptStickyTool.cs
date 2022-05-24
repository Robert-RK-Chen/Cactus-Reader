using Cactus_Reader.Entities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace Cactus_Reader.Sources.ToolKits
{
    public class EncryptStickyTool
    {
        private readonly ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        private readonly AESEncryptTool aesEncryptTool = new AESEncryptTool();
        private readonly MD5EncryptTool md5EncryptTool = new MD5EncryptTool();
        private static EncryptStickyTool instance;

        public static EncryptStickyTool Instance
        {
            get
            {
                return instance ?? (instance = new EncryptStickyTool());
            }
        }
        
        public async Task<bool> LockStickyAsync(string stickySerial)
        {
            try
            {
                string UID = localSettings.Values["UID"].ToString();
                StorageFolder stickyFolder = await ApplicationData.Current.LocalFolder.GetFolderAsync(UID);
                stickyFolder = await stickyFolder.GetFolderAsync("Sticky");
                StorageFile stickyFile = await stickyFolder.GetFileAsync(stickySerial + ".ctsnote");

                string stickyText = aesEncryptTool.DecryptStringFromBytesAes(File.ReadAllText(stickyFile.Path), md5EncryptTool.GetSystemEncryptedKey(), md5EncryptTool.GetSystemEncryptedVector());

                Sticky sticky = JsonConvert.DeserializeObject<Sticky>(stickyText);
                sticky.IsLock = true;

                string encryptText = aesEncryptTool.EncryptStringToBytesAes(JsonConvert.SerializeObject(sticky), md5EncryptTool.GetSystemEncryptedKey(), md5EncryptTool.GetSystemEncryptedVector());
                File.WriteAllText(stickyFile.Path, encryptText);
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
        }

        public async Task<bool> UnlockStickyAsync(string stickySerial)
        {
            try
            {
                string UID = localSettings.Values["UID"].ToString();
                StorageFolder stickyFolder = await ApplicationData.Current.LocalFolder.GetFolderAsync(UID);
                stickyFolder = await stickyFolder.GetFolderAsync("Sticky");
                StorageFile stickyFile = await stickyFolder.GetFileAsync(stickySerial + ".ctsnote");

                string stickyText = aesEncryptTool.DecryptStringFromBytesAes(File.ReadAllText(stickyFile.Path), md5EncryptTool.GetSystemEncryptedKey(), md5EncryptTool.GetSystemEncryptedVector());

                Sticky sticky = JsonConvert.DeserializeObject<Sticky>(stickyText);
                sticky.IsLock = false;

                string encryptText = aesEncryptTool.EncryptStringToBytesAes(JsonConvert.SerializeObject(sticky), md5EncryptTool.GetSystemEncryptedKey(), md5EncryptTool.GetSystemEncryptedVector());
                File.WriteAllText(stickyFile.Path, encryptText);
                return true;
            }
            catch(FileNotFoundException)
            {
                return false;
            }
        }

        public async Task<bool> IsStickyLockedAsync(string stickySerial)
        {
            try
            {
                string UID = localSettings.Values["UID"].ToString();
                StorageFolder stickyFolder = await ApplicationData.Current.LocalFolder.GetFolderAsync(UID);
                stickyFolder = await stickyFolder.GetFolderAsync("Sticky");
                StorageFile stickyFile = await stickyFolder.GetFileAsync(stickySerial + ".ctsnote");

                string stickyText = aesEncryptTool.DecryptStringFromBytesAes(File.ReadAllText(stickyFile.Path), md5EncryptTool.GetSystemEncryptedKey(), md5EncryptTool.GetSystemEncryptedVector());

                Sticky sticky = JsonConvert.DeserializeObject<Sticky>(stickyText);
                return sticky.IsLock;
            }
            catch(FileNotFoundException)
            {
                return false;
            }
        }
        public async void UnlockAllSticky()
        {
            try
            {
                string UID = localSettings.Values["UID"].ToString();
                StorageFolder stickyFolder = await ApplicationData.Current.LocalFolder.GetFolderAsync(UID);
                stickyFolder = await stickyFolder.GetFolderAsync("Sticky");
                IReadOnlyList<StorageFile> fileList = await stickyFolder.GetFilesAsync();
                
                foreach(StorageFile stickyFile in fileList)
                {
                    string stickyText = aesEncryptTool.DecryptStringFromBytesAes(File.ReadAllText(stickyFile.Path), md5EncryptTool.GetSystemEncryptedKey(), md5EncryptTool.GetSystemEncryptedVector());

                    Sticky sticky = JsonConvert.DeserializeObject<Sticky>(stickyText);
                    sticky.IsLock = false;

                    string encryptText = aesEncryptTool.EncryptStringToBytesAes(JsonConvert.SerializeObject(sticky), md5EncryptTool.GetSystemEncryptedKey(), md5EncryptTool.GetSystemEncryptedVector());
                    File.WriteAllText(stickyFile.Path, encryptText);
                }
            }
            catch (FileNotFoundException)
            {
            }
        }
    }
}
