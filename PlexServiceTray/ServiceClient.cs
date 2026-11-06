#nullable enable
using System;
using System.ServiceModel;
using System.ServiceModel.Security;
using PlexServiceCommon;
using PlexServiceCommon.Interface;
using Serilog.Events;

namespace PlexServiceTray
{
    /// <summary>
    /// Wraps the WCF connection to the service and reconnects on demand
    /// </summary>
    internal sealed class ServiceClient : IDisposable
    {
        private ITrayInteraction? _channel;

        private TrayApplicationSettings _settings;

        public ServiceClient(TrayApplicationSettings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// Raised when the state of plex changes, on the thread the connection was created on
        /// </summary>
        public event EventHandler<StatusChangeEventArgs>? StateChanged;

        public bool IsConnected => _channel is { State: CommunicationState.Opened };

        public string LastError { get; private set; } = string.Empty;

        public TrayApplicationSettings Settings => _settings;

        public void UpdateSettings(TrayApplicationSettings settings)
        {
            _settings = settings;
            Disconnect();
        }

        /// <summary>
        /// Makes sure there is an open connection, returns false if the service can't be reached
        /// </summary>
        public bool EnsureConnected()
        {
            if (IsConnected) return true;
            Disconnect();

            //Use reliable connection so we know when we have been disconnected
            var binding = new NetTcpBinding(SecurityMode.Transport) {
                OpenTimeout = TimeSpan.FromSeconds(2),
                CloseTimeout = TimeSpan.FromSeconds(1),
                SendTimeout = TimeSpan.FromSeconds(10),
                MaxReceivedMessageSize = int.MaxValue,
                MaxBufferSize = int.MaxValue,
                ReaderQuotas = { MaxStringContentLength = int.MaxValue, MaxArrayLength = int.MaxValue },
                ReliableSession = {
                    Enabled = true,
                    InactivityTimeout = TimeSpan.FromMinutes(1)
                }
            };
            binding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Windows;

            var callback = new TrayCallback();
            callback.StateChange += (s, e) => StateChanged?.Invoke(s, e);

            try
            {
                var client = new TrayInteractionClient(callback, binding, new EndpointAddress(_settings.GetServiceAddress()));
                var channel = client.ChannelFactory.CreateChannel();
                channel.Subscribe();
                channel.Faulted += (_, _) => _channel = null;
                channel.Closed += (_, _) => _channel = null;
                _channel = channel;
                LastError = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                _channel = null;
                return false;
            }
        }

        /// <summary>
        /// Calls the service, returns the fallback if the call fails. Access denied errors are rethrown so the UI can report them.
        /// </summary>
        public T Call<T>(Func<ITrayInteraction, T> call, T fallback)
        {
            if (!EnsureConnected() || _channel == null) return fallback;
            try
            {
                return call(_channel);
            }
            catch (SecurityAccessDeniedException)
            {
                throw new UnauthorizedAccessException("You need administrator rights on the Plex server to do this.");
            }
            catch (FaultException ex) when (ex.Code?.SubCode?.Name == "FailedAuthentication" || ex.Message.Contains("Access is denied"))
            {
                throw new UnauthorizedAccessException("You need administrator rights on the Plex server to do this.");
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Abort();
                return fallback;
            }
        }

        public void Call(Action<ITrayInteraction> call)
        {
            Call(c => { call(c); return true; }, false);
        }

        public PlexState? GetStatus() => Call<PlexState?>(c => c.GetStatus(), null);

        public bool CanControl()
        {
            // services before this version didn't know about permissions, assume yes
            if (!EnsureConnected() || _channel == null) return false;
            try
            {
                return _channel.CanControl();
            }
            catch (ActionNotSupportedException)
            {
                return true;
            }
            catch (FaultException ex) when (ex.Code?.SubCode?.Name == "ActionNotSupported")
            {
                return true;
            }
            catch
            {
                Abort();
                return false;
            }
        }

        public void Log(string message, LogEventLevel level = LogEventLevel.Debug)
        {
            if (!IsConnected || _channel == null) return;
            try
            {
                _channel.LogMessage(message, level);
            }
            catch
            {
                // logging must never break anything
            }
        }

        private void Abort()
        {
            try
            {
                _channel?.Abort();
            }
            catch
            {
                // ignored
            }
            _channel = null;
        }

        public void Disconnect()
        {
            var channel = _channel;
            _channel = null;
            if (channel == null) return;
            try
            {
                if (channel.State == CommunicationState.Opened)
                {
                    channel.UnSubscribe();
                    channel.Close();
                }
                else
                {
                    channel.Abort();
                }
            }
            catch
            {
                channel.Abort();
            }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
