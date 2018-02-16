using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PlexServiceCommon;

namespace PlexServiceCommon
{
    public class TrayCallback : Interface.ITrayCallback
    {
        public void OnPlexStateChange(PlexState state)
        {
            switch (state)
            {
                case PlexState.Running:
                    OnStateChange(string.Format("Plex {0}", state.ToString()));
                    break;
                case PlexState.Stopped:
                    OnStateChange(string.Format("Plex {0}", state.ToString()));
                    break;
                case PlexState.Pending:
                    OnStateChange(string.Format("Plex Start {0}", state.ToString()));
                    break;
                case PlexState.Stopping:
                    OnStateChange(string.Format("Plex {0}", state.ToString()));
                    break;
                default:
                    break;
            }
        }

        #region StateChange

        public event EventHandler<StatusChangeEventArgs> StateChange;

        protected void OnStateChange(string message)
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

        protected void OnStopped()
        {
            Stopped?.Invoke(this, new EventArgs());
        }

        #endregion

    }
}
