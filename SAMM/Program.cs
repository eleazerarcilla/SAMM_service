using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using NLog;
using System.Configuration.Install;

namespace SAMM
{
    partial class Program : ServiceBase
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        public Logger log = LogManager.GetCurrentClassLogger();
        static void Main(string[] args)
        {
            if (System.Environment.UserInteractive)
            {
                string parameter = string.Concat(args);
                try
                {
                    switch (parameter)
                    {
                        case "-test":
                            Service1 sammService = new Service1();
                           // sammService.PerformGETCallback();
                            break;
                    }
                }
                catch (Exception)
                {

                    throw;
                }
            }
            else
            {
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[]
                {
                new Service1()
                };
                ServiceBase.Run(ServicesToRun);

            }
        }
    }
}
