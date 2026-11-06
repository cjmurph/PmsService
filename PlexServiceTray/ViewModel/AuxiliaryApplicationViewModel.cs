using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using PlexServiceCommon;

namespace PlexServiceTray.ViewModel
{
    public class AuxiliaryApplicationViewModel : ViewModelBase
    {
        private readonly SettingsViewModel _owner;
        private readonly string _savedName;

        private string _name;
        private string _filePath;
        private string _workingFolder;
        private string _argument;
        private string _url;
        private bool _keepAlive;
        private bool _logOutput;
        private bool _startBeforePlex;
        private int _startDelay;
        private bool _running;

        public AuxiliaryApplicationViewModel(AuxiliaryApplication model, SettingsViewModel owner, bool isNew = false)
        {
            _owner = owner;
            IsNew = isNew;
            _savedName = isNew ? null : model.Name;
            _name = model.Name ?? string.Empty;
            _filePath = model.FilePath ?? string.Empty;
            _workingFolder = model.WorkingFolder ?? string.Empty;
            _argument = model.Argument ?? string.Empty;
            _url = model.Url ?? string.Empty;
            _keepAlive = model.KeepAlive;
            _logOutput = model.LogOutput;
            _startBeforePlex = model.StartBeforePlex;
            _startDelay = model.StartDelay;
        }

        /// <summary>
        /// Not saved yet, the service doesn't know about it
        /// </summary>
        public bool IsNew { get; }

        public string Name
        {
            get => _name;
            set
            {
                if (!SetProperty(ref _name, value)) return;
                _owner.MarkDirty();
                _owner.RevalidateAuxApps();
            }
        }

        public string FilePath
        {
            get => _filePath;
            set
            {
                if (SetProperty(ref _filePath, value?.Trim().Trim('"'))) _owner.MarkDirty();
            }
        }

        public string WorkingFolder
        {
            get => _workingFolder;
            set
            {
                if (SetProperty(ref _workingFolder, value)) _owner.MarkDirty();
            }
        }

        public string Argument
        {
            get => _argument;
            set
            {
                if (SetProperty(ref _argument, value)) _owner.MarkDirty();
            }
        }

        public string Url
        {
            get => _url;
            set
            {
                if (SetProperty(ref _url, value?.Trim())) _owner.MarkDirty();
            }
        }

        public bool KeepAlive
        {
            get => _keepAlive;
            set
            {
                if (SetProperty(ref _keepAlive, value)) _owner.MarkDirty();
            }
        }

        public bool LogOutput
        {
            get => _logOutput;
            set
            {
                if (SetProperty(ref _logOutput, value)) _owner.MarkDirty();
            }
        }

        public bool StartBeforePlex
        {
            get => _startBeforePlex;
            set
            {
                if (SetProperty(ref _startBeforePlex, value)) _owner.MarkDirty();
            }
        }

        public int StartDelay
        {
            get => _startDelay;
            set
            {
                if (SetProperty(ref _startDelay, Math.Max(0, value))) _owner.MarkDirty();
            }
        }

        public bool Running
        {
            get => _running;
            set
            {
                if (SetProperty(ref _running, value)) OnPropertyChanged(nameof(StatusText));
            }
        }

        public string StatusText => IsNew ? "Not saved yet" : Running ? "Running" : "Not running";

        /// <summary>
        /// Start/stop only work for apps the service knows under this name
        /// </summary>
        public bool CanStartStop => !IsNew && _savedName == Name;

        protected override IEnumerable<string> ValidatedProperties => new[] { nameof(Name), nameof(FilePath), nameof(Url) };

        protected override string Validate(string propertyName)
        {
            switch (propertyName)
            {
                case nameof(Name):
                    if (string.IsNullOrWhiteSpace(Name)) return "Enter a name";
                    if (_owner.AuxiliaryApplications.Count(a => string.Equals(a.Name, Name, StringComparison.OrdinalIgnoreCase)) > 1)
                        return $"There's already an application called {Name}";
                    break;
                case nameof(FilePath):
                    if (string.IsNullOrWhiteSpace(FilePath)) return "Select the program to run";
                    break;
                case nameof(Url):
                    if (!string.IsNullOrEmpty(Url) && !Shell.IsWebUrl(Url)) return "Enter a full address starting with http:// or https://";
                    break;
            }
            return null;
        }

        internal void Revalidate() => OnPropertyChanged(nameof(Name));

        public AuxiliaryApplication ToModel() => new()
        {
            Name = Name.Trim(),
            FilePath = FilePath,
            WorkingFolder = WorkingFolder ?? string.Empty,
            Argument = Argument ?? string.Empty,
            Url = Url ?? string.Empty,
            KeepAlive = KeepAlive,
            LogOutput = LogOutput,
            StartBeforePlex = StartBeforePlex,
            StartDelay = StartDelay
        };

        #region Commands

        private RelayCommand _browseCommand;
        public RelayCommand BrowseCommand => _browseCommand ??= new RelayCommand(_ =>
        {
            var ofd = new OpenFileDialog {
                Title = "Select the program to run",
                Filter = "Programs and scripts|*.exe;*.bat;*.cmd;*.ps1|All files|*.*",
                FileName = Path.GetFileName(FilePath)
            };
            var dir = SafeDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir)) ofd.InitialDirectory = dir;
            if (ofd.ShowDialog() != true) return;

            FilePath = ofd.FileName;
            if (string.IsNullOrEmpty(WorkingFolder)) WorkingFolder = Path.GetDirectoryName(FilePath);
            if (string.IsNullOrWhiteSpace(Name) || Name == SettingsViewModel.NewAppName) Name = Path.GetFileNameWithoutExtension(FilePath);
        });

        private RelayCommand _browseFolderCommand;
        public RelayCommand BrowseFolderCommand => _browseFolderCommand ??= new RelayCommand(_ =>
        {
            var fbd = new VistaFolderBrowserDialog {
                Description = "Select the working folder",
                UseDescriptionForTitle = true,
                SelectedPath = WorkingFolder
            };
            if (fbd.ShowDialog() == true) WorkingFolder = fbd.SelectedPath;
        });

        private RelayCommand _openUrlCommand;
        public RelayCommand OpenUrlCommand => _openUrlCommand ??= new RelayCommand(_ => Shell.OpenUrl(Url), _ => Shell.IsWebUrl(Url));

        private RelayCommand _startCommand;
        public RelayCommand StartCommand => _startCommand ??= new RelayCommand(_ => _owner.StartAuxApp(this), _ => CanStartStop && !Running && _owner.CanEditServer);

        private RelayCommand _stopCommand;
        public RelayCommand StopCommand => _stopCommand ??= new RelayCommand(_ => _owner.StopAuxApp(this), _ => CanStartStop && Running && _owner.CanEditServer);

        #endregion

        private static string SafeDirectoryName(string path)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                return Directory.Exists(dir) ? dir : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
