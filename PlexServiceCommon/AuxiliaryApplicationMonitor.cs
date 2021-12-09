using System;
using System.Diagnostics;
using System.IO;

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
        void auxProcess_Exited(object sender, EventArgs e)
        {
            if (_aux.KeepAlive)
            {
                OnStatusChange(new StatusChangeEventArgs(_aux.Name + " has stopped!"));
                //unsubscribe
                _auxProcess.Exited -= auxProcess_Exited;
                End();
                //restart as required
                if (!_stopping)
                {
                    OnStatusChange(new StatusChangeEventArgs("Re-starting " + _aux.Name));
                    //wait some seconds first
                    var autoEvent = new System.Threading.AutoResetEvent(false);
                    var t = new System.Threading.Timer(_ => { ProcStart(); autoEvent.Set(); }, null, 5000, System.Threading.Timeout.Infinite);
                    autoEvent.WaitOne();
                    t.Dispose();
                }
                else
                {
                    OnStatusChange(new StatusChangeEventArgs(_aux.Name + " stopped"));
                    Running = false;
                }
            }
            else
            {
                OnStatusChange(new StatusChangeEventArgs(_aux.Name + " has completed"));
                //unsubscribe
                _auxProcess.Exited -= auxProcess_Exited;
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
            OnStatusChange(new StatusChangeEventArgs("Attempting to start " + _aux.Name));
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
            _auxProcess.Exited += auxProcess_Exited;
            if (_aux.LogOutput) {
                LogWriter.Information("Enabling logging for " + _aux.Name);
                _auxProcess.OutputDataReceived += (_, e) => {
                    if (string.IsNullOrEmpty(e.Data)) return;
                    LogWriter.Debug($"{_aux.Name}:{e.Data}");
                };
            }
            try
            {
                _auxProcess.Start();
                _auxProcess.BeginOutputReadLine();
                OnStatusChange(new StatusChangeEventArgs(_aux.Name + " Started."));
                Running = true;
            }
            catch (Exception ex)
            {
                OnStatusChange(new StatusChangeEventArgs(_aux.Name + " failed to start. " + ex.Message));
            }
            LogWriter.Information("Done starting app.");
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

            OnStatusChange(new StatusChangeEventArgs("Killing " + _aux.Name));
            try
            {
                _auxProcess.Kill();
            } catch (Exception ex) {
                LogWriter.Warning($"Exception stopping auxProc {_aux.Name}: " + ex.Message);
            } finally
            {
                _auxProcess.Dispose();
                _auxProcess = null;
                Running = false;
            }
        }

        #endregion

        #endregion

        #region Events

        //status change
        /// <summary>
        /// Status change delegate
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        public delegate void StatusChangeHandler(object sender, StatusChangeEventArgs data);

        /// <summary>
        /// Status change event
        /// </summary>
        public event StatusChangeHandler StatusChange;

        /// <summary>
        /// Method to fire the status change event
        /// </summary>
        /// <param name="data"></param>
        private void OnStatusChange(StatusChangeEventArgs data) {
            //Check if event has been subscribed to
            //call the event
            StatusChange?.Invoke(this, data);
        }
        #endregion
    }
}
