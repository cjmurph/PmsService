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

        private readonly static TimeSpan _timeOut = TimeSpan.FromMilliseconds(2000);

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
            _notifyIcon.ContextMenuStrip.Opening += ContextMenuStrip_Opening;
        }

        /// <summary>
        /// Connect to WCF service
        /// </summary>
        private void connect()
        {
            var localSettings = ConnectionSettings.Load();
            var plexServiceBinding = new NetTcpBinding();
            var plexServiceEndpoint = new EndpointAddress(localSettings.getServiceAddress());
            var plexServiceChannelFactory = new ChannelFactory<PlexServiceCommon.Interface.ITrayInteraction>(plexServiceBinding, plexServiceEndpoint);

            _plexService = null;

            try
            {
                _plexService = plexServiceChannelFactory.CreateChannel();
                ((ICommunicationObject)_plexService).Open(_timeOut);
            }
            catch
            {
                if (_plexService != null)
                {
                    ((ICommunicationObject)_plexService).Abort();
                }
            }
        }

        /// <summary>
        /// Disconnect from WCF service
        /// </summary>
        private void disconnect()
        {
            if (_plexService != null && connected())
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
        /// Check connection to WCF service
        /// </summary>
        /// <returns></returns>
        private bool connected()
        {
            if (_plexService != null)
            {
                try
                {
                    if (((ICommunicationObject)_plexService).State == CommunicationState.Opened)
                    {
                        return true;
                    }
                }
                catch { }
            }
            return false;
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

            //take a hit if we're not connected just in case we had a temporary loss of comms
            if (!connected())
            {
                //try again just in case
                connect();
            }

            if (connected())
            {
                try
                {
                    string state = _plexService.GetStatus();
                    switch (state)
                    {
                        case "Stopped":
                            _notifyIcon.ContextMenuStrip.Items.Add("Start Service", null, startService_Click);
                            break;
                        case "Running":
                            _notifyIcon.ContextMenuStrip.Items.Add("Open Web Manager", null, openManager_Click);
                            _notifyIcon.ContextMenuStrip.Items.Add("Stop Service", null, stopService_Click);
                            break;
                        default:
                            _notifyIcon.ContextMenuStrip.Items.Add("Plex state unknown", null, openManager_Click);
                            break;
                    }
                    _notifyIcon.ContextMenuStrip.Items.Add("View Logs", null, viewLogs_Click);
                    _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
                    _notifyIcon.ContextMenuStrip.Items.Add("Settings", null, settingsCommand);
                }
                catch
                {
                    _notifyIcon.ContextMenuStrip.Items.Add("Unable to connect to service. Check settings");
                }
            }
            else
            {
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
            if (connected())
            {
                try
                {
                    Settings settings = Settings.Deserialize(_plexService.GetSettings());
                    SettingsWindowViewModel settingsViewModel = new SettingsWindowViewModel(settings);
                    SettingsWindow settingsWindow = new SettingsWindow(settingsViewModel);
                    if (settingsWindow.ShowDialog() == true && connected())
                    {
                        _plexService.SetSettings(settingsViewModel.WorkingSettings.Serialize());
                        if (_plexService.GetStatus() == "Running")
                        {
                            if (MessageBox.Show("Settings changed, do you want to restart the service?", "Settings changed!", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                            {
                                try
                                {
                                    _plexService.Restart();
                                }
                                catch (System.ComponentModel.Win32Exception ex)
                                {
                                    MessageBox.Show("Unable to restart service" + Environment.NewLine + ex.Message);
                                }
                            }
                        }
                    }
                }
                catch { }
            }
            else
            {
                ConnectionSettingsWindow connectionSettingsWindow = new ConnectionSettingsWindow();
                if (connectionSettingsWindow.ShowDialog() == true)
                {
                    try
                    {
                        disconnect();
                        connect();
                    }
                    catch { }
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
        private void startService_Click(object sender, EventArgs e)
        {
            //start it
            if (connected())
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
        private void stopService_Click(object sender, EventArgs e)
        {
            //stop it
            if (connected())
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
            Process.Start("http://" + ConnectionSettings.Load().ServerAddress + ":32400/web");
        }

        /// <summary>
        /// View the server log file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void viewLogs_Click(object sender, EventArgs e)
        {
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
