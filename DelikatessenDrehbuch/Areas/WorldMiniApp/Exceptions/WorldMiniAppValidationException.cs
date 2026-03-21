namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Exceptions
{
    public class WorldMiniAppValidationException : WorldMiniAppException
    {
        public WorldMiniAppValidationException(string message)
            : base(400, message) { }

        public WorldMiniAppValidationException(string message, Exception innerException)
            : base(400, message, innerException) { }
    }
}
