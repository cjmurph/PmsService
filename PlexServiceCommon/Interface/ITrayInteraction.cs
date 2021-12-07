using System.ServiceModel;

namespace PlexServiceCommon.Interface
{
    /// <summary>
    /// WCF service contract
    /// </summary>
    [ServiceContract(SessionMode = SessionMode.Required, CallbackContract = typeof(ITrayCallback))]
    public interface ITrayInteraction
    {
        [OperationContract]
        void Start();

        [OperationContract]
        void Stop();

        [OperationContract]
        void Restart();

        [OperationContract]
        void SetSettings(string settings);

        [OperationContract]
        string GetSettings();

        [OperationContract]
        string GetLog();

        [OperationContract]
        PlexState GetStatus();

        [OperationContract]
        bool IsAuxAppRunning(string name);

        [OperationContract]
        void StartAuxApp(string name);

        [OperationContract]
        void StopAuxApp(string name);

        [OperationContract]
        void Subscribe();

        [OperationContract]
        void UnSubscribe();
    }
}
