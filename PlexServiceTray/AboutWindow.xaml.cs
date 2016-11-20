using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PlexServiceTray
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        public string Version
        {
            get { return (string)GetValue(VersionProperty); }
            set { SetValue(VersionProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Version.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty VersionProperty =
            DependencyProperty.Register("Version", typeof(string), typeof(AboutWindow), new PropertyMetadata(string.Empty));


        public string Help
        {
            get { return (string)GetValue(HelpProperty); }
            set { SetValue(HelpProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Help.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty HelpProperty =
            DependencyProperty.Register("Help", typeof(string), typeof(AboutWindow), new PropertyMetadata(string.Empty));


        public string HelpLink
        {
            get { return (string)GetValue(HelpLinkProperty); }
            set { SetValue(HelpLinkProperty, value); }
        }

        // Using a DependencyProperty as the backing store for HelpLink.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty HelpLinkProperty =
            DependencyProperty.Register("HelpLink", typeof(string), typeof(AboutWindow), new PropertyMetadata("https://github.com/cjmurph/PmsService/issues"));


        public string HelpLinkDisplayText
        {
            get { return (string)GetValue(HelpLinkDisplayTextProperty); }
            set { SetValue(HelpLinkDisplayTextProperty, value); }
        }

        // Using a DependencyProperty as the backing store for HelpLinkDisplayText.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty HelpLinkDisplayTextProperty =
            DependencyProperty.Register("HelpLinkDisplayText", typeof(string), typeof(AboutWindow), new PropertyMetadata("PMS Service GitHub Project Issues"));


        public string File
        {
            get { return (string)GetValue(FileProperty); }
            set { SetValue(FileProperty, value); }
        }

        // Using a DependencyProperty as the backing store for File.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty FileProperty =
            DependencyProperty.Register("File", typeof(string), typeof(AboutWindow), new PropertyMetadata(string.Empty));


        public bool? DialogueResult
        {
            get { return (bool?)GetValue(DialogueResultProperty); }
            set { SetValue(DialogueResultProperty, value); }
        }

        // Using a DependencyProperty as the backing store for DialogueResult.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DialogueResultProperty =
            DependencyProperty.Register("DialogueResult", typeof(bool?), typeof(AboutWindow), new PropertyMetadata(null));


        public AboutWindow()
        {
            InitializeComponent();
            File = "LICENCE.rtf";
            Version = string.Format("PMS Service {0}", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString());
            Help = "Please report any bugs or issues to:";
            DataContext = this;
        }

        private void Title_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        #region OkCommand
        RelayCommand _okCommand = null;
        public ICommand OkCommand
        {
            get
            {
                if (_okCommand == null)
                {
                    _okCommand = new RelayCommand((p) => OnOk(p), (p) => CanOk(p));
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

        public static bool? ShowAboutDialog()
        {
            return new AboutWindow().ShowDialog();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
    }
}
