using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public sealed class RecipeAiVariant
    {
        public int Id { get; set; }
        public int BaseRecipeId { get; set; }
        public RecipeBaseData BaseRecipe { get; set; } = null!;
        public int? ParentVariantId { get; set; }
        public RecipeAiVariant? ParentVariant { get; set; }
        public string VariantType { get; set; } = string.Empty;
        public string Language { get; set; } = "de";
        public string? CreatedByUserHash { get; set; }
        public int AppliedChangeCount { get; set; }
        public string? LatestUserNote { get; set; }
        public bool IsSharedCanonical { get; set; }
        public string StepPlanJson { get; set; } = "[]";
        public string RenderedStepsJson { get; set; } = "[]";
        public string RenderedIngredientsJson { get; set; } = "[]";
        public string RenderedHighlightsJson { get; set; } = "[]";
        public string RenderedTitle { get; set; } = string.Empty;
        public string RenderedSummary { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
