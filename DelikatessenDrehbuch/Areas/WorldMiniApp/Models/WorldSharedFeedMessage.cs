namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class WorldSharedFeedMessage
    {
        public int Id { get; set; }
        public int WorldSharedFeedId { get; set; }
        public string UserHash { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public WorldSharedFeed? Feed { get; set; }
    }
}
