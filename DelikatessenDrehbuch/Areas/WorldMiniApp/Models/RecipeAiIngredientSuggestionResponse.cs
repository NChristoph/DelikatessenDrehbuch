namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public sealed class RecipeAiIngredientSuggestionResponse
    {
        public string VariantType { get; set; } = string.Empty;
        public string VariantLabel { get; set; } = string.Empty;
        public string Strategy { get; set; } = string.Empty;
        public string Reasoning { get; set; } = string.Empty;
        public bool UsedFallback { get; set; }
        public List<string> PreferredCategoryKeys { get; set; } = new();
        public List<string> PreferredCategoryLabels { get; set; } = new();
        public List<RecipeAiIngredientSuggestionItem> Suggestions { get; set; } = new();
    }

    public sealed class RecipeAiIngredientSuggestionItem
    {
        public int IngredientId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CategoryKey { get; set; } = string.Empty;
        public string CategoryLabel { get; set; } = string.Empty;
        public decimal ProteinPer100g { get; set; }
        public decimal CarbsPer100g { get; set; }
        public decimal FatPer100g { get; set; }
        public decimal FiberPer100g { get; set; }
        public decimal CaloriesPer100g { get; set; }
        public decimal Score { get; set; }
        public string AiReason { get; set; } = string.Empty;
    }
}
