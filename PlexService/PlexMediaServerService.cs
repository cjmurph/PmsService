using System;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceProcess;
using PlexServiceCommon;
using PlexServiceWCF;
using System.Threading;
using Serilog;

namespace PlexService
{
    /// <summary>
    /// Service that runs an instance of PmsMonitor to maintain an instance of Plex Media Server in session 0
    /// </summary>
    public partial class PlexMediaServerService : ServiceBase
    {
        private const string _baseAddress = "net.tcp://localhost:{0}/PlexService";

        /// <summary>
        /// Default the address with port 8787
        /// </summary>
        private string _address = string.Format(_baseAddress, 8787);

        private static readonly TimeSpan _timeOut = TimeSpan.FromSeconds(2);

        private ServiceHost _host;

        private PlexServiceCommon.Interface.ITrayInteraction _plexService;

        private readonly AutoResetEvent _stopped = new(false);

        public PlexMediaServerService()
        {
            InitializeComponent();
            //This is a simple start stop service, no pause and resume.
            CanPauseAndContinue = false;
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
                if (_host != null) _host.Close();

                var port = SettingsHandler.Load().ServerPort;
                //sanity check the port setting
                if (port == 0)
                    port = 8787;

                _address = string.Format(_baseAddress, port);

                Uri[] addressBase = { new(_address) };
                _host = new ServiceHost(typeof(TrayInteraction), addressBase);

                var behave = new ServiceMetadataBehavior();
                _host.Description.Behaviors.Add(behave);

                //Setup a TCP binding with appropriate timeouts.
                //use a reliable connection so the clients can be notified when the receive timeout has elapsed and the connection is torn down.
                var netTcpB = new NetTcpBinding {
                    OpenTimeout = _timeOut,
                    CloseTimeout = _timeOut,
                    ReceiveTimeout = TimeSpan.FromMinutes(10),
                    ReliableSession = {
                        Enabled = true,
                        InactivityTimeout = TimeSpan.FromMinutes(5)
                    }
                };
                _host.AddServiceEndpoint(typeof(PlexServiceCommon.Interface.ITrayInteraction), netTcpB, _address);
                _host.AddServiceEndpoint(typeof(IMetadataExchange),
                MetadataExchangeBindings.CreateMexTcpBinding(), "mex");
                
                //once the host is opened, start plex
                //_host.Opened += (s, e) => System.Threading.Tasks.Task.Factory.StartNew(() => startPlex());
                // Open the ServiceHostBase to create listeners and start 
                // listening for messages.
                _host.Open();
            }
            catch (Exception ex)
            {
                Log.Warning("Exception starting Plex Service: " + ex.Message + " at " + ex.StackTrace);
            }
            Log.Information("Plex Service Started.");

            base.OnStart(args);
        }

        /// <summary>
        /// Fires when the service is stopped
        /// </summary>
        protected override void OnStop()
        {
            if (_host != null)
            {
                //Try and connect to the WCF service and call its stop method
                try {
                    if (_plexService == null) {
                        Log.Information("Connecting to plex service.");
                        Connect();
                    }

                    if (_plexService != null)
                    {
                        Log.Information("Stopping plex service.");
                        _plexService.Stop();
                        //wait for plex to stop for 10 seconds
                        if(!_stopped.WaitOne(10000))
                        {
                            Log.Warning("Timed out waiting for plex service to stop.");
                        }
                        Disconnect();
                    }
                } catch (Exception ex) {
                    Log.Warning("Exception in OnStop: " + ex.Message);
                }

                try {
                    _host.Close();
                } catch (Exception ex) {
                    Log.Warning("Exception closing host: " + ex.Message);
                }
                finally
                {
                    _host = null;
                }
            }
            Log.Information("Plex Service Stopped.");
            base.OnStop();
        }

        /// <summary>
        /// Connect to WCF service
        /// </summary>
        private void Connect()
        {
            //Create a NetTcp binding to the service and set some appropriate timeouts.
            //Use reliable connection so we know when we have been disconnected
            var plexServiceBinding = new NetTcpBinding {
                OpenTimeout = _timeOut,
                CloseTimeout = _timeOut,
                SendTimeout = _timeOut,
                ReliableSession = {
                    Enabled = true,
                    InactivityTimeout = TimeSpan.FromMinutes(1)
                }
            };
            //Generate the endpoint from the local settings
            var plexServiceEndpoint = new EndpointAddress(_address);

            var callback = new TrayCallback();
            callback.Stopped += (_,_) => _stopped.Set();
            var client = new TrayInteractionClient(callback, plexServiceBinding, plexServiceEndpoint);

            //Make a channel factory so we can create the link to the service
            //var plexServiceChannelFactory = new ChannelFactory<PlexServiceCommon.Interface.ITrayInteraction>(plexServiceBinding, plexServiceEndpoint);

            _plexService = null;

            try
            {
                _plexService = client.ChannelFactory.CreateChannel();
                
                _plexService.Subscribe();
                //If we lose connection to the service, set the object to null so we will know to reconnect the next time the tray icon is clicked
                _plexService.Faulted += (_, _) => _plexService = null;
                _plexService.Closed += (_, _) => _plexService = null;
            }
            catch (Exception ex)
            {
                Log.Warning("Exception connecting PMS/WCF: " + ex.Message);
                _plexService = null;
            }
        }

        /// <summary>
        /// Disconnect from WCF service
        /// </summary>
        private void Disconnect()
        {
            //try and be nice...
            if (_plexService != null)
            {
                try
                {
                    _plexService.Close();
                } catch (Exception ex) {
                    Log.Warning("Exception disconnecting PMS/WCF: " + ex.Message);
                }
            }
            _plexService = null;
        }
    }
}
