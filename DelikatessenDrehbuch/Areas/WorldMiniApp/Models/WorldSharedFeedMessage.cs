namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class WorldSharedFeedMessage
    {
        public int Id { get; set; }
        public int WorldSharedFeedId { get; set; }
        public string UserHash { get; set; } = string.Empty;
        // Empfänger einer privaten 1:1-Nachricht innerhalb der Gruppe.
        // null = (Legacy-)Gruppennachricht; gesetzt = private DM.
        public string? RecipientUserHash { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public WorldSharedFeed? Feed { get; set; }
    }
}
