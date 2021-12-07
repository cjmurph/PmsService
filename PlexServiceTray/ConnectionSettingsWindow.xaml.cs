using ControlzEx.Theming;

namespace PlexServiceTray
{
    /// <summary>
    /// Interaction logic for ConnectionSettingsWindow.xaml
    /// </summary>
    public partial class ConnectionSettingsWindow {
        public ConnectionSettingsWindow(string theme)
        {
            InitializeComponent();
            DataContext = new ConnectionSettingsViewModel();
            ThemeManager.Current.ChangeTheme(this, theme);
        }
    }
}
