using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PlexServiceCommon;

namespace PlexServiceTray.ViewModel
{
    public class DriveMapViewModel : ViewModelBase
    {
        private static readonly Regex UncPath = new(@"^\\\\[^\\/:*?""<>|]+(\\[^\\/:*?""<>|]+)+\\?$");

        public static IReadOnlyList<string> DriveLetters { get; } = Enumerable.Range('A', 26).Select(c => ((char)c).ToString()).ToList();

        private readonly SettingsViewModel _owner;
        private string _shareName;
        private string _driveLetter;

        public DriveMapViewModel(DriveMap driveMap, SettingsViewModel owner)
        {
            _owner = owner;
            _shareName = driveMap.ShareName ?? string.Empty;
            _driveLetter = string.IsNullOrEmpty(driveMap.DriveLetter) ? "Z" : driveMap.DriveLetter.Substring(0, 1).ToUpperInvariant();
        }

        public string ShareName
        {
            get => _shareName;
            set
            {
                if (SetProperty(ref _shareName, value?.Trim())) _owner.MarkDirty();
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

        internal void Revalidate() => OnPropertyChanged(nameof(DriveLetter));

        protected override IEnumerable<string> ValidatedProperties => new[] { nameof(ShareName), nameof(DriveLetter) };

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
            }
            return null;
        }

        public DriveMap ToModel() => new(ShareName, DriveLetter);
    }
}
