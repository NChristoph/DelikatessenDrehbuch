namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public sealed class SaveAiRecipeVariantRequest
    {
        public int BaseRecipeId { get; set; }
        public string? AiProvider { get; set; }
        public List<int>? SelectedIngredientIds { get; set; }
        public string? SelectedConceptKey { get; set; }
        public string? LatestUserNote { get; set; }
        public RecipeAiTransformPreview? Preview { get; set; }
    }
}
