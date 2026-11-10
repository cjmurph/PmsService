using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PlexServiceCommon;

namespace PlexServiceWCF
{
    /// <summary>
    /// Passwords of drive mappings are encrypted for the account of the service and never handed back to a client.
    /// </summary>
    internal static class DriveMapSecrets
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("PlexService.DriveMap");

        /// <summary>
        /// Prepares settings coming from a client for saving. A mapping without a new password keeps the saved one.
        /// </summary>
        internal static void Apply(Settings incoming, Settings stored)
        {
            foreach (var map in incoming.DriveMaps?.Where(m => m != null) ?? Enumerable.Empty<DriveMap>())
            {
                var entered = map.Password;
                map.Password = null;
                map.HasPassword = false;

                if (!map.HasUserName)
                {
                    map.UserName = null;
                    map.ProtectedPassword = null;
                }
                else
                {
                    map.ProtectedPassword = string.IsNullOrEmpty(entered) ? Find(stored, map)?.ProtectedPassword : Protect(entered);
                }
            }
        }

        /// <summary>
        /// Removes everything secret before settings are sent to a client
        /// </summary>
        internal static void Redact(Settings settings, bool includeAccounts)
        {
            foreach (var map in settings.DriveMaps)
            {
                map.HasPassword = includeAccounts && !string.IsNullOrEmpty(map.ProtectedPassword);
                map.ProtectedPassword = null;
                map.Password = null;
                if (!includeAccounts) map.UserName = null;
            }
        }

        /// <summary>
        /// The password to test a mapping with, either just entered or saved earlier
        /// </summary>
        internal static string ForTest(DriveMap map, Settings stored)
        {
            if (!string.IsNullOrEmpty(map.Password)) return map.Password;
            return Reveal(Find(stored, map));
        }

        /// <summary>
        /// Decrypts the saved password of a mapping, null if it has none
        /// </summary>
        internal static string Reveal(DriveMap map)
        {
            if (string.IsNullOrEmpty(map?.ProtectedPassword)) return null;
            try
            {
                var data = ProtectedData.Unprotect(Convert.FromBase64String(map.ProtectedPassword), Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(data);
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException)
            {
                // happens when the service runs under another account now or its password was reset
                throw new InvalidOperationException("The saved password can't be read anymore, please enter it again.", ex);
            }
        }

        private static DriveMap Find(Settings stored, DriveMap map)
        {
            return string.IsNullOrEmpty(map.Id) ? null : stored.DriveMaps.FirstOrDefault(s => s.Id == map.Id);
        }

        private static string Protect(string password)
        {
            return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(password), Entropy, DataProtectionScope.CurrentUser));
        }
    }
}
