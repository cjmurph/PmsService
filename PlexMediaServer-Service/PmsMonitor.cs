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
        private static string plexName = "Plex Media Server";
        private static string dlnaServerName = "PlexDlnaServer";
        private static string plexScriptHostName = "PlexScriptHost";
        //private static string explorerName = "explorer";

        #endregion

        #region Private variables

        /// <summary>
        /// The name of the plex media server executable
        /// </summary>
        private string executableFileName = string.Empty;

        /// <summary>
        /// Explorer process
        /// </summary>
        private Process explorer;
        /// <summary>
        /// Plex process
        /// </summary>
        private Process plex;
        /// <summary>
        /// Flag for actual stop rather than crash we should attempt to restart from
        /// </summary>
        private bool stopping;

        private string appPath;

        #endregion

        #region Properties

        #endregion

        #region Constructor
        internal PmsMonitor() : this("") { }

        internal PmsMonitor(string appPath)
        {
            this.appPath = appPath;
        }
        #endregion

        #region Start

        /// <summary>
        /// Start monitoring plex
        /// </summary>
        internal void Start()
        {
            this.stopping = false;
            //Find the plex executable
            this.executableFileName = getPlexExecutable();
            if (string.IsNullOrEmpty(this.executableFileName))
            {
                this.OnPlexStatusChange(this, new PlexRunningStatusChangeEventArgs("Plex Media Server does not appear to be installed!", EventLogEntryType.Error));
                this.OnPlexStop(this, new EventArgs());
            }
            else
            {
                this.OnPlexStatusChange(this, new PlexRunningStatusChangeEventArgs("Plex executable found at " + this.executableFileName));
                this.startPlexWithExplorer(10000);
            }
        }

        #endregion

        #region Stop

        /// <summary>
        /// Stop the monitor and kill the processes
        /// </summary>
        internal void Stop()
        {
            this.stopping = true;
            this.endPlex();
            this.endExplorer();
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
            this.OnPlexStatusChange(this, new PlexRunningStatusChangeEventArgs("Plex Media Server has stopped!"));
            //unsubscribe
            this.plex.Exited -= this.plex_Exited;
            //try to restart
            this.endPlex();
            this.endExplorer();
            //restart as required
            if (!this.stopping)
            {
                this.OnPlexStatusChange(this, new PlexRunningStatusChangeEventArgs("Re-starting Plex process."));
                //wait some seconds first
                System.Threading.Thread.Sleep(5000);
                this.startPlexWithExplorer(5000);
            }
            else
            {
                this.OnPlexStatusChange(this, new PlexRunningStatusChangeEventArgs("Service Stopped"));
            }
        }

        /// <summary>
        /// This event fires when the explorer process we have a refrence to exits
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void explorer_Exited(object sender, EventArgs e)
        {
            this.explorer.Exited -= this.explorer_Exited;
            //if plex is running it will likely fail not long after this...
        }

        #endregion

        #region Start methods

        /// <summary>
        /// Starts an instance of Explorer, waits 10 seconds then starts Plex Media Server
        /// </summary>
        private void startPlexWithExplorer(int msDelay)
        {
            this.OnPlexStatusChange(this, new PlexRunningStatusChangeEventArgs("Attempting to start Explorer"));
            this.startExplorer();
            //wait delay to be sure that explorer has started
            this.OnPlexStatusChange(this, new PlexRunningStatusChangeEventArgs(string.Format("Waiting {0} milliseconds", msDelay.ToString())));
            System.Threading.Thread.Sleep(msDelay);
            this.OnPlexStatusChange(this, new PlexRunningStatusChangeEventArgs("Attempting to start Plex"));
            this.startPlex();
        }

        /// <summary>
        /// Start a new/get a handle on existing Explorer process
        /// </summary>
        private void startExplorer()
        {
            if (this.explorer == null)
            {
                this.explorer = new Process();
                ProcessStartInfo explorerStartInfo = new ProcessStartInfo(System.IO.Path.Combine(Environment.ExpandEnvironmentVariables("%windir%"), @"explorer.exe"));
                explorerStartInfo.UseShellExecute = false;
                this.explorer.StartInfo = explorerStartInfo;
                this.explorer.EnableRaisingEvents = true;
                this.explorer.Exited += new EventHandler(explorer_Exited);
                this.explorer.Start();
                this.OnPlexStatusChange(this, new PlexRunningStatusChangeEventArgs("Explorer Started."));
            }
        }

        /// <summary>
        /// Start a new/get a handle on existing Plex process
        /// </summary>
        private void startPlex()
        {
            if (this.plex == null)
            {
                //see if its running already
                this.plex = Process.GetProcessesByName(PmsMonitor.plexName).FirstOrDefault();
                if (this.plex == null)
                {
                    //plex process
                    this.plex = new Process();
                    ProcessStartInfo plexStartInfo = new ProcessStartInfo(this.executableFileName);
                    plexStartInfo.WorkingDirectory = Path.GetDirectoryName(this.executableFileName);
                    plexStartInfo.UseShellExecute = false;
                    this.plex.StartInfo = plexStartInfo;
                    this.plex.EnableRaisingEvents = true;
                    this.plex.Exited += new EventHandler(plex_Exited);
                    this.plex.Start();
                    this.OnPlexStatusChange(this, new PlexRunningStatusChangeEventArgs("Plex Media Server Started."));
                }
                else
                {
                    //its running, most likely in the wrong session. monitor this instance and if it ends, start a new one
                    //register to the exited event so we know when to start a new one
                    this.OnPlexStatusChange(this, new PlexRunningStatusChangeEventArgs("Plex Media Server already running in session 0."));
                    this.plex.EnableRaisingEvents = true;
                    this.plex.Exited += new EventHandler(plex_Exited);
                }
            }
        }

        #endregion

        #region End methods

        /// <summary>
        /// Kill the explorer process
        /// </summary>
        private void endExplorer()
        {
            this.OnPlexStatusChange(this, new PlexRunningStatusChangeEventArgs("Killing Explorer."));
            if (this.explorer != null)
            {
                try
                {
                    this.explorer.Kill();
                }
                catch { }
                finally
                {
                    this.explorer.Dispose();
                    this.explorer = null;
                }
            }
        }

        /// <summary>
        /// Kill the plex process
        /// </summary>
        private void endPlex()
        {
            
            if (this.plex != null)
            {
                this.OnPlexStatusChange(this, new PlexRunningStatusChangeEventArgs("Killing Plex."));
                try
                {
                    this.plex.Kill();
                }
                catch { }
                finally
                {
                    this.plex.Dispose();
                    this.plex = null;
                }
            }
            //kill the supporting processes.
            killDlnaServer();
            killPlexScriptHost();
        }

        private void killDlnaServer()
        {
            //see if its running
            Process[] dlnaServers = Process.GetProcessesByName(PmsMonitor.dlnaServerName);
            if (dlnaServers.Length > 0)
            {
                foreach (Process dlnaServer in dlnaServers)
                {
                    try
                    {
                        dlnaServer.Kill();
                    }
                    catch
                    {
                    }
                    finally
                    {
                        dlnaServer.Dispose();
                    }
                }
                this.OnPlexStatusChange(this, new PlexRunningStatusChangeEventArgs("Plex Dlna Server Stopped."));
            }
        }

        private void killPlexScriptHost()
        {
            //see if its running
            Process[] scriptHosts = Process.GetProcessesByName(PmsMonitor.plexScriptHostName);
            if (scriptHosts.Length > 0)
            {
                foreach (Process scriptHost in scriptHosts)
                {
                    try
                    {
                        scriptHost.Kill();
                    }
                    catch
                    {
                    }
                    finally
                    {
                        scriptHost.Dispose();
                    }
                }
                this.OnPlexStatusChange(this, new PlexRunningStatusChangeEventArgs("Plex Script Host Stopped."));
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
            if(!string.IsNullOrEmpty(this.appPath))
            {
                string location = Path.Combine(this.appPath, "location.txt");
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

                //plex doesnt put this nice stuff in the registry so we need to go hunting for it ourselves
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
        internal delegate void PlexStatusChangeHandler(object sender, PlexRunningStatusChangeEventArgs data);

        /// <summary>
        /// Status change event
        /// </summary>
        internal event PlexStatusChangeHandler PlexStatusChange;

        /// <summary>
        /// Method to fire the status change event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        protected void OnPlexStatusChange(object sender, PlexRunningStatusChangeEventArgs data)
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
