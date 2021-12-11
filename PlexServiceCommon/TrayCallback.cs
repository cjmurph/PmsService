using System;
using Serilog;

namespace PlexServiceCommon
{
    public class TrayCallback : Interface.ITrayCallback
    {
        public void OnPlexStateChange(PlexState state)
        {
            switch (state)
            {
                case PlexState.Running:
                    OnStateChange($"Plex {state.ToString()}");
                    break;
                case PlexState.Stopped:
                    OnStateChange($"Plex {state.ToString()}");
                    break;
                case PlexState.Updating:
                    OnStateChange($"Plex is {state.ToString()}");
                    break;
                case PlexState.Pending:
                    OnStateChange($"Plex Start {state.ToString()}");
                    break;
                case PlexState.Stopping:
                    OnStateChange($"Plex {state.ToString()}");
                    break;
            }
        }

        #region StateChange

        public event EventHandler<StatusChangeEventArgs> StateChange;

        private void OnStateChange(string message)
        {
            StateChange?.Invoke(this, new StatusChangeEventArgs(message));
        }

        #endregion
        
        #region SettingChange

        public event EventHandler<SettingChangeEventArgs> SettingChange;

        public void OnSettingChange(Settings settings) {
            Log.Debug("Setting change...");
            SettingChange?.Invoke(this, new SettingChangeEventArgs(settings));
        }

        #endregion

        public void OnPlexStopped()
        {
            OnStopped();
        }

        #region StateChange

        public event EventHandler<EventArgs> Stopped;

        private void OnStopped()
        {
            Stopped?.Invoke(this, EventArgs.Empty);
        }

        #endregion

    }

    public class SettingChangeEventArgs {
        public Settings Settings;
        public SettingChangeEventArgs(Settings settings) {
            Settings = settings;
        }
    }
}
