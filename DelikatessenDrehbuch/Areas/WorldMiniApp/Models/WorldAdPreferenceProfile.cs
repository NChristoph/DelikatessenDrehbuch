using System.ComponentModel.DataAnnotations.Schema;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class WorldAdPreferenceProfile
    {
        public int Id { get; set; }
        public string UserHash { get; set; } = string.Empty;
        public bool AllowPersonalizedAds { get; set; }
        public bool AllowCategoryTargeting { get; set; }
        public bool AllowKeywordTargeting { get; set; }
        public bool AllowCreatorTargeting { get; set; }
        public string PreferredLanguage { get; set; } = "de";
        public string TopCategoriesJson { get; set; } = "[]";
        public string TopKeywordsJson { get; set; } = "[]";
        public string TopCreatorsJson { get; set; } = "[]";
        public string SegmentLabelsJson { get; set; } = "[]";
        public int QualifiedWatchCount { get; set; }
        public int LikedRecipeCount { get; set; }
        public int FollowedCreatorCount { get; set; }
        public DateTime? LastProfileRefreshUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
