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

namespace PlexServiceTray
{
    class NotifyIconApplicationContext : ApplicationContext
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer _components = null;

        private System.Windows.Forms.NotifyIcon _notifyIcon;

        private readonly static string _serviceName = "PmsService";

        private readonly static TimeSpan _timeOut = TimeSpan.FromMilliseconds(30000);

        private string _logFile;

        private bool _serviceDetected;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (_components != null))
            {
                _components.Dispose();
                _notifyIcon.Dispose();
            }
            base.Dispose(disposing);
        }

        public NotifyIconApplicationContext()
        {
            initializeContext();
        }

        /// <summary>
        /// Setup our tray icon
        /// </summary>
        private void initializeContext()
        {
            _logFile = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Plex Service\plexServiceLog.txt");

            _components = new System.ComponentModel.Container();
            _notifyIcon = new NotifyIcon(_components);
            _notifyIcon.ContextMenuStrip = new ContextMenuStrip();
            _notifyIcon.Icon = Properties.Resources.PlexService;// new Icon(GetType().Module.Assembly.GetManifestResourceStream("PlexServiceTray.PlexService.ico"));
            _notifyIcon.Text = "Manage Plex Media Server Service";
            _notifyIcon.Visible = true;
            _notifyIcon.Click += new EventHandler(notifyIcon_Click);
            _notifyIcon.ContextMenuStrip.Opening += new System.ComponentModel.CancelEventHandler(ContextMenuStrip_Opening);
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

            using (ServiceController pmsService = new ServiceController(_serviceName))
            {
                try
                {
                    switch (pmsService.Status)
                    {
                        case ServiceControllerStatus.Stopped:
                            _notifyIcon.ContextMenuStrip.Items.Add("Start Service", null, startService_Click);
                            break;
                        case ServiceControllerStatus.Running:
                            _notifyIcon.ContextMenuStrip.Items.Add("Open Web Manager", null, openManager_Click);
                            _notifyIcon.ContextMenuStrip.Items.Add("Stop Service", null, stopService_Click);
                            break;
                        default:
                            _notifyIcon.ContextMenuStrip.Items.Add("Service: " + pmsService.Status.ToString());
                            break;
                    }
                    _serviceDetected = true;
                    _notifyIcon.ContextMenuStrip.Items.Add("View Logs", null, viewLogs_Click);
                }
                catch
                {
                    _serviceDetected = false;
                    _notifyIcon.ContextMenuStrip.Items.Add("Service does not appear to be installed");
                }
            }
            _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            _notifyIcon.ContextMenuStrip.Items.Add("Settings", null, settingsCommand);
            _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            _notifyIcon.ContextMenuStrip.Items.Add("Exit", null, exitCommand);
        }

        private void settingsCommand(object sender, EventArgs e)
        {
            SettingsWindow settings = new SettingsWindow();
            if (settings.ShowDialog() == true && _serviceDetected)
            {
                using (ServiceController pmsService = new ServiceController(_serviceName))
                {
                    if (pmsService.Status == ServiceControllerStatus.Running)
                    {
                        if (MessageBox.Show("Settings changed, do you want to restart the service?", "Settings changed!", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        {
                            try
                            {
                                pmsService.Stop();
                                pmsService.WaitForStatus(ServiceControllerStatus.Stopped, _timeOut);
                                pmsService.Start();
                                pmsService.WaitForStatus(ServiceControllerStatus.Running, _timeOut);
                            }
                            catch (System.ComponentModel.Win32Exception ex)
                            {
                                MessageBox.Show("Unable to restart service" + Environment.NewLine + ex.Message);
                            }

                        }
                    }

                }
            }
        }

        private void exitCommand(object sender, EventArgs e)
        {
            ExitThread();
        }

        private void startService_Click(object sender, EventArgs e)
        {
            //start it
            using (ServiceController pmsService = new ServiceController(_serviceName))
            {
                try
                {
                    pmsService.Start();
                    pmsService.WaitForStatus(ServiceControllerStatus.Running, _timeOut);
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    MessageBox.Show("Unable to start service" + Environment.NewLine + ex.Message);
                }
            }
        }

        private void stopService_Click(object sender, EventArgs e)
        {
            //stop it
            using (ServiceController pmsService = new ServiceController(_serviceName))
            {
                try
                {
                    pmsService.Stop();
                    pmsService.WaitForStatus(ServiceControllerStatus.Stopped, _timeOut);
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    MessageBox.Show("Unable to stop service" + Environment.NewLine + ex.Message);
                }
            }
        }

        private void openManager_Click(object sender, EventArgs e)
        {
            Process.Start("http://localhost:32400/web");
        }

        private void viewLogs_Click(object sender, EventArgs e)
        {
            if (File.Exists(_logFile))
            {
                Process.Start(_logFile);
            }
            else
            {
                MessageBox.Show("Unable to find log file");
            }
        }

    }
}
