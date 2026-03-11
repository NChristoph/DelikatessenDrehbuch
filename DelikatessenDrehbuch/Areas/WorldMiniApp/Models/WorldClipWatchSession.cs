using System.ComponentModel.DataAnnotations.Schema;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class WorldClipWatchSession
    {
        public int Id { get; set; }
        public int WorldUserPostingId { get; set; }
        public int RecipeId { get; set; }
        public string ViewerUserHash { get; set; } = string.Empty;
        public string CreatorUserHash { get; set; } = string.Empty;
        public decimal WatchedSeconds { get; set; }
        public bool IsQualifiedView { get; set; }
        public DateTime SessionStartedAtUtc { get; set; }
        public DateTime SessionEndedAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(WorldUserPostingId))]
        public WorldUserPosting? Posting { get; set; }
    }
}
