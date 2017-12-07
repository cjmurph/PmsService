using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows.Input;
using PlexServiceCommon;

namespace PlexServiceTray
{
    public class SettingsWindowViewModel:ObservableObject
    {
        /// <summary>
        /// The server endpoint port
        /// </summary>
        public int ServerPort
        {
            get
            {
                return WorkingSettings.ServerPort;
            }
            set
            {
                if (WorkingSettings.ServerPort != value)
                {
                    WorkingSettings.ServerPort = value;
                    OnPropertyChanged("ServerPort");
                }
            }
        }

        /// <summary>
        /// Plex restart delay
        /// </summary>
        public int RestartDelay
        {
            get
            {
                return WorkingSettings.RestartDelay;
            }
            set
            {
                if (WorkingSettings.RestartDelay != value)
                {
                    WorkingSettings.RestartDelay = value;
                    OnPropertyChanged("RestartDelay");
                }
            }
        }

        public bool AutoRestart
        {
            get
            {
                return WorkingSettings.AutoRestart;
            }
            set
            {
                if (WorkingSettings.AutoRestart != value)
                {
                    WorkingSettings.AutoRestart = value;
                    OnPropertyChanged("AutoRestart");
                }
            }
        }

        private int _selectedTab;

        public int SelectedTab
        {
            get
            {
                return _selectedTab;
            }
            set
            {
                if (_selectedTab != value)
                {
                    _selectedTab = value;
                    OnPropertyChanged("SelectedTab");
                    OnPropertyChanged("RemoveToolTip");
                    OnPropertyChanged("AddToolTip");
                }
            }
        }


        private ObservableCollection<AuxiliaryApplicationViewModel> _auxilaryApplications;
        /// <summary>
        /// Collection of Auxiliary applications to run alongside plex
        /// </summary>
        public ObservableCollection<AuxiliaryApplicationViewModel> AuxiliaryApplications
        {
            get
            {
                return _auxilaryApplications;
            }
            set
            {
                if (_auxilaryApplications != value)
                {
                    _auxilaryApplications = value;
                    OnPropertyChanged("AuxiliaryApplications");
                }
            }
        }

        private AuxiliaryApplicationViewModel _selectedAuxApplication;

        public AuxiliaryApplicationViewModel SelectedAuxApplication
        {
            get
            {
                return _selectedAuxApplication;
            }
            set
            {
                if (_selectedAuxApplication != value)
                {
                    _selectedAuxApplication = value;
                    OnPropertyChanged("SelectedAuxApplication");
                    OnPropertyChanged("RemoveToolTip");
                }
            }
        }

        private ObservableCollection<DriveMapViewModel> _driveMaps;

        public ObservableCollection<DriveMapViewModel> DriveMaps
        {
            get
            {
                return _driveMaps;
            }
            set
            {
                if (_driveMaps != value)
                {
                    _driveMaps = value;
                    OnPropertyChanged("DriveMaps");
                }
            }
        }

        private DriveMapViewModel _selectedDriveMap;

        public DriveMapViewModel SelectedDriveMap
        {
            get
            {
                return _selectedDriveMap;
            }
            set
            {
                if (_selectedDriveMap != value)
                {
                    _selectedDriveMap = value;
                    OnPropertyChanged("SelectedDriveMap");
                    OnPropertyChanged("RemoveToolTip");
                }
            }
        }

        public string RemoveToolTip
        {
            get
            {
                switch (SelectedTab)
                {
                    case 0:
                        if (SelectedAuxApplication != null)
                        {
                            return "Remove " + SelectedAuxApplication.Name;
                        }
                        break;
                    case 1:
                        if (SelectedDriveMap != null)
                        {
                            return "Remove Drive Map " + SelectedDriveMap.DriveLetter + " -> " + SelectedDriveMap.ShareName;
                        }
                        break;
                    default:
                        break; ;
                }
                return "Nothing selected!";
            }
        }

        public string AddToolTip
        {
            get
            {
                switch (SelectedTab)
                {
                    case 0:
                        return "Add Auxiliary Application";
                    case 1:
                        return "Add Drive Map";
                    default:
                        return null;
                }
            }
        }

        private bool? _dialogResult;

        public bool? DialogResult
        {
            get
            {
                return _dialogResult;
            }
            set
            {
                if (_dialogResult != value)
                {
                    _dialogResult = value;
                    OnPropertyChanged("DialogResult");
                }
            }
        }

        /// <summary>
        /// Use one settings instance for the life of the window.
        /// </summary>
        public Settings WorkingSettings { get; set; }

        public SettingsWindowViewModel(Settings settings)
        {
            WorkingSettings = settings;
            AuxiliaryApplications = new ObservableCollection<AuxiliaryApplicationViewModel>();
            DriveMaps = new ObservableCollection<DriveMapViewModel>();

            WorkingSettings.AuxiliaryApplications.ForEach(x =>
            {
                var auxApp = new AuxiliaryApplicationViewModel(x, this);
                auxApp.StartRequest += OnAuxAppStartRequest;
                auxApp.StopRequest += OnAuxAppStopRequest;
                auxApp.CheckRunningRequest += OnAuxAppCheckRunRequest;
                AuxiliaryApplications.Add(auxApp);
            });

            WorkingSettings.DriveMaps.ForEach(x => DriveMaps.Add(new DriveMapViewModel(x)));

            if (AuxiliaryApplications.Count > 0)
            {
                AuxiliaryApplications[0].IsExpanded = true;
            }
        }

        /// <summary>
        /// Allow the user to add a new Auxiliary application
        /// </summary>
        #region AddCommand
        RelayCommand _addCommand = null;
        public ICommand AddCommand
        {
            get
            {
                if (_addCommand == null)
                {
                    _addCommand = new RelayCommand((p) => OnAdd(p), (p) => CanAdd(p));
                }

                return _addCommand;
            }
        }

        private bool CanAdd(object parameter)
        {
            return true;
        }

        private void OnAdd(object parameter)
        {
            switch (SelectedTab)
            {
                case 0:
                    AuxiliaryApplication newAuxApp = new AuxiliaryApplication();
                    newAuxApp.Name = "New Auxiliary Application";
                    AuxiliaryApplicationViewModel newAuxAppViewModel = new AuxiliaryApplicationViewModel(newAuxApp, this);
                    newAuxAppViewModel.StartRequest += OnAuxAppStartRequest;
                    newAuxAppViewModel.StopRequest += OnAuxAppStopRequest;
                    newAuxAppViewModel.CheckRunningRequest += OnAuxAppCheckRunRequest;
                    newAuxAppViewModel.IsExpanded = true;
                    AuxiliaryApplications.Add(newAuxAppViewModel);
                    break;
                case 1:
                    DriveMap newDriveMap = new DriveMap(@"\\computer\share", "Z");
                    DriveMapViewModel newDriveMapViewModel = new DriveMapViewModel(newDriveMap);
                    DriveMaps.Add(newDriveMapViewModel);
                    break;
                default:
                    break;
            }
            
        }

        #endregion AddCommand

        /// <summary>
        /// Remove the selected auxiliary application
        /// </summary>
        #region RemoveCommand
        RelayCommand _removeCommand = null;
        public ICommand RemoveCommand
        {
            get
            {
                if (_removeCommand == null)
                {
                    _removeCommand = new RelayCommand((p) => OnRemove(p), (p) => CanRemove(p));
                }

                return _removeCommand;
            }
        }

        private bool CanRemove(object parameter)
        {
            switch (SelectedTab)
            {
                case 0:
                    return SelectedAuxApplication != null;
                case 1:
                    return SelectedDriveMap != null;
                default:
                    return false;
            }
            
        }

        private void OnRemove(object parameter)
        {
            switch (SelectedTab)
            {
                case 0:
                    SelectedAuxApplication.StartRequest -= OnAuxAppStartRequest;
                    SelectedAuxApplication.StopRequest -= OnAuxAppStopRequest;
                    AuxiliaryApplications.Remove(SelectedAuxApplication);
                    break;
                case 1:
                    DriveMaps.Remove(SelectedDriveMap);
                    break;
                default:
                    break;
            }
            
        }

        #endregion RemoveCommand

        /// <summary>
        /// Save the settings file
        /// </summary>
        #region SaveCommand
        RelayCommand _saveCommand = null;
        public ICommand SaveCommand
        {
            get
            {
                if (_saveCommand == null)
                {
                    _saveCommand = new RelayCommand((p) => OnSave(p), (p) => CanSave(p));
                }

                return _saveCommand;
            }
        }

        private bool CanSave(object parameter)
        {
            return ServerPort > 0 && string.IsNullOrEmpty(Error) && !AuxiliaryApplications.Any(a => !string.IsNullOrEmpty(a.Error) || string.IsNullOrEmpty(a.Name)) && !DriveMaps.Any(dm => !string.IsNullOrEmpty(dm.Error) || string.IsNullOrEmpty(dm.ShareName) || string.IsNullOrEmpty(dm.DriveLetter));
        }

        private void OnSave(object parameter)
        {
            WorkingSettings.AuxiliaryApplications.Clear();
            foreach (AuxiliaryApplicationViewModel aux in AuxiliaryApplications)
            {
                WorkingSettings.AuxiliaryApplications.Add(aux.GetAuxiliaryApplication());
            }
            WorkingSettings.DriveMaps.Clear();
            foreach(DriveMapViewModel dMap in DriveMaps)
            {
                WorkingSettings.DriveMaps.Add(dMap.GetDriveMap());
            }
            DialogResult = true;
        }

        #endregion SaveCommand

        /// <summary>
        /// Close the dialogue without saving changes
        /// </summary>
        #region CancelCommand
        RelayCommand _cancelCommand = null;
        public ICommand CancelCommand
        {
            get
            {
                if (_cancelCommand == null)
                {
                    _cancelCommand = new RelayCommand((p) => OnCancel(p), (p) => CanCancel(p));
                }

                return _cancelCommand;
            }
        }

        private bool CanCancel(object parameter)
        {
            return true;
        }

        private void OnCancel(object parameter)
        {
            DialogResult = false;
        }

        #endregion CancelCommand

        #region Aux app start/stop request handling

        private void OnAuxAppStopRequest(object sender, EventArgs e)
        {
            AuxAppStopRequest?.Invoke(sender, e);
        }

        public event EventHandler AuxAppStopRequest;

        private void OnAuxAppStartRequest(object sender, EventArgs e)
        {
            AuxAppStartRequest?.Invoke(sender, e);
        }

        public event EventHandler AuxAppStartRequest;

        private void OnAuxAppCheckRunRequest(object sender, EventArgs e)
        {
            AuxAppCheckRunRequest?.Invoke(sender, e);
        }

        public event EventHandler AuxAppCheckRunRequest;

        #endregion
    }
}
