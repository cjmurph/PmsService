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
        private const string _address = "http://localhost:8787/PlexService";

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
            //_trayInteraction.Start(_serviceGuid);
            base.OnStart(args);
        }

        /// <summary>
        /// Fires when the service is stopped
        /// </summary>
        protected override void OnStop()
        {
            try
            {
                if (!connected())
                    connect();

                if (connected())
                {

                    _plexService.Stop();
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
