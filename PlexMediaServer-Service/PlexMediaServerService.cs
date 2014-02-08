using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;

namespace PlexMediaServer_Service
{
    /// <summary>
    /// Service that runs an instance of PmsMonitor to maintain an instance of Plex Media Server in session 0
    /// </summary>
    public partial class PlexMediaServerService : ServiceBase
    {
        private PmsMonitor pms;

        internal static string APP_DATA_PATH = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Plex Service\");

        public PlexMediaServerService()
        {
            InitializeComponent();
            //This is a simple start stop service, no pause and resume.
            this.CanPauseAndContinue = false;

            //setup main plex media server monitor
            this.pms = new PmsMonitor();

            this.pms.PlexStatusChange += new PmsMonitor.PlexStatusChangeHandler(pms_PlexStatusChange);
            this.pms.PlexStop += new PmsMonitor.PlexStopHandler(pms_PlexStop);
        }

        /// <summary>
        /// Fires when the monitor stops
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        void pms_PlexStop(object sender, EventArgs data)
        {
            this.Stop();
        }

        /// <summary>
        /// Fires when the status of the monitor changes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        void pms_PlexStatusChange(object sender, StatusChangeEventArgs data)
        {
            WriteToLog(data.Description);
        }

        /// <summary>
        /// Fires when the service is started
        /// </summary>
        /// <param name="args"></param>
        protected override void OnStart(string[] args)
        {
            //We have one minute now, but should be able to do it all pretty safely within that.
            WriteToLog("PlexMediaServerService Started");
            this.pms.Start();

            base.OnStart(args);
        }

        /// <summary>
        /// Fires when the service is stopped
        /// </summary>
        protected override void OnStop()
        {
            this.pms.Stop();
            WriteToLog("PlexMediaServerService Stopped");
            base.OnStop();
        }

        /// <summary>
        /// Write the passed string to the logfile
        /// </summary>
        /// <param name="data"></param>
        public void WriteToLog(string data)
        {
            if (!System.IO.Directory.Exists(APP_DATA_PATH))
            {
                System.IO.Directory.CreateDirectory(APP_DATA_PATH);
            }
            string fileName = System.IO.Path.Combine(APP_DATA_PATH, "plexServiceLog.txt");
            LogWriter.WriteLine(data, fileName);
            
        }
    }
}
