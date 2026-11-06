#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PlexServiceCommon;
using Serilog;

namespace PlexServiceWCF
{
    /// <summary>
    /// Class for monitoring the status of Plex Media Server
    /// </summary>
    internal class PmsMonitor
    {
        #region static strings

        //Process name
        private const string PlexName = "Plex Media Server";
        //Executable name
        private const string PlexExecutable = "Plex Media Server.exe";
        //Updater name
        private const string PlexUpdaterName = "Plex Update Service Launcher";
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

        private readonly object _sync = new();

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

        private readonly List<AuxiliaryApplicationMonitor> _auxAppMonitors = new();

        private Settings _settings;

        private bool _restartRequested;

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

        public Version? PlexVersion { get; private set; }

        #endregion

        #region Constructor

        internal PmsMonitor()
        {
            _settings = SettingsHandler.Load();
        }

        #endregion

        #region Registry

        /// <summary>
        /// This method will look for and remove the "run at startup" registry key for plex media server.
        /// </summary>
        private static void PurgeAutoStartRegistryEntry()
        {
            const string keyName = @"Software\Microsoft\Windows\CurrentVersion\Run";
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(keyName, true);
                if (key?.GetValue(PlexName) == null) {
                    return;
                }

                key.DeleteValue(PlexName);
                Log.Information("Successfully removed auto start entry from registry");
            }
            catch(Exception ex)
            {
                Log.Warning($"Unable to remove auto start registry value. Error: {ex.Message}");
            }
        }

        /// <summary>
        /// This method will set the "FirstRun" registry key to 0 to prevent PMS from spawning the default browser.
        /// </summary>
        private static void DisableFirstRun() {
            const string keyName = @"Software\Plex, Inc.\Plex Media Server";
            var is64Bit = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432"));
            var architecture = is64Bit ? RegistryView.Registry64 : RegistryView.Registry32;

            try {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, architecture);
                using var pmsDataKey = baseKey.CreateSubKey(keyName, RegistryKeyPermissionCheck.ReadWriteSubTree);
                if (pmsDataKey == null) return;

                if (pmsDataKey.GetValue("FirstRun") is int firstRun && firstRun == 0) return;

                pmsDataKey.SetValue("FirstRun", 0, RegistryValueKind.DWord);
                Log.Information("Successfully set the 'FirstRun' registry key to 0");
            } catch (Exception ex) {
                Log.Warning($"Unable to set the 'FirstRun' registry key to 0. Error: {ex.Message}");
            }
        }

        #endregion

        #region Start

        /// <summary>
        /// Start monitoring plex
        /// </summary>
        internal void Start()
        {
            lock (_sync)
            {
                //clear any restart request flag
                _restartRequested = false;
                //make sure this doesn't get stuck for any reason.
                _updating = false;
                //reload the settings just in case
                _settings = SettingsHandler.Load();

                //stop any running aux apps, they get started again below
                StopAuxApps();

                //Find the plex executable
                _executableFileName = GetPlexExecutable();
                if (string.IsNullOrEmpty(_executableFileName))
                {
                    Log.Warning("Plex Media Server does not appear to be installed! Set the location of the executable in the service settings.");
                    State = PlexState.Stopped;
                    OnPlexStop(EventArgs.Empty);
                    return;
                }

                Log.Information("Plex executable found at " + _executableFileName);
                State = PlexState.Pending;

                //map network drives
                var drivesMapped = true;
                if (_settings.DriveMaps.Count > 0) {
                    Log.Information("Mapping Network Drives");
                    drivesMapped = _settings.DriveMaps.Select(map => TryMap(map, _settings)).ToList().All(x => x);
                }

                if (!drivesMapped && !_settings.StartPlexOnMountFail)
                {
                    Log.Warning("One or more drive mappings failed and settings are configured to *not* start Plex on mount failure. Please check your drives and try again.");
                    State = PlexState.Stopped;
                    return;
                }

                _settings.AuxiliaryApplications.ForEach(x => _auxAppMonitors.Add(new AuxiliaryApplicationMonitor(x)));

                //some apps (e.g. tuners) have to be up before plex starts
                var before = _auxAppMonitors.Where(a => a.StartBeforePlex).ToList();
                if (before.Count > 0)
                {
                    Log.Information("Starting auxiliary applications that need to run before Plex");
                    before.ForEach(a => a.Start());
                    var delay = before.Max(a => a.StartDelay);
                    if (delay > 0)
                    {
                        Log.Information($"Waiting {delay} seconds before starting Plex");
                        Thread.Sleep(TimeSpan.FromSeconds(delay));
                    }
                }

                StartPlex();

                _auxAppMonitors.Where(a => !a.StartBeforePlex).AsParallel().ForAll(x => x.Start());
            }
        }

        private static bool TryMap(DriveMap map, Settings settings)
        {
            var count = settings.AutoRemount ? Math.Max(1, settings.AutoRemountCount) : 1;

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

                count--;
                if (count > 0) Thread.Sleep(TimeSpan.FromSeconds(Math.Max(1, settings.AutoRemountDelay)));
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
            lock (_sync)
            {
                if (_plex == null)
                {
                    // nothing to wait for, make sure everything that belongs to plex is gone
                    StopAuxApps();
                    KillSupportingProcesses();
                    State = PlexState.Stopped;
                    OnPlexStop(EventArgs.Empty);
                    return;
                }

                State = PlexState.Stopping;
                EndPlex();
            }
        }

        /// <summary>
        /// Stop plex and wait until the process is really gone. Used when the service stops.
        /// </summary>
        internal void StopAndWait(TimeSpan timeout)
        {
            Process? plex;
            lock (_sync)
            {
                _restartRequested = false;
                plex = _plex;
            }

            Stop();

            try
            {
                if (plex != null && !plex.WaitForExit((int)timeout.TotalMilliseconds))
                {
                    Log.Warning("Timed out waiting for Plex to exit.");
                }
            }
            catch (Exception ex)
            {
                // the process object may already be disposed by the exited handler
                Log.Debug("Waiting for plex: " + ex.Message);
            }

            KillSupportingProcesses();
            StopAuxApps();
            State = PlexState.Stopped;
        }

        #endregion

        #region Restart

        /// <summary>
        /// Restart plex, the restart delay from the settings is used between stop and start
        /// </summary>
        internal void Restart()
        {
            Log.Information("Restarting Plex...");
            lock (_sync)
            {
                if (_plex != null)
                {
                    _restartRequested = true;
                    State = PlexState.Stopping;
                    EndPlex();
                    return;
                }
            }

            // plex isn't running, just start it
            Start();
        }

        #endregion

        #region Process handling

        #region Exit events

        private void Updater_Exited(object sender, EventArgs e)
        {
            lock (_sync)
            {
                if (_plexUpdater != null)
                {
                    _plexUpdater.Exited -= Updater_Exited;
                    _plexUpdater.Dispose();
                    _plexUpdater = null;
                }

                _updating = false;
            }

            Log.Information("PMS update is complete, restarting Plex.");
            Start();
        }

        /// <summary>
        /// This event fires when the plex process we have a reference to exits
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Plex_Exited(object sender, EventArgs e)
        {
            Log.Information("Plex Media Server has stopped!");
            bool restart;
            lock (_sync)
            {
                if (sender is Process exited)
                {
                    exited.Exited -= Plex_Exited;
                    if (!ReferenceEquals(exited, _plex))
                    {
                        // an old process we already replaced
                        exited.Dispose();
                        return;
                    }
                }

                _plex?.Dispose();
                _plex = null;

                if (_updating)
                {
                    Log.Information("Plex is updating, waiting for finish before re-starting.");
                    return;
                }

                // only look for the updater if nobody asked plex to stop
                if (State != PlexState.Stopping)
                {
                    _plexUpdater = Process.GetProcessesByName(PlexUpdaterName).FirstOrDefault();
                    if (_plexUpdater != null)
                    {
                        Log.Information("Plex update started.");
                        State = PlexState.Updating;
                        _updating = true;
                        _plexUpdater.EnableRaisingEvents = true;
                        _plexUpdater.Exited += Updater_Exited;
                        return;
                    }
                }

                //kill the supporting processes.
                KillSupportingProcesses();

                //restart as required
                _settings = SettingsHandler.Load();
                restart = (State != PlexState.Stopping && _settings.AutoRestart) || _restartRequested;
                _restartRequested = false;

                if (!restart)
                {
                    StopAuxApps();
                    State = PlexState.Stopped;
                    return;
                }

                State = PlexState.Pending;
            }

            Log.Information($"Waiting {_settings.RestartDelay} seconds before re-starting the Plex process.");
            Task.Delay(TimeSpan.FromSeconds(Math.Max(0, _settings.RestartDelay))).ContinueWith(_ =>
            {
                // the user may have stopped plex in the meantime
                if (State == PlexState.Pending) Start();
            });
        }

        #endregion

        #region Start methods

        /// <summary>
        /// Start a new plex process
        /// </summary>
        private void StartPlex()
        {
            State = PlexState.Pending;
            //always try to get rid of the plex auto start registry entry
            PurgeAutoStartRegistryEntry();
            // make sure we don't spawn a browser
            DisableFirstRun();

            if (_plex != null)
            {
                Log.Information("Plex is already being monitored.");
                State = PlexState.Running;
                return;
            }

            //see if its running already (e.g. started by the user)
            foreach (var running in Process.GetProcessesByName(PlexName))
            {
                Log.Information("Killing existing Plex process.");
                try
                {
                    running.Kill();
                    running.WaitForExit(10000);
                }
                catch (Exception ex)
                {
                    Log.Warning("Unable to stop existing Plex process: " + ex.Message);
                }
                finally
                {
                    running.Dispose();
                }
            }
            KillSupportingProcesses();

            Log.Information("Attempting to start Plex.");
            try
            {
                var plexStartInfo = new ProcessStartInfo(_executableFileName) {
                    WorkingDirectory = Path.GetDirectoryName(_executableFileName) ?? string.Empty,
                    UseShellExecute = false
                };

                //check version to see if we can use the startup argument
                PlexVersion = GetFileVersion(_executableFileName);
                if (PlexVersion != null && PlexVersion < new Version(0, 9, 8, 12))
                {
                    Log.Information($"Plex Media Server version is {PlexVersion}. Cannot use startup argument.");
                }
                else
                {
                    Log.Information($"Plex Media Server version is {PlexVersion?.ToString() ?? "unknown"}. Using startup arguments.");
                    plexStartInfo.Arguments = "-noninteractive -noautorestart";
                }

                var plex = new Process { StartInfo = plexStartInfo, EnableRaisingEvents = true };
                plex.Exited += Plex_Exited;
                if (plex.Start())
                {
                    _plex = plex;
                    State = PlexState.Running;
                    Log.Information("Plex Media Server Started.");
                    return;
                }

                plex.Dispose();
            }
            catch(Exception ex)
            {
                Log.Warning("Plex Media Server failed to start. " + ex.Message);
            }

            //set the state back to stopped if we didn't achieve a running state
            State = PlexState.Stopped;
        }

        private static Version? GetFileVersion(string file)
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(file);
                return new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
            }
            catch (Exception ex)
            {
                Log.Warning("Unable to read the Plex version: " + ex.Message);
                return null;
            }
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

            //kill each auxiliary process unless we come back right away
            if (!_restartRequested) StopAuxApps();

            OnPlexStop(EventArgs.Empty);
        }

        private void StopAuxApps()
        {
            lock (_auxAppMonitors)
            {
                _auxAppMonitors.ForEach(appMonitor => appMonitor.Stop());
                _auxAppMonitors.Clear();
            }
        }

        /// <summary>
        /// Kill all processes with the specified names
        /// </summary>
        private static void KillSupportingProcesses()
        {
            Log.Debug("Killing supporting processes.");
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
            foreach (var supportProcess in Process.GetProcessesByName(name))
            {
                Log.Information($"Stopping {name} with PID of {supportProcess.Id}.");
                try
                {
                    supportProcess.Kill();
                }
                catch (Exception ex)
                {
                    Log.Warning($"Unable to stop process {supportProcess.Id}: {ex.Message}");
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

        private AuxiliaryApplicationMonitor? FindAuxApp(string name)
        {
            lock (_auxAppMonitors)
            {
                return _auxAppMonitors.FirstOrDefault(a => a.Name == name);
            }
        }

        public bool IsAuxAppRunning(string name)
        {
            return FindAuxApp(name) is { Running: true };
        }

        public void StartAuxApp(string name)
        {
            var auxApp = FindAuxApp(name);
            if (auxApp == null)
            {
                // the app might have been added after plex was started
                var settings = SettingsHandler.Load().AuxiliaryApplications.FirstOrDefault(a => a.Name == name);
                if (settings == null) return;
                auxApp = new AuxiliaryApplicationMonitor(settings);
                lock (_auxAppMonitors) _auxAppMonitors.Add(auxApp);
            }
            if (!auxApp.Running) auxApp.Start();
        }

        public void StopAuxApp(string name)
        {
            var auxApp = FindAuxApp(name);
            if (auxApp is { Running: true }) auxApp.Stop();
        }

        #endregion

        #region File Methods

        /// <summary>
        /// Turns a value that could either be the executable or the folder it lives in into the executable path.
        /// </summary>
        private static string ResolveExecutable(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var path = value!.Trim().Trim('"');
            try
            {
                if (Directory.Exists(path)) path = Path.Combine(path, PlexExecutable);
                return File.Exists(path) ? path : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Returns the full path and filename of the plex media server executable
        /// </summary>
        /// <returns></returns>
        private string GetPlexExecutable()
        {
            //first see if its defined in the settings
            var result = ResolveExecutable(_settings.UserDefinedInstallLocation);
            if (!string.IsNullOrEmpty(result))
            {
                Log.Information("Plex executable using user specified value in settings");
                return result;
            }

            //next we will do a dirty check for a text file with the executable path in our log folder.
            //this is here to help anyone having issues and let them specify it manually themselves.
            var location = Path.Combine(TrayInteraction.AppDataPath, "location.txt");
            if (File.Exists(location))
            {
                result = ResolveExecutable(File.ReadLines(location).FirstOrDefault());
                if (!string.IsNullOrEmpty(result))
                {
                    Log.Information("Plex executable using user specified value in location.txt");
                    return result;
                }
            }

            //the plex registry key holds the install folder, check HKLM first and then the user
            result = ResolveExecutable(PlexRegistryHelper.ReadLocalMachineRegistryValue("InstallFolder"));
            if (!string.IsNullOrEmpty(result))
            {
                Log.Information("Plex executable found in HKLM plex registry key");
                return result;
            }

            result = ResolveExecutable(PlexRegistryHelper.ReadUserRegistryValue("InstallFolder"));
            if (!string.IsNullOrEmpty(result))
            {
                Log.Information("Plex executable found in HKCU plex registry key");
                return result;
            }

            //the default install locations
            var possibleLocations = new[] {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetEnvironmentVariable("ProgramW6432"),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };
            foreach (var programFiles in possibleLocations.Where(p => !string.IsNullOrEmpty(p)))
            {
                result = ResolveExecutable(Path.Combine(programFiles, @"Plex\Plex Media Server"));
                if (string.IsNullOrEmpty(result)) continue;
                Log.Information("Plex executable found in the default install location");
                return result;
            }

            //so if we still can't find it, we need to do a more exhaustive check through the installer locations in the registry
            return FindInInstallerRegistry();
        }

        private static string FindInInstallerRegistry()
        {
            try
            {
                //work out the os type (32 or 64) and set the registry view to suit. this is only a reliable check when this project is compiled to x86.
                var is64Bit = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432"));
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, is64Bit ? RegistryView.Registry64 : RegistryView.Registry32);
                using var userDataKey = baseKey.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Installer\UserData");
                if (userDataKey == null) return string.Empty;

                foreach (var userKeyName in userDataKey.GetSubKeyNames())
                {
                    using var componentsKey = userDataKey.OpenSubKey(userKeyName + @"\Components");
                    if (componentsKey == null) continue;

                    foreach (var guidKeyName in componentsKey.GetSubKeyNames())
                    {
                        using var guidKey = componentsKey.OpenSubKey(guidKeyName);
                        if (guidKey == null) continue;

                        foreach (var valueName in guidKey.GetValueNames())
                        {
                            var value = guidKey.GetValue(valueName)?.ToString() ?? string.Empty;
                            if (!value.EndsWith(PlexExecutable, StringComparison.OrdinalIgnoreCase) || !File.Exists(value)) continue;

                            Log.Information("Plex executable found in windows installer registry keys");
                            return value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning("Unable to search the installer registry: " + ex.Message);
            }

            return string.Empty;
        }

        #endregion

        #region Events

        /// <summary>
        /// Stop Event
        /// </summary>
        internal event EventHandler? PlexStop;

        private void OnPlexStop(EventArgs data)
        {
            PlexStop?.Invoke(this, data);
        }

        public event EventHandler? StateChange;

        private void OnStateChange()
        {
            StateChange?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    }
}
