namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    public static class DebugLogger
    {
        private static readonly List<string> Logs = new();
        private static readonly object LogLock = new();

        public static void Log(string message)
        {
            lock (LogLock)
            {
                Logs.Add($"{DateTime.UtcNow:HH:mm:ss.fff} - {message}");
                if (Logs.Count > 200)
                {
                    Logs.RemoveAt(0);
                }
            }
        }

        public static List<string> GetLogs()
        {
            lock (LogLock)
            {
                return Logs.ToList();
            }
        }

        public static void Clear()
        {
            lock (LogLock)
            {
                Logs.Clear();
            }
        }
    }
}
