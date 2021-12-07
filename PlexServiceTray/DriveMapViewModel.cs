using PlexServiceCommon;
using System.ComponentModel.DataAnnotations;

namespace PlexServiceTray
{
    public class DriveMapViewModel : ObservableObject
    {
        [Required(ErrorMessage = "Please enter a UNC path to map")]
        [RegularExpression(@"", ErrorMessage = "Please enter a UNC path to map")]
        public string ShareName
        {
            get => _driveMap.ShareName;
            set {
                if (_driveMap.ShareName == value) {
                    return;
                }

                _driveMap.ShareName = value;
                OnPropertyChanged("ShareName");
            }
        }

        [Required(ErrorMessage = "Please enter a single character A-Z")]
        [RegularExpression("[a-zA-Z]", ErrorMessage = "Please enter a single character A-Z")]
        public string DriveLetter
        {
            get => _driveMap.DriveLetter;
            set {
                if (_driveMap.DriveLetter == value) {
                    return;
                }

                _driveMap.DriveLetter = value;
                OnPropertyChanged("DriveLetter");
            }
        }

        private readonly DriveMap _driveMap;
        public DriveMapViewModel(DriveMap driveMap)
        {
            _driveMap = driveMap;
            ValidationContext = this;
        }

        public DriveMap GetDriveMap()
        {
            return _driveMap;
        }
    }
}
