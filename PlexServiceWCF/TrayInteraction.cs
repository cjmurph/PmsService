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
    /// <summary>
    /// WCF service implementation
    /// </summary>
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

        /// <summary>
        /// Start Plex
        /// </summary>
        public void Start()
        {
            _pms.Start();
        }

        /// <summary>
        /// Stop Plex
        /// </summary>
        public void Stop()
        {
            _pms.Stop();
            WriteToLog("Plex Service Stopped");
        }

        /// <summary>
        /// Restart Plex
        /// </summary>
        public void Restart()
        {
            //stop and restart plex and the auxilliary apps
            _pms.Stop();
            _pms.Start();
        }

        /// <summary>
        /// Write the settings to the server
        /// </summary>
        /// <param name="settings">Json serialised Settings instance</param>
        public void SetSettings(string settings)
        {
            SettingsHandler.Save(Settings.Deserialize(settings));
        }

        /// <summary>
        /// Returns the settings file from the server as a json string
        /// </summary>
        /// <returns></returns>
        public string GetSettings()
        {
            return SettingsHandler.Load().Serialize();
        }

        /// <summary>
        /// Returns the log file as a string
        /// </summary>
        /// <returns></returns>
        public string GetLog()
        {
            return LogWriter.Read();
        }

        /// <summary>
        /// Returns Running or Stopped
        /// </summary>
        /// <returns></returns>
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
