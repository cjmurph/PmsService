using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace PlexServiceCommon
{
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
            public string Comment;
            public string Provider;
        }

        public string ShareName { get; set; }

        public string DriveLetter { get; set; }

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
                NetworkResource netRes = new NetworkResource();
                netRes.Scope = 2;
                netRes.Type = 0x1;
                netRes.DisplayType = 3;
                netRes.Usage = 1;
                netRes.RemoteName = ShareName;
                netRes.LocalName = drive;
                //if force, unmap ready for new connection
                if (force)
                {
                    try
                    {
                        UnMapDrive(true);
                    }
                    catch { }
                }
                //call and return
                int i = WNetAddConnection2A(ref netRes, null, null, 0);

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
                int i = WNetCancelConnection2A(drive, 0, Convert.ToInt32(force));

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
