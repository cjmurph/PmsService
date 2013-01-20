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
        private string logPath = "";

        public PlexMediaServerService()
        {
            InitializeComponent();
            //This is a simple start stop service, no pause and resume.
            this.CanPauseAndContinue = false;

            logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PlexService\\Logs\\");

            this.pms = new PmsMonitor(logPath);

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

        public void WriteToLog(string data)
        {
            if (!System.IO.Directory.Exists(logPath))
            {
                System.IO.Directory.CreateDirectory(logPath);
            }
            //build the filename to create a new file each day
            string fileName = System.IO.Path.Combine(logPath, string.Format("{0:yyyyMMdd}plxLog.txt", DateTime.Now));
            LogWriter.WriteLine(data, fileName);

            //keep three days of logs, that should be enough. May not actually write to the log very often if everything is running well so run this whenever we get the chance
            List<string> files = System.IO.Directory.GetFiles(logPath, "*plxLog.txt", System.IO.SearchOption.TopDirectoryOnly).ToList();
            //get them in order (there is no guarantee according to msdn).
            files = files = files.OrderBy(f => f).ToList();
            while (files.Count > 10)
            {
                System.IO.File.Delete(files[0]);
                files.RemoveAt(0);
            }
            
        }
    }
}
