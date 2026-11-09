using Microsoft.Win32.SafeHandles;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Serilog;

namespace PlexServiceCommon
{
    [JsonObject(MemberSerialization=MemberSerialization.OptIn)]
    public class DriveMap
    {
        private const int LogonNewCredentials = 9;
        private const int ProviderWinNt50 = 3;

        [DllImport("mpr.dll", CharSet = CharSet.Unicode)] private static extern int WNetAddConnection2W(ref NetworkResource netRes, string password, string username, int flags);
        [DllImport("mpr.dll", CharSet = CharSet.Unicode)] private static extern int WNetCancelConnection2W(string name, int flags, int force);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool LogonUserW(string username, string domain, string password, int logonType, int logonProvider, out SafeAccessTokenHandle token);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NetworkResource
        {
            public int Scope;
            public int Type;
            public int DisplayType;
            public int Usage;
            public string LocalName;
            public string RemoteName;
            private readonly string Comment;
            private readonly string Provider;
        }

        /// <summary>
        /// Identifies the mapping so a saved password stays attached to it when other fields change
        /// </summary>
        [JsonProperty]
        public string Id { get; set; }

        [JsonProperty]
        public string ShareName { get; set; }

        [JsonProperty]
        public string DriveLetter { get; set; }

        /// <summary>
        /// Account used to connect, the account of the service is used when empty
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string UserName { get; set; }

        /// <summary>
        /// The encrypted password in the settings file. Never leaves the service.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ProtectedPassword { get; set; }

        /// <summary>
        /// A newly entered password on its way from the tray to the service, never written to disk
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Tells the tray that the service has a saved password
        /// </summary>
        public bool HasPassword { get; set; }

        [JsonConstructor]
        public DriveMap()
        {
            ShareName = string.Empty;
            DriveLetter = string.Empty;
        }

        public DriveMap(string shareName, string driveLetter)
        {
            ShareName = shareName;
            DriveLetter = driveLetter;
        }

        public bool HasUserName => !string.IsNullOrWhiteSpace(UserName);

        private string RemoteName => (ShareName ?? string.Empty).TrimEnd('\\');

        /// <summary>
        /// Map network drive
        /// </summary>
        /// <param name="force">do unmap first</param>
        /// <param name="password">password for <see cref="UserName"/></param>
        public void MapDrive(bool force, string password = null)
        {
            if (DriveLetter.Length > 0)
            {
                var drive = DriveLetter.Substring(0,1) + ":";

                //create struct data
                var netRes = new NetworkResource {
                    Scope = 2,
                    Type = 0x1,
                    DisplayType = 3,
                    Usage = 1,
                    RemoteName = RemoteName,
                    LocalName = drive
                };
                //if force, unmap ready for new connection
                if (force)
                {
                    try
                    {
                        UnMapDrive(true);
                    } catch (Exception e){
                        Log.Warning("Exception unmapping drive: " + e.Message);
                    }
                }
                //call and return
                var i = HasUserName
                    ? WNetAddConnection2W(ref netRes, password, UserName, 0)
                    : WNetAddConnection2W(ref netRes, null, null, 0);

                if (i > 0)
                    throw new Win32Exception(i, DescribeError(i));
            }
            else
            {
                throw new Exception("Invalid drive letter: " + DriveLetter);
            }
        }

        /// <summary>
        /// Unmap network drive
        /// </summary>
        /// <param name="force">Specifies whether the disconnection should occur if there are open files or jobs on the connection. If this parameter is FALSE, the function fails if there are open files or jobs.</param>
        public void UnMapDrive(bool force)
        {
            if (DriveLetter.Length > 0)
            {
                var drive = DriveLetter.Substring(0, 1) + ":";

                //call unmap and return
                var i = WNetCancelConnection2W(drive, 0, Convert.ToInt32(force));

                if (i > 0)
                    throw new System.ComponentModel.Win32Exception(i);
            }
            else
            {
                throw new Exception("Invalid drive letter: " + DriveLetter);
            }
        }

        /// <summary>
        /// Connects to the share without assigning a drive letter and reads it, for the account this mapping would use.
        /// </summary>
        public DriveMapTestResult TestConnection(string password)
        {
            try
            {
                var share = RemoteName;
                if (!HasUserName) return Probe(share, null, null, WindowsIdentity.GetCurrent().Name);

                // Windows reuses an open connection to a server, so a wrong password could look fine if the
                // service is already connected. A new logon session has no connections yet. The account given
                // here is never used, the connection below brings its own.
                if (!LogonUserW("PlexService", "PlexService", "-", LogonNewCredentials, ProviderWinNt50, out var token))
                {
                    return new DriveMapTestResult(false, "Unable to start the test: " + new Win32Exception(Marshal.GetLastWin32Error()).Message);
                }

                using (token)
                {
                    return WindowsIdentity.RunImpersonated(token, () => Probe(share, password, UserName, UserName));
                }
            }
            catch (Exception ex)
            {
                return new DriveMapTestResult(false, "Unable to test the share: " + ex.Message);
            }
        }

        private static DriveMapTestResult Probe(string share, string password, string userName, string account)
        {
            var netRes = new NetworkResource { Type = 0x1, RemoteName = share };
            var error = WNetAddConnection2W(ref netRes, password, userName, 0);
            if (error != 0) return new DriveMapTestResult(false, DescribeError(error));

            try
            {
                // connecting isn't enough, the share has to be readable as well
                Directory.EnumerateFileSystemEntries(share).FirstOrDefault();
                return new DriveMapTestResult(true, $"Connected as {account}, the share can be read.");
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                return new DriveMapTestResult(false, $"Connected as {account}, but the share can't be read: {ex.Message}");
            }
            finally
            {
                // with a share name this only removes connections without a drive letter
                WNetCancelConnection2W(share, 0, 1);
            }
        }

        private static string DescribeError(int error)
        {
            switch (error)
            {
                case 5: return "Access denied, this account isn't allowed to use the share.";
                case 53:
                case 64:
                case 1203:
                case 1231:
                case 1232: return "The server can't be reached. Check the name and that it is turned on.";
                case 67: return "The share wasn't found. Check the name of the server and the share.";
                case 86:
                case 1326: return "The user name or password is incorrect.";
                case 1219: return "Windows is already connected to this server with a different account, and it only allows one account per server.";
                case 1327: return "The account isn't allowed to sign in, for example because it has no password.";
                case 1330: return "The password of this account has expired.";
                case 1331: return "This account is disabled.";
                case 1909: return "This account is locked.";
                default: return new Win32Exception(error).Message;
            }
        }
    }
}
