using PlexServiceCommon;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;

namespace PlexServiceTray
{
    public class DriveMapViewModel : ObservableObject
    {
        [Required(ErrorMessage = "Please enter a UNC path to map")]
        [RegularExpression(@"^\\\\[a-zA-Z0-9\.\-_]{1,}(\\[a-zA-Z0-9\-_]{1,}[\$]{0,1}){1,}$", ErrorMessage = "Please enter a UNC path to map")]
        public string ShareName
        {
            get
            {
                return _driveMap.ShareName;
            }
            set
            {
                if (_driveMap.ShareName != value)
                {
                    _driveMap.ShareName = value;
                    OnPropertyChanged("ShareName");
                }
            }
        }

        [Required(ErrorMessage = "Please enter a single character A-Z")]
        [RegularExpression("[a-zA-Z]", ErrorMessage = "Please enter a single character A-Z")]
        public string DriveLetter
        {
            get
            {
                return _driveMap.DriveLetter;
            }
            set
            {
                if (_driveMap.DriveLetter != value)
                {
                    _driveMap.DriveLetter = value;
                    OnPropertyChanged("DriveLetter");
                }
            }
        }

        private DriveMap _driveMap;
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
