using System.ServiceProcess;
using PlexServiceCommon;

namespace PlexService
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args) {
            LogWriter.Init();

#if (!DEBUG)
            var servicesToRun = new ServiceBase[] 
            { 
                new PlexMediaServerService() 
            };
            ServiceBase.Run(servicesToRun);
#else
            PlexMediaServerService serviceCall = new();
            serviceCall.OnDebug(args);
#endif

            //if (args.Length > 0 && args[0].ToUpper() == "DEBUG")
            //{
            //    System.Diagnostics.Debugger.Launch();
            //}

            //var servicesToRun = new ServiceBase[]
            //{
            //    new PlexMediaServerService()
            //};
            //ServiceBase.Run(servicesToRun);
        }
    }
}
