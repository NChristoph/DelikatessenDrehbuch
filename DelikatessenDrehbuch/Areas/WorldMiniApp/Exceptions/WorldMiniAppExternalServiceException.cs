namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Exceptions
{
    public class WorldMiniAppExternalServiceException : WorldMiniAppException
    {
        public string ServiceName { get; }

        public WorldMiniAppExternalServiceException(string serviceName, string message)
            : base(502, message)
        {
            ServiceName = serviceName;
        }

        public WorldMiniAppExternalServiceException(string serviceName, string message, Exception innerException)
            : base(502, message, innerException)
        {
            ServiceName = serviceName;
        }
    }
}
