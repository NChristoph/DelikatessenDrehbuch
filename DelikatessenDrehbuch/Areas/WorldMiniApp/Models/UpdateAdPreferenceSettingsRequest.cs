namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class UpdateAdPreferenceSettingsRequest
    {
        public bool AllowPersonalizedAds { get; set; }
        public bool AllowCategoryTargeting { get; set; }
        public bool AllowKeywordTargeting { get; set; }
        public bool AllowCreatorTargeting { get; set; }
    }
}
