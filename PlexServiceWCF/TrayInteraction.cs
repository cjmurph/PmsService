using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceModel;
using System.Threading.Tasks;
using PlexServiceCommon;
using PlexServiceCommon.Interface;
using Serilog;
using Serilog.Events;

namespace PlexServiceWCF
{
    /// <summary>
    /// WCF service implementation
    /// </summary>
    [ServiceBehavior(ConfigurationName = "PlexServiceWCF:PlexServiceWCF.TrayInteraction", InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class TrayInteraction : ITrayInteraction
    {
        public static readonly string AppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Plex Service\");

        private const int MaxLogMessageLength = 4000;

        private readonly PmsMonitor _pms;

        private readonly List<ITrayCallback> _callbackChannels = new();

        public TrayInteraction() {
            _pms = new PmsMonitor();
            _pms.StateChange += PlexStateChange;
            _pms.PlexStop += PlexStopped;
        }

        private List<ITrayCallback> GetCallbacks()
        {
            lock (_callbackChannels)
            {
                // drop channels of trays that went away
                _callbackChannels.RemoveAll(c => c is not ICommunicationObject { State: CommunicationState.Opened });
                return new List<ITrayCallback>(_callbackChannels);
            }
        }

        private void PlexStopped(object sender, EventArgs e)
        {
            GetCallbacks().ForEach(callback => {
                try
                {
                    callback.OnPlexStopped();
                } catch (Exception ex) {
                    Log.Warning("Exception running callback: " + ex.Message);
                }
            });
        }

        private void PlexStateChange(object sender, EventArgs e)
        {
            var state = _pms.State;
            GetCallbacks().ForEach(callback => {
                try
                {
                    callback.OnPlexStateChange(state);
                } catch (Exception ex) {
                    Log.Warning("Exception on plex state change callback: " + ex.Message);
                }
            });
        }

        /// <summary>
        /// Start Plex
        /// </summary>
        public void Start()
        {
            //do this in another thread to return immediately so we don't hold up the service starting
            Task.Run(() => _pms.Start());
        }

        /// <summary>
        /// Stop Plex
        /// </summary>
        public void Stop()
        {
            Task.Run(() => _pms.Stop());
        }

        /// <summary>
        /// Restart Plex
        /// </summary>
        public void Restart()
        {
            //stop and restart plex and the auxiliary apps
            Task.Run(() => _pms.Restart());
        }

        /// <summary>
        /// Stops plex and everything that belongs to it and waits for it to finish. Used when the service shuts down.
        /// </summary>
        public void Shutdown(TimeSpan timeout)
        {
            _pms.StopAndWait(timeout);
        }

        /// <summary>
        /// Ask the local plex server to update (scan) all libraries.
        /// The request is made from the service so it comes from localhost and the machine hosting plex.
        /// </summary>
        public void UpdateAllLibraries()
        {
            //fire the request off in another thread so the tray isn't kept waiting
            Task.Run(() =>
            {
                var address = "http://localhost:32400/library/sections/all/refresh";
                //include the token if we can find one, localhost is usually trusted without it
                var token = PlexRegistryHelper.ReadUserRegistryValue("PlexOnlineToken");
                if (!string.IsNullOrEmpty(token))
                {
                    address += "?X-Plex-Token=" + token;
                }
                try
                {
                    var request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(address);
                    request.Timeout = 30000;
                    using var response = (System.Net.HttpWebResponse)request.GetResponse();
                    Log.Write(LogEventLevel.Information, "Update of all libraries requested, plex responded: " + response.StatusCode);
                }
                catch (Exception ex)
                {
                    Log.Warning("Exception requesting library update: " + ex.Message);
                }
            });
        }

        /// <summary>
        /// Write the settings to the server
        /// </summary>
        /// <param name="settings">Json serialised Settings instance</param>
        public void SetSettings(Settings settings)
        {
            if (settings == null) return;
            SettingsHandler.Save(settings);
        }

        public void LogMessage(string message, LogEventLevel level=LogEventLevel.Debug) {
            if (string.IsNullOrEmpty(message)) return;
            if (message.Length > MaxLogMessageLength) message = message.Substring(0, MaxLogMessageLength);
            // keep the client from forging extra log lines
            message = message.Replace('\r', ' ').Replace('\n', ' ');
            Log.Write(level, "[Tray] " + message);
        }

        /// <summary>
        /// Returns the log file as a string
        /// </summary>
        /// <returns></returns>
        public string GetLog()
        {
            return LogWriter.Read().GetAwaiter().GetResult();
        }

        public string GetLogPath() {
            return LogWriter.GetLatestLog();
        }

        public string GetPmsDataPath() {
            return PlexDirHelper.GetPlexDataDir();
        }

        /// <summary>
        /// Returns the settings file from the server as a json string
        /// </summary>
        /// <returns></returns>
        public Settings GetSettings()
        {
            return SettingsHandler.Load();
        }

        /// <summary>
        /// Returns Running or Stopped
        /// </summary>
        /// <returns></returns>
        public PlexState GetStatus()
        {
            return _pms.State;
        }

        /// <summary>
        /// Lets the tray know if the calling user is allowed to change anything
        /// </summary>
        public bool CanControl()
        {
            return AdminAuthorizationManager.IsPrivileged(ServiceSecurityContext.Current?.WindowsIdentity);
        }

        /// <summary>
        /// A request from the client for the running status of a specific auxiliary application
        /// </summary>
        /// <param name="name">the name of the auxiliary application to check</param>
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

        public string GetWebLink()
        {
            var address = "http://localhost:32400/web";
            var version = _pms.PlexVersion;
            Log.Information("WebLink requested, plex version is: " + (version?.ToString() ?? "unknown"));

            if (version == null || version <= new Version("1.32.0.0")) return address;

            //try to read the token
            var token = PlexRegistryHelper.ReadUserRegistryValue("PlexOnlineToken");
            if (!string.IsNullOrEmpty(token)) return address;

            Log.Information("Plex online token is empty or cannot be read, checking for claim url...");
            //empty token means the server is unclaimed and we should try and hit the claim url
            var dataDir = PlexDirHelper.GetPlexDataDir();
            if (string.IsNullOrEmpty(dataDir)) return address;

            var claimUrlFile = Path.Combine(dataDir, ".claimURL");
            var setupPlex = Path.Combine(dataDir, "Setup Plex.html");
            if (File.Exists(claimUrlFile))
            {
                var claimUrl = File.ReadAllText(claimUrlFile).Trim();
                if (Uri.TryCreate(claimUrl, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    Log.Information("Claim url found: " + claimUrl);
                    return claimUrl;
                }
            }

            if (File.Exists(setupPlex))
            {
                Log.Information("No claim url found, returning the setup address");
                return setupPlex;
            }

            return address;
        }

        public void Subscribe()
        {
            var channel = OperationContext.Current.GetCallbackChannel<ITrayCallback>();
            lock (_callbackChannels)
            {
                if (!_callbackChannels.Contains(channel)) _callbackChannels.Add(channel);
            }
        }

        public void UnSubscribe()
        {
            var channel = OperationContext.Current.GetCallbackChannel<ITrayCallback>();
            lock (_callbackChannels)
            {
                _callbackChannels.Remove(channel);
            }
        }

        #region ICommunicationObject

        // The contract inherits ICommunicationObject so the client proxy exposes the channel state.
        // None of this is meaningful for the service implementation itself.

        public void Abort() { }

        public void Close() { }

        public void Close(TimeSpan timeout) { }

        public IAsyncResult BeginClose(AsyncCallback callback, object state) => throw new NotSupportedException();

        public IAsyncResult BeginClose(TimeSpan timeout, AsyncCallback callback, object state) => throw new NotSupportedException();

        public void EndClose(IAsyncResult result) => throw new NotSupportedException();

        public void Open() { }

        public void Open(TimeSpan timeout) { }

        public IAsyncResult BeginOpen(AsyncCallback callback, object state) => throw new NotSupportedException();

        public IAsyncResult BeginOpen(TimeSpan timeout, AsyncCallback callback, object state) => throw new NotSupportedException();

        public void EndOpen(IAsyncResult result) => throw new NotSupportedException();

        public CommunicationState State => CommunicationState.Opened;

        public event EventHandler Closed { add { } remove { } }

        public event EventHandler Closing { add { } remove { } }

        public event EventHandler Faulted { add { } remove { } }

        public event EventHandler Opened { add { } remove { } }

        public event EventHandler Opening { add { } remove { } }

        #endregion
    }
}
