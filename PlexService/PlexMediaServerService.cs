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

namespace PlexService
{
    /// <summary>
    /// Service that runs an instance of PmsMonitor to maintain an instance of Plex Media Server in session 0
    /// </summary>
    public partial class PlexMediaServerService : ServiceBase
    {
        private const string _baseAddress = "http://localhost:{0}/PlexService";

        /// <summary>
        /// Default the address with port 8787
        /// </summary>
        private string _address = string.Format(_baseAddress, 8787);

        private readonly static TimeSpan _timeOut = TimeSpan.FromMilliseconds(2000);

        private ServiceHost _host;

        private PlexServiceCommon.Interface.ITrayInteraction _plexService;

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

                ServiceMetadataBehavior mBehave = new ServiceMetadataBehavior();
                _host.Description.Behaviors.Add(mBehave);

                WSHttpBinding httpb = new WSHttpBinding();
                _host.AddServiceEndpoint(typeof(PlexServiceCommon.Interface.ITrayInteraction), httpb, _address);
                _host.AddServiceEndpoint(typeof(IMetadataExchange),
                MetadataExchangeBindings.CreateMexHttpBinding(), "mex");

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

        /// <summary>
        /// Fires when the service is stopped
        /// </summary>
        protected override void OnStop()
        {
            //Try and connect to the WCF service and call its stop method
            try
            {
                if (!connected())
                    connect();

                if (connected())
                {

                    _plexService.Stop();
                    disconnect();
                }
            }
            catch { }
            TrayInteraction.WriteToLog("Plex Service Stopped");
            if (_host != null)
            {
                _host.Close();
                _host = null;
            }
            base.OnStop();
        }

        /// <summary>
        /// Connect to the WCF service
        /// </summary>
        private void connect()
        {
            var plexServiceBinding = new WSHttpBinding();
            var plexServiceEndpoint = new EndpointAddress(_address);
            var plexServiceChannelFactory = new ChannelFactory<PlexServiceCommon.Interface.ITrayInteraction>(plexServiceBinding, plexServiceEndpoint);

            _plexService = null;

            try
            {
                _plexService = plexServiceChannelFactory.CreateChannel();
                ((ICommunicationObject)_plexService).Open(_timeOut);
            }
            catch
            {
                if (_plexService != null)
                {
                    ((ICommunicationObject)_plexService).Abort();
                }
            }
        }

        /// <summary>
        /// Disconnect from the WCF instance
        /// </summary>
        private void disconnect()
        {
            if (_plexService != null && connected())
            {
                try
                {
                    ((ICommunicationObject)_plexService).Close();
                }
                catch { }
            }
            _plexService = null;
        }

        /// <summary>
        /// Check connection to the WCF service
        /// </summary>
        /// <returns></returns>
        private bool connected()
        {
            if (_plexService != null)
            {
                try
                {
                    if (((ICommunicationObject)_plexService).State == CommunicationState.Opened)
                    {
                        return true;
                    }
                }
                catch { }
            }
            return false;
        }
    }
}
