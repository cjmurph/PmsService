using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace PlexMediaServer_Service
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
        private static string[] _supportingProcesses = { "PlexDlnaServer", "PlexScriptHost", "PlexTranscoder", "PlexNewTranscoder", "Plex Media Scanner" };

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
        /// <summary>
        /// Flag for actual stop rather than crash we should attempt to restart from
        /// </summary>
        private bool _stopping;

        private List<AuxiliaryApplicationMonitor> _auxAppMonitors;

        #endregion

        #region Properties

        #endregion

        #region Constructor

        internal PmsMonitor()
        {
            _auxAppMonitors = new List<AuxiliaryApplicationMonitor>();
        }
        #endregion

        #region PurgeAutoStart

        /// <summary>
        /// This method will look for and remove the "run at startup" registry key for plex media server.
        /// </summary>
        /// <returns></returns>
        private void purgeAutoStartRegistryEntry()
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

        #region Start

        /// <summary>
        /// Start monitoring plex
        /// </summary>
        internal void Start()
        {
            _stopping = false;

            //Find the plex executable
            _executableFileName = getPlexExecutable();
            if (string.IsNullOrEmpty(_executableFileName))
            {
                OnPlexStatusChange(this, new StatusChangeEventArgs("Plex Media Server does not appear to be installed!", EventLogEntryType.Error));
                OnPlexStop(this, new EventArgs());
            }
            else
            {
                OnPlexStatusChange(this, new StatusChangeEventArgs("Plex executable found at " + _executableFileName));
                startPlex();
                //load the settings and start a thread that will attempt to bring up all the auxiliary processes
                Settings settings = Settings.Load();
                _auxAppMonitors.Clear();
                settings.AuxiliaryApplications.ForEach(x => _auxAppMonitors.Add(new AuxiliaryApplicationMonitor(x)));
                //hook up the state change event for all the applications
                _auxAppMonitors.ForEach(x => x.StatusChange += new AuxiliaryApplicationMonitor.StatusChangeHandler(aux_StatusChange));
                _auxAppMonitors.AsParallel().ForAll(x => x.Start());
            }
        }

        void aux_StatusChange(object sender, StatusChangeEventArgs data)
        {
            //bubble up the state change
            OnPlexStatusChange(sender, data);
        }

        #endregion

        #region Stop

        /// <summary>
        /// Stop the monitor and kill the processes
        /// </summary>
        internal void Stop()
        {
            _stopping = true;
            endPlex();
            //kill each auxiliary process
            _auxAppMonitors.ForEach(x => 
                {
                    x.Stop();
                    //remove event hook
                    x.StatusChange -= aux_StatusChange;
                });
        }

        #endregion

        #region Process handling

        #region Exit events

        /// <summary>
        /// This event fires when the plex process we have a reference to exits
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void plex_Exited(object sender, EventArgs e)
        {
            OnPlexStatusChange(this, new StatusChangeEventArgs("Plex Media Server has stopped!"));
            //unsubscribe
            _plex.Exited -= plex_Exited;
            //try to restart
            endPlex();
            //restart as required
            if (!_stopping)
            {
                OnPlexStatusChange(this, new StatusChangeEventArgs("Re-starting Plex process."));
                //wait some seconds first
                System.Threading.Thread.Sleep(10000);
                startPlex();
            }
            else
            {
                OnPlexStatusChange(this, new StatusChangeEventArgs("Plex process stopped"));
            }
        }

        #endregion

        #region Start methods

        /// <summary>
        /// Start a new/get a handle on existing Plex process
        /// </summary>
        private void startPlex()
        {
            //always try to get rid of the plex auto start registry entry
            purgeAutoStartRegistryEntry();

            OnPlexStatusChange(this, new StatusChangeEventArgs("Attempting to start Plex"));
            if (_plex == null)
            {
                //see if its running already
                _plex = Process.GetProcessesByName(PmsMonitor._plexName).FirstOrDefault();
                if (_plex == null)
                {
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
                    _plex.Exited += new EventHandler(plex_Exited);
                    try
                    {
                        _plex.Start();
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
                        _plex.Exited += new EventHandler(plex_Exited);
                    }
                    catch
                    {
                        OnPlexStatusChange(this, new StatusChangeEventArgs("Unable to attach to already running Plex Media Server instance. The existing instance will continue unmanaged. Please close all instances of Plex Media Server on this computer prior to starting the service"));
                        OnPlexStop(this, new EventArgs());
                    }
                }
            }
        }

        #endregion

        #region End methods

        /// <summary>
        /// Kill the plex process
        /// </summary>
        private void endPlex()
        {
            
            if (_plex != null)
            {
                OnPlexStatusChange(this, new StatusChangeEventArgs("Killing Plex."));
                try
                {
                    _plex.Kill();
                }
                catch { }
                finally
                {
                    _plex.Dispose();
                    _plex = null;
                }
            }
            //kill the supporting processes.
            killSupportingProcesses(PmsMonitor._supportingProcesses);
        }

        /// <summary>
        /// Kill all processes with the specified names
        /// </summary>
        /// <param name="names">The names of the processes to kill</param>
        private void killSupportingProcesses(string[] names)
        {
            foreach (string name in names)
            {
                killSupportingProcess(name);
            }
        }

        /// <summary>
        /// Kill all instances of the specified process.
        /// </summary>
        /// <param name="name">The name of the process to kill</param>
        private void killSupportingProcess(string name)
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

        #region File Methods

        /// <summary>
        /// Returns the full path and filename of the plex media server executable
        /// </summary>
        /// <returns></returns>
        private string getPlexExecutable()
        {
            string result = string.Empty;

            //first we will do a dirty check for a text file with the executable path in our log folder.
            //this is here to help anyone having issues and let them specify it manually themseves.
            if(!string.IsNullOrEmpty(PlexMediaServerService.APP_DATA_PATH))
            {
                string location = Path.Combine(PlexMediaServerService.APP_DATA_PATH, "location.txt");
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
        //stop everything
        /// <summary>
        /// Stop Delegate
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        internal delegate void PlexStopHandler(object sender, EventArgs data);
        /// <summary>
        /// Stop Event
        /// </summary>
        internal event PlexStopHandler PlexStop;
        /// <summary>
        /// Method to stop the monitor
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        protected void OnPlexStop(object sender, EventArgs data)
        {
            //Check if event has been subscribed to
            if (PlexStop != null)
            {
                //call the event
                PlexStop(this, data);
            }
        }

        //status change
        /// <summary>
        /// Status change delegate
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        internal delegate void PlexStatusChangeHandler(object sender, StatusChangeEventArgs data);

        /// <summary>
        /// Status change event
        /// </summary>
        internal event PlexStatusChangeHandler PlexStatusChange;

        /// <summary>
        /// Method to fire the status change event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        protected void OnPlexStatusChange(object sender, StatusChangeEventArgs data)
        {
            //Check if event has been subscribed to
            if (PlexStatusChange != null)
            {
                //call the event
                PlexStatusChange(this, data);
            }
        }
        #endregion
    }
}
