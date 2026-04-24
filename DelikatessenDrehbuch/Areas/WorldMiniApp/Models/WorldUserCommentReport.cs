namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class WorldUserCommentReport
    {
        public int Id { get; set; }
        public int WorldUserCommentId { get; set; }
        public string UserHash { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
    }
}
