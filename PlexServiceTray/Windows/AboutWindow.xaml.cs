using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using ControlzEx.Theming;
using PlexServiceTray.ViewModel;

namespace PlexServiceTray.Windows
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow
    {
        public string Version
        {
            get => (string)GetValue(VersionProperty);
            set => SetValue(VersionProperty, value);
        }

        // Using a DependencyProperty as the backing store for Version.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty VersionProperty =
            DependencyProperty.Register("Version", typeof(string), typeof(AboutWindow), new PropertyMetadata(string.Empty));


        public string Help
        {
            get => (string)GetValue(HelpProperty);
            set => SetValue(HelpProperty, value);
        }

        // Using a DependencyProperty as the backing store for Help.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty HelpProperty =
            DependencyProperty.Register("Help", typeof(string), typeof(AboutWindow), new PropertyMetadata(string.Empty));


        public string HelpLink
        {
            get => (string)GetValue(HelpLinkProperty);
            set => SetValue(HelpLinkProperty, value);
        }

        // Using a DependencyProperty as the backing store for HelpLink.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty HelpLinkProperty =
            DependencyProperty.Register("HelpLink", typeof(string), typeof(AboutWindow), new PropertyMetadata("https://github.com/cjmurph/PmsService/issues"));


        public string HelpLinkDisplayText
        {
            get => (string)GetValue(HelpLinkDisplayTextProperty);
            set => SetValue(HelpLinkDisplayTextProperty, value);
        }

        // Using a DependencyProperty as the backing store for HelpLinkDisplayText.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty HelpLinkDisplayTextProperty =
            DependencyProperty.Register("HelpLinkDisplayText", typeof(string), typeof(AboutWindow), new PropertyMetadata("PMS Service GitHub Project Issues"));


        public string File
        {
            get => (string)GetValue(FileProperty);
            set => SetValue(FileProperty, value);
        }

        // Using a DependencyProperty as the backing store for File.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty FileProperty =
            DependencyProperty.Register("File", typeof(string), typeof(AboutWindow), new PropertyMetadata(string.Empty));


        public bool? DialogueResult
        {
            get => (bool?)GetValue(DialogueResultProperty);
            set => SetValue(DialogueResultProperty, value);
        }

        // Using a DependencyProperty as the backing store for DialogueResult.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DialogueResultProperty =
            DependencyProperty.Register("DialogueResult", typeof(bool?), typeof(AboutWindow), new PropertyMetadata(null));


        public AboutWindow(string theme)
        {
            InitializeComponent();
            ChangeTheme(theme);
            File = "LICENCE.rtf";
            Version = $"PMS Service {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}";
            Help = "Please report any bugs or issues to:";
            DataContext = this;
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

        private void Title_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        #region OkCommand
        RelayCommand _okCommand;
        public RelayCommand OkCommand
        {
            get
            {
                if (_okCommand == null)
                {
                    _okCommand = new RelayCommand(p => OnOk(p), p => CanOk(p));
                }

                return _okCommand;
            }
        }

        private bool CanOk(object parameter)
        {
            return true;
        }

        private void OnOk(object parameter)
        {
            DialogResult = true;
        }

        #endregion OkCommand

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
    }
}
