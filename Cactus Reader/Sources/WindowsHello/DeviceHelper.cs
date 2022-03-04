using System;
using Windows.Security.ExchangeActiveSyncProvisioning;

namespace Cactus_Reader.Sources.WindowsHello
{
    internal class DeviceHelper
    {
        public static string GetDeviceId()
        {
            EasClientDeviceInformation deviceInformation = new EasClientDeviceInformation();
            return deviceInformation.Id.ToString("D").ToUpper();
        }
    }
}
