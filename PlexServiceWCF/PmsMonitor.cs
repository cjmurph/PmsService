using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using PlexServiceCommon;

namespace PlexServiceWCF
{
    /// <summary>
    /// Class for monitoring the status of Plex Media Server
    /// </summary>
    internal class PmsMonitor
    {
        #region static strings

        //Process names
        private static string _plexName = "Plex Media Server";
        //List of processes spawned by plex that we need to get rid of
        private static string[] _supportingProcesses =
        {
            "Plex DLNA Server",
            "PlexScriptHost",
            "PlexTranscoder",
            "PlexNewTranscoder",
            "Plex Media Scanner",
            "PlexRelay",
            "EasyAudioEncoder",
            "Plex Tuner Service"
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
        private Process _plex;

        private List<AuxiliaryApplicationMonitor> _auxAppMonitors;

        #endregion

        #region Properties

        private PlexState _state;

        public PlexState State
        {
            get
            {
                return _state;
            }
            set
            {
                if (_state != value)
                {
                    _state = value;
                    OnStateChange();
                }
            }
        }

        #endregion

        #region Constructor

        internal PmsMonitor()
        {
            State = PlexState.Stopped;
            _auxAppMonitors = new List<AuxiliaryApplicationMonitor>();
            Settings settings = SettingsHandler.Load();
            settings.AuxiliaryApplications.ForEach(x => _auxAppMonitors.Add(new AuxiliaryApplicationMonitor(x)));
            //hook up the state change event for all the applications
            _auxAppMonitors.ForEach(x => x.StatusChange += new AuxiliaryApplicationMonitor.StatusChangeHandler(OnPlexStatusChange));

        }
        #endregion

        #region PurgeAutoStart

        /// <summary>
        /// This method will look for and remove the "run at startup" registry key for plex media server.
        /// </summary>
        /// <returns></returns>
        private void PurgeAutoStartRegistryEntry()
        {
            string keyName = @"Software\Microsoft\Windows\CurrentVersion\Run";
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(keyName, true))
            {
                if (key != null)
                {
                    if (key.GetValue("Plex Media Server") != null)
                    {
                        try
                        {
                            key.DeleteValue("Plex Media Server");
                            OnPlexStatusChange(this, new StatusChangeEventArgs("Successfully removed auto start entry from registry"));
                        }
                        catch(Exception ex)
                        {
                            OnPlexStatusChange(this, new StatusChangeEventArgs(string.Format("Unable to remove auto start registry value. Error: {0}", ex.Message)));
                        }
                    }
                }
            }
        }

        #endregion

        #region DisableFirstRun

        /// <summary>
        /// This method will set the "FirstRun" registry key to 0 to prevent PMS from spawning the default browser.
        /// </summary>
        /// <returns></returns>
        private void DisableFirstRun()
        {
            string keyName = @"Software\Plex, Inc.\Plex Media Server";
            // CreateSubKey just in case it isn't already there for some reason.
            // The installer adds values under here during install, but this can't hurt.
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyName, RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                if (key != null)
                {
                    if (!Object.Equals(key.GetValue("FirstRun") as string, "0"))
                    {
                        try
                        {
                            key.SetValue("FirstRun", 0, RegistryValueKind.DWord);
                            OnPlexStatusChange(this, new StatusChangeEventArgs("Successfully set the 'FirstRun' registry key to 0"));
                        }
                        catch(Exception ex)
                        {
                            OnPlexStatusChange(this, new StatusChangeEventArgs(string.Format("Unable to set the 'FirstRun' registry key to 0. Error: {0}", ex.Message)));
                        }
                    }
                }
            }
        }

        #endregion

        #region Start

        /// <summary>
        /// Start monitoring plex
        /// </summary>
        internal void Start()
        {
            //Find the plex executable
            _executableFileName = GetPlexExecutable();
            if (string.IsNullOrEmpty(_executableFileName))
            {
                OnPlexStatusChange(this, new StatusChangeEventArgs("Plex Media Server does not appear to be installed!", EventLogEntryType.Error));
                OnPlexStop(this, new EventArgs());
                State = PlexState.Stopped;
            }
            else
            {
                //load the settings
                Settings settings = SettingsHandler.Load();

                OnPlexStatusChange(this, new StatusChangeEventArgs("Plex executable found at " + _executableFileName));
                
                //map network drives
                if (settings.DriveMaps.Count > 0)
                {
                    OnPlexStatusChange(this, new StatusChangeEventArgs("Mapping Network Drives"));
                    foreach(DriveMap map in settings.DriveMaps)
                    {
                        try
                        {
                            map.MapDrive(true);
                            OnPlexStatusChange(this, new StatusChangeEventArgs(string.Format("Map share {0} to letter '{1}' successful", map.ShareName, map.DriveLetter)));
                        }
                        catch(Exception ex)
                        {
                            OnPlexStatusChange(this, new StatusChangeEventArgs(string.Format("Unable to map share {0} to letter '{1}': {2}", map.ShareName, map.DriveLetter, ex.Message), EventLogEntryType.Error));
                        }
                    }
                }


                StartPlex();
                
                //stop any running aux apps
                _auxAppMonitors.ForEach(a => a.Stop());
                _auxAppMonitors.Clear();
                settings.AuxiliaryApplications.ForEach(x => _auxAppMonitors.Add(new AuxiliaryApplicationMonitor(x)));
                //hook up the state change event for all the applications
                _auxAppMonitors.ForEach(x => x.StatusChange += new AuxiliaryApplicationMonitor.StatusChangeHandler(OnPlexStatusChange));
                _auxAppMonitors.AsParallel().ForAll(x => x.Start());
            }
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
        /// <param name="msDelay">The amount of time in ms to wait before starting after stop</param>
        internal void Restart(int delay)
        {
            Stop();
            State = PlexState.Pending;
            System.Threading.AutoResetEvent autoEvent = new System.Threading.AutoResetEvent(false);
            System.Threading.Timer t = new System.Threading.Timer((x) => { Start(); autoEvent.Set(); }, null, delay, System.Threading.Timeout.Infinite);
            autoEvent.WaitOne();
            t.Dispose();
        }

        #endregion

        #region Process handling

        #region Exit events

        /// <summary>
        /// This event fires when the plex process we have a reference to exits
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Plex_Exited(object sender, EventArgs e)
        {
            OnPlexStatusChange(this, new StatusChangeEventArgs("Plex Media Server has stopped!"));
            //unsubscribe
            _plex.Exited -= Plex_Exited;

            //kill the supporting processes.
            KillSupportingProcesses();

            if (_plex != null)
            {
                _plex.Dispose();
                _plex = null;
            }

            //restart as required
            Settings settings = SettingsHandler.Load();
            if (State != PlexState.Stopping && settings.AutoRestart)
            {
                OnPlexStatusChange(this, new StatusChangeEventArgs(string.Format("Waiting {0} seconds before re-starting the Plex process.", settings.RestartDelay)));
                State = PlexState.Pending;
                System.Threading.AutoResetEvent autoEvent = new System.Threading.AutoResetEvent(false);
                System.Threading.Timer t = new System.Threading.Timer((x) => { Start(); autoEvent.Set(); }, null, settings.RestartDelay * 1000, System.Threading.Timeout.Infinite);
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
            DisableFirstRun(); 
            if (_plex == null)
            {
                //see if its running already
                _plex = Process.GetProcessesByName(PmsMonitor._plexName).FirstOrDefault();
                if (_plex == null)
                {
                    OnPlexStatusChange(this, new StatusChangeEventArgs("Attempting to start Plex"));
                    //plex process
                    _plex = new Process();
                    ProcessStartInfo plexStartInfo = new ProcessStartInfo(_executableFileName);
                    plexStartInfo.WorkingDirectory = Path.GetDirectoryName(_executableFileName);
                    plexStartInfo.UseShellExecute = false;
                    //check version to see if we can use the startup argument
                    string plexVersion = FileVersionInfo.GetVersionInfo(_executableFileName).FileVersion;
                    Version v = new Version(plexVersion);
                    Version minimumVersion = new Version("0.9.8.12");
                    if (v.CompareTo(minimumVersion) == -1)
                    {
                        OnPlexStatusChange(this, new StatusChangeEventArgs(string.Format("Plex Media Server version is {0}. Cannot use startup argument.", plexVersion)));
                    }
                    else
                    {
                        OnPlexStatusChange(this, new StatusChangeEventArgs(string.Format("Plex Media Server version is {0}. Can use startup argument.", plexVersion)));
                        plexStartInfo.Arguments = "-noninteractive";
                    }
                    _plex.StartInfo = plexStartInfo;
                    _plex.EnableRaisingEvents = true;
                    _plex.Exited += new EventHandler(Plex_Exited);
                    try
                    {
                        _plex.Start();
                        State = PlexState.Running;
                        OnPlexStatusChange(this, new StatusChangeEventArgs("Plex Media Server Started."));
                    }
                    catch(Exception ex)
                    {
                        OnPlexStatusChange(this, new StatusChangeEventArgs("Plex Media Server failed to start. " + ex.Message));
                    }
                }
                else
                {
                    //its running, most likely in the wrong session. monitor this instance and if it ends, start a new one
                    //register to the exited event so we know when to start a new one
                    OnPlexStatusChange(this, new StatusChangeEventArgs(string.Format("Plex Media Server already running in session {0}.", _plex.SessionId)));
                    try
                    {
                        _plex.EnableRaisingEvents = true;
                        _plex.Exited += new EventHandler(Plex_Exited);
                        State = PlexState.Running;
                    }
                    catch
                    {
                        OnPlexStatusChange(this, new StatusChangeEventArgs("Unable to attach to already running Plex Media Server instance. The existing instance will continue unmanaged. Please close all instances of Plex Media Server on this computer prior to starting the service"));
                        OnPlexStop(this, new EventArgs());
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
                OnPlexStatusChange(this, new StatusChangeEventArgs("Killing Plex."));
                try
                {
                    _plex.Kill();
                }
                catch { }
            }
            //kill each auxiliary process
            _auxAppMonitors.ForEach(appMonitor =>
            {
                appMonitor.Stop();
                //remove event hook
                appMonitor.StatusChange -= OnPlexStatusChange;
            });

            OnPlexStop(this, new EventArgs());
        }

        /// <summary>
        /// Kill all processes with the specified names
        /// </summary>
        /// <param name="names">The names of the processes to kill</param>
        private void KillSupportingProcesses()
        {
            foreach (string name in _supportingProcesses)
            {
                KillSupportingProcess(name);
            }
        }

        /// <summary>
        /// Kill all instances of the specified process.
        /// </summary>
        /// <param name="name">The name of the process to kill</param>
        private void KillSupportingProcess(string name)
        {
            //see if its running
            Process[] supportProcesses = Process.GetProcessesByName(name);
            if (supportProcesses.Length > 0)
            {
                foreach (Process supportProcess in supportProcesses)
                {
                    try
                    {
                        supportProcess.Kill();
                    }
                    catch
                    {
                    }
                    finally
                    {
                        supportProcess.Dispose();
                    }
                }
                OnPlexStatusChange(this, new StatusChangeEventArgs(string.Format("{0} Stopped.", name)));
            }
        }

        #endregion

        #endregion

        #region Aux app interaction methods

        public bool IsAuxAppRunning(string name)
        {
            var auxApp = _auxAppMonitors.FirstOrDefault(a => a.Name == name);
            if (auxApp != null)
            {
                return auxApp.Running;
            }
            return false;
        }

        public void StartAuxApp(string name)
        {
            var auxApp = _auxAppMonitors.FirstOrDefault(a => a.Name == name);
            if (auxApp != null)
            {
                if (!auxApp.Running)
                    auxApp.Start();
            }
        }

        public void StopAuxApp(string name)
        {
            var auxApp = _auxAppMonitors.FirstOrDefault(a => a.Name == name);
            if (auxApp != null)
            {
                if (auxApp.Running)
                    auxApp.Stop();
            }
        }

        #endregion

        #region File Methods

        /// <summary>
        /// Returns the full path and filename of the plex media server executable
        /// </summary>
        /// <returns></returns>
        private string GetPlexExecutable()
        {
            string result = string.Empty;

            //first we will do a dirty check for a text file with the executable path in our log folder.
            //this is here to help anyone having issues and let them specify it manually themseves.
            if(!string.IsNullOrEmpty(TrayInteraction.APP_DATA_PATH))
            {
                string location = Path.Combine(TrayInteraction.APP_DATA_PATH, "location.txt");
                if (File.Exists(location))
                {
                    string userSpecified = string.Empty;
                    using (StreamReader sr = new StreamReader(location))
                    {
                        userSpecified = sr.ReadLine();
                    }
                    if (File.Exists(userSpecified))
                    {
                        result = userSpecified;
                    }
                }
            }

            //if theres nothing there go for the easy defaults
            if (string.IsNullOrEmpty(result))
            {

                //plex doesn't put this nice stuff in the registry so we need to go hunting for it ourselves
                //this method is crap. I dont like having to iterate through directories looking to see if a file exists or not.
                //start by looking in the program files directory, even if we are on 64bit windows, plex may be 64bit one day... maybe

                List<string> possibleLocations = new List<string>();

                //some hard coded attempts, this is nice and fast and should hit 90% of the time... even if it is ugly
                possibleLocations.Add(@"C:\Program Files\Plex\Plex Media Server\Plex Media Server.exe");
                possibleLocations.Add(@"C:\Program Files (x86)\Plex\Plex Media Server\Plex Media Server.exe");
                //special folder
                possibleLocations.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Plex\Plex Media Server\Plex Media Server.exe"));


                foreach (string location in possibleLocations)
                {
                    if (File.Exists(location))
                    {
                        result = location;
                        break;
                    }
                }
            }

            //so if we still can't find it, we need to do a more exhaustive check through the installer locations in the registry
            if (string.IsNullOrEmpty(result))
            {
                //let's have a flag to break out of the loops below for faster execution, because this is nasty.
                bool resultFound = false;

                //work out the os type (32 or 64) and set the registry view to suit. this is only a reliable check when this project is compiled to x86.
                bool is64bit = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432"));

                RegistryView architecture = RegistryView.Registry32;
                if (is64bit)
                {
                    architecture = RegistryView.Registry64;
                }

                using (RegistryKey userDataKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, architecture).OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Installer\UserData"))
                {
                    if(userDataKey != null)
                    {
                        foreach (string userKeyName in userDataKey.GetSubKeyNames())
                        {
                            using (RegistryKey userKey = userDataKey.OpenSubKey(userKeyName))
                            {
                                using (RegistryKey componentsKey = userKey.OpenSubKey("Components"))
                                {
                                    if (componentsKey != null)       // Make sure there are Assemblies
                                    {
                                        foreach (string guidKeyName in componentsKey.GetSubKeyNames())
                                        {
                                            using (RegistryKey guidKey = componentsKey.OpenSubKey(guidKeyName))
                                            {
                                                foreach (string valueName in guidKey.GetValueNames())
                                                {
                                                    string value = guidKey.GetValue(valueName).ToString();
                                                    if (value.ToLower().Contains("plex media server.exe"))
                                                    {
                                                        //found it hooray!
                                                        result = value;
                                                        resultFound = true;
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
                            }
                            if (resultFound) //break this loop if we have a result
                            {
                                break;
                            }
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
        internal event EventHandler PlexStop;
        /// <summary>
        /// Method to stop the monitor
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        protected void OnPlexStop(object sender, EventArgs data)
        {
            PlexStop?.Invoke(this, data);
        }

        /// <summary>
        /// Status change event
        /// </summary>
        internal event EventHandler<StatusChangeEventArgs> PlexStatusChange;

        /// <summary>
        /// Method to fire the status change event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        protected void OnPlexStatusChange(object sender, StatusChangeEventArgs data)
        {
            PlexStatusChange?.Invoke(this, data);
        }

        #region StateChange

        public event EventHandler StateChange;

        protected void OnStateChange()
        {
            StateChange?.Invoke(this, new EventArgs());
        }

        #endregion

        #endregion
    }
}
