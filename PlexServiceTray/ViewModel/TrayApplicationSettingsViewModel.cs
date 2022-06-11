

using System;
using System.Collections.ObjectModel;

namespace PlexServiceTray.ViewModel
{
    /// <summary>
    /// View model class for connection settings window
    /// </summary>
    public class TrayApplicationSettingsViewModel:ObservableObject
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

        public string Theme
        {
            get => _settings.Theme?.Replace(".", " ") ?? "Dark Red";
            set
            {
                if (_settings.Theme?.Replace(".", " ") == value) return;
                _settings.Theme = value.Replace(" ", ".");
                OnPropertyChanged(nameof(Theme));
            }
        }

        public ObservableCollection<string> Themes { get; } = new()
        {
            "Dark Amber",
            "Dark Blue",
            "Dark Brown",
            "Dark Cobalt",
            "Dark Crimson",
            "Dark Cyan",
            "Dark Emerald",
            "Dark Green",
            "Dark Indigo",
            "Dark Lime",
            "Dark Magenta",
            "Dark Mauve",
            "Dark Olive",
            "Dark Orange",
            "Dark Pink",
            "Dark Purple",
            "Dark Red",
            "Dark Sienna",
            "Dark Steel",
            "Dark Taupe",
            "Dark Teal",
            "Dark Violet",
            "Dark Yellow",
            "Light Amber",
            "Light Blue",
            "Light Brown",
            "Light Cobalt",
            "Light Crimson",
            "Light Cyan",
            "Light Emerald",
            "Light Green",
            "Light Indigo",
            "Light Lime",
            "Light Magenta",
            "Light Mauve",
            "Light Olive",
            "Light Orange",
            "Light Pink",
            "Light Purple",
            "Light Red",
            "Light Sienna",
            "Light Steel",
            "Light Taupe",
            "Light Teal",
            "Light Violet",
            "Light Yellow"
        };

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

        readonly TrayApplicationSettings _settings;

        internal TrayApplicationSettingsViewModel()
        {
            _settings = TrayApplicationSettings.Load();
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
