using System.ServiceModel;
using Serilog.Events;

namespace PlexServiceCommon.Interface
{
    /// <summary>
    /// WCF service contract
    /// </summary>
    [ServiceContract(SessionMode = SessionMode.Required, CallbackContract = typeof(ITrayCallback))]
    public interface ITrayInteraction : ICommunicationObject
    {
        [OperationContract]
        void Start();

        [OperationContract]
        void Stop();

        [OperationContract]
        void Restart();

        [OperationContract]
        void SetSettings(Settings settings);

        [OperationContract]
        void LogMessage(string message, LogEventLevel level = LogEventLevel.Debug);

        [OperationContract]
        public string GetLog();

        [OperationContract]
        public string GetLogPath();

        [OperationContract]
        string GetPmsDataPath();

        [OperationContract]
        Settings GetSettings();

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
