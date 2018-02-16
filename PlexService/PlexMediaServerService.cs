using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceProcess;
using System.Text;
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
        private const string _baseAddress = "net.tcp://localhost:{0}/PlexService";

        /// <summary>
        /// Default the address with port 8787
        /// </summary>
        private string _address = string.Format(_baseAddress, 8787);

        private readonly static TimeSpan _timeOut = TimeSpan.FromSeconds(2);

        private ServiceHost _host;

        private PlexServiceCommon.Interface.ITrayInteraction _plexService;

        private AutoResetEvent _stopped = new AutoResetEvent(false);

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

                int port = SettingsHandler.Load().ServerPort;
                //sanity check the port setting
                if (port == 0)
                    port = 8787;

                _address = string.Format(_baseAddress, port);

                Uri[] adrbase = { new Uri(_address) };
                _host = new ServiceHost(typeof(TrayInteraction), adrbase);

                ServiceMetadataBehavior behave = new ServiceMetadataBehavior();
                _host.Description.Behaviors.Add(behave);

                //Setup a TCP binding with appropriate timeouts.
                //use a reliable connection so the clients can be notified when the recieve timeout has elapsed and the connection is torn down.
                NetTcpBinding netTcpB = new NetTcpBinding();
                netTcpB.OpenTimeout = _timeOut;
                netTcpB.CloseTimeout = _timeOut;
                netTcpB.ReceiveTimeout = TimeSpan.FromMinutes(10);
                netTcpB.ReliableSession.Enabled = true;
                netTcpB.ReliableSession.InactivityTimeout = TimeSpan.FromMinutes(5);
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
                TrayInteraction.WriteToLog(ex.Message);
            }
            TrayInteraction.WriteToLog("Plex Service Started");

            base.OnStart(args);
        }

        private void StartPlex()
        {
            //Try and connect to the WCF service and call its start method
            try
            {
                if (_plexService == null)
                    Connect();

                if (_plexService != null)
                {
                    _plexService.Start();
                    Disconnect();
                }
            }
            catch { }
        }

        /// <summary>
        /// Fires when the service is stopped
        /// </summary>
        protected override void OnStop()
        {
            if (_host != null)
            {
                //Try and connect to the WCF service and call its stop method
                try
                {
                    if (_plexService == null)
                        Connect();

                    if (_plexService != null)
                    {
                        _plexService.Stop();
                        //wait for plex to stop for 10 seconds
                        if(!_stopped.WaitOne(10000))
                        {
                            TrayInteraction.WriteToLog("Timed out waiting for plex to stop");
                        }
                        Disconnect();
                    }
                }
                catch { }
                try
                {
                    _host.Close();
                }
                finally
                {
                    _host = null;
                }
            }
            TrayInteraction.WriteToLog("Plex Service Stopped");
            base.OnStop();
        }

        /// <summary>
        /// Connect to WCF service
        /// </summary>
        private void Connect()
        {
            //Create a NetTcp binding to the service and set some appropriate timeouts.
            //Use reliable connection so we know when we have been disconnected
            var plexServiceBinding = new NetTcpBinding();
            plexServiceBinding.OpenTimeout = _timeOut;
            plexServiceBinding.CloseTimeout = _timeOut;
            plexServiceBinding.SendTimeout = _timeOut;
            plexServiceBinding.ReliableSession.Enabled = true;
            plexServiceBinding.ReliableSession.InactivityTimeout = TimeSpan.FromMinutes(1);
            //Generate the endpoint from the local settings
            var plexServiceEndpoint = new EndpointAddress(_address);

            TrayCallback callback = new TrayCallback();
            callback.Stopped += (s,e) => _stopped.Set();
            var client = new TrayInteractionClient(callback, plexServiceBinding, plexServiceEndpoint);

            //Make a channel factory so we can create the link to the service
            //var plexServiceChannelFactory = new ChannelFactory<PlexServiceCommon.Interface.ITrayInteraction>(plexServiceBinding, plexServiceEndpoint);

            _plexService = null;

            try
            {
                _plexService = client.ChannelFactory.CreateChannel();
                
                _plexService.Subscribe();
                //If we lose connection to the service, set the object to null so we will know to reconnect the next time the tray icon is clicked
                ((ICommunicationObject)_plexService).Faulted += (s, e) => _plexService = null;
                ((ICommunicationObject)_plexService).Closed += (s, e) => _plexService = null;
            }
            catch
            {
                if (_plexService != null)
                {
                    _plexService = null;
                }
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
                }
                catch { }
            }
            _plexService = null;
        }
    }
}
