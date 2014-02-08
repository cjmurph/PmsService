using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace PlexMediaServer_Service
{
    internal class StatusChangeEventArgs: EventArgs
    {
        public EventLogEntryType EventType { get; private set; }
        public string Description { get; private set; }

        internal StatusChangeEventArgs(string description, EventLogEntryType eventType = EventLogEntryType.Information)
        {
            this.EventType = eventType;
            this.Description = description;
        }
    }
}
