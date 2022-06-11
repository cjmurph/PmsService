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
            ChangeTheme(theme);
        }

        public void ChangeTheme(string theme)
        {
            if (string.IsNullOrEmpty(theme)) return;
            try
            {
                _ = ThemeManager.Current.ChangeTheme(this, theme);
            }
            catch { }
        }
    }
}
