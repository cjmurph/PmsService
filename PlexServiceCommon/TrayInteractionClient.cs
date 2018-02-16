using PlexServiceCommon.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;

namespace PlexServiceCommon
{
    public class TrayInteractionClient:DuplexClientBase<ITrayInteraction>
    {
        public TrayInteractionClient(object callbackInstance, Binding binding, EndpointAddress remoteAddress)
        : base(callbackInstance, binding, remoteAddress) { }
    }
}
