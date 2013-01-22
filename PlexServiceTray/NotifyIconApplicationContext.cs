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
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.NotifyIcon notifyIcon;

        private ServiceController pmsService;// = new ServiceController(serviceName);

        private readonly static string serviceName = "PmsService";

        private readonly static TimeSpan timeOut = TimeSpan.FromMilliseconds(30000);

        private string logFile;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                this.components.Dispose();
                this.notifyIcon.Dispose();
                this.pmsService.Dispose();
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
            this.logFile = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Plex Service\plexServiceLog.txt");

            this.components = new System.ComponentModel.Container();
            this.pmsService = new ServiceController(serviceName);
            this.notifyIcon = new NotifyIcon(this.components);
            this.notifyIcon.ContextMenuStrip = new ContextMenuStrip();
            this.notifyIcon.Icon = new Icon("PlexService.ico");
            this.notifyIcon.Text = "Manage Plex Media Server Service";
            this.notifyIcon.Visible = true;
            this.notifyIcon.Click += new EventHandler(notifyIcon_Click);
            this.notifyIcon.ContextMenuStrip.Opening += new System.ComponentModel.CancelEventHandler(ContextMenuStrip_Opening);
        }

        /// <summary>
        /// Open the context menu if we left click too
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void notifyIcon_Click(object sender, EventArgs e)
        {
            MethodInfo mi = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
            mi.Invoke(this.notifyIcon, null);
        }

        /// <summary>
        /// build the context menu each time it opens to ensure appropriate options
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void ContextMenuStrip_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = false;
            this.notifyIcon.ContextMenuStrip.Items.Clear();
            bool? isRunning = this.serviceIsRunning();
            if (isRunning == false)
            {
                this.notifyIcon.ContextMenuStrip.Items.Add("Start Service", null, startService_Click);
                this.notifyIcon.ContextMenuStrip.Items.Add("View Logs", null, viewLogs_Click);
            }
            else if (isRunning == true)
            {
                this.notifyIcon.ContextMenuStrip.Items.Add("Open Web Manager", null, openManager_Click);
                this.notifyIcon.ContextMenuStrip.Items.Add("Stop Service", null, stopService_Click);
                this.notifyIcon.ContextMenuStrip.Items.Add("View Logs", null, viewLogs_Click);
            }
            else
            {
                this.notifyIcon.ContextMenuStrip.Items.Add("Service does not appear to be installed");
            }
            this.notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            this.notifyIcon.ContextMenuStrip.Items.Add("Exit", null, exitCommand);
        }

        /// <summary>
        /// See if the service is running
        /// </summary>
        /// <returns></returns>
        private bool? serviceIsRunning()
        {
            bool? result = null;
            try
            {
                if (this.pmsService.Status == ServiceControllerStatus.Running)
                {
                    result = true;
                }
                else
                {
                    result = false;
                }
            }
            catch
            {
            }
            return result;
        }

        private void exitCommand(object sender, EventArgs e)
        {
            ExitThread();
        }

        private void startService_Click(object sender, EventArgs e)
        {
            //start it
            try
            {
                this.pmsService.Start();
                this.pmsService.WaitForStatus(ServiceControllerStatus.Running, timeOut);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                MessageBox.Show("Unable to start service" + Environment.NewLine + ex.Message);
            }
        }

        private void stopService_Click(object sender, EventArgs e)
        {
            //stop it
            try
            {
                this.pmsService.Stop();
                this.pmsService.WaitForStatus(ServiceControllerStatus.Stopped, timeOut);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                MessageBox.Show("Unable to stop service" + Environment.NewLine + ex.Message);
            }
        }

        private void openManager_Click(object sender, EventArgs e)
        {
            Process.Start("http://localhost:32400/web");
        }

        private void viewLogs_Click(object sender, EventArgs e)
        {
            if (File.Exists(this.logFile))
            {
                Process.Start(this.logFile);
            }
            else
            {
                MessageBox.Show("Unable to find log file");
            }
        }

    }
}
