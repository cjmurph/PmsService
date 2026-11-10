#nullable enable
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using PlexServiceCommon;
using PlexServiceTray.ViewModel;
using PlexServiceTray.Windows;
using Serilog.Events;

namespace PlexServiceTray
{
    /// <summary>
    /// Tray icon context
    /// </summary>
    internal class NotifyIconApplicationContext : ApplicationContext
    {
        private static readonly Color RunningColor = Color.FromArgb(46, 160, 67);
        private static readonly Color BusyColor = Color.FromArgb(219, 150, 20);
        private static readonly Color StoppedColor = Color.FromArgb(209, 52, 56);

        private readonly System.ComponentModel.IContainer _components = new System.ComponentModel.Container();
        private readonly NotifyIcon _notifyIcon;
        private readonly ServiceClient _service;
        private TrayApplicationSettings _traySettings;
        private SettingsWindow? _settingsWindow;
        private LibraryUpdateWindow? _libraryWindow;
        private readonly SessionEndWindow _sessionWindow;

        public NotifyIconApplicationContext(System.Threading.WaitHandle showSettingsSignal, bool openSettings, SettingsPage startPage = SettingsPage.General)
        {
            var uiContext = System.Threading.SynchronizationContext.Current;
            System.Threading.ThreadPool.RegisterWaitForSingleObject(showSettingsSignal,
                (_, _) => uiContext?.Post(_ => ShowSettings(SettingsPage.General), null), null, -1, false);
            if (openSettings) uiContext?.Post(_ => ShowSettings(startPage), null);

            _traySettings = TrayApplicationSettings.Load();
            Theme.Initialize(_traySettings.Theme);
            _service = new ServiceClient(_traySettings);
            _service.StateChanged += Service_StateChanged;

            _notifyIcon = new NotifyIcon(_components)
            {
                ContextMenuStrip = new ContextMenuStrip { ShowImageMargin = false, Padding = new Padding(0, 4, 0, 4) },
                Icon = new Icon(typeof(NotifyIconApplicationContext).Assembly.GetManifestResourceStream("PlexServiceTray.PlexService.ico")!, SystemInformation.SmallIconSize),
                Text = "Plex Service",
                Visible = true
            };
            _notifyIcon.MouseDoubleClick += NotifyIcon_DoubleClick;
            _notifyIcon.ContextMenuStrip.Opening += ContextMenuStrip_Opening;
            TrayMenuRenderer.RoundCorners(_notifyIcon.ContextMenuStrip);
            ApplyMenuTheme();

            _sessionWindow = new SessionEndWindow(ExitThread);

            if (_service.EnsureConnected()) UpdateTooltip(_service.GetStatus());
        }

        /// <summary>
        /// Without a visible window nobody answers when Windows or an installer asks the app to close
        /// </summary>
        private sealed class SessionEndWindow : NativeWindow
        {
            private const int WmQueryEndSession = 0x0011;
            private const int WmEndSession = 0x0016;

            private readonly Action _exit;

            public SessionEndWindow(Action exit)
            {
                _exit = exit;
                CreateHandle(new CreateParams());
            }

            protected override void WndProc(ref Message m)
            {
                switch (m.Msg)
                {
                    case WmQueryEndSession:
                        m.Result = (IntPtr)1;
                        return;
                    case WmEndSession:
                        if (m.WParam != IntPtr.Zero) _exit();
                        m.Result = IntPtr.Zero;
                        return;
                    default:
                        base.WndProc(ref m);
                        return;
                }
            }
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _sessionWindow.DestroyHandle();
                _service.Dispose();
                _notifyIcon.Visible = false;
                _components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void ApplyMenuTheme()
        {
            var renderer = new TrayMenuRenderer(_traySettings.IsDark);
            var menu = _notifyIcon.ContextMenuStrip;
            menu.Renderer = renderer;
            menu.BackColor = renderer.BackColor;
            menu.ForeColor = renderer.ForeColor;
        }

        private void Service_StateChanged(object sender, StatusChangeEventArgs e)
        {
            UpdateTooltip(_service.GetStatus());
            if (_traySettings.ShowNotifications)
            {
                _notifyIcon.ShowBalloonTip(2000, "Plex Service", e.Description, ToolTipIcon.Info);
            }
        }

        private void UpdateTooltip(PlexState? state)
        {
            var text = state == null ? "Plex Service - not connected" : "Plex Media Server - " + Describe(state.Value);
            // the tooltip is limited to 63 characters
            _notifyIcon.Text = text.Length > 63 ? text.Substring(0, 63) : text;
        }

        private static string Describe(PlexState state) => state switch
        {
            PlexState.Running => "Running",
            PlexState.Stopped => "Stopped",
            PlexState.Pending => "Starting",
            PlexState.Stopping => "Stopping",
            PlexState.Updating => "Updating",
            _ => "Unknown"
        };

        private static Color ColorOf(PlexState? state) => state switch
        {
            PlexState.Running => RunningColor,
            PlexState.Stopped or null => StoppedColor,
            _ => BusyColor
        };

        /// <summary>
        /// Opens the web manager on a double left click
        /// </summary>
        private void NotifyIcon_DoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) OpenPlex();
        }

        /// <summary>
        /// build the context menu each time it opens to ensure appropriate options
        /// </summary>
        private void ContextMenuStrip_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = false;
            var items = _notifyIcon.ContextMenuStrip.Items;
            items.Clear();

            var connected = _service.EnsureConnected();
            var state = connected ? _service.GetStatus() : null;
            var canControl = connected && _service.CanControl();
            UpdateTooltip(state);

            var bold = new Font(_notifyIcon.ContextMenuStrip.Font, FontStyle.Bold);
            items.Add(new ToolStripLabel("Plex Media Server") { Font = bold, Padding = new Padding(4, 2, 0, 0) });
            var status = state == null
                ? "●  Not connected to the service"
                : "●  " + Describe(state.Value) + (canControl ? string.Empty : "  (read only)");
            items.Add(new ToolStripLabel(status) { Tag = ColorOf(state), Padding = new Padding(4, 0, 0, 2) });
            items.Add(new ToolStripSeparator());

            if (connected)
            {
                var open = Add("Open Plex Web", (_, _) => OpenPlex());
                open.Font = bold;
                open.Enabled = state == PlexState.Running;
                Add("Start Plex", (_, _) => Run(c => c.Start())).Enabled = canControl && state == PlexState.Stopped;
                Add("Stop Plex", (_, _) => Run(c => c.Stop())).Enabled = canControl && state is PlexState.Running or PlexState.Pending;
                Add("Restart Plex", (_, _) => Run(c => c.Restart())).Enabled = canControl && state == PlexState.Running;
                Add("Update Plex Libraries...", (_, _) => UpdateLibraries()).Enabled = state == PlexState.Running;
                items.Add(new ToolStripSeparator());

                var settings = _service.Call(c => c.GetSettings(), null);
                var links = settings?.AuxiliaryApplications.Where(a => Shell.IsWebUrl(a.Url)).ToList();
                if (links is { Count: > 0 })
                {
                    var auxItem = new ToolStripMenuItem("Auxiliary apps");
                    links.ForEach(aux => auxItem.DropDownItems.Add(aux.Name, null, (_, _) => Shell.OpenUrl(aux.Url)));
                    auxItem.DropDown.Renderer = _notifyIcon.ContextMenuStrip.Renderer;
                    auxItem.DropDown.BackColor = _notifyIcon.ContextMenuStrip.BackColor;
                    auxItem.DropDown.ForeColor = _notifyIcon.ContextMenuStrip.ForeColor;
                    TrayMenuRenderer.RoundCorners(auxItem.DropDown);
                    items.Add(auxItem);
                }

                var dataDir = GetDataDir();
                Add("Open Plex data folder", (_, _) => Shell.OpenFolder(dataDir)).Enabled = !string.IsNullOrEmpty(dataDir);
                Add("Open service log", (_, _) => ViewLog());
                items.Add(new ToolStripSeparator());
            }

            Add(connected ? "Settings..." : "Connection settings...", (_, _) => ShowSettings(connected ? SettingsPage.General : SettingsPage.Tray));
            Add("About", (_, _) => ShowSettings(SettingsPage.About));
            items.Add(new ToolStripSeparator());
            Add("Exit", (_, _) => ExitThread());
        }

        private ToolStripMenuItem Add(string text, EventHandler onClick)
        {
            var item = new ToolStripMenuItem(text, null, (s, e) =>
            {
                try
                {
                    onClick(s, e);
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
            }) { Padding = new Padding(4, 3, 4, 3) };
            _notifyIcon.ContextMenuStrip.Items.Add(item);
            return item;
        }

        private void Run(Action<PlexServiceCommon.Interface.ITrayInteraction> action)
        {
            _service.Call(action);
        }

        private void UpdateLibraries()
        {
            if (_libraryWindow != null)
            {
                _libraryWindow.Activate();
                return;
            }

            var viewModel = new LibraryUpdateViewModel(_service);
            viewModel.Updated += (_, _) => _notifyIcon.ShowBalloonTip(2000, "Plex Service", "Library update requested", ToolTipIcon.Info);
            _libraryWindow = new LibraryUpdateWindow(viewModel, _traySettings.Theme);
            _libraryWindow.Closed += (_, _) => _libraryWindow = null;
            _libraryWindow.Show();
            _libraryWindow.Activate();
        }

        private void ShowError(Exception ex)
        {
            _service.Log("Tray error: " + ex.Message, LogEventLevel.Warning);
            var title = ex is UnauthorizedAccessException ? "Access denied" : "Plex Service";
            MessageBox.Show(ex.Message, title, MessageBoxButtons.OK, ex is UnauthorizedAccessException ? MessageBoxIcon.Warning : MessageBoxIcon.Error);
        }

        private void ShowSettings(SettingsPage page)
        {
            if (_settingsWindow != null)
            {
                _settingsWindow.ViewModel.SelectedPage = page;
                _settingsWindow.Activate();
                return;
            }

            var viewModel = new SettingsViewModel(_service, _traySettings) { SelectedPage = page };
            viewModel.TraySettingsSaved += (_, settings) =>
            {
                _traySettings = settings;
                _service.UpdateSettings(settings);
                ApplyMenuTheme();
                Theme.Apply(settings.Theme);
            };
            _settingsWindow = new SettingsWindow(viewModel);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
            _settingsWindow.Activate();
        }

        /// <summary>
        /// Try to open the web manager
        /// </summary>
        private void OpenPlex()
        {
            try
            {
                var address = _service.Call(c => c.GetWebLink(), null);
                var host = _traySettings.ServerAddress is "0.0.0.0" ? "localhost" : _traySettings.ServerAddress;
                if (string.IsNullOrEmpty(address)) address = $"http://{host}:32400/web";
                if (Shell.IsWebUrl(address))
                {
                    if (!_traySettings.IsLocal) address = address!.Replace("localhost", host);
                    Shell.OpenUrl(address!);
                    return;
                }

                // the setup page is a local file next to the plex data
                if (_traySettings.IsLocal && Shell.OpenFile(address!, ".html")) return;

                _notifyIcon.ShowBalloonTip(3000, "Plex Service", "Please go to the computer hosting Plex and run the initial setup", ToolTipIcon.Warning);
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        /// <summary>
        /// View the server log file
        /// </summary>
        private void ViewLog()
        {
            if (_traySettings.IsLocal)
            {
                var path = _service.Call(c => c.GetLogPath(), string.Empty);
                if (Shell.OpenFile(path, ".log")) return;
            }

            // remote, or the service runs as a different user whose profile we can't read
            var logData = _service.Call(c => c.GetLog(), null);
            if (logData == null)
            {
                MessageBox.Show("The service log could not be loaded.", "Plex Service", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var tmpFile = Path.Combine(Path.GetTempPath(), "PlexService.log");
            File.WriteAllText(tmpFile, logData);
            Shell.OpenFile(tmpFile, ".log");
        }

        private string GetDataDir()
        {
            var path = _service.Call(c => c.GetPmsDataPath(), string.Empty);
            if (string.IsNullOrEmpty(path)) return string.Empty;
            if (_traySettings.IsLocal) return Directory.Exists(path) ? path : string.Empty;

            // If we're not local, see if we can access PMS data dir over the admin share
            if (path.Length < 3 || path[1] != ':') return string.Empty;
            var unc = Path.Combine(@"\\" + _traySettings.ServerAddress, path.Substring(0, 1) + "$", path.Substring(3));
            return Directory.Exists(unc) ? unc : string.Empty;
        }
    }
}
