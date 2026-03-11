namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class TopCreatorClipViewModel
    {
        public int PostingId { get; set; }
        public int RecipeId { get; set; }
        public string RecipeTitle { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public int QualifiedViews { get; set; }
        public decimal TotalWatchMinutes { get; set; }
        public decimal AverageWatchSeconds { get; set; }
    }
}
