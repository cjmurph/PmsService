using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using PlexServiceCommon;
using System.Windows.Input;
using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using PlexServiceTray.Validation;
using System.ComponentModel.DataAnnotations;

namespace PlexServiceTray
{
    public class AuxiliaryApplicationViewModel:ObservableObject
    {
        #region Properties

        [UniqueAuxAppName]
        public string Name
        {
            get
            {
                return _auxApplication.Name;
            }
            set
            {
                if (_auxApplication.Name != value)
                {
                    _auxApplication.Name = value;
                    OnPropertyChanged("Name");
                }
            }
        }

        [Required(ErrorMessage ="A path to execute must be specified")]
        public string FilePath
        {
            get
            {
                return _auxApplication.FilePath;
            }
            set
            {
                if (_auxApplication.FilePath != value)
                {
                    _auxApplication.FilePath = value;
                    OnPropertyChanged("FilePath");
                }
            }
        }

        public string WorkingFolder
        {
            get
            {
                return _auxApplication.WorkingFolder;
            }
            set
            {
                if (_auxApplication.WorkingFolder != value)
                {
                    _auxApplication.WorkingFolder = value;
                    OnPropertyChanged("WorkingFolder");
                }
            }
        }

        public string Argument
        {
            get
            {
                return _auxApplication.Argument;
            }
            set
            {
                if (_auxApplication.Argument != value)
                {
                    _auxApplication.Argument = value;
                    OnPropertyChanged("Argument");
                }
            }
        }

        public bool KeepAlive
        {
            get
            {
                return _auxApplication.KeepAlive;
            }
            set
            {
                if (_auxApplication.KeepAlive != value)
                {
                    _auxApplication.KeepAlive = value;
                    OnPropertyChanged("KeepAlive");
                }
            }
        }

        private bool _running;

        public bool Running
        {
            get
            {
                return _running;
            }
            set
            {
                if (_running != value)
                {
                    _running = value;
                    OnPropertyChanged("Running");
                }
            }
        }

        public string Url
        {
            get
            {
                return _auxApplication.Url;
            }
            set
            {
                if (_auxApplication.Url != value)
                {
                    _auxApplication.Url = value;
                    OnPropertyChanged("Url");
                }
            }
        }

        #endregion Properties

        private AuxiliaryApplication _auxApplication;
        private SettingsWindowViewModel _context;

        public AuxiliaryApplicationViewModel(AuxiliaryApplication auxApplication, SettingsWindowViewModel context)
        {
            _context = context;
            ValidationContext = context;
            _auxApplication = auxApplication;
            IsExpanded = false;
        }

        public AuxiliaryApplication GetAuxiliaryApplication()
        {
            return _auxApplication;
        }

        #region BrowseCommand
        RelayCommand _browseCommand = null;
        public ICommand BrowseCommand
        {
            get
            {
                if (_browseCommand == null)
                {
                    _browseCommand = new RelayCommand((p) => OnBrowse(p), (p) => CanBrowse(p));
                }

                return _browseCommand;
            }
        }

        private bool CanBrowse(object parameter)
        {
            return true;
        }

        private void OnBrowse(object parameter)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.FileName = FilePath;
            if (ofd.ShowDialog() == true)
            {
                FilePath = ofd.FileName;
                if(string.IsNullOrEmpty(WorkingFolder))
                {
                    WorkingFolder = System.IO.Path.GetDirectoryName(FilePath);
                }
            }
        }

        #endregion BrowseCommand

        #region BrowseFolderCommand
        RelayCommand _browseFolderCommand = null;
        public ICommand BrowseFolderCommand
        {
            get
            {
                if (_browseFolderCommand == null)
                {
                    _browseFolderCommand = new RelayCommand((p) => OnBrowseFolder(p), (p) => CanBrowseFolder(p));
                }

                return _browseFolderCommand;
            }
        }

        private bool CanBrowseFolder(object parameter)
        {
            return true;
        }

        private void OnBrowseFolder(object parameter)
        {
            VistaFolderBrowserDialog fbd = new VistaFolderBrowserDialog();
            fbd.Description = "Please select working directory";
            fbd.UseDescriptionForTitle = true;
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
        RelayCommand _startCommand = null;
        public ICommand StartCommand
        {
            get
            {
                if (_startCommand == null)
                {
                    _startCommand = new RelayCommand((p) => OnStart(p), (p) => CanStart(p));
                }

                return _startCommand;
            }
        }

        private bool CanStart(object parameter)
        {
            return !Running;
        }

        private void OnStart(object parameter)
        {
            StartRequest?.Invoke(this, new EventArgs());
        }

        public event EventHandler StartRequest;

        #endregion StartCommand

        #region StopCommand
        RelayCommand _stopCommand = null;
        public ICommand StopCommand
        {
            get
            {
                if (_stopCommand == null)
                {
                    _stopCommand = new RelayCommand((p) => OnStop(p), (p) => CanStop(p));
                }

                return _stopCommand;
            }
        }

        private bool CanStop(object parameter)
        {
            return Running;
        }

        private void OnStop(object parameter)
        {
            StopRequest?.Invoke(this, new EventArgs());
        }

        public event EventHandler StopRequest;

        #endregion StopCommand

        #region GoToUrlCommand
        RelayCommand _goToUrlCommand = null;
        public ICommand GoToUrlCommand
        {
            get
            {
                if (_goToUrlCommand == null)
                {
                    _goToUrlCommand = new RelayCommand((p) => OnGoToUrl(p), (p) => CanGoToUrl(p));
                }

                return _goToUrlCommand;
            }
        }

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

        protected void OnCheckRunningRequest()
        {
            CheckRunningRequest?.Invoke(this, new EventArgs());
        }

        #endregion

        public override bool IsSelected
        {
            get
            {
                return _isSelected;
            }
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged("IsSelected");
                    if (value)
                        OnCheckRunningRequest();
                }
            }
        }
        
    }
}
