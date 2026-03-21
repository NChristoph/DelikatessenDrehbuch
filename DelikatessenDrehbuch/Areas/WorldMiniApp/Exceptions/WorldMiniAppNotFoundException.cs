namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Exceptions
{
    public class WorldMiniAppNotFoundException : WorldMiniAppException
    {
        public WorldMiniAppNotFoundException(string message)
            : base(404, message) { }

        public WorldMiniAppNotFoundException(string message, Exception innerException)
            : base(404, message, innerException) { }
    }
}
