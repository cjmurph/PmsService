namespace PlexServiceCommon
{
    /// <summary>
    /// Outcome of testing a network share from the service
    /// </summary>
    public class DriveMapTestResult
    {
        public bool Success { get; set; }

        public string Message { get; set; }

        public DriveMapTestResult()
        {
        }

        public DriveMapTestResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }
    }
}
