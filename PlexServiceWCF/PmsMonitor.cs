#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Threading;
using PlexServiceCommon;
using Serilog;
using System.Runtime.InteropServices;
using System.Runtime;
using System.Threading.Tasks;
using System.ComponentModel;

namespace PlexServiceWCF
{
    /// <summary>
    /// Class for monitoring the status of Plex Media Server
    /// </summary>
    internal class PmsMonitor
    {
        #region static strings

        //Process name
        private static readonly string _plexName = "Plex Media Server";
        //Updater name
        private static readonly string _plexUpdaterName = "Plex Update Service Launcher";
        //List of processes spawned by plex that we need to get rid of
        private static readonly string[] SupportingProcesses =
        {
            "Plex DLNA Server",
            "PlexScriptHost",
            "PlexTranscoder",
            "Plex Transcoder",
            "PlexNewTranscoder",
            "Plex Media Scanner",
            "PlexRelay",
            "Plex Relay",
            "EasyAudioEncoder",
            "Plex Tuner Service",
            "Plex Media Fingerprinter"
        };

        #endregion

        #region Private variables

        /// <summary>
        /// The name of the plex media server executable
        /// </summary>
        private string _executableFileName = string.Empty;

        /// <summary>
        /// Plex process
        /// </summary>
        private Process? _plex;

        /// <summary>
        /// Plex Updater Launcher process
        /// </summary>
        private Process? _plexUpdater;

        /// <summary>
        /// Flag to determine if PMS is updating itself.
        /// </summary>
        private bool _updating;

        private readonly List<AuxiliaryApplicationMonitor> _auxAppMonitors;

        private Settings _settings;

        private bool _restartRequested = false;

        #endregion

        #region Properties

        private PlexState _state = PlexState.Stopped;

        public PlexState State
        {
            get => _state;
            private set {
                if (_state == value) {
                    return;
                }

                _state = value;
                OnStateChange();
            }
        }

        private Version? _plexVersion;

        public Version? PlexVersion
        {
            get => _plexVersion;
            set
            {
                if (_plexVersion == value) return;
                _plexVersion = value;
            }
        }


        #endregion

        #region Constructor

        internal PmsMonitor()
        {
            State = PlexState.Stopped;
            _auxAppMonitors = new List<AuxiliaryApplicationMonitor>();
            _settings = SettingsHandler.Load();
            _settings.AuxiliaryApplications.ForEach(x => _auxAppMonitors.Add(new AuxiliaryApplicationMonitor(x)));
        }
        #endregion

        #region PurgeAutoStart

        /// <summary>
        /// This method will look for and remove the "run at startup" registry key for plex media server.
        /// </summary>
        /// <returns></returns>
        private void PurgeAutoStartRegistryEntry()
        {
            const string keyName = @"Software\Microsoft\Windows\CurrentVersion\Run";
            using var key = Registry.CurrentUser.OpenSubKey(keyName, true);
            if (key?.GetValue("Plex Media Server") == null) {
                return;
            }

            try
            {
                key.DeleteValue("Plex Media Server");
                Log.Information("Successfully removed auto start entry from registry");
            }
            catch(Exception ex)
            {
                Log.Warning($"Unable to remove auto start registry value. Error: {ex.Message}");
            }
        }

        #endregion

        #region DisableFirstRun

        /// <summary>
        /// This method will set the "FirstRun" registry key to 0 to prevent PMS from spawning the default browser.
        /// </summary>
        /// <returns></returns>
        private void DisableFirstRun() {
            RegistryKey? pmsDataKey = null;
            const string keyName = @"Software\Plex, Inc.\Plex Media Server";
            var is64Bit = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432"));

            var architecture = is64Bit ? RegistryView.Registry64 : RegistryView.Registry32;

            try {
                pmsDataKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, architecture).OpenSubKey(keyName, true);
                if (pmsDataKey != null) {
                    var firstRun = (int) pmsDataKey.GetValue("FirstRun");
                    Log.Debug("First run is: " + firstRun);
                    if (firstRun == 0) return;
                }
            } catch (Exception e) {
                Log.Warning("Exception getting pms key: " + e.Message);
            }

            // CreateSubKey just in case it isn't already there for some reason.
            // The installer adds values under here during install, but this can't hurt.
            if (pmsDataKey == null) {
                using var key = Registry.CurrentUser.CreateSubKey(keyName, RegistryKeyPermissionCheck.ReadWriteSubTree);
                if (key == null) {
                    return;
                }
                key.CreateSubKey("FirstRun");
                pmsDataKey = key;
            }
            
            try
            {
                pmsDataKey.SetValue("FirstRun", 0, RegistryValueKind.DWord);
                Log.Information("Successfully set the 'FirstRun' registry key to 0");
            }
            catch(Exception ex)
            {
                Log.Information($"Unable to set the 'FirstRun' registry key to 0. Error: {ex.Message}");
            }
        }

        #endregion

        #region Start

        /// <summary>
        /// Start monitoring plex
        /// </summary>
        internal void Start()
        {
            //clear any restart request flag
            _restartRequested = false;
            //reload the settings just in case
            _settings = SettingsHandler.Load();
            //Find the plex executable
            _executableFileName = GetPlexExecutable();
            if (string.IsNullOrEmpty(_executableFileName))
            {
                Log.Information("Plex Media Server does not appear to be installed!");
                OnPlexStop(EventArgs.Empty);
                State = PlexState.Stopped;
            }
            else
            {
                //make sure this doesn't get stuck for any reason.
                _updating = false;
                Log.Information("Plex executable found at " + _executableFileName);
                
                //map network drives
                var drivesMapped = true;
                if (_settings.DriveMaps.Count > 0) {
                    Log.Information("Mapping Network Drives");

                    drivesMapped = _settings.DriveMaps.All(toMap => TryMap(toMap, _settings));
                }

                if (!drivesMapped && !_settings.StartPlexOnMountFail) 
                {
                    Log.Warning("One or more drive mappings failed and settings are configured to *not* start Plex on mount failure. Please check your drives and try again.");
                } 
                else 
                {
                    StartPlex();
                }
                
                //stop any running aux apps
                _auxAppMonitors.ForEach(a => a.Stop());
                _auxAppMonitors.Clear();
                _settings.AuxiliaryApplications.ForEach(x => _auxAppMonitors.Add(new AuxiliaryApplicationMonitor(x)));
                //hook up the state change event for all the applications
                _auxAppMonitors.AsParallel().ForAll(x => x.Start());
            }
        }

        private static bool TryMap(DriveMap map, Settings settings)
        {
            var count = settings.AutoRemount ? settings.AutoRemountCount : 1;

            while (count > 0)
            {
                try
                {
                    map.MapDrive(true);
                    Log.Information($"Map share {map.ShareName} to letter '{map.DriveLetter}' successful");
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Information($"Unable to map share {map.ShareName} to letter '{map.DriveLetter}': {ex.Message}, {count - 1} more attempts remaining.");
                }
                // Wait 5s
                Thread.Sleep(settings.AutoRemountDelay * 1000);
                count--;
            }

            return false;
        }

        #endregion

        #region Stop

        /// <summary>
        /// Stop the monitor and kill the processes
        /// </summary>
        internal void Stop()
        {
            State = PlexState.Stopping;
            EndPlex();
        }

        #endregion

        #region Restart

        /// <summary>
        /// Restart plex, wait for the specified delay between stop and start
        /// </summary>
        /// <param name="delay">The amount of time in ms to wait before starting after stop</param>
        internal void Restart(int delay)
        {
            Log.Information("Restarting Plex...");
            _restartRequested = true;
            Stop();
            //var autoEvent = new AutoResetEvent(false);
            //var t = new Timer(_ => { Start(); autoEvent.Set(); }, null, delay, Timeout.Infinite);
            //autoEvent.WaitOne();
            //t.Dispose();
        }

        #endregion

        #region Process handling

        #region Exit events

        private async void Updater_Exited(object sender, EventArgs e)
        {

            if (_plexUpdater != null)
            {
                _plexUpdater.Dispose();
                _plexUpdater = null;
            }

            _updating = false;
            Log.Information("PMS update is complete, killing and restarting process.");
            Start();
        }

        /// <summary>
        /// This event fires when the plex process we have a reference to exits
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Plex_Exited(object sender, EventArgs e)
        {
            Log.Information("Plex Media Server has stopped!");
            //unsubscribe
            if (_plex != null) _plex.Exited -= Plex_Exited;

            if (_plex != null)
            {
                _plex.Dispose();
                _plex = null;
            }

            if (_updating)
            {
                //State = PlexState.Stopped;
                Log.Information("Plex is updating, waiting for finish before re-starting.");
                return;
            }

            _plexUpdater = Process.GetProcessesByName(_plexUpdaterName).FirstOrDefault();
            if (_plexUpdater != null)
            {
                Log.Debug("Updater Detected");
                State = PlexState.Updating;
                Log.Information("Plex update started.");
                _updating = true;
                _plexUpdater.EnableRaisingEvents = true;
                _plexUpdater.Exited += Updater_Exited;
                return;
            }

            //kill the supporting processes.
            KillSupportingProcesses();

            //restart as required
            _settings = SettingsHandler.Load();
            if ((State != PlexState.Stopping && _settings.AutoRestart) || _restartRequested)
            {
                
                Log.Information($"Waiting {_settings.RestartDelay} seconds before re-starting the Plex process.");
                State = PlexState.Pending;
                var autoEvent = new AutoResetEvent(false);
                var t = new Timer(_ => { Start(); autoEvent.Set(); }, null, _settings.RestartDelay * 1000, Timeout.Infinite);
                autoEvent.WaitOne();
                t.Dispose();
            }
            else
            {
                //set the status
                State = PlexState.Stopped;
            }
        }

        #endregion

        #region Start methods

        /// <summary>
        /// Start a new/get a handle on existing Plex process
        /// </summary>
        private void StartPlex()
        {
            State = PlexState.Pending;
            //always try to get rid of the plex auto start registry entry
            PurgeAutoStartRegistryEntry();
            // make sure we don't spawn a browser
            Log.Debug("Disabling first run.");
            DisableFirstRun(); 
            if (_plex == null)
            {
                Log.Debug("No plex defined, checking for running process.");
                //see if its running already
                _plex = Process.GetProcessesByName(_plexName).FirstOrDefault();
                if (_plex != null) {
                    Log.Information("Killing existing Plex service.");
                    _plex.Kill();
                    KillSupportingProcesses();
                    _plex = null;
                }
                
                if (_plex == null)
                {
                    Log.Information("Attempting to start Plex.");
                    //plex process
                    _plex = new Process();
                    var plexStartInfo = new ProcessStartInfo(_executableFileName) {
                        WorkingDirectory = Path.GetDirectoryName(_executableFileName) ?? string.Empty,
                        UseShellExecute = false
                    };
                    //check version to see if we can use the startup argument
                    var plexVersion = FileVersionInfo.GetVersionInfo(_executableFileName).FileVersion;
                    var v = new Version(plexVersion);
                    PlexVersion = v;
                    var minimumVersion = new Version("0.9.8.12");
                    if (v.CompareTo(minimumVersion) == -1)
                    {
                        Log.Information($"Plex Media Server version is {plexVersion}. Cannot use startup argument.");
                    }
                    else
                    {
                        Log.Information($"Plex Media Server version is {plexVersion}. Can use startup argument.");
                        plexStartInfo.Arguments = "-noninteractive";
                    }
                    _plex.StartInfo = plexStartInfo;
                    _plex.EnableRaisingEvents = true;
                    _plex.Exited += Plex_Exited;
                    try
                    {
                        if (_plex.Start()) {
                            State = PlexState.Running;
                            Log.Information("Plex Media Server Started.");    
                        }
                    }
                    catch(Exception ex)
                    {
                        Log.Warning("Plex Media Server failed to start. " + ex.Message);
                    }
                }
            }
            //set the state back to stopped if we didn't achieve a running state
            if (State != PlexState.Running)
                State = PlexState.Stopped;
        }

        #endregion

        #region End methods

        /// <summary>
        /// Kill the plex process
        /// </summary>
        private void EndPlex()
        {
            if (_plex != null)
            {
                Log.Information("Killing Plex.");
                try
                {
                    _plex.Kill();
                } catch (Exception e) {
                    Log.Warning("Exception killing Plex: " + e.Message);
                }
            }
            //kill each auxiliary process
            _auxAppMonitors.ForEach(appMonitor =>
            {
                appMonitor.Stop();
            });

            OnPlexStop(EventArgs.Empty);
        }

        /// <summary>
        /// Kill all processes with the specified names
        /// </summary>
        private static void KillSupportingProcesses()
        {
            Log.Information("Killing supporting processes.");
            foreach (var name in SupportingProcesses)
            {
                KillSupportingProcess(name);
            }
        }

        /// <summary>
        /// Kill all instances of the specified process.
        /// </summary>
        /// <param name="name">The name of the process to kill</param>
        private static void KillSupportingProcess(string name)
        {
            //see if its running
            Log.Information("Looking for process: " + name);
            var supportProcesses = Process.GetProcessesByName(name);
            Log.Information(supportProcesses.Length + " instances of " + name + " found.");
            if (supportProcesses.Length <= 0) {
                return;
            }

            foreach (var supportProcess in supportProcesses)
            {
                Log.Information($"Stopping {name} with PID of {supportProcess.Id}.");
                try
                {
                    supportProcess.Kill();
                    Log.Information(name + " with PID stopped");
                }
                catch
                {
                    Log.Warning("Unable to stop process " + supportProcess.Id);
                }
                finally
                {
                    supportProcess.Dispose();
                }
            }
        }

        #endregion

        #endregion

        #region Aux app interaction methods

        public bool IsAuxAppRunning(string name)
        {
            var auxApp = _auxAppMonitors.FirstOrDefault(a => a.Name == name);
            return auxApp is { Running: true };
        }

        public void StartAuxApp(string name)
        {
            var auxApp = _auxAppMonitors.FirstOrDefault(a => a.Name == name);
            if (auxApp is { Running: false }) auxApp.Start();
        }

        public void StopAuxApp(string name)
        {
            var auxApp = _auxAppMonitors.FirstOrDefault(a => a.Name == name);
            if (auxApp is { Running: true }) auxApp.Stop();
        }

        #endregion

        #region File Methods

        /// <summary>
        /// Returns the full path and filename of the plex media server executable
        /// </summary>
        /// <returns></returns>
        private string GetPlexExecutable()
        {
            var result = string.Empty;
            //first see if its defined in the settings

            if(!string.IsNullOrEmpty(_settings.UserDefinedInstallLocation) && File.Exists(_settings.UserDefinedInstallLocation))
            {
                result = _settings.UserDefinedInstallLocation;
                Log.Information($"Plex executable using user specified value in settings");
            }

            //next we will do a dirty check for a text file with the executable path in our log folder.
            //this is here to help anyone having issues and let them specify it manually themselves.
            if(string.IsNullOrEmpty(result) && !string.IsNullOrEmpty(TrayInteraction.AppDataPath))
            {
                var location = Path.Combine(TrayInteraction.AppDataPath, "location.txt");
                if (File.Exists(location))
                {
                    string userSpecified;
                    using (var sr = new StreamReader(location))
                    {
                        userSpecified = sr.ReadLine() ?? string.Empty;
                    }
                    if (File.Exists(userSpecified))
                    {
                        result = userSpecified;
                        Log.Information($"Plex executable using user specified value in location.txt");
                    }
                }
            }

            //if theres nothing there go for the easy defaults
            if (string.IsNullOrEmpty(result)) {
                //plex doesn't put this nice stuff in the registry so we need to go hunting for it ourselves
                //this method is crap. I dont like having to iterate through directories looking to see if a file exists or not.
                //start by looking in the program files directory, even if we are on 64bit windows, plex may be 64bit one day... maybe

                var possibleLocations = new List<string> {
                    //some hard coded attempts, this is nice and fast and should hit 90% of the time... even if it is ugly
                    @"C:\Program Files\Plex\Plex Media Server\Plex Media Server.exe",
                    @"C:\Program Files (x86)\Plex\Plex Media Server\Plex Media Server.exe",
                    //special folder
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Plex\Plex Media Server\Plex Media Server.exe")
                };


                foreach (var location in possibleLocations.Where(File.Exists)) 
                {
                    result = location;
                    Log.Information($"Plex executable found using hard coded lookup");
                    break;
                }
            }

            //work out the os type (32 or 64) and set the registry view to suit. this is only a reliable check when this project is compiled to x86.
            var is64Bit = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432"));

            //the plex registry key in software holds a key called installFolder, check for that in both 32 and 64 bit registry views
            if (string.IsNullOrEmpty(result))
            {
                using var softwareKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, is64Bit ? RegistryView.Registry64 : RegistryView.Registry32).OpenSubKey(@"SOFTWARE\Plex, Inc.\Plex Media Server");
                if (softwareKey != null)
                {
                    if (softwareKey != null)
                    {
                        var names = softwareKey.GetValueNames();
                        if (names.Contains("InstallFolder"))
                        {
                            var possible = softwareKey.GetValue("InstallFolder").ToString();
                            if (File.Exists(possible))
                            {
                                result = possible;
                                Log.Information($"Plex executable found in plex registry key");
                            }
                        }
                    }
                }

            }

            //so if we still can't find it, we need to do a more exhaustive check through the installer locations in the registry
            if (string.IsNullOrEmpty(result))
            {
                //let's have a flag to break out of the loops below for faster execution, because this is nasty.
                var resultFound = false;

                using var userDataKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, is64Bit ? RegistryView.Registry64 : RegistryView.Registry32).OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Installer\UserData");
                if(userDataKey != null)
                {
                    foreach (var userKeyName in userDataKey.GetSubKeyNames())
                    {
                        using (var userKey = userDataKey.OpenSubKey(userKeyName)) 
                        {
                            using var componentsKey = userKey?.OpenSubKey("Components");
                            if (componentsKey != null) // Make sure there are Assemblies
                            {
                                foreach (var guidKeyName in componentsKey.GetSubKeyNames()) {
                                    using (var guidKey = componentsKey.OpenSubKey(guidKeyName)) {
                                        if (guidKey != null) {
                                            foreach (var valueName in guidKey.GetValueNames()) {
                                                var value = guidKey.GetValue(valueName).ToString();
                                                if (!value.ToLower().Contains("plex media server.exe")) {
                                                    continue;
                                                }

                                                //found it hooray!
                                                result = value;
                                                resultFound = true;
                                                Log.Information($"Plex executable found in windows installer registry keys");
                                                break;
                                            }
                                        }
                                    }

                                    if (resultFound) //don't keep looping if we have a result
                                    {
                                        break;
                                    }
                                }
                            }
                        }
                        if (resultFound) //break this loop if we have a result
                        {
                            break;
                        }
                    }
                }
            }
            return result;
        }
        
        #endregion

        #region Events

        /// <summary>
        /// Stop Event
        /// </summary>
        internal event EventHandler? PlexStop;

        /// <summary>
        /// Method to stop the monitor
        /// </summary>
        /// <param name="data"></param>
        private void OnPlexStop(EventArgs data)
        {
            PlexStop?.Invoke(this, data);
        }

        
        #region StateChange

        public event EventHandler? StateChange;

        private void OnStateChange()
        {
            StateChange?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #endregion
    }
}
