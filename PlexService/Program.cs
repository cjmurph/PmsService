using System.ServiceProcess;

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

            var servicesToRun = new ServiceBase[] 
            { 
                new PlexMediaServerService() 
            };
            ServiceBase.Run(servicesToRun);
        }
    }
}
