using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace SAMM
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : System.Configuration.Install.Installer
    {
        public ProjectInstaller()
        {
            InitializeComponent();
            ServiceProcessInstaller serviceProcessInstaller = new ServiceProcessInstaller();
            ServiceInstaller serviceInstaller = new ServiceInstaller();

            // Service Account Information
            serviceProcessInstaller.Account = ServiceAccount.LocalSystem;
            serviceProcessInstaller.Username = null;
            serviceProcessInstaller.Password = null;

            // Service Information
            serviceInstaller.ServiceName = "SAMM";
            serviceInstaller.DisplayName = "SAMM";
            serviceInstaller.StartType = ServiceStartMode.Manual; // or automatic

            this.Installers.Add(serviceProcessInstaller);
            this.Installers.Add(serviceInstaller);

            this.AfterInstall += new InstallEventHandler(ProcessInstaller_AfterInstall);
        }

        private void serviceInstaller_AfterInstall_1(object sender, System.Configuration.Install.InstallEventArgs e)
        {
            
        }

        private void ProcessInstaller_AfterInstall(object sender, System.Configuration.Install.InstallEventArgs e)
        {
            ServiceController sc = new ServiceController("SAMM");
            // start immediately
            sc.Start();

        }
    }
}
