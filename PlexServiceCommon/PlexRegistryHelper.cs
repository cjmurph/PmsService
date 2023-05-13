using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlexServiceCommon
{
    public static class PlexRegistryHelper
    {
        public static string ReadUserRegistryValue(string name)
        {
            var is64Bit = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432"));
            string subKey = @"Software\Plex, Inc.\Plex Media Server";
            try
            {
                using var pmsDataKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, is64Bit ? RegistryView.Registry64 : RegistryView.Registry32).OpenSubKey(subKey);

                if (pmsDataKey is not null)
                {
                    return pmsDataKey.GetValue(name).ToString();
                }
            }
            catch { }
            return string.Empty;
        }
    }
}
