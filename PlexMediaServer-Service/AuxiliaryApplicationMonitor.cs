using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace PlexMediaServer_Service
{
    internal class AuxiliaryApplicationMonitor
    {
        /// <summary>
        /// Auxiliary process
        /// </summary>
        private Process auxProcess;
        /// <summary>
        /// Flag for actual stop rather than crash we should attempt to restart from
        /// </summary>
        private bool stopping;
        /// <summary>
        /// Auxiliary Application to monitor
        /// </summary>
        private AuxiliaryApplication aux;

        internal AuxiliaryApplicationMonitor(AuxiliaryApplication aux)
        {
            this.aux = aux;
        }

        #region Start

        /// <summary>
        /// Start monitoring plex
        /// </summary>
        internal void Start()
        {
            this.stopping = false;

            //every time a start attempt is made, check for the existance of the auto start registry key and remove it.
            if(!string.IsNullOrEmpty(this.aux.FilePath) && File.Exists(this.aux.FilePath))
            {
                this.start();
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
            this.end();
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
            if (this.aux.KeepAlive)
            {
                this.OnStatusChange(this, new StatusChangeEventArgs(aux.Name + " has stopped!"));
                //unsubscribe
                this.auxProcess.Exited -= this.auxProcess_Exited;
                this.end();
                //restart as required
                if (!this.stopping)
                {
                    this.OnStatusChange(this, new StatusChangeEventArgs("Re-starting " + aux.Name));
                    //wait some seconds first
                    System.Threading.Thread.Sleep(10000);
                    this.start();
                }
                else
                {
                    this.OnStatusChange(this, new StatusChangeEventArgs(aux.Name + " stopped"));
                }
            }
            else
            {
                this.OnStatusChange(this, new StatusChangeEventArgs(aux.Name + " has completed"));
                //unsubscribe
                this.auxProcess.Exited -= this.auxProcess_Exited;
                this.auxProcess.Dispose();
            }
        }

        #endregion

        #region Start methods

        /// <summary>
        /// Start a new/get a handle on existing Plex process
        /// </summary>
        private void start()
        {
            this.OnStatusChange(this, new StatusChangeEventArgs("Attempting to start " + aux.Name));
            if (this.auxProcess == null)
            {
                //we dont care if this is already running, depending on teh application, this could cause lots of issues but hey... 
                
                //Auxiliary process
                this.auxProcess = new Process();
                auxProcess.StartInfo.FileName = aux.FilePath;
                auxProcess.StartInfo.UseShellExecute = false;
                auxProcess.StartInfo.Arguments = aux.Argument;
                this.auxProcess.EnableRaisingEvents = true;
                this.auxProcess.Exited += new EventHandler(auxProcess_Exited);
                try
                {
                    this.auxProcess.Start();
                    this.OnStatusChange(this, new StatusChangeEventArgs(aux.Name + " Started."));
                }
                catch (Exception ex)
                {
                    this.OnStatusChange(this, new StatusChangeEventArgs(aux.Name + " failed to start. " + ex.Message));
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

            if (this.auxProcess != null)
            {
                this.OnStatusChange(this, new StatusChangeEventArgs("Killing " + aux.Name));
                try
                {
                    this.auxProcess.Kill();
                }
                catch { }
                finally
                {
                    this.auxProcess.Dispose();
                    this.auxProcess = null;
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
        internal delegate void StatusChangeHandler(object sender, StatusChangeEventArgs data);

        /// <summary>
        /// Status change event
        /// </summary>
        internal event StatusChangeHandler StatusChange;

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
