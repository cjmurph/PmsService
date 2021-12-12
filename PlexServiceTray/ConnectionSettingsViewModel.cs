using System.ComponentModel;
using System.Windows.Input;

namespace PlexServiceTray
{
    /// <summary>
    /// View model class for connection settings window
    /// </summary>
    public class ConnectionSettingsViewModel:ObservableObject
    {
        public string ServerAddress
        {
            get => _settings.ServerAddress;
            set
            {
                if (_settings.ServerAddress == value) return;

                _settings.ServerAddress = value;
                OnPropertyChanged(nameof(ServerAddress));
            }
        }

        public int ServerPort
        {
            get => _settings.ServerPort;
            set
            {
                if (_settings.ServerPort == value) return;

                _settings.ServerPort = value;
                OnPropertyChanged(nameof(ServerPort));
            }
        }

        private bool? _dialogResult;

        public bool? DialogResult
        {
            get => _dialogResult;
            set
            {
                if (_dialogResult == value) return;
                _dialogResult = value;
                OnPropertyChanged(nameof(DialogResult));
            }
        }

        readonly ConnectionSettings _settings;

        internal ConnectionSettingsViewModel()
        {
            _settings = ConnectionSettings.Load();
        }

        #region CancelCommand
        RelayCommand _cancelCommand;
        public RelayCommand CancelCommand => _cancelCommand ??= new RelayCommand((p) => DialogResult = true);

        #endregion CancelCommand

        #region SaveCommand
        RelayCommand _saveCommand;
        public RelayCommand SaveCommand => _saveCommand ??= new RelayCommand(OnSave, CanSave);

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
    }
}
