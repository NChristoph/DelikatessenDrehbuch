using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public sealed class RecipeAiVariantSelectedIngredient
    {
        public int Id { get; set; }
        public int RecipeAiVariantId { get; set; }
        public RecipeAiVariant RecipeAiVariant { get; set; } = null!;
        public int IngredientId { get; set; }
        public IngredientsAndNutrients? Ingredient { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
