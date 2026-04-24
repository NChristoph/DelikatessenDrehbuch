namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public sealed class RecipeAiTransformPreview
    {
        public string VariantType { get; set; } = string.Empty;
        public string VariantLabel { get; set; } = string.Empty;
        public string AiProvider { get; set; } = "openai";
        public string LanguageCode { get; set; } = "de";
        public string SelectedIngredientKey { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string PreparationText { get; set; } = string.Empty;
        public string IngredientsText { get; set; } = string.Empty;
        public bool UsedFallback { get; set; }
        public bool IsCached { get; set; }
        public bool UserNoteApplied { get; set; }
        public int RemainingChanges { get; set; }
        public List<RecipeAiTransformIngredientPreview> Ingredients { get; set; } = new();
        public List<RecipeAiTransformStepPlanItem> StepPlan { get; set; } = new();
        public List<RecipeAiTransformStepPreview> Steps { get; set; } = new();
        public List<string> Highlights { get; set; } = new();
        public RecipeAiTransformNutritionPreview Nutrition { get; set; } = new();
    }

    public sealed class RecipeAiTransformNutritionPreview
    {
        public decimal Calories { get; set; }
        public decimal Protein { get; set; }
        public decimal Carbs { get; set; }
        public decimal Fat { get; set; }
        public decimal Sugar { get; set; }
    }

    public sealed class RecipeAiTransformIngredientPreview
    {
        public int IngredientId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Quantity { get; set; } = string.Empty;
        public string Measure { get; set; } = string.Empty;
        public string? ChangeHint { get; set; }
        public bool IsModified { get; set; }
    }

    public sealed class RecipeAiTransformStepPreview
    {
        public int Index { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    public sealed class RecipeAiTransformStepPlanItem
    {
        public string MasterStepKey { get; set; } = string.Empty;
        public int Phase { get; set; }
        public List<RecipeAiTransformStepVariableItem> Variables { get; set; } = new();
        public string RenderedText { get; set; } = string.Empty;
    }

    public sealed class RecipeAiTransformStepVariableItem
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
