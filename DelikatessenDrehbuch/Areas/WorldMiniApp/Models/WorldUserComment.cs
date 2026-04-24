namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class WorldUserComment
    {
        public int Id { get; set; }
        public int WorldUserPostingId { get; set; }
        public int? ParentCommentId { get; set; }
        public string UserHash { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string VerificationLevel { get; set; } = string.Empty;
        public string CommentText { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsPinned { get; set; }
    }
}
