using ControlzEx.Theming;
using PlexServiceTray.ViewModel;

namespace PlexServiceTray.Windows
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow
    {
        public SettingsViewModel Context { get; set; }
        public SettingsWindow(SettingsViewModel settingsViewModel, string theme)
        {
            InitializeComponent();
            Context = settingsViewModel;
            settingsViewModel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(Context.Theme))
                    ChangeTheme(Context.Theme.Replace(" ", "."));
            };
            DataContext = Context;
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
