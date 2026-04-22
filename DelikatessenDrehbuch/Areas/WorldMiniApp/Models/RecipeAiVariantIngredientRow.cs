using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public sealed class RecipeAiVariantIngredientRow
    {
        public int Id { get; set; }
        public int RecipeAiVariantId { get; set; }
        public RecipeAiVariant RecipeAiVariant { get; set; } = null!;

        // Preferred representation: a stable DB reference to ingredient + quantity + measure.
        // We keep the legacy free-text fields for backwards compatibility and display fallbacks.
        public int? IngredientMeasureQuantityId { get; set; }
        public IngredientMeasureQuantity? IngredientMeasureQuantity { get; set; }

        public int? IngredientId { get; set; }
        public IngredientsAndNutrients? Ingredient { get; set; }
        public int? MeasureId { get; set; }
        public Measure? Measure { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string QuantityText { get; set; } = string.Empty;
        public bool IsAiGenerated { get; set; }
        public int SortOrder { get; set; }
    }
}
