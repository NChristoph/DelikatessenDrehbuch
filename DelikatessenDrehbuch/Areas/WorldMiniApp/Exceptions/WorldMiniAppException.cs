namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Exceptions
{
    public abstract class WorldMiniAppException : Exception
    {
        public int StatusCode { get; }

        protected WorldMiniAppException(int statusCode, string message)
            : base(message)
        {
            StatusCode = statusCode;
        }

        protected WorldMiniAppException(int statusCode, string message, Exception innerException)
            : base(message, innerException)
        {
            StatusCode = statusCode;
        }
    }
}
