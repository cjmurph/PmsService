using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using PlexServiceCommon;

namespace PlexServiceTray.ViewModel
{
    public enum ShareTestState
    {
        None,
        Testing,
        Passed,
        Failed
    }

    public class DriveMapViewModel : ViewModelBase
    {
        private static readonly Regex UncPath = new(@"^\\\\[^\\/:*?""<>|]+(\\[^\\/:*?""<>|]+)+\\?$");

        public static IReadOnlyList<string> DriveLetters { get; } = Enumerable.Range('A', 26).Select(c => ((char)c).ToString()).ToList();

        private readonly SettingsViewModel _owner;
        private readonly string _id;
        private readonly bool _hasSavedPassword;
        private string _shareName;
        private string _driveLetter;
        private string _userName;
        private string _password = string.Empty;
        private string _verified;

        public DriveMapViewModel(DriveMap driveMap, SettingsViewModel owner)
        {
            _owner = owner;
            _id = driveMap.Id;
            _hasSavedPassword = driveMap.HasPassword;
            _shareName = driveMap.ShareName ?? string.Empty;
            _driveLetter = string.IsNullOrEmpty(driveMap.DriveLetter) ? "Z" : driveMap.DriveLetter.Substring(0, 1).ToUpperInvariant();
            _userName = driveMap.UserName ?? string.Empty;
            // what is already saved doesn't have to pass a test again, a NAS that's off shouldn't block other changes
            _verified = string.IsNullOrEmpty(_shareName) ? null : Connection;
        }

        public string ShareName
        {
            get => _shareName;
            set
            {
                if (SetProperty(ref _shareName, value?.Trim())) ConnectionChanged();
            }
        }

        public string DriveLetter
        {
            get => _driveLetter;
            set
            {
                if (!SetProperty(ref _driveLetter, value)) return;
                _owner.MarkDirty();
                _owner.RevalidateDriveMaps();
            }
        }

        public string UserName
        {
            get => _userName;
            set
            {
                if (SetProperty(ref _userName, value?.Trim() ?? string.Empty)) ConnectionChanged();
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                if (SetProperty(ref _password, value ?? string.Empty)) ConnectionChanged();
            }
        }

        public string PasswordHint => _hasSavedPassword ? "Saved, type to replace" : "Password";

        private string Connection => string.Join("\n", _shareName, _userName, _password);

        private string Server => (_shareName ?? string.Empty).TrimStart('\\').Split('\\')[0];

        /// <summary>
        /// Share or account changed since the last successful test
        /// </summary>
        internal bool NeedsTest => Connection != _verified;

        private void ConnectionChanged()
        {
            TestState = ShareTestState.None;
            TestMessage = null;
            _owner.MarkDirty();
            _owner.RevalidateDriveMaps();
        }

        #region Test

        private ShareTestState _testState;
        public ShareTestState TestState
        {
            get => _testState;
            private set
            {
                if (!SetProperty(ref _testState, value)) return;
                OnPropertyChanged(nameof(IsTesting));
                OnPropertyChanged(nameof(IsPassed));
                OnPropertyChanged(nameof(IsFailed));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsTesting => TestState == ShareTestState.Testing;

        public bool IsPassed => TestState == ShareTestState.Passed;

        public bool IsFailed => TestState == ShareTestState.Failed;

        private string _testMessage;
        public string TestMessage
        {
            get => _testMessage;
            private set => SetProperty(ref _testMessage, value);
        }

        private RelayCommand _testCommand;
        public RelayCommand TestCommand => _testCommand ??= new RelayCommand(async _ => await TestAsync(), _ => !HasErrors && !_owner.IsBusy);

        /// <summary>
        /// Asks the service to connect to the share, returns true if that worked
        /// </summary>
        internal async Task<bool> TestAsync()
        {
            var connection = Connection;
            var model = ToModel();
            TestState = ShareTestState.Testing;
            TestMessage = "Connecting...";

            DriveMapTestResult result;
            try
            {
                result = await Task.Run(() => _owner.TestDriveMap(model));
            }
            catch (Exception ex)
            {
                result = new DriveMapTestResult(false, ex.Message);
            }

            // edited while the test was running, the result says nothing about the current values
            if (connection != Connection) return false;

            if (result.Success) _verified = connection;
            TestMessage = result.Message;
            TestState = result.Success ? ShareTestState.Passed : ShareTestState.Failed;
            return result.Success;
        }

        #endregion

        internal void Revalidate()
        {
            OnPropertyChanged(nameof(DriveLetter));
            OnPropertyChanged(nameof(UserName));
            OnPropertyChanged(nameof(Password));
        }

        protected override IEnumerable<string> ValidatedProperties => new[] { nameof(ShareName), nameof(DriveLetter), nameof(UserName), nameof(Password) };

        protected override string Validate(string propertyName)
        {
            switch (propertyName)
            {
                case nameof(ShareName):
                    if (string.IsNullOrWhiteSpace(ShareName)) return "Enter the network share, e.g. \\\\server\\share";
                    if (!UncPath.IsMatch(ShareName)) return "Must be a share like \\\\server\\share, a server on its own can't be mapped";
                    break;
                case nameof(DriveLetter):
                    if (string.IsNullOrEmpty(DriveLetter)) return "Pick a drive letter";
                    if (_owner.DriveMaps.Count(d => d.DriveLetter == DriveLetter) > 1) return $"{DriveLetter}: is used more than once";
                    break;
                case nameof(UserName):
                    if (string.IsNullOrEmpty(UserName) && !string.IsNullOrEmpty(Password)) return "Enter the user name for this password";
                    if (_owner.DriveMaps.Any(UsesOtherAccountOnSameServer)) return "Windows needs the same account for all shares on a server";
                    break;
                case nameof(Password):
                    if (!string.IsNullOrEmpty(UserName) && string.IsNullOrEmpty(Password) && !_hasSavedPassword) return "Enter the password";
                    break;
            }
            return null;
        }

        private bool UsesOtherAccountOnSameServer(DriveMapViewModel other)
        {
            return !ReferenceEquals(other, this) &&
                   string.Equals(other.Server, Server, StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(other.UserName, UserName, StringComparison.OrdinalIgnoreCase);
        }

        public DriveMap ToModel() => new(ShareName, DriveLetter)
        {
            Id = _id,
            UserName = string.IsNullOrEmpty(UserName) ? null : UserName,
            Password = string.IsNullOrEmpty(Password) ? null : Password
        };
    }
}
