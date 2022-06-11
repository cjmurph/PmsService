using System;
using PlexServiceCommon;
using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using PlexServiceTray.Validation;
using System.ComponentModel.DataAnnotations;

namespace PlexServiceTray.ViewModel
{
    public class AuxiliaryApplicationViewModel : ObservableObject
    {
        #region Properties

        [UniqueAuxAppName]
        public string Name
        {
            get => _auxApplication.Name;
            set 
            {
                if (_auxApplication.Name == value) return;

                _auxApplication.Name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        [Required(ErrorMessage ="A path to execute must be specified")]
        public string FilePath
        {
            get => _auxApplication.FilePath;
            set 
            {
                if (_auxApplication.FilePath == value) return;

                _auxApplication.FilePath = value;
                OnPropertyChanged(nameof(FilePath));
            }
        }

        public string WorkingFolder
        {
            get => _auxApplication.WorkingFolder;
            set 
            {
                if (_auxApplication.WorkingFolder == value) return;

                _auxApplication.WorkingFolder = value;
                OnPropertyChanged(nameof(WorkingFolder));
            }
        }

        public string Argument
        {
            get => _auxApplication.Argument;
            set 
            {
                if (_auxApplication.Argument == value) return;

                _auxApplication.Argument = value;
                OnPropertyChanged(nameof(Argument));
            }
        }

        public bool KeepAlive
        {
            get => _auxApplication.KeepAlive;
            set
            {
                if (_auxApplication.KeepAlive == value) return;

                _auxApplication.KeepAlive = value;
                OnPropertyChanged(nameof(KeepAlive));
            }
        }
        
        public bool LogOutput
        {
            get => _auxApplication.LogOutput;
            set
            {
                if (_auxApplication.LogOutput == value) return;

                _auxApplication.LogOutput = value;
                OnPropertyChanged(nameof(LogOutput));
            }
        }

        private bool _running;

        public bool Running
        {
            get => _running;
            set 
            {
                if (_running == value) return;

                _running = value;
                OnPropertyChanged(nameof(Running));
            }
        }

        public string Url
        {
            get => _auxApplication.Url;
            set 
            {
                if (_auxApplication.Url == value) return;

                _auxApplication.Url = value;
                OnPropertyChanged(nameof(Url));
            }
        }

        public override bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;

                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));

                if (value)
                    OnCheckRunningRequest();
            }
        }

        #endregion Properties

        private readonly AuxiliaryApplication _auxApplication;

        public AuxiliaryApplicationViewModel(AuxiliaryApplication auxApplication, SettingsWindowViewModel context)
        {
            ValidationContext = context;
            _auxApplication = auxApplication;
            IsExpanded = false;
        }

        public AuxiliaryApplication GetAuxiliaryApplication()
        {
            return _auxApplication;
        }

        #region BrowseCommand
        RelayCommand _browseCommand;
        public RelayCommand BrowseCommand => _browseCommand ??= new RelayCommand(OnBrowse);

        private void OnBrowse(object parameter)
        {
            var ofd = new OpenFileDialog {
                FileName = FilePath
            };
            if (ofd.ShowDialog() != true) {
                return;
            }

            FilePath = ofd.FileName;
            if(string.IsNullOrEmpty(WorkingFolder))
            {
                WorkingFolder = System.IO.Path.GetDirectoryName(FilePath);
            }
        }

        #endregion BrowseCommand

        #region BrowseFolderCommand
        RelayCommand _browseFolderCommand;
        public RelayCommand BrowseFolderCommand=> _browseFolderCommand ??= new RelayCommand(OnBrowseFolder);

        private void OnBrowseFolder(object parameter)
        {
            var fbd = new VistaFolderBrowserDialog {
                Description = "Please select working directory",
                UseDescriptionForTitle = true
            };
            if (!string.IsNullOrEmpty(WorkingFolder))
            {
                fbd.SelectedPath = WorkingFolder;
            }
            if (fbd.ShowDialog() == true)
            {
                WorkingFolder = fbd.SelectedPath;
            }
        }

        #endregion BrowseFolderCommand

        #region StartCommand
        RelayCommand _startCommand;
        public RelayCommand StartCommand => _startCommand ??= new RelayCommand(OnStart, CanStart); 

        private bool CanStart(object parameter)
        {
            return !Running;
        }

        private void OnStart(object parameter)
        {
            StartRequest?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler StartRequest;

        #endregion StartCommand

        #region StopCommand
        RelayCommand _stopCommand;
        public RelayCommand StopCommand => _stopCommand ??= new RelayCommand(OnStop, CanStop);

        private bool CanStop(object parameter)
        {
            return Running;
        }

        private void OnStop(object parameter)
        {
            StopRequest?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler StopRequest;

        #endregion StopCommand

        #region GoToUrlCommand
        RelayCommand _goToUrlCommand;
        public RelayCommand GoToUrlCommand => _goToUrlCommand ??= new RelayCommand(OnGoToUrl, CanGoToUrl);

        private bool CanGoToUrl(object parameter)
        {
            return !string.IsNullOrEmpty(Url);
        }

        private void OnGoToUrl(object parameter)
        {
            System.Diagnostics.Process.Start(Url);
        }

        #endregion GoToUrlCommand

        #region CheckRunningRequest

        public event EventHandler CheckRunningRequest;

        private void OnCheckRunningRequest()
        {
            CheckRunningRequest?.Invoke(this, EventArgs.Empty);
        }

        #endregion        
    }
}
