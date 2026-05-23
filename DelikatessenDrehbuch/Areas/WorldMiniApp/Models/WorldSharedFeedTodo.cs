namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class WorldSharedFeedTodo
    {
        public int Id { get; set; }
        public int WorldSharedFeedId { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public string CreatedByUserHash { get; set; } = string.Empty;
        public string? CompletedByUserHash { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAtUtc { get; set; }
        public int SortOrder { get; set; }

        public WorldSharedFeed? Feed { get; set; }
    }
}
