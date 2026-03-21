namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Exceptions
{
    public class WorldMiniAppAuthException : WorldMiniAppException
    {
        public WorldMiniAppAuthException(string message)
            : base(401, message) { }

        public WorldMiniAppAuthException(string message, Exception innerException)
            : base(401, message, innerException) { }
    }
}
