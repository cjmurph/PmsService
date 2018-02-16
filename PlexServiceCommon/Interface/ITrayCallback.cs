using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;

namespace PlexServiceCommon.Interface
{
    public interface ITrayCallback
    {
        [OperationContract]
        void OnPlexStateChange(PlexState state);

        [OperationContract]
        void OnPlexStopped();
    }
}
