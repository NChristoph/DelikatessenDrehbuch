namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public sealed class WorldAiEditedRecipeCard
    {
        // The AI variant to open.
        public int AiVariantId { get; set; }

        public int BaseRecipeId { get; set; }
        public string BaseTitle { get; set; } = string.Empty;
        public string VariantTitle { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;

        public string VariantType { get; set; } = string.Empty;
        public string VariantLabel { get; set; } = string.Empty;
        public DateTime UpdatedAtUtc { get; set; }
    }
}
