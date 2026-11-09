using System.ComponentModel;
using System.Windows;
using PlexServiceTray.ViewModel;

namespace PlexServiceTray.Windows
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow
    {
        private readonly string _savedTheme;

        public SettingsViewModel ViewModel { get; }

        public SettingsWindow(SettingsViewModel viewModel)
        {
            ViewModel = viewModel;
            _savedTheme = viewModel.BaseTheme;
            Theme.Apply(_savedTheme);
            InitializeComponent();
            DataContext = viewModel;

            viewModel.PropertyChanged += (_, args) =>
            {
                // live preview of the theme
                if (args.PropertyName == nameof(SettingsViewModel.ThemeName)) Theme.Apply(viewModel.BaseTheme);
            };
            viewModel.CloseRequested += (_, _) => Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (ViewModel.IsDirty &&
                MessageBox.Show(this, "Discard your unsaved changes?", "Plex Service", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            // undo a theme preview that wasn't saved
            if (ViewModel.IsDirty) Theme.Apply(_savedTheme);
            ViewModel.Abandoned = true;
            base.OnClosing(e);
        }
    }
}
