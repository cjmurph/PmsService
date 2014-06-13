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

        public override void Install(IDictionary stateSaver)
        {
            //install as the selected type
            if (Context.Parameters["credentialType"] == "Configurable")
            {
                plexServiceProcessInstaller.Account = ServiceAccount.User;
                plexServiceProcessInstaller.Username = Context.Parameters["user"];
                plexServiceProcessInstaller.Password = Context.Parameters["pwd"];
            }
            else
            {
                switch (Context.Parameters["predefined"])
                {
                    case "Local System":
                        plexServiceProcessInstaller.Account = ServiceAccount.LocalSystem;
                        break;
                    case "Network Service":
                        plexServiceProcessInstaller.Account = ServiceAccount.NetworkService;
                        break;
                    case "Local Service":
                        plexServiceProcessInstaller.Account = ServiceAccount.LocalService;
                        break;
                    default:
                        plexServiceProcessInstaller.Account = ServiceAccount.LocalSystem;
                        break;
                }
                plexServiceProcessInstaller.Username = null;
                plexServiceProcessInstaller.Password = null;
            }

            base.Install(stateSaver);
        }

        /// <summary>
        /// Start the service after install.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void plexServiceInstaller_AfterInstall(object sender, InstallEventArgs e)
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

        private void plexServiceInstaller_Committed(object sender, InstallEventArgs e)
        {
            int exitCode;
            using (var process = new System.Diagnostics.Process())
            {
                var startInfo = process.StartInfo;
                startInfo.FileName = "sc";
                startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;

                // tell Windows that the service should restart if it fails
                startInfo.Arguments = string.Format("failure \"{0}\" reset= 0 actions= restart/60000", plexServiceInstaller.ServiceName);

                process.Start();
                process.WaitForExit();

                exitCode = process.ExitCode;
            }

            if (exitCode != 0)
            {
                PlexMediaServerService.WriteToLog("Unable to set recovery options for service.");
            }
            else
            {
                PlexMediaServerService.WriteToLog("Recovery options for service set to restart on fail.");
            }
        }
    }
}
