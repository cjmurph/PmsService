using Newtonsoft.Json;
using System;
using System.Runtime.InteropServices;

namespace PlexServiceCommon
{
    [JsonObject(MemberSerialization=MemberSerialization.OptIn)]
    public class DriveMap
    {
        [DllImport("mpr.dll")] private static extern int WNetAddConnection2A(ref NetworkResource netRes, string password, string username, int flags);
        [DllImport("mpr.dll")] private static extern int WNetCancelConnection2A(string name, int flags, int force);

        [StructLayout(LayoutKind.Sequential)]
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

        [JsonProperty]
        public string ShareName { get; set; }

        [JsonProperty]
        public string DriveLetter { get; set; }

        [JsonConstructor]
        private DriveMap()
        {
            ShareName = string.Empty;
            DriveLetter = string.Empty;
        }

        public DriveMap(string shareName, string driveLetter)
        {
            ShareName = shareName;
            DriveLetter = driveLetter;
        }

        /// <summary>
        /// Map network drive
        /// </summary>
        /// <param name="force">do unmap first</param>
        public void MapDrive(bool force)
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
                    RemoteName = ShareName,
                    LocalName = drive
                };
                //if force, unmap ready for new connection
                if (force)
                {
                    try
                    {
                        UnMapDrive(true);
                    } catch (Exception e){
                        LogWriter.WriteLine("Exception unmapping drive: " + e.Message);
                    }
                }
                //call and return
                var i = WNetAddConnection2A(ref netRes, null, null, 0);

                if (i > 0)
                    throw new System.ComponentModel.Win32Exception(i);
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
                var i = WNetCancelConnection2A(drive, 0, Convert.ToInt32(force));

                if (i > 0)
                    throw new System.ComponentModel.Win32Exception(i);
            }
            else
            {
                throw new Exception("Invalid drive letter: " + DriveLetter);
            }
        }
    }
}
