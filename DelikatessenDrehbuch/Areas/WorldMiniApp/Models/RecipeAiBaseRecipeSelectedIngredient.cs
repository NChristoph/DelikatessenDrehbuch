using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public sealed class RecipeAiBaseRecipeSelectedIngredient
    {
        public int Id { get; set; }

        public int RecipeAiBaseRecipeId { get; set; }
        public RecipeAiBaseRecipe RecipeAiBaseRecipe { get; set; } = null!;

        public int IngredientId { get; set; }
        public IngredientsAndNutrients Ingredient { get; set; } = null!;

        public int SortOrder { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}

