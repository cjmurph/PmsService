using System;
using System.Threading;
using System.Windows.Forms;

namespace PlexServiceTray
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            var openSettings = Array.Exists(args, a => string.Equals(a, "--settings", StringComparison.OrdinalIgnoreCase));

            // one tray per user session
            using var mutex = new Mutex(true, @"Local\PlexServiceTray", out var createdNew);
            using var showSettings = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\PlexServiceTray.ShowSettings");
            if (!createdNew)
            {
                // let the running tray open its settings window
                if (openSettings) showSettings.Set();
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => ShowError(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) => ShowError(e.ExceptionObject as Exception);
            System.Windows.Threading.Dispatcher.CurrentDispatcher.UnhandledException += (_, e) =>
            {
                ShowError(e.Exception);
                e.Handled = true;
            };
            // callbacks from the service get marshalled to this thread
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

            try
            {
                var page = ViewModel.SettingsPage.General;
                var index = Array.FindIndex(args, a => string.Equals(a, "--settings", StringComparison.OrdinalIgnoreCase));
                if (index >= 0 && index + 1 < args.Length) Enum.TryParse(args[index + 1], true, out page);
                Application.Run(new NotifyIconApplicationContext(showSettings, openSettings, page));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Program Terminated Unexpectedly", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void ShowError(Exception ex)
        {
            if (ex == null) return;
            MessageBox.Show(ex.Message, "Plex Service", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
