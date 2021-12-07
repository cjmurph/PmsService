using System;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceProcess;
using PlexServiceCommon;
using PlexServiceWCF;
using System.Threading;

namespace PlexService
{
    /// <summary>
    /// Service that runs an instance of PmsMonitor to maintain an instance of Plex Media Server in session 0
    /// </summary>
    public partial class PlexMediaServerService : ServiceBase
    {
        private const string BaseAddress = "net.tcp://localhost:{0}/PlexService";

        /// <summary>
        /// Default the address with port 8787
        /// </summary>
        private string _address = string.Format(BaseAddress, 8787);

        private static readonly TimeSpan TimeOut = TimeSpan.FromSeconds(2);

        private ServiceHost _host;

        private PlexServiceCommon.Interface.ITrayInteraction _plexService;

        private readonly AutoResetEvent _stopped = new(false);

        public PlexMediaServerService()
        {
            InitializeComponent();
            //This is a simple start stop service, no pause and resume.
            CanPauseAndContinue = false;
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

                _address = string.Format(BaseAddress, port);

                Uri[] addressBase = { new(_address) };
                _host = new ServiceHost(typeof(TrayInteraction), addressBase);

                var behave = new ServiceMetadataBehavior();
                _host.Description.Behaviors.Add(behave);

                //Setup a TCP binding with appropriate timeouts.
                //use a reliable connection so the clients can be notified when the receive timeout has elapsed and the connection is torn down.
                var netTcpB = new NetTcpBinding {
                    OpenTimeout = TimeOut,
                    CloseTimeout = TimeOut,
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
                LogWriter.WriteLine("Exception starting Plex Service: " + ex.Message + " at " + ex.StackTrace);
            }
            LogWriter.WriteLine("Plex Service Started.");

            base.OnStart(args);
        }

        private void StartPlex()
        {
            //Try and connect to the WCF service and call its start method
            try
            {
                if (_plexService == null) Connect();

                if (_plexService == null) {
                    return;
                }

                _plexService.Start();
                Disconnect();
            } catch (Exception ex) {
                LogWriter.WriteLine("Exception starting Plex: " + ex.Message);
            }
        }

        /// <summary>
        /// Fires when the service is stopped
        /// </summary>
        protected override void OnStop()
        {
            LogWriter.WriteLine("OnStop called.");
            if (_host != null)
            {
                //Try and connect to the WCF service and call its stop method
                try {
                    if (_plexService == null) {
                        LogWriter.WriteLine("Connecting to plex service.");
                        Connect();
                    }

                    if (_plexService != null)
                    {
                        LogWriter.WriteLine("Stopping plex service.");
                        _plexService.Stop();
                        //wait for plex to stop for 10 seconds
                        if(!_stopped.WaitOne(10000))
                        {
                            LogWriter.WriteLine("Timed out waiting for plexservice to stop.");
                        }
                        LogWriter.WriteLine("Disconnecting...");
                        Disconnect();
                    }
                } catch (Exception ex) {
                    LogWriter.WriteLine("Exception in OnStop: " + ex.Message);
                }

                try {
                    LogWriter.WriteLine("Closing host.");
                    _host.Close();
                } catch (Exception ex) {
                    LogWriter.WriteLine("Exception closing host: " + ex.Message);
                }
                finally
                {
                    LogWriter.WriteLine("Clearing host.");
                    _host = null;
                }
            }
            LogWriter.WriteLine("Plex Service Stopped.");
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
                OpenTimeout = TimeOut,
                CloseTimeout = TimeOut,
                SendTimeout = TimeOut,
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
                ((ICommunicationObject)_plexService).Faulted += (_, _) => _plexService = null;
                ((ICommunicationObject)_plexService).Closed += (_, _) => _plexService = null;
            }
            catch (Exception ex)
            {
                LogWriter.WriteLine("Exception connecting PMS/WCF: " + ex.Message);
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
                    ((ICommunicationObject)_plexService).Close();
                } catch (Exception ex) {
                    LogWriter.WriteLine("Exception disconnecting PMS/WCF: " + ex.Message);
                }
            }
            _plexService = null;
        }
    }
}
