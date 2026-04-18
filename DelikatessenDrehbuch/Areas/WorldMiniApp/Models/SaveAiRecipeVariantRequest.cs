namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public sealed class SaveAiRecipeVariantRequest
    {
        public int BaseRecipeId { get; set; }
        public string? LatestUserNote { get; set; }
        public RecipeAiTransformPreview? Preview { get; set; }
    }
}
