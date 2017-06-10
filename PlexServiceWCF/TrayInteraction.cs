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

        private static readonly List<ITrayCallback> CallbackChannels = new List<ITrayCallback>();

        public TrayInteraction()
        {
            _pms = new PmsMonitor();
            _pms.PlexStatusChange += OnPlexEvent;
            _pms.StateChange += PlexStateChange;
            ///Start plex
            Start();
        }

        private void PlexStateChange(object sender, EventArgs e)
        {
            if (_pms != null)
            {
                CallbackChannels.ForEach(callback =>
                {
                    if (callback != null)
                    {
                        try
                        {
                            callback.OnPlexStateChange(_pms.State);
                        }
                        catch { }
                    }
                });
            }
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

        /// <summary>
        /// A request from the client for the running status of a specific auxilliary application
        /// </summary>
        /// <param name="name">the name of the auxilliary application to check</param>
        /// <returns></returns>
        public bool IsAuxAppRunning(string name)
        {
            return _pms.IsAuxAppRunning(name);
        }

        public void StartAuxApp(string name)
        {
            _pms.StartAuxApp(name);
        }

        public void StopAuxApp(string name)
        {
            _pms.StopAuxApp(name);
        }

        public void Subscribe()
        {
            var channel = OperationContext.Current.GetCallbackChannel<ITrayCallback>();
            if (!CallbackChannels.Contains(channel)) //if CallbackChannels not contain current one.
            {
                CallbackChannels.Add(channel);
            }
        }

        public void UnSubscribe()
        {
            var channel = OperationContext.Current.GetCallbackChannel<ITrayCallback>();
            if (CallbackChannels.Contains(channel)) //if CallbackChannels not contain current one.
            {
                CallbackChannels.Remove(channel);
            }
        }
    }
}
