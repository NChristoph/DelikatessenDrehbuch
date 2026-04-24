namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class WorldUserCommentReaction
    {
        public int Id { get; set; }
        public int WorldUserCommentId { get; set; }
        public string UserHash { get; set; } = string.Empty;
        public bool IsLike { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
