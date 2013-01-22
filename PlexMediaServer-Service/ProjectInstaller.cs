using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.ServiceProcess;


namespace PlexMediaServer_Service
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : System.Configuration.Install.Installer
    {
        public ProjectInstaller()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Start the service after install.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ProjectInstaller_AfterInstall(object sender, InstallEventArgs e)
        {
            using(ServiceController s = new ServiceController(plexServiceInstaller.ServiceName))
            {
                try
                {
                    s.Start();
                }
                catch
                {
                }
            }
        }
    }
}
