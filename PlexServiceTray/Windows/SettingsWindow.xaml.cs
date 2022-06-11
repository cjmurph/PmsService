using System.Windows;
using System.Windows.Input;
using ControlzEx.Theming;
using PlexServiceTray.ViewModel;

namespace PlexServiceTray.Windows
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow
    {
        private bool _maximiseRequired;
        
        public SettingsWindow(SettingsWindowViewModel settingsViewModel, string theme)
        {
            InitializeComponent();
            DataContext = settingsViewModel;
            if (!string.IsNullOrEmpty(theme)) ThemeManager.Current.ChangeTheme(this, theme);
        }

        private void TitleMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount >= 2)
            {
                _maximiseRequired = true;
            }
            else
            {
                DragMove();
            }
        }

        private void TitleMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            if (!_maximiseRequired) {
                return;
            }
            _maximiseRequired = false;
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
    }
}
