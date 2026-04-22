namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public sealed class RecipeAiIngredientSuggestionRequest
    {
        public int RecipeId { get; set; }
        public string VariantType { get; set; } = string.Empty;
        public string? AiProvider { get; set; }
        public string? UserNote { get; set; }
    }
}
