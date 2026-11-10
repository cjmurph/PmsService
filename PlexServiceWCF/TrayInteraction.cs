using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
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

        private static readonly TimeSpan ShareTestTimeout = TimeSpan.FromSeconds(30);

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
            RefreshLibraries(new[] { "all" });
        }

        /// <summary>
        /// Ask the local plex server to update the given libraries
        /// </summary>
        /// <param name="keys">section keys as returned by <see cref="GetLibraries"/></param>
        public void UpdateLibraries(string[] keys)
        {
            // the keys end up in a url, plex only uses numbers for them
            var valid = keys?.Where(k => !string.IsNullOrEmpty(k) && k.All(c => c >= '0' && c <= '9')).Distinct().ToArray();
            if (valid is { Length: > 0 }) RefreshLibraries(valid);
        }

        /// <summary>
        /// Returns the libraries of the local plex server, null if plex can't be asked
        /// </summary>
        public PlexLibrary[] GetLibraries()
        {
            try
            {
                using var response = PlexRequest("/library/sections", 8000);
                using var stream = response.GetResponseStream();
                return XDocument.Load(stream).Root?.Elements("Directory")
                    .Select(d => new PlexLibrary { Key = (string)d.Attribute("key"), Title = (string)d.Attribute("title"), Type = (string)d.Attribute("type") })
                    .OrderBy(l => l.Title, StringComparer.CurrentCultureIgnoreCase)
                    .ToArray();
            }
            catch (Exception ex) when (ex is WebException or XmlException)
            {
                Log.Warning("Unable to read the libraries from plex: " + ex.Message);
                return null;
            }
        }

        private static void RefreshLibraries(string[] keys)
        {
            //fire the requests off in another thread so the tray isn't kept waiting
            Task.Run(() =>
            {
                foreach (var key in keys)
                {
                    try
                    {
                        using var response = PlexRequest($"/library/sections/{key}/refresh", 30000);
                        Log.Write(LogEventLevel.Information, $"Update of library '{key}' requested, plex responded: " + response.StatusCode);
                    }
                    catch (WebException ex)
                    {
                        Log.Warning($"Exception requesting update of library '{key}': " + ex.Message);
                    }
                }
            });
        }

        private static HttpWebResponse PlexRequest(string path, int timeout)
        {
            var request = (HttpWebRequest)WebRequest.Create("http://localhost:32400" + path);
            request.Timeout = timeout;
            //include the token if we can find one, localhost is usually trusted without it
            var token = PlexRegistryHelper.ReadUserRegistryValue("PlexOnlineToken");
            if (!string.IsNullOrEmpty(token)) request.Headers["X-Plex-Token"] = token;
            return (HttpWebResponse)request.GetResponse();
        }

        /// <summary>
        /// Write the settings to the server
        /// </summary>
        /// <param name="settings">Json serialised Settings instance</param>
        public void SetSettings(Settings settings)
        {
            if (settings == null) return;
            DriveMapSecrets.Apply(settings, SettingsHandler.Load());
            SettingsHandler.Save(settings);
        }

        /// <summary>
        /// Drive letters that are taken on this machine. Network drives from our own mappings don't count, they are free to be kept.
        /// </summary>
        public string[] GetUsedDriveLetters()
        {
            var own = SettingsHandler.Load().DriveMaps
                .Where(m => !string.IsNullOrEmpty(m.DriveLetter))
                .Select(m => m.DriveLetter.Substring(0, 1).ToUpperInvariant())
                .ToList();
            return DriveInfo.GetDrives()
                .Select(d => new { Letter = d.Name.Substring(0, 1).ToUpperInvariant(), d.DriveType })
                .Where(d => d.DriveType != DriveType.Network || !own.Contains(d.Letter))
                .Select(d => d.Letter)
                .ToArray();
        }

        /// <summary>
        /// Checks from the service, with the account the mapping would use, that a share can be connected and read
        /// </summary>
        public DriveMapTestResult TestDriveMap(DriveMap map)
        {
            if (string.IsNullOrWhiteSpace(map?.ShareName)) return new DriveMapTestResult(false, "Enter the network share first.");

            string password;
            try
            {
                password = DriveMapSecrets.ForTest(map, SettingsHandler.Load());
            }
            catch (InvalidOperationException ex)
            {
                return new DriveMapTestResult(false, ex.Message);
            }
            if (map.HasUserName && string.IsNullOrEmpty(password)) return new DriveMapTestResult(false, "Enter the password for this account.");

            // an unreachable server can block for a long time, the task finishes and cleans up on its own
            var test = Task.Run(() => map.TestConnection(password));
            var result = test.Wait(ShareTestTimeout)
                ? test.Result
                : new DriveMapTestResult(false, $"The server didn't answer within {ShareTestTimeout.TotalSeconds:0} seconds.");
            Log.Information($"Test of share {map.ShareName}: {result.Message}");
            return result;
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
            var settings = SettingsHandler.Load();
            DriveMapSecrets.Redact(settings, CanControl());
            return settings;
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
