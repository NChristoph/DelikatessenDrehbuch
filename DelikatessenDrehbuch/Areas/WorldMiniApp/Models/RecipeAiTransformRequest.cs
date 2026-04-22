namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public sealed class RecipeAiTransformRequest
    {
        public int RecipeId { get; set; }
        public string VariantType { get; set; } = string.Empty;
        public string? AiProvider { get; set; }
        public string? UserNote { get; set; }
        public int? SelectedIngredientId { get; set; }
        public List<int>? SelectedIngredientIds { get; set; }
        public string? SelectedConceptKey { get; set; }
        public string? SelectedConceptTitle { get; set; }
        public string? SelectedConceptSummary { get; set; }
        public string? SelectedConceptApproach { get; set; }
        public List<RecipeAiConceptIngredientPlanItem>? SelectedConceptIngredientPlan { get; set; }
        public int AppliedChangeCount { get; set; }
    }
}
