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
        private string logPath;
        private string appPath;

        public PlexMediaServerService()
        {
            InitializeComponent();
            //This is a simple start stop service, no pause and resume.
            this.CanPauseAndContinue = false;

            this.appPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            this.logPath = System.IO.Path.Combine(appPath, @"Logs\");//Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PlexService\\Logs\\");

            this.pms = new PmsMonitor(this.appPath);

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
        void pms_PlexStatusChange(object sender, PlexRunningStatusChangeEventArgs data)
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
            if (!System.IO.Directory.Exists(logPath))
            {
                System.IO.Directory.CreateDirectory(logPath);
            }
            string fileName = System.IO.Path.Combine(logPath, "plexServiceLog.txt");
            LogWriter.WriteLine(data, fileName);
            
        }
    }
}
