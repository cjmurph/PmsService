using System.ServiceModel;

namespace PlexServiceCommon.Interface
{
    [ServiceContract]
    public interface ITrayCallback
    {
        [OperationContract]
        void OnPlexStateChange(PlexState state);

        [OperationContract]
        void OnPlexStopped();
    }
}
