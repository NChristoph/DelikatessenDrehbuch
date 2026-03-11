namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class CreatorWatchAnalyticsViewModel
    {
        public int QualifiedViewCount { get; set; }
        public int UniqueQualifiedViewerCount { get; set; }
        public decimal TotalQualifiedWatchMinutes { get; set; }
        public decimal AverageQualifiedWatchSeconds { get; set; }
        public int ViewerQualifiedClipCount { get; set; }
        public decimal ViewerQualifiedWatchMinutes { get; set; }
        public List<TopCreatorClipViewModel> TopClips { get; set; } = new();
    }
}
