using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    /// <summary>
    /// Bridge table between an AI recipe and a stable IngredientMeasureQuantity entry.
    /// </summary>
    public sealed class RecipeAiBaseRecipeIngredient
    {
        public int Id { get; set; }

        public int RecipeAiBaseRecipeId { get; set; }
        public RecipeAiBaseRecipe RecipeAiBaseRecipe { get; set; } = null!;

        public int IngredientMeasureQuantityId { get; set; }
        public IngredientMeasureQuantity IngredientMeasureQuantity { get; set; } = null!;

        public int SortOrder { get; set; }

        public bool IsModified { get; set; }
        public string? ChangeHint { get; set; }
    }
}

