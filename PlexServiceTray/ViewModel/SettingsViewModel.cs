using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using PlexServiceCommon;

namespace PlexServiceTray.ViewModel
{
    public enum SettingsPage
    {
        General,
        AuxiliaryApps,
        DriveMappings,
        Tray,
        About
    }

    public class SettingsViewModel : ViewModelBase
    {
        internal const string NewAppName = "New application";

        private readonly ServiceClient _service;
        private readonly TrayApplicationSettings _traySettings;
        private Settings _settings;

        internal SettingsViewModel(ServiceClient service, TrayApplicationSettings traySettings)
        {
            _service = service;
            _traySettings = traySettings.Clone();
            _serverAddress = _traySettings.ServerAddress;
            _trayPort = _traySettings.ServerPort;
            _baseTheme = _traySettings.BaseTheme;
            _showNotifications = _traySettings.ShowNotifications;
            Reload();
        }

        /// <summary>
        /// Raised after the local tray settings were saved
        /// </summary>
        internal event EventHandler<TrayApplicationSettings> TraySettingsSaved;

        /// <summary>
        /// Raised when the window should close
        /// </summary>
        public event EventHandler CloseRequested;

        #region Connection state

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            private set
            {
                if (!SetProperty(ref _isConnected, value)) return;
                OnPropertyChanged(nameof(CanEditServer));
                OnPropertyChanged(nameof(ServerNotice));
            }
        }

        private bool _canControl;
        public bool CanControl
        {
            get => _canControl;
            private set
            {
                if (!SetProperty(ref _canControl, value)) return;
                OnPropertyChanged(nameof(CanEditServer));
                OnPropertyChanged(nameof(ServerNotice));
            }
        }

        /// <summary>
        /// The service settings can only be changed when connected and allowed to
        /// </summary>
        public bool CanEditServer => IsConnected && CanControl;

        public string ServerNotice => !IsConnected
            ? $"Can't reach the Plex Service at {_traySettings.ServerAddress}:{_traySettings.ServerPort}. Check the address on the Tray app page and make sure the service is running."
            : !CanControl
                ? "You can look at the settings, but changing them requires administrator rights on the Plex server."
                : null;

        public bool HasServerNotice => ServerNotice != null;

        public string ConnectionStatus => IsConnected ? $"Connected to {_traySettings.ServerAddress}:{_traySettings.ServerPort}" : "Not connected";

        #endregion

        #region Navigation

        private SettingsPage _selectedPage;
        public SettingsPage SelectedPage
        {
            get => _selectedPage;
            set
            {
                if (!SetProperty(ref _selectedPage, value)) return;
                OnPropertyChanged(nameof(SelectedPageIndex));
                if (value == SettingsPage.AuxiliaryApps) RefreshAuxAppStates();
            }
        }

        public int SelectedPageIndex
        {
            get => (int)SelectedPage;
            set => SelectedPage = (SettingsPage)Math.Max(0, value);
        }

        #endregion

        #region Dirty tracking

        private bool _isDirty;
        public bool IsDirty
        {
            get => _isDirty;
            private set => SetProperty(ref _isDirty, value);
        }

        private bool _loading;

        internal void MarkDirty()
        {
            if (!_loading) IsDirty = true;
            OnPropertyChanged(nameof(ValidationSummary));
        }

        #endregion

        #region General

        private string _installLocation;
        public string UserDefinedInstallLocation
        {
            get => _installLocation;
            set
            {
                if (SetProperty(ref _installLocation, value?.Trim().Trim('"'))) MarkDirty();
            }
        }

        private bool _autoRestart;
        public bool AutoRestart
        {
            get => _autoRestart;
            set
            {
                if (SetProperty(ref _autoRestart, value)) MarkDirty();
            }
        }

        private int _restartDelay;
        public int RestartDelay
        {
            get => _restartDelay;
            set
            {
                if (SetProperty(ref _restartDelay, value)) MarkDirty();
            }
        }

        private int _serverPort;
        public int ServerPort
        {
            get => _serverPort;
            set
            {
                if (!SetProperty(ref _serverPort, value)) return;
                MarkDirty();
                OnPropertyChanged(nameof(PortChanged));
            }
        }

        public bool PortChanged => _settings != null && ServerPort != _settings.ServerPort;

        #endregion

        #region Drive mappings

        public ObservableCollection<DriveMapViewModel> DriveMaps { get; } = new();

        /// <summary>
        /// Letters of drives that already exist on the server
        /// </summary>
        internal IReadOnlyCollection<string> UsedDriveLetters { get; private set; } = new string[0];

        internal void RefreshAvailableLetters(DriveMapViewModel changed = null)
        {
            foreach (var map in DriveMaps.Where(m => !ReferenceEquals(m, changed))) map.RefreshAvailableLetters();
        }

        private bool _autoRemount;
        public bool AutoRemount
        {
            get => _autoRemount;
            set
            {
                if (SetProperty(ref _autoRemount, value)) MarkDirty();
            }
        }

        private int _autoRemountCount;
        public int AutoRemountCount
        {
            get => _autoRemountCount;
            set
            {
                if (SetProperty(ref _autoRemountCount, value)) MarkDirty();
            }
        }

        private int _autoRemountDelay;
        public int AutoRemountDelay
        {
            get => _autoRemountDelay;
            set
            {
                if (SetProperty(ref _autoRemountDelay, value)) MarkDirty();
            }
        }

        private bool _startPlexOnMountFail;
        public bool StartPlexOnMountFail
        {
            get => _startPlexOnMountFail;
            set
            {
                if (SetProperty(ref _startPlexOnMountFail, value)) MarkDirty();
            }
        }

        internal void RevalidateDriveMaps()
        {
            foreach (var map in DriveMaps) map.Revalidate();
        }

        internal DriveMapTestResult TestDriveMap(DriveMap map)
        {
            return _service.TestDriveMap(map) ?? new DriveMapTestResult(false, "The service can't be reached. " + _service.LastError);
        }

        /// <summary>
        /// A share is being tested or the settings are being saved
        /// </summary>
        internal bool IsBusy => _saving || DriveMaps.Any(m => m.IsTesting);

        #endregion

        #region Auxiliary applications

        public ObservableCollection<AuxiliaryApplicationViewModel> AuxiliaryApplications { get; } = new();

        private AuxiliaryApplicationViewModel _selectedAuxApplication;
        public AuxiliaryApplicationViewModel SelectedAuxApplication
        {
            get => _selectedAuxApplication;
            set
            {
                if (!SetProperty(ref _selectedAuxApplication, value)) return;
                OnPropertyChanged(nameof(HasSelectedAuxApplication));
                if (value != null) RefreshAuxAppState(value);
            }
        }

        public bool HasSelectedAuxApplication => SelectedAuxApplication != null;

        internal void RevalidateAuxApps()
        {
            foreach (var app in AuxiliaryApplications) app.Revalidate();
        }

        private void RefreshAuxAppStates()
        {
            foreach (var app in AuxiliaryApplications) RefreshAuxAppState(app);
        }

        private void RefreshAuxAppState(AuxiliaryApplicationViewModel app)
        {
            if (!IsConnected || !app.CanStartStop) return;
            app.Running = _service.Call(c => c.IsAuxAppRunning(app.Name), false);
        }

        internal void StartAuxApp(AuxiliaryApplicationViewModel app)
        {
            Guard(() => _service.Call(c => c.StartAuxApp(app.Name)));
            RefreshAuxAppState(app);
        }

        internal void StopAuxApp(AuxiliaryApplicationViewModel app)
        {
            Guard(() => _service.Call(c => c.StopAuxApp(app.Name)));
            RefreshAuxAppState(app);
        }

        #endregion

        #region Tray app

        private string _serverAddress;
        public string ServerAddress
        {
            get => _serverAddress;
            set
            {
                if (SetProperty(ref _serverAddress, value?.Trim())) MarkDirty();
            }
        }

        private int _trayPort;
        public int TrayPort
        {
            get => _trayPort;
            set
            {
                if (SetProperty(ref _trayPort, value)) MarkDirty();
            }
        }

        public IReadOnlyList<string> BaseThemes => TrayApplicationSettings.BaseThemes;

        private string _baseTheme;
        public string BaseTheme
        {
            get => _baseTheme;
            set
            {
                if (!SetProperty(ref _baseTheme, value)) return;
                MarkDirty();
                OnPropertyChanged(nameof(ThemeName));
            }
        }

        /// <summary>
        /// The resolved MahApps theme for the current selection, used for a live preview
        /// </summary>
        public string ThemeName => TrayApplicationSettings.ResolveTheme(BaseTheme);

        private bool _showNotifications;
        public bool ShowNotifications
        {
            get => _showNotifications;
            set
            {
                if (SetProperty(ref _showNotifications, value)) MarkDirty();
            }
        }

        #endregion

        #region About

        public string Version => "Version " + typeof(SettingsViewModel).Assembly.GetName().Version.ToString(3);

        public string ProjectUrl => "https://github.com/cjmurph/PmsService";

        public string IssuesUrl => "https://github.com/cjmurph/PmsService/issues";

        /// <summary>
        /// License text with the hard line breaks of the file removed so it wraps nicely
        /// </summary>
        public string LicenseText
        {
            get
            {
                using var stream = typeof(SettingsViewModel).Assembly.GetManifestResourceStream("PlexServiceTray.LICENSE.md");
                if (stream == null) return string.Empty;
                using var reader = new StreamReader(stream);
                var paragraphs = reader.ReadToEnd().Replace("\r\n", "\n").Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
                return string.Join(Environment.NewLine + Environment.NewLine, paragraphs.Select(p => p.Replace("\n", " ").Trim()));
            }
        }

        #endregion

        #region Validation

        protected override IEnumerable<string> ValidatedProperties => new[] { nameof(ServerAddress), nameof(TrayPort), nameof(ServerPort), nameof(UserDefinedInstallLocation) };

        protected override string Validate(string propertyName)
        {
            switch (propertyName)
            {
                case nameof(ServerAddress):
                    if (string.IsNullOrWhiteSpace(ServerAddress)) return "Enter the name or address of the computer running the service";
                    break;
                case nameof(TrayPort):
                    if (TrayPort is <= 0 or > 65535) return "Enter a port between 1 and 65535";
                    if (TrayPort == 32400) return "32400 is the Plex port, the service uses its own port (default 8787)";
                    break;
                case nameof(ServerPort):
                    if (!IsConnected) break;
                    if (ServerPort is <= 0 or > 65535) return "Enter a port between 1 and 65535";
                    if (ServerPort == 32400) return "32400 is used by Plex itself, pick a different port";
                    break;
                case nameof(UserDefinedInstallLocation):
                    if (!string.IsNullOrEmpty(UserDefinedInstallLocation) && _traySettings.IsLocal &&
                        !File.Exists(UserDefinedInstallLocation) && !Directory.Exists(UserDefinedInstallLocation))
                        return "The file doesn't exist";
                    break;
            }
            return null;
        }

        /// <summary>
        /// First problem that prevents saving, shown in the footer
        /// </summary>
        public string ValidationSummary
        {
            get
            {
                var error = Error;
                if (error != null) return error;
                var aux = AuxiliaryApplications.FirstOrDefault(a => a.HasErrors);
                if (aux != null) return $"{(string.IsNullOrWhiteSpace(aux.Name) ? "Auxiliary app" : aux.Name)}: {aux.Error}";
                var map = DriveMaps.FirstOrDefault(m => m.HasErrors);
                if (map != null) return $"Drive {map.DriveLetter}: {map.Error}";
                return null;
            }
        }

        #endregion

        #region Load / Save

        private void Reload()
        {
            _loading = true;
            try
            {
                IsConnected = _service.EnsureConnected();
                _settings = IsConnected ? _service.Call(c => c.GetSettings(), null) : null;
                IsConnected = _settings != null;
                CanControl = IsConnected && _service.CanControl();

                var settings = _settings ?? new Settings();
                UserDefinedInstallLocation = settings.UserDefinedInstallLocation ?? string.Empty;
                AutoRestart = settings.AutoRestart;
                RestartDelay = settings.RestartDelay;
                ServerPort = settings.ServerPort;
                AutoRemount = settings.AutoRemount;
                AutoRemountCount = settings.AutoRemountCount;
                AutoRemountDelay = settings.AutoRemountDelay;
                StartPlexOnMountFail = settings.StartPlexOnMountFail;

                UsedDriveLetters = IsConnected ? _service.Call(c => c.GetUsedDriveLetters(), new string[0]) : new string[0];
                DriveMaps.Clear();
                settings.DriveMaps.ForEach(d => DriveMaps.Add(new DriveMapViewModel(d, this)));
                RefreshAvailableLetters();
                AuxiliaryApplications.Clear();
                settings.AuxiliaryApplications.ForEach(a => AuxiliaryApplications.Add(new AuxiliaryApplicationViewModel(a, this)));
                SelectedAuxApplication = AuxiliaryApplications.FirstOrDefault();
                OnPropertyChanged(nameof(ConnectionStatus));
                OnPropertyChanged(nameof(HasServerNotice));
            }
            finally
            {
                _loading = false;
            }
            IsDirty = false;
        }

        private bool CanSave => IsDirty && !IsBusy && ValidationSummary == null;

        private RelayCommand _saveCommand;
        public RelayCommand SaveCommand => _saveCommand ??= new RelayCommand(async _ =>
        {
            if (await SaveAsync()) CloseRequested?.Invoke(this, EventArgs.Empty);
        }, _ => CanSave);

        private RelayCommand _applyCommand;
        public RelayCommand ApplyCommand => _applyCommand ??= new RelayCommand(async _ => await SaveAsync(), _ => CanSave);

        private RelayCommand _cancelCommand;
        public RelayCommand CancelCommand => _cancelCommand ??= new RelayCommand(_ => CloseRequested?.Invoke(this, EventArgs.Empty));

        private bool _saving;

        /// <summary>
        /// Set when the window was closed, a test that is still running must not lead to a save anymore
        /// </summary>
        internal bool Abandoned { get; set; }

        /// <summary>
        /// New or changed shares have to work before anything is saved
        /// </summary>
        private async Task<bool> SaveAsync()
        {
            _saving = true;
            try
            {
                var allPassed = true;
                if (CanEditServer && _settings != null)
                {
                    foreach (var map in DriveMaps.Where(m => m.NeedsTest).ToList())
                    {
                        if (!await map.TestAsync()) allPassed = false;
                    }
                }

                if (Abandoned) return false;
                if (allPassed) return Save();
                SelectedPage = SettingsPage.DriveMappings;
                return false;
            }
            finally
            {
                _saving = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool Save()
        {
            return Guard(() =>
            {
                var oldPort = _settings?.ServerPort;
                if (CanEditServer && _settings != null)
                {
                    var settings = new Settings
                    {
                        UserDefinedInstallLocation = UserDefinedInstallLocation,
                        AutoRestart = AutoRestart,
                        RestartDelay = RestartDelay,
                        ServerPort = ServerPort,
                        AutoRemount = AutoRemount,
                        AutoRemountCount = AutoRemountCount,
                        AutoRemountDelay = AutoRemountDelay,
                        StartPlexOnMountFail = StartPlexOnMountFail,
                        AuxiliaryApplications = AuxiliaryApplications.Select(a => a.ToModel()).ToList(),
                        DriveMaps = DriveMaps.Select(d => d.ToModel()).ToList()
                    };
                    if (!_service.Call(c => { c.SetSettings(settings); return true; }, false))
                    {
                        throw new InvalidOperationException("The settings could not be sent to the service. " + _service.LastError);
                    }
                }

                var connectionChanged = ServerAddress != _traySettings.ServerAddress || TrayPort != _traySettings.ServerPort;
                _traySettings.ServerAddress = ServerAddress;
                _traySettings.ServerPort = TrayPort;
                _traySettings.Theme = BaseTheme;
                _traySettings.ShowNotifications = ShowNotifications;
                _traySettings.Save();
                TraySettingsSaved?.Invoke(this, _traySettings.Clone());

                if (oldPort != null && CanEditServer && ServerPort != oldPort)
                {
                    MessageBox.Show(
                        $"The service will use port {ServerPort} after it has been restarted (Services > PlexService > Restart).\n\nRemember to set the same port on the Tray app page.",
                        "Restart required", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                Reload();
                if (connectionChanged && !IsConnected)
                {
                    MessageBox.Show($"The tray can't reach the service at {ServerAddress}:{TrayPort}.", "Not connected",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            });
        }

        private RelayCommand _reconnectCommand;
        public RelayCommand ReconnectCommand => _reconnectCommand ??= new RelayCommand(_ =>
        {
            if (IsDirty && MessageBox.Show("Reconnecting discards your unsaved changes. Continue?", "Plex Service",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            _service.Disconnect();
            Reload();
        });

        private static bool Guard(Action action)
        {
            try
            {
                action();
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show(ex.Message, "Access denied", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Plex Service", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return false;
        }

        #endregion

        #region Commands

        private RelayCommand _browseForPlexCommand;
        public RelayCommand BrowseForPlexCommand => _browseForPlexCommand ??= new RelayCommand(_ =>
        {
            var initialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            try
            {
                var dir = Path.GetDirectoryName(UserDefinedInstallLocation);
                if (Directory.Exists(dir)) initialDirectory = dir;
            }
            catch
            {
                // keep the default
            }

            var ofd = new OpenFileDialog
            {
                FileName = "Plex Media Server.exe",
                Filter = "Plex Media Server|Plex Media Server.exe|Programs|*.exe",
                InitialDirectory = initialDirectory,
                CheckFileExists = true,
                Title = "Locate Plex Media Server"
            };
            if (ofd.ShowDialog() == true) UserDefinedInstallLocation = ofd.FileName;
        }, _ => CanEditServer && _traySettings.IsLocal);

        private RelayCommand _addAuxAppCommand;
        public RelayCommand AddAuxAppCommand => _addAuxAppCommand ??= new RelayCommand(_ =>
        {
            var name = NewAppName;
            for (var i = 2; AuxiliaryApplications.Any(a => a.Name == name); i++) name = $"{NewAppName} {i}";
            var app = new AuxiliaryApplicationViewModel(new AuxiliaryApplication { Name = name }, this, true);
            AuxiliaryApplications.Add(app);
            SelectedAuxApplication = app;
            MarkDirty();
        }, _ => CanEditServer);

        private RelayCommand _removeAuxAppCommand;
        public RelayCommand RemoveAuxAppCommand => _removeAuxAppCommand ??= new RelayCommand(p =>
        {
            if (p is not AuxiliaryApplicationViewModel app) return;
            if (!app.IsNew && MessageBox.Show($"Remove {app.Name}? The service stops it when you save.", "Remove application",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            var index = AuxiliaryApplications.IndexOf(app);
            AuxiliaryApplications.Remove(app);
            SelectedAuxApplication = AuxiliaryApplications.Count == 0 ? null : AuxiliaryApplications[Math.Min(index, AuxiliaryApplications.Count - 1)];
            RevalidateAuxApps();
            MarkDirty();
        }, p => CanEditServer && p is AuxiliaryApplicationViewModel);

        private RelayCommand _addDriveMapCommand;
        public RelayCommand AddDriveMapCommand => _addDriveMapCommand ??= new RelayCommand(_ =>
        {
            var letter = DriveMapViewModel.FirstFreeLetter(DriveMaps.Select(d => d.DriveLetter).Concat(UsedDriveLetters));
            DriveMaps.Add(new DriveMapViewModel(new DriveMap(string.Empty, letter), this));
            RefreshAvailableLetters();
            MarkDirty();
        }, _ => CanEditServer);

        private RelayCommand _removeDriveMapCommand;
        public RelayCommand RemoveDriveMapCommand => _removeDriveMapCommand ??= new RelayCommand(p =>
        {
            if (p is not DriveMapViewModel map) return;
            DriveMaps.Remove(map);
            RevalidateDriveMaps();
            RefreshAvailableLetters();
            MarkDirty();
        }, p => CanEditServer && p is DriveMapViewModel);

        private RelayCommand _openLinkCommand;
        public RelayCommand OpenLinkCommand => _openLinkCommand ??= new RelayCommand(p => Shell.OpenUrl(p as string));

        #endregion
    }
}
