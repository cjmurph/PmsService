using System;
using System.Diagnostics;
using System.IO;
using Serilog;

namespace PlexServiceCommon
{
    /// <summary>
    /// Class that runs up and monitors the life of auxiliary applications
    /// </summary>
    public class AuxiliaryApplicationMonitor
    {
        public string Name => _aux.Name;

        public bool Running { get; private set; }

        /// <summary>
        /// Auxiliary process
        /// </summary>
        private Process _auxProcess;
        
        /// <summary>
        /// Flag for actual stop rather than crash we should attempt to restart from
        /// </summary>
        private bool _stopping;

        /// <summary>
        /// Auxiliary Application to monitor
        /// </summary>
        private readonly AuxiliaryApplication _aux;

        public AuxiliaryApplicationMonitor(AuxiliaryApplication aux)
        {
            _aux = aux;
        }

        #region Start

        /// <summary>
        /// Start monitoring plex
        /// </summary>
        public void Start()
        {
            _stopping = false;

            if(!string.IsNullOrEmpty(_aux.FilePath) && File.Exists(_aux.FilePath))
            {
                ProcStart();
            }
        }

        #endregion

        #region Stop

        /// <summary>
        /// Stop the monitor and kill the processes
        /// </summary>
        public void Stop()
        {
            _stopping = true;
            End();
        }

        #endregion

        #region Process handling

        #region Exit events

        /// <summary>
        /// This event fires when the process we have a reference to exits
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void AuxProcess_Exited(object sender, EventArgs e)
        {
            if (_aux.KeepAlive) {
                Log.Information(_aux.Name + " has stopped!");
                //unsubscribe
                _auxProcess.Exited -= AuxProcess_Exited;
                End();
                //restart as required
                if (!_stopping)
                {
                    Log.Information("Re-starting " + _aux.Name);
                    //wait some seconds first
                    var autoEvent = new System.Threading.AutoResetEvent(false);
                    var t = new System.Threading.Timer(_ => { ProcStart(); autoEvent.Set(); }, null, 5000, System.Threading.Timeout.Infinite);
                    autoEvent.WaitOne();
                    t.Dispose();
                }
                else
                {
                    Log.Information(_aux.Name + " stopped");
                    Running = false;
                }
            }
            else
            {
                Log.Information(_aux.Name + " has completed");
                //unsubscribe
                _auxProcess.Exited -= AuxProcess_Exited;
                _auxProcess.Dispose();
                Running = false;
            }
        }

        #endregion

        #region Start methods

        /// <summary>
        /// Start a new/get a handle on existing process
        /// </summary>
        private void ProcStart()
        {
            Log.Information("Attempting to start " + _aux.Name);
            if (_auxProcess != null) {
                return;
            }
            //we dont care if this is already running, depending on the application, this could cause lots of issues but hey... 
                
            //Auxiliary process
            _auxProcess = new Process();
            _auxProcess.StartInfo.FileName = _aux.FilePath;
            _auxProcess.StartInfo.WorkingDirectory = _aux.WorkingFolder;
            _auxProcess.StartInfo.UseShellExecute = false;
            _auxProcess.StartInfo.Arguments = _aux.Argument;
            _auxProcess.EnableRaisingEvents = true;
            _auxProcess.StartInfo.RedirectStandardError = true;
            _auxProcess.StartInfo.RedirectStandardOutput = true;
            _auxProcess.Exited += AuxProcess_Exited;
            if (_aux.LogOutput) {
                Log.Information("Enabling logging for " + _aux.Name);
                _auxProcess.OutputDataReceived += (_, e) => {
                    if (string.IsNullOrEmpty(e.Data)) return;
                    Log.Debug($"{_aux.Name}:{e.Data}");
                };
            }
            try
            {
                _auxProcess.Start();
                _auxProcess.BeginOutputReadLine();
                Log.Information(_aux.Name + " Started.");
                Running = true;
            }
            catch (Exception ex)
            {
                Log.Information(_aux.Name + " failed to start. " + ex.Message);
            }
            Log.Information("Done starting app.");
        }


        #endregion

        #region End methods

        /// <summary>
        /// Kill the plex process
        /// </summary>
        private void End() {
            if (_auxProcess == null) {
                return;
            }

            Log.Information("Killing " + _aux.Name);
            try
            {
                _auxProcess.Kill();
            } catch (Exception ex) {
                Log.Warning($"Exception stopping auxProc {_aux.Name}: " + ex.Message);
            } finally
            {
                _auxProcess.Dispose();
                _auxProcess = null;
                Running = false;
            }
        }

        #endregion

        #endregion

        
    }
}
