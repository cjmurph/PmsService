using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;

namespace PlexService
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            if (args.Length > 0 && args[0].ToUpper() == "DEBUG")
            {
                System.Diagnostics.Debugger.Launch();
            }
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[] 
			{ 
				new PlexMediaServerService() 
			};
            ServiceBase.Run(ServicesToRun);
        }
    }
}
