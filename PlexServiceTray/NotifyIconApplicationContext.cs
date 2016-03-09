using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.ServiceProcess;
using System.Diagnostics;
using System.Reflection;
using System.IO;
using PlexServiceCommon;
using System.ServiceModel;

namespace PlexServiceTray
{
    /// <summary>
    /// Tray icon context
    /// </summary>
    class NotifyIconApplicationContext : ApplicationContext
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer _components = null;

        private System.Windows.Forms.NotifyIcon _notifyIcon;

        private readonly static TimeSpan _timeOut = TimeSpan.FromSeconds(2);

        private PlexServiceCommon.Interface.ITrayInteraction _plexService;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (_components != null))
            {
                disconnect();
                _components.Dispose();
                _notifyIcon.Dispose();
            }
            base.Dispose(disposing);
        }

        public NotifyIconApplicationContext()
        {
            initializeContext();
            connect();
        }

        /// <summary>
        /// Setup our tray icon
        /// </summary>
        private void initializeContext()
        {
            _components = new System.ComponentModel.Container();
            _notifyIcon = new NotifyIcon(_components);
            _notifyIcon.ContextMenuStrip = new ContextMenuStrip();
            _notifyIcon.Icon = Properties.Resources.PlexService;
            _notifyIcon.Text = "Manage Plex Media Server Service";
            _notifyIcon.Visible = true;
            _notifyIcon.Click += new EventHandler(notifyIcon_Click);
            _notifyIcon.DoubleClick += new EventHandler(openManager_Click);
            _notifyIcon.ContextMenuStrip.Opening += ContextMenuStrip_Opening;
        }

        /// <summary>
        /// Connect to WCF service
        /// </summary>
        private void connect()
        {
            var localSettings = ConnectionSettings.Load();
            //Create a NetTcp binding to the service and set some appropriate timeouts.
            //Use reliable connection so we know when we have been disconnected
            var plexServiceBinding = new NetTcpBinding();
            plexServiceBinding.OpenTimeout = _timeOut;
            plexServiceBinding.CloseTimeout = _timeOut;
            plexServiceBinding.SendTimeout = _timeOut;
            plexServiceBinding.ReliableSession.Enabled = true;
            plexServiceBinding.ReliableSession.InactivityTimeout = TimeSpan.FromMinutes(1);
            //Generate the endpoint from the local settings
            var plexServiceEndpoint = new EndpointAddress(localSettings.getServiceAddress());
            //Make a channel factory so we can create the link to the service
            var plexServiceChannelFactory = new ChannelFactory<PlexServiceCommon.Interface.ITrayInteraction>(plexServiceBinding, plexServiceEndpoint);

            _plexService = null;

            try
            {
                _plexService = plexServiceChannelFactory.CreateChannel();
                //If we lose connection to the service, set the object to null so we will know to reconnect the next time the tray icon is clicked
                ((ICommunicationObject)_plexService).Faulted += (s, e) => _plexService = null;
            }
            catch
            {
                if (_plexService != null)
                {
                    _plexService = null;
                }
            }
        }

        /// <summary>
        /// Disconnect from WCF service
        /// </summary>
        private void disconnect()
        {
            //try and be nice...
            if (_plexService != null)
            {
                try
                {
                    ((ICommunicationObject)_plexService).Close();
                }
                catch { }
            }
            _plexService = null;
        }

        /// <summary>
        /// Open the context menu if we left click too
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void notifyIcon_Click(object sender, EventArgs e)
        {
            MethodInfo mi = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
            mi.Invoke(_notifyIcon, null);
        }

        /// <summary>
        /// build the context menu each time it opens to ensure appropriate options
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void ContextMenuStrip_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = false;
            _notifyIcon.ContextMenuStrip.Items.Clear();

            //see if we are still connected.
            if (_plexService == null)
            {
                connect();
            }

            if (_plexService != null)
            {
                try
                {
                    var state = _plexService.GetStatus();
                    switch (state)
                    {
                        case PlexState.Running:
                            _notifyIcon.ContextMenuStrip.Items.Add("Open Web Manager", null, openManager_Click);
                            _notifyIcon.ContextMenuStrip.Items.Add("Stop Plex", null, stopPlex_Click);
                            break;
                        case PlexState.Stopped:
                            _notifyIcon.ContextMenuStrip.Items.Add("Start Plex", null, startPlex_Click);
                            break;
                        case PlexState.Pending:
                            _notifyIcon.ContextMenuStrip.Items.Add("Restart Pending");
                            break;
                        case PlexState.Stopping:
                            _notifyIcon.ContextMenuStrip.Items.Add("Stopping");
                            break;
                        default:
                            _notifyIcon.ContextMenuStrip.Items.Add("Plex state unknown");
                            break;
                    }
                    _notifyIcon.ContextMenuStrip.Items.Add("View Logs", null, viewLogs_Click);
                    _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
                    _notifyIcon.ContextMenuStrip.Items.Add("Settings", null, settingsCommand);
                }
                catch
                {
                    disconnect();
                    _notifyIcon.ContextMenuStrip.Items.Add("Unable to connect to service. Check settings");
                }
            }
            else
            {
                disconnect();
                _notifyIcon.ContextMenuStrip.Items.Add("Unable to connect to service. Check settings");

            }
            _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            _notifyIcon.ContextMenuStrip.Items.Add("Connection Settings", null, connectionSettingsCommand);
            _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            _notifyIcon.ContextMenuStrip.Items.Add("Exit", null, exitCommand);
        }

        /// <summary>
        /// Show the settings dialogue
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void settingsCommand(object sender, EventArgs e)
        {
            if (_plexService != null)
            {
                Settings settings = null;
                try
                {
                    settings = Settings.Deserialize(_plexService.GetSettings());
                }
                catch 
                {
                    disconnect();
                }

                if (settings != null)
                {
                    //Save the current server port setting for reference
                    int oldPort = settings.ServerPort;
                    SettingsWindowViewModel settingsViewModel = new SettingsWindowViewModel(settings);
                    SettingsWindow settingsWindow = new SettingsWindow(settingsViewModel);
                    if (settingsWindow.ShowDialog() == true)
                    {
                        PlexState status = PlexState.Pending;
                        try
                        {
                            _plexService.SetSettings(settingsViewModel.WorkingSettings.Serialize());
                            status = _plexService.GetStatus();
                        }
                        catch(Exception ex)
                        {
                            disconnect();
                            MessageBox.Show("Unable to save settings" + Environment.NewLine + ex.Message);
                        }     
                        //The only setting that would require a restart of the service is the listening port.
                        //If that gets changed notify the user to restart the service from the service snap in
                        if (settingsViewModel.WorkingSettings.ServerPort != oldPort)
                        {
                            MessageBox.Show("Server port changed! You will need to restart the service from the services snap in for the change to be applied", "Settings changed!", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Show the connection settings dialogue
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void connectionSettingsCommand(object sender, EventArgs e)
        {
            ConnectionSettingsWindow connectionSettingsWindow = new ConnectionSettingsWindow();
            if (connectionSettingsWindow.ShowDialog() == true)
            {
                //if the user saved the settings, then reconnect using the new values
                try
                {
                    disconnect();
                    connect();
                }
                catch { }
            }
        }

        /// <summary>
        /// Close the notify icon
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void exitCommand(object sender, EventArgs e)
        {
            disconnect();
            ExitThread();
        }

        /// <summary>
        /// Start Plex
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void startPlex_Click(object sender, EventArgs e)
        {
            //start it
            if (_plexService != null)
            {
                try
                {
                    _plexService.Start();
                }
                catch 
                {
                    disconnect();
                }
            }
        }

        /// <summary>
        /// Stop Plex
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void stopPlex_Click(object sender, EventArgs e)
        {
            //stop it
            if (_plexService != null)
            {
                try
                {
                    _plexService.Stop();
                }
                catch 
                {
                    disconnect();
                }
            }
        }

        /// <summary>
        /// Try to open the web manager
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void openManager_Click(object sender, EventArgs e)
        {
            //The web manager should be located at the server address in the connection settings
            Process.Start("http://" + ConnectionSettings.Load().ServerAddress + ":32400/web");
        }

        /// <summary>
        /// View the server log file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void viewLogs_Click(object sender, EventArgs e)
        {
            //Show the data from the server in notepad, but don't save it to disk locally.
            try
            {
                NotepadHelper.ShowMessage(_plexService.GetLog(), "Plex Service Log");
            }
            catch
            {
                disconnect();
            }
        }

    }
}
