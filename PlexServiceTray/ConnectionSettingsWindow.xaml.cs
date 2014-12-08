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
    /// Interaction logic for ConnectionSettingsWindow.xaml
    /// </summary>
    public partial class ConnectionSettingsWindow : Window
    {
        public ConnectionSettingsWindow()
        {
            InitializeComponent();
            DataContext = new ConnectionSettingsViewModel();
        }
    }
}
