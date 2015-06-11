using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using PlexServiceCommon;
using System.Windows.Input;
using Microsoft.Win32;
using Ookii.Dialogs.Wpf;

namespace PlexServiceTray
{
    public class AuxiliaryApplicationViewModel:INotifyPropertyChanged
    {
        #region Properties

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

        #endregion Properties

        private AuxiliaryApplication _auxApplication;

        public AuxiliaryApplicationViewModel(AuxiliaryApplication auxApplication)
        {
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
