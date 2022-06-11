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
                    ThemeChanged();
            };
            DataContext = Context;
            ThemeManager.Current.ChangeTheme(this, theme);
        }

        private void ThemeChanged()
        {
            if (string.IsNullOrEmpty(Context?.Theme)) return;
            try
            {
                ThemeManager.Current.ChangeTheme(this, Context.Theme?.Replace(" ", "."));
            }
            catch { //let it go
            }
        }
    }
}
