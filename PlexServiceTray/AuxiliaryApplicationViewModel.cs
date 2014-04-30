using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using PlexMediaServer_Service;
using System.Windows.Input;
using Microsoft.Win32;

namespace PlexServiceTray
{
    public class AuxiliaryApplicationViewModel:INotifyPropertyChanged
    {
        public string Name
        {
            get
            {
                return this.auxApplication.Name;
            }
            set
            {
                if (this.auxApplication.Name != value)
                {
                    this.auxApplication.Name = value;
                    OnPropertyChanged("Name");
                }
            }
        }

        public string FilePath
        {
            get
            {
                return this.auxApplication.FilePath;
            }
            set
            {
                if (this.auxApplication.FilePath != value)
                {
                    this.auxApplication.FilePath = value;
                    OnPropertyChanged("FilePath");
                }
            }
        }

        public string Argument
        {
            get
            {
                return this.auxApplication.Argument;
            }
            set
            {
                if (this.auxApplication.Argument != value)
                {
                    this.auxApplication.Argument = value;
                    OnPropertyChanged("Argument");
                }
            }
        }

        public bool KeepAlive
        {
            get
            {
                return this.auxApplication.KeepAlive;
            }
            set
            {
                if (this.auxApplication.KeepAlive != value)
                {
                    this.auxApplication.KeepAlive = value;
                    OnPropertyChanged("KeepAlive");
                }
            }
        }

        private bool _isExpanded;

        public bool IsExpanded
        {
            get
            {
                return _isExpanded;
            }
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged("IsExpanded");
                }
            }
        }


        private AuxiliaryApplication auxApplication;

        public AuxiliaryApplicationViewModel(AuxiliaryApplication auxApplication)
        {
            this.auxApplication = auxApplication;
            IsExpanded = false;
        }

        public AuxiliaryApplication GetAuxiliaryApplication()
        {
            return this.auxApplication;
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
            }
        }

        #endregion BrowseCommand


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
