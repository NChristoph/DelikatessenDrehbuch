namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class WorldSharedFeed
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string OwnerUserHash { get; set; } = string.Empty;
        public string InviteToken { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime LastActivityAtUtc { get; set; } = DateTime.UtcNow;

        public List<WorldSharedFeedMember> Members { get; set; } = new();
        public List<WorldSharedFeedItem> Items { get; set; } = new();
    }

    public class WorldSharedFeedMember
    {
        public int Id { get; set; }
        public int WorldSharedFeedId { get; set; }
        public string UserHash { get; set; } = string.Empty;
        public string Role { get; set; } = "member";
        public string AddedByUserHash { get; set; } = string.Empty;
        public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;

        public WorldSharedFeed? Feed { get; set; }
    }

    public class WorldSharedFeedItem
    {
        public int Id { get; set; }
        public int WorldSharedFeedId { get; set; }
        // "mealplan" | "shoppinglist" | "recipe" | "text" (Text-Posting im Gruppen-Feed)
        public string ContentType { get; set; } = string.Empty;
        public string SourceToken { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        // Body eines Text-Postings (ContentType = "text"); bei anderen Typen null.
        public string? Text { get; set; }
        public string AddedByUserHash { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public WorldSharedFeed? Feed { get; set; }
    }

    // Like eines Gruppenmitglieds auf ein Feed-Item (Text-Posting oder geteiltes Item).
    public class WorldSharedFeedItemLike
    {
        public int Id { get; set; }
        public int WorldSharedFeedItemId { get; set; }
        public string UserHash { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public WorldSharedFeedItem? Item { get; set; }
    }
}
