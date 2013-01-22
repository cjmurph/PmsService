using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;

namespace PlexServiceTray
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            string appProcessName = Path.GetFileNameWithoutExtension(Application.ExecutablePath);
            Process[] RunningProcesses = Process.GetProcessesByName(appProcessName);
            if (RunningProcesses.Length <= 1) // just me, so run!
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                try
                {
                    NotifyIconApplicationContext applicationContext = new NotifyIconApplicationContext();
                    Application.Run(applicationContext);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Program Terminated Unexpectedly", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
