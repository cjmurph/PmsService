using ControlzEx.Theming;
using PlexServiceTray.ViewModel;
using System;
using System.Windows.Controls;

namespace PlexServiceTray.Windows
{
    /// <summary>
    /// Interaction logic for ConnectionSettingsWindow.xaml
    /// </summary>
    public partial class TrayApplicationSettingsWindow
    {
        public TrayApplicationSettingsViewModel Context { get; }
        public TrayApplicationSettingsWindow(string theme)
        {
            InitializeComponent();
            Context = new TrayApplicationSettingsViewModel();
            Context.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(TrayApplicationSettingsViewModel.Theme))
                    ChangeTheme(Context.Theme?.Replace(" ", "."));
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
