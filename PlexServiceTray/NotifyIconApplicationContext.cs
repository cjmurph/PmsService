#nullable enable
using System;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using PlexServiceCommon;
using System.ServiceModel;
using System.Windows;
using Serilog;
using Serilog.Events;

namespace PlexServiceTray
{
    /// <summary>
    /// Tray icon context
    /// </summary>
    internal class NotifyIconApplicationContext : ApplicationContext
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private readonly System.ComponentModel.IContainer _components;
        private readonly NotifyIcon _notifyIcon;

        private PlexServiceCommon.Interface.ITrayInteraction? _plexService;

        private SettingsWindow? _settingsWindow;
        private ConnectionSettingsWindow? _connectionSettingsWindow;
        private Settings? _settings;
        private readonly ConnectionSettings _connectionSettings;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && _components != null)
            {
                Disconnect();
                _components.Dispose();
                _notifyIcon.Dispose();
            }
            base.Dispose(disposing);
        }

        public NotifyIconApplicationContext()
        {
            // Moved directly to constructor to suppress nullable warnings.
            _components = new System.ComponentModel.Container();
            _notifyIcon = new NotifyIcon(_components)
            {
                ContextMenuStrip = new ContextMenuStrip
                {
                    ForeColor = Color.FromArgb(232, 234, 237),
                    BackColor = Color.FromArgb(41, 42, 45),
                    RenderMode = ToolStripRenderMode.System
                },
                Icon = new Icon(Properties.Resources.PlexService, SystemInformation.SmallIconSize),
                Text = "Manage Plex Media Server Service",
                Visible = true
            };
            _notifyIcon.MouseClick += NotifyIcon_Click;
            _notifyIcon.MouseDoubleClick += NotifyIcon_DoubleClick;
            _notifyIcon.ContextMenuStrip.Opening += ContextMenuStrip_Opening;
            _connectionSettings = ConnectionSettings.Load();
            Connect();
            // Load settings and store them, versus retrieving them. If updated, we'll listen for an event and re-cache them.
            if (_plexService != null) _settings = _plexService.GetSettings();
        }

        
        /// <summary>
        /// Connect to WCF service
        /// </summary>
        public void Connect()
        {
            //Create a NetTcp binding to the service and set some appropriate timeouts.
            //Use reliable connection so we know when we have been disconnected
            var plexServiceBinding = new NetTcpBinding {
                OpenTimeout = TimeSpan.FromMilliseconds(500),
                CloseTimeout = TimeSpan.FromMilliseconds(500),
                SendTimeout = TimeSpan.FromMilliseconds(500),
                MaxReceivedMessageSize = 2147483647,
                MaxBufferSize = 2147483647,
                MaxBufferPoolSize =  2147483647,
                ReliableSession = {
                    Enabled = true,
                    InactivityTimeout = TimeSpan.FromMinutes(1)
                }
            };
            //Generate the endpoint from the local settings
            var plexServiceEndpoint = new EndpointAddress(_connectionSettings.GetServiceAddress());
            var callback = new TrayCallback();
            callback.StateChange += Callback_StateChange;
            callback.SettingChange += Callback_SettingChange;
            var client = new TrayInteractionClient(callback, plexServiceBinding, plexServiceEndpoint);
            _plexService = null;

            try {
                _plexService = client.ChannelFactory.CreateChannel();
                _plexService.Subscribe();
                //If we lose connection to the service, set the object to null so we will know to reconnect the next time the tray icon is clicked
                _plexService.Faulted += (_, _) => _plexService = null;
                _plexService.Closed += (_, _) => _plexService = null;
            }
            catch (Exception e)
            {
                Logger("Exception connecting: " + e.Message, LogEventLevel.Warning);
                _plexService = null;
            }
        }

        private void Callback_StateChange(object sender, StatusChangeEventArgs e)
        {
            _notifyIcon.ShowBalloonTip(2000, "Plex Service", e.Description, ToolTipIcon.Info);
        }

        private void Callback_SettingChange(object sender, SettingChangeEventArgs e) {
            Logger("Settings updated...");
            _settings = e.Settings;
        }

        /// <summary>
        /// Disconnect from WCF service
        /// </summary>
        private void Disconnect()
        {
            try
            {
                if (_plexService is { State: CommunicationState.Opened }) {
                    _plexService.UnSubscribe();
                    _plexService.Close();
                }
            } catch {
                //
            }
            
            _plexService = null;
        }

        /// <summary>
        /// Open the context menu on right click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NotifyIcon_Click(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                _notifyIcon.ContextMenuStrip.Show();
            }
        }

        /// <summary>
        /// Opens the web manager on a double left click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NotifyIcon_DoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                OpenManager_Click(sender, e);
            }
        }

        /// <summary>
        /// build the context menu each time it opens to ensure appropriate options
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContextMenuStrip_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = false;
            _notifyIcon.ContextMenuStrip.Items.Clear();

            //see if we are still connected.
            if (_plexService == null)
            {
                Connect();
            }
            
            if (_plexService != null) _settings = _plexService.GetSettings();

            if (_plexService is { State: CommunicationState.Opened })
            {
                try
                {
                    var state = _plexService.GetStatus();
                    switch (state)
                    {
                        case PlexState.Running:
                            _notifyIcon.ContextMenuStrip.Items.Add("Stop Plex", null, StopPlex_Click);
                            _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
                            _notifyIcon.ContextMenuStrip.Items.Add("Open Plex...", null, OpenManager_Click);
                            break;
                        case PlexState.Stopped:
                            _notifyIcon.ContextMenuStrip.Items.Add("Start Plex", null, StartPlex_Click);
                            break;
                        case PlexState.Pending:
                            _notifyIcon.ContextMenuStrip.Items.Add("Restart Pending");
                            break;
                        case PlexState.Updating:
                            _notifyIcon.ContextMenuStrip.Items.Add("Plex updating");
                            break;
                        case PlexState.Stopping:
                            _notifyIcon.ContextMenuStrip.Items.Add("Stopping");
                            break;
                        default:
                            _notifyIcon.ContextMenuStrip.Items.Add("Plex state unknown");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning("Exception with strip: " + ex.Message, LogEventLevel.Warning);
                    Disconnect();
                    _notifyIcon.ContextMenuStrip.Items.Add("Unable to connect to service. Check settings");
                }
                if (!string.IsNullOrEmpty(GetDataDir())) _notifyIcon.ContextMenuStrip.Items.Add("PMS Data Folder", null, PMSData_Click);
                if (_settings != null) {
                    _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
                    var auxAppsToLink = _settings.AuxiliaryApplications.Where(aux => !string.IsNullOrEmpty(aux.Url))
                        .ToList();
                    if (auxAppsToLink.Count > 0) {
                        var auxAppsItem = new ToolStripMenuItem
                        {
                            Text = "Auxiliary Applications"
                        };
                        auxAppsToLink.ForEach(aux => {
                            auxAppsItem.DropDownItems.Add(aux.Name, null, (_, _) => {
                                try {
                                    Process.Start(aux.Url);
                                } catch (Exception ex) {
                                    Logger("Aux exception: " + ex.Message, LogEventLevel.Warning);
                                    System.Windows.Forms.MessageBox.Show(ex.Message, "Whoops!", MessageBoxButtons.OK,
                                        MessageBoxIcon.Error);
                                }
                            });
                        });
                        _notifyIcon.ContextMenuStrip.Items.Add(auxAppsItem);
                        _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
                    }

                    var settingsItem = _notifyIcon.ContextMenuStrip.Items.Add("Settings", null, SettingsCommand);
                    if (_settingsWindow != null) {
                        settingsItem.Enabled = false;
                    }
                }
            }
            else
            {
                Disconnect();
                _notifyIcon.ContextMenuStrip.Items.Add("Unable to connect to service. Check settings");
                _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
                _notifyIcon.ContextMenuStrip.Items.Add("PMS Data", null, PMSData_Click);
            }
            var connectionSettingsItem = _notifyIcon.ContextMenuStrip.Items.Add("Connection Settings", null, ConnectionSettingsCommand);
            if (_connectionSettingsWindow != null)
                connectionSettingsItem.Enabled = false;

            _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            
            _notifyIcon.ContextMenuStrip.Items.Add("View Log", null, ViewLogs_Click);
            var aboutItem = _notifyIcon.ContextMenuStrip.Items.Add("About", null, AboutCommand);
            if (AboutWindow.Shown)
                aboutItem.Enabled = false;
            _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            _notifyIcon.ContextMenuStrip.Items.Add("Exit", null, ExitCommand);
        }

        /// <summary>
        /// Show the settings dialogue
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SettingsCommand(object sender, EventArgs e)
        {
            //Don't continue if teh service or settings are null
            if (_plexService is null || _settings is null) return;


            var viewModel = new SettingsWindowViewModel(_settings);
            viewModel.AuxAppStartRequest += (s, _) => 
            {
                if (s is not AuxiliaryApplicationViewModel requester) {
                    return;
                }

                _plexService.StartAuxApp(requester.Name);
                requester.Running = _plexService.IsAuxAppRunning(requester.Name);
            };
            viewModel.AuxAppStopRequest += (s, _) => 
            {
                if (s is not AuxiliaryApplicationViewModel requester) {
                    return;
                }

                _plexService.StopAuxApp(requester.Name);
                requester.Running = _plexService.IsAuxAppRunning(requester.Name);
            };
            viewModel.AuxAppCheckRunRequest += (s, _) => 
            {
                if (s is AuxiliaryApplicationViewModel requester) {
                    requester.Running = _plexService.IsAuxAppRunning(requester.Name);
                }
            };
            
            _settingsWindow = new SettingsWindow(viewModel, _plexService);
            if (_settingsWindow.ShowDialog() == true)
            {
                try
                {
                    _plexService.SetSettings(viewModel.WorkingSettings);
                    _plexService.GetStatus();
                }
                catch(Exception ex)
                {
                    Logger("Exception saving settings: " + ex.Message, LogEventLevel.Warning);
                    Disconnect();
                    System.Windows.MessageBox.Show("Unable to save settings" + Environment.NewLine + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }     
                var oldPort = viewModel.WorkingSettings.ServerPort;

                //The only setting that would require a restart of the service is the listening port.
                //If that gets changed notify the user to restart the service from the service snap in
                if (viewModel.WorkingSettings.ServerPort != oldPort)
                {
                    System.Windows.MessageBox.Show("Server port changed! You will need to restart the service from the services snap in for the change to be applied", "Settings changed!", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            _settingsWindow = null;
        }

        private void Logger(string message, LogEventLevel level = LogEventLevel.Debug) 
        {
            if (_plexService is null) return;

            try 
            {
                if (_plexService.State == CommunicationState.Opened) {
                    _plexService.LogMessage(message, level);
                }
            }
            catch 
            {
                // Ignored
            }
        }
        private string GetTheme() 
        {
            if (_plexService is null || _settings is null)  return "Dark.Amber";
            
            return _settings.Theme;
        }
        
        /// <summary>
        /// Show the connection settings dialogue
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ConnectionSettingsCommand(object sender, EventArgs e) 
        {
            var theme = GetTheme();
            _connectionSettingsWindow = new ConnectionSettingsWindow(theme);
            if (_connectionSettingsWindow.ShowDialog() == true)
            {
                //if the user saved the settings, then reconnect using the new values
                try
                {
                    Disconnect();
                    Connect();
                } 
                catch (Exception ex)
                {
                    Logger("Exception on connection setting command" + ex.Message, LogEventLevel.Warning);
                }
            }
            _connectionSettingsWindow = null;
        }

        /// <summary>
        /// Open the About dialog
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AboutCommand(object sender, EventArgs e)
        {
            AboutWindow.ShowAboutDialog(GetTheme());
        }

        /// <summary>
        /// Close the notify icon
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ExitCommand(object sender, EventArgs e)
        {
            Disconnect();
            ExitThread();
        }

        /// <summary>
        /// Start Plex
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StartPlex_Click(object sender, EventArgs e) 
        {
            //start it
            if (_plexService is null) return;

            try
            {
                _plexService.Start();
            }
            catch (Exception ex) 
            {
                Logger("Exception on startPlex click: " + ex, LogEventLevel.Warning);
                Disconnect();
            }
        }

        /// <summary>
        /// Stop Plex
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StopPlex_Click(object sender, EventArgs e) 
        {
            //stop it
            if (_plexService == null) {
                return;
            }

            try
            {
                _plexService.Stop();
            }
            catch (Exception ex)
            {
                Logger("Exception stopping Plex..." + ex.Message);
                Disconnect();
            }
        }

        /// <summary>
        /// Try to open the web manager
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenManager_Click(object sender, EventArgs e)
        {
            //this is pretty old school, we should probably go to app.plex.tv...
            //The web manager should be located at the server address in the connection settings
            Process.Start("http://" + _connectionSettings.ServerAddress + ":32400/web");
        }

        /// <summary>
        /// View the server log file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ViewLogs_Click(object sender, EventArgs e) 
        {
            var sa = _connectionSettings.ServerAddress;
            // Use windows shell to open log file in whatever app the user uses...
            var fileToOpen = string.Empty;
            try
            {
                // If we're local to the service, just open the file.
                if (sa is "127.0.0.1" or "0.0.0.0" or "localhost") {
                    fileToOpen = _plexService?.GetLogPath() ?? LogWriter.LogFile;
                } else {
                    Logger("Requesting log.");
                    // Otherwise, request the log data from the server, save it to a temp file, and open that.
                    var logData = _plexService?.GetLog();
                    if (logData == null) {
                        Logger("No log data received.", LogEventLevel.Warning);
                        return;
                    }
                    Logger("Data received: " + logData);
                    var tmpPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    var tmpFile = Path.Combine(tmpPath, "pmss.log");
                    Logger("Writing to " + tmpFile);
                    File.WriteAllText(tmpFile, logData);
                    if (File.Exists(tmpFile)) fileToOpen = tmpFile;
                }

                if (string.IsNullOrEmpty(fileToOpen)) return;
                
                var process = new Process();
                process.StartInfo = new ProcessStartInfo {
                    UseShellExecute = true,
                    FileName = fileToOpen
                };
                process.Start();
            }
            catch (Exception ex) 
            {
                Logger("Exception viewing logs: " + ex.Message);
                Disconnect();
            }
        }
        
        private void PMSData_Click(object sender, EventArgs e) 
        {
            //Open a windows explorer window to PMS data
            var dir = GetDataDir();
            try 
            {                
                if (!string.IsNullOrEmpty(dir)) Process.Start($@"{dir}");
            }
            catch (Exception ex)
            {
                Logger($"Error opening PMS Data folder at {dir}: " + ex.Message, LogEventLevel.Warning);
                Disconnect();
            }
        }

        private string GetDataDir() 
        {
            var dir = string.Empty;
            var path = _plexService?.GetPmsDataPath() ?? string.Empty;
            if (string.IsNullOrEmpty(path)) return dir;
            // If we're not local, see if we can access PMS data dir over UNC
            if (!_connectionSettings.IsLocal) 
            {
                var drive = path.Substring(0, 1);
                var ext = path.Substring(3);
                var unc = Path.Combine("\\\\" + _connectionSettings.ServerAddress, drive + "$", ext);
                if (Directory.Exists(unc)) dir = unc;
            } 
            else 
            {
                dir = path;
            }

            return dir;
        }

    }
}
