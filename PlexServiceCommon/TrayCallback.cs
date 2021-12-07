using System;

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
}
