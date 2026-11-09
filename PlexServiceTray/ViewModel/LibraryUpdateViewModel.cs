using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using PlexServiceCommon;

namespace PlexServiceTray.ViewModel
{
    public class LibraryItemViewModel : ViewModelBase
    {
        private readonly LibraryUpdateViewModel _owner;
        private bool _isSelected;

        internal LibraryItemViewModel(PlexLibrary library, LibraryUpdateViewModel owner)
        {
            _owner = owner;
            Key = library.Key;
            Title = library.Title;
            Type = library.Type switch
            {
                "movie" => "Movies",
                "show" => "TV shows",
                "artist" => "Music",
                "photo" => "Photos",
                _ => library.Type
            };
        }

        public string Key { get; }

        public string Title { get; }

        public string Type { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value)) _owner.SelectionChanged();
            }
        }

        internal void Select(bool value) => SetProperty(ref _isSelected, value, nameof(IsSelected));
    }

    public class LibraryUpdateViewModel : ViewModelBase
    {
        private readonly ServiceClient _service;

        internal LibraryUpdateViewModel(ServiceClient service)
        {
            _service = service;
        }

        /// <summary>
        /// Raised after the update was requested
        /// </summary>
        public event EventHandler Updated;

        public ObservableCollection<LibraryItemViewModel> Libraries { get; } = new();

        private bool _isLoading = true;
        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        private string _message;
        public string Message
        {
            get => _message;
            private set => SetProperty(ref _message, value);
        }

        public bool HasLibraries => Libraries.Count > 0;

        private int SelectedCount => Libraries.Count(l => l.IsSelected);

        /// <summary>
        /// null when only some libraries are selected
        /// </summary>
        public bool? AllSelected
        {
            get
            {
                var count = SelectedCount;
                return count == 0 ? false : count == Libraries.Count ? true : null;
            }
            set
            {
                var select = value == true;
                foreach (var library in Libraries) library.Select(select);
                SelectionChanged();
            }
        }

        public string UpdateText
        {
            get
            {
                var count = SelectedCount;
                if (count == 0) return "Update";
                if (count == Libraries.Count) return "Update all";
                return count == 1 ? "Update 1 library" : $"Update {count} libraries";
            }
        }

        internal void SelectionChanged()
        {
            OnPropertyChanged(nameof(HasLibraries));
            OnPropertyChanged(nameof(AllSelected));
            OnPropertyChanged(nameof(UpdateText));
            CommandManager.InvalidateRequerySuggested();
        }

        public async Task LoadAsync()
        {
            PlexLibrary[] libraries = null;
            try
            {
                libraries = await Task.Run(() => _service.Call(c => c.GetLibraries(), null));
            }
            catch (Exception ex)
            {
                Message = ex.Message;
            }

            IsLoading = false;
            if (libraries == null)
            {
                Message ??= "The libraries could not be loaded. Make sure Plex is running.";
                return;
            }

            foreach (var library in libraries) Libraries.Add(new LibraryItemViewModel(library, this));
            if (Libraries.Count == 0) Message = "This Plex server has no libraries yet.";
            SelectionChanged();
        }

        private RelayCommand _updateCommand;
        public RelayCommand UpdateCommand => _updateCommand ??= new RelayCommand(_ =>
        {
            // one request for everything, that's what Plex does itself for "scan all"
            var all = AllSelected == true;
            var keys = Libraries.Where(l => l.IsSelected).Select(l => l.Key).ToArray();
            var sent = _service.Call(c =>
            {
                if (all) c.UpdateAllLibraries();
                else c.UpdateLibraries(keys);
                return true;
            }, false);

            if (sent) Updated?.Invoke(this, EventArgs.Empty);
            else Message = "The update could not be requested. " + _service.LastError;
        }, _ => SelectedCount > 0);
    }
}
