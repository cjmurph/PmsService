using System.ComponentModel;
using System.Windows.Input;

namespace PlexServiceTray
{
    /// <summary>
    /// View model class for connection settings window
    /// </summary>
    public class ConnectionSettingsViewModel:INotifyPropertyChanged
    {
        public string ServerAddress
        {
            get => _settings.ServerAddress;
            set
            {
                if (_settings.ServerAddress != value)
                {
                    _settings.ServerAddress = value;
                    OnPropertyChanged("ServerAddress");
                }
            }
        }

        public int ServerPort
        {
            get => _settings.ServerPort;
            set
            {
                if (_settings.ServerPort != value)
                {
                    _settings.ServerPort = value;
                    OnPropertyChanged("ServerPort");
                }
            }
        }

        private bool? _dialogResult;

        public bool? DialogResult
        {
            get => _dialogResult;
            set
            {
                if (_dialogResult != value)
                {
                    _dialogResult = value;
                    OnPropertyChanged("DialogResult");
                }
            }
        }

        readonly ConnectionSettings _settings;

        internal ConnectionSettingsViewModel()
        {
            _settings = ConnectionSettings.Load();
        }

        #region CancelCommand
        RelayCommand _cancelCommand;
        public ICommand CancelCommand
        {
            get
            {
                if (_cancelCommand == null)
                {
                    _cancelCommand = new RelayCommand(OnCancel, CanCancel);
                }

                return _cancelCommand;
            }
        }

        private static bool CanCancel(object parameter)
        {
            return true;
        }

        private void OnCancel(object parameter)
        {
            DialogResult = false;
        }

        #endregion CancelCommand

        #region SaveCommand
        RelayCommand _saveCommand;
        public ICommand SaveCommand
        {
            get { return _saveCommand ?? (_saveCommand = new RelayCommand(p => OnSave(p), p => CanSave(p))); }
        }

        private bool CanSave(object parameter)
        {
            return !string.IsNullOrEmpty(ServerAddress) && ServerPort > 0;
        }

        private void OnSave(object parameter)
        {
            _settings.Save();
            DialogResult = true;
        }

        #endregion SaveCommand


        #region PropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        /// <summary>
        /// This is required to create on property changed events
        /// </summary>
        /// <param name="name">What property of this object has changed</param>
        protected void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        #endregion
    }
}
