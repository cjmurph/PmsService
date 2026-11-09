using System.Windows;
using PlexServiceTray.ViewModel;

namespace PlexServiceTray.Windows
{
    /// <summary>
    /// Lets the user pick the libraries plex should update
    /// </summary>
    public partial class LibraryUpdateWindow
    {
        public LibraryUpdateWindow(LibraryUpdateViewModel viewModel, string theme)
        {
            Theme.Apply(theme);
            InitializeComponent();
            DataContext = viewModel;
            viewModel.Updated += (_, _) => Close();
            Loaded += async (_, _) => await viewModel.LoadAsync();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
