namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class AdPreferenceDashboardViewModel
    {
        public bool AllowPersonalizedAds { get; set; }
        public bool AllowCategoryTargeting { get; set; }
        public bool AllowKeywordTargeting { get; set; }
        public bool AllowCreatorTargeting { get; set; }
        public string PreferredLanguage { get; set; } = "de";
        public int QualifiedWatchCount { get; set; }
        public int LikedRecipeCount { get; set; }
        public int FollowedCreatorCount { get; set; }
        public DateTime? LastProfileRefreshUtc { get; set; }
        public List<string> TopCategories { get; set; } = new();
        public List<string> TopKeywords { get; set; } = new();
        public List<string> TopCreators { get; set; } = new();
        public List<string> SegmentLabels { get; set; } = new();
        public List<AdPreferenceInterestViewModel> TopInterests { get; set; } = new();
    }
}
