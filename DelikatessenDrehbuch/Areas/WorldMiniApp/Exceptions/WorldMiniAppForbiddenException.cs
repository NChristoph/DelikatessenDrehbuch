namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Exceptions
{
    public class WorldMiniAppForbiddenException : WorldMiniAppException
    {
        public WorldMiniAppForbiddenException(string message)
            : base(403, message) { }

        public WorldMiniAppForbiddenException(string message, Exception innerException)
            : base(403, message, innerException) { }
    }
}
