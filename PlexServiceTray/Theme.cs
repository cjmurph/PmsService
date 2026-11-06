using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Markup;

namespace PlexServiceTray
{
    /// <summary>
    /// Hosts the WPF-UI resources. The tray runs a WinForms message loop, so there is no App.xaml.
    /// </summary>
    internal static class Theme
    {
        public static void Initialize(string theme)
        {
            var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            if (app.Resources.MergedDictionaries.Count == 0)
            {
                app.Resources.MergedDictionaries.Add(new ThemesDictionary { Theme = ToApplicationTheme(theme) });
                app.Resources.MergedDictionaries.Add(new ControlsDictionary());
            }
            Apply(theme);
        }

        /// <summary>
        /// Applies Light or Dark together with the Windows accent color
        /// </summary>
        public static void Apply(string theme)
        {
            ApplicationThemeManager.Apply(ToApplicationTheme(theme), WindowBackdropType.Mica, true);
        }

        private static ApplicationTheme ToApplicationTheme(string theme) =>
            TrayApplicationSettings.ResolveTheme(theme) == "Dark" ? ApplicationTheme.Dark : ApplicationTheme.Light;
    }
}
