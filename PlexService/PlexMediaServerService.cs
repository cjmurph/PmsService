using System;
using System.ServiceModel;
using System.ServiceProcess;
using PlexServiceCommon;
using PlexServiceWCF;
using Serilog;

namespace PlexService
{
    /// <summary>
    /// Service that runs an instance of PmsMonitor to maintain an instance of Plex Media Server in session 0
    /// </summary>
    public partial class PlexMediaServerService : ServiceBase
    {
        /// <summary>
        /// Custom service commands, usable with "sc control PlexService 128|129|130"
        /// </summary>
        private const int CommandStartPlex = 128;
        private const int CommandStopPlex = 129;
        private const int CommandRestartPlex = 130;

        private const string BaseAddress = "net.tcp://localhost:{0}/PlexService";

        private static readonly TimeSpan TimeOut = TimeSpan.FromSeconds(2);

        private ServiceHost _host;

        private TrayInteraction _trayInteraction;

        public PlexMediaServerService()
        {
            InitializeComponent();
            //This is a simple start stop service, no pause and resume.
            CanPauseAndContinue = false;
            CanShutdown = true;
        }

        public void OnDebug(string[] args)
        {
            OnStart(args);
            Console.WriteLine("Press any key to exit...");
            Console.ReadLine();
            Console.WriteLine("Stopping Plex...");
            OnStop();
        }

        /// <summary>
        /// Fires when the service is started
        /// </summary>
        /// <param name="args"></param>
        protected override void OnStart(string[] args)
        {
            try
            {
                _host?.Close();

                var port = SettingsHandler.Load().ServerPort;
                var address = string.Format(BaseAddress, port);

                _trayInteraction = new TrayInteraction();
                _host = new ServiceHost(_trayInteraction, new Uri(address));
                _host.Authorization.ServiceAuthorizationManager = new AdminAuthorizationManager();

                //Setup a TCP binding with appropriate timeouts.
                //use a reliable connection so the clients can be notified when the receive timeout has elapsed and the connection is torn down.
                var netTcpB = new NetTcpBinding(SecurityMode.Transport) {
                    OpenTimeout = TimeOut,
                    CloseTimeout = TimeOut,
                    ReceiveTimeout = TimeSpan.FromMinutes(10),
                    ReliableSession = {
                        Enabled = true,
                        InactivityTimeout = TimeSpan.FromMinutes(5)
                    }
                };
                netTcpB.Security.Transport.ClientCredentialType = TcpClientCredentialType.Windows;
                _host.AddServiceEndpoint(typeof(PlexServiceCommon.Interface.ITrayInteraction), netTcpB, address);
                _host.Open();
                Log.Information($"Plex Service listening on {address}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception starting Plex Service");
            }

            // start plex even if the endpoint couldn't be opened, the tray is optional
            _trayInteraction?.Start();
            Log.Information("Plex Service Started.");

            base.OnStart(args);
        }

        /// <summary>
        /// Fires when the service is stopped
        /// </summary>
        protected override void OnStop()
        {
            RequestAdditionalTime(30000);
            try
            {
                Log.Information("Stopping plex.");
                _trayInteraction?.Shutdown(TimeSpan.FromSeconds(20));
            }
            catch (Exception ex)
            {
                Log.Warning("Exception stopping plex: " + ex.Message);
            }

            if (_host != null)
            {
                try {
                    _host.Close(TimeOut);
                } catch (Exception ex) {
                    Log.Warning("Exception closing host: " + ex.Message);
                    _host.Abort();
                }
                finally
                {
                    _host = null;
                }
            }
            Log.Information("Plex Service Stopped.");
            Log.CloseAndFlush();
            base.OnStop();
        }

        protected override void OnShutdown()
        {
            OnStop();
            base.OnShutdown();
        }

        protected override void OnCustomCommand(int command)
        {
            if (_trayInteraction == null) return;
            switch (command)
            {
                case CommandStartPlex:
                    Log.Information("Start requested via service control");
                    _trayInteraction.Start();
                    break;
                case CommandStopPlex:
                    Log.Information("Stop requested via service control");
                    _trayInteraction.Stop();
                    break;
                case CommandRestartPlex:
                    Log.Information("Restart requested via service control");
                    _trayInteraction.Restart();
                    break;
                default:
                    base.OnCustomCommand(command);
                    break;
            }
        }
    }
}
