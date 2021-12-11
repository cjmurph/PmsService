using PlexServiceCommon.Interface;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace PlexServiceCommon
{
    public class TrayInteractionClient:DuplexClientBase<ITrayInteraction>
    {
        public TrayInteractionClient(object callbackInstance, Binding binding, EndpointAddress remoteAddress)
            : base(callbackInstance, binding, remoteAddress) {
        }
    }
}
