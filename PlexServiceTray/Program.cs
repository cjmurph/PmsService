using System;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using PlexServiceCommon;
using Serilog;

namespace PlexServiceTray
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            var appProcessName = Path.GetFileNameWithoutExtension(Application.ExecutablePath);
            var runningProcesses = Process.GetProcessesByName(appProcessName);
            if (runningProcesses.Length > 1) {
                return;
            }
            LogWriter.Init();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                var applicationContext = new NotifyIconApplicationContext();
                Application.Run(applicationContext);
            }
            catch (Exception ex)
            {
                Log.Warning("Exception running application: " + ex.Message);
                MessageBox.Show(ex.Message, "Program Terminated Unexpectedly", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
