using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
        }

        /// <summary>
        /// Start Plex
        /// </summary>
        public void Start()
        {
            //do this in another thread to return immediately so we don't hold up the service starting
            Task.Factory.StartNew(() => _pms.Start());
        }

        /// <summary>
        /// Stop Plex
        /// </summary>
        public void Stop()
        {
            //do this in the calling thread so it only returns upon completion of stop
            _pms.Stop();
        }

        /// <summary>
        /// Restart Plex
        /// </summary>
        public void Restart()
        {
            //stop and restart plex and the auxilliary apps
            Task.Factory.StartNew(() =>
                {
                    _pms.Restart(5000);
                });
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
        public PlexState GetStatus()
        {
            if (_pms != null)
                return _pms.State;
            return PlexState.Stopped;
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
