using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.Threading;
using PlexServiceCommon;
using PlexServiceCommon.Interface;

namespace PlexServiceWCF
{
    [ServiceBehavior(ConfigurationName = "PlexServiceWCF:PlexServiceWCF.TrayInteraction", InstanceContextMode = InstanceContextMode.Single)]
    public class TrayInteraction : ITrayInteraction
    {
        public static string APP_DATA_PATH = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Plex Service\");

        private PmsMonitor _pms;

        public TrayInteraction()
        {
            _pms = new PmsMonitor();
            _pms.PlexStatusChange += OnPlexEvent;
            Start();
        }

        public void Start()
        {
            _pms.Start();
        }

        public void Stop()
        {
            _pms.Stop();
            WriteToLog("Plex Service Stopped");
        }

        public void Restart()
        {
            //stop and restart plex and the auxilliary apps
            _pms.Stop();
            _pms.Start();
        }

        public void SetSettings(string settings)
        {
            SettingsHandler.Save(Settings.Deserialize(settings));
        }

        public string GetSettings()
        {
            return SettingsHandler.Load().Serialize();
        }

        public string GetLog()
        {
            return LogWriter.Read();
        }

        public string GetStatus()
        {
            if(_pms != null && _pms.Running)
                return "Running";
            return "Stopped";
        }

        /// <summary>
        /// Write the passed string to the logfile
        /// </summary>
        /// <param name="data"></param>
        public static void WriteToLog(string data)
        {
            try
            {
                LogWriter.WriteLine(data);
            }
            catch (System.IO.IOException ex)
            {
                System.Diagnostics.EventLog.WriteEntry("PlexService", "Log file could not be written to" + Environment.NewLine + ex.Message);
            }
        }

        /// <summary>
        /// Plex status change event handler, forward any status changes to the clients
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnPlexEvent(object sender, StatusChangeEventArgs e)
        {
            WriteToLog(e.Description);
        }
    }
}
