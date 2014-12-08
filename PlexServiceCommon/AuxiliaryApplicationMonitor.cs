using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace PlexServiceCommon
{
    /// <summary>
    /// Class that runs up and monitors the life of auxilliary applications
    /// </summary>
    public class AuxiliaryApplicationMonitor
    {
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
        private AuxiliaryApplication _aux;

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

            //every time a start attempt is made, check for the existance of the auto start registry key and remove it.
            if(!string.IsNullOrEmpty(_aux.FilePath) && File.Exists(_aux.FilePath))
            {
                start();
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
            end();
        }

        #endregion

        #region Process handling

        #region Exit events

        /// <summary>
        /// This event fires when the process we have a refrence to exits
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void auxProcess_Exited(object sender, EventArgs e)
        {
            if (_aux.KeepAlive)
            {
                OnStatusChange(this, new StatusChangeEventArgs(_aux.Name + " has stopped!"));
                //unsubscribe
                _auxProcess.Exited -= auxProcess_Exited;
                end();
                //restart as required
                if (!_stopping)
                {
                    OnStatusChange(this, new StatusChangeEventArgs("Re-starting " + _aux.Name));
                    //wait some seconds first
                    System.Threading.Thread.Sleep(10000);
                    start();
                }
                else
                {
                    OnStatusChange(this, new StatusChangeEventArgs(_aux.Name + " stopped"));
                }
            }
            else
            {
                OnStatusChange(this, new StatusChangeEventArgs(_aux.Name + " has completed"));
                //unsubscribe
                _auxProcess.Exited -= this.auxProcess_Exited;
                _auxProcess.Dispose();
            }
        }

        #endregion

        #region Start methods

        /// <summary>
        /// Start a new/get a handle on existing Plex process
        /// </summary>
        private void start()
        {
            OnStatusChange(this, new StatusChangeEventArgs("Attempting to start " + _aux.Name));
            if (_auxProcess == null)
            {
                //we dont care if this is already running, depending on teh application, this could cause lots of issues but hey... 
                
                //Auxiliary process
                _auxProcess = new Process();
                _auxProcess.StartInfo.FileName = _aux.FilePath;
                _auxProcess.StartInfo.UseShellExecute = false;
                _auxProcess.StartInfo.Arguments = _aux.Argument;
                _auxProcess.EnableRaisingEvents = true;
                _auxProcess.Exited += new EventHandler(auxProcess_Exited);
                try
                {
                    _auxProcess.Start();
                    OnStatusChange(this, new StatusChangeEventArgs(_aux.Name + " Started."));
                }
                catch (Exception ex)
                {
                    OnStatusChange(this, new StatusChangeEventArgs(_aux.Name + " failed to start. " + ex.Message));
                }
            }
        }

        #endregion

        #region End methods

        /// <summary>
        /// Kill the plex process
        /// </summary>
        private void end()
        {

            if (_auxProcess != null)
            {
                OnStatusChange(this, new StatusChangeEventArgs("Killing " + _aux.Name));
                try
                {
                    _auxProcess.Kill();
                }
                catch { }
                finally
                {
                    _auxProcess.Dispose();
                    _auxProcess = null;
                }
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
        /// <param name="sender"></param>
        /// <param name="data"></param>
        protected void OnStatusChange(object sender, StatusChangeEventArgs data)
        {
            //Check if event has been subscribed to
            if (StatusChange != null)
            {
                //call the event
                StatusChange(this, data);
            }
        }
        #endregion
    }
}
