namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class TrackClipWatchRequest
    {
        public int PostingId { get; set; }
        public decimal WatchedSeconds { get; set; }
        public DateTime? SessionStartedAtUtc { get; set; }
        public DateTime? SessionEndedAtUtc { get; set; }
    }
}
