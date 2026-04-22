using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    /// <summary>
    /// Canonical storage for an AI-generated recipe variant.
    /// This avoids duplicating ingredient data as free text/JSON and relies on DB references instead.
    /// </summary>
    public sealed class RecipeAiBaseRecipe
    {
        public int Id { get; set; }

        public int BaseRecipeId { get; set; }
        public RecipeBaseData BaseRecipe { get; set; } = null!;

        public int? ParentAiRecipeId { get; set; }
        public RecipeAiBaseRecipe? ParentAiRecipe { get; set; }

        public string VariantType { get; set; } = string.Empty;
        public string Language { get; set; } = "de";
        public string AiProvider { get; set; } = "openai";

        public string SelectedIngredientKey { get; set; } = string.Empty;

        public string? CreatedByUserHash { get; set; }
        public int AppliedChangeCount { get; set; }
        public string? LatestUserNote { get; set; }
        public bool IsSharedCanonical { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public int PersonCount { get; set; }
        public int PreparationTimeMinutes { get; set; }
        public string PreparationText { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public List<RecipeAiBaseRecipeIngredient> Ingredients { get; set; } = new();
        public List<RecipeAiBaseRecipeStep> Steps { get; set; } = new();
        public List<RecipeAiBaseRecipeSelectedIngredient> SelectedIngredients { get; set; } = new();
    }
}

