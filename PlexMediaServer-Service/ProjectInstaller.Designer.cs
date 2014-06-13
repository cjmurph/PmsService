namespace PlexMediaServer_Service
{
    partial class ProjectInstaller
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.plexServiceProcessInstaller = new System.ServiceProcess.ServiceProcessInstaller();
            this.plexServiceInstaller = new System.ServiceProcess.ServiceInstaller();
            // 
            // plexServiceProcessInstaller
            // 
            this.plexServiceProcessInstaller.Password = null;
            this.plexServiceProcessInstaller.Username = null;
            // 
            // plexServiceInstaller
            // 
            this.plexServiceInstaller.Description = "Plex Media Server Service";
            this.plexServiceInstaller.DisplayName = "PmsService";
            this.plexServiceInstaller.ServiceName = "PlexMediaServerService";
            this.plexServiceInstaller.ServicesDependedOn = new string[] {
        "LanmanServer"};
            this.plexServiceInstaller.StartType = System.ServiceProcess.ServiceStartMode.Automatic;
            this.plexServiceInstaller.Committed += new System.Configuration.Install.InstallEventHandler(this.plexServiceInstaller_Committed);
            this.plexServiceInstaller.AfterInstall += new System.Configuration.Install.InstallEventHandler(this.plexServiceInstaller_AfterInstall);
            // 
            // ProjectInstaller
            // 
            this.Installers.AddRange(new System.Configuration.Install.Installer[] {
            this.plexServiceProcessInstaller,
            this.plexServiceInstaller});

        }

        #endregion

        private System.ServiceProcess.ServiceProcessInstaller plexServiceProcessInstaller;
        private System.ServiceProcess.ServiceInstaller plexServiceInstaller;
    }
}