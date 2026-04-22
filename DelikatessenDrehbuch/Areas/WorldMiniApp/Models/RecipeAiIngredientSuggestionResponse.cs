namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public sealed class RecipeAiIngredientSuggestionResponse
    {
        public string VariantType { get; set; } = string.Empty;
        public string VariantLabel { get; set; } = string.Empty;
        public string Strategy { get; set; } = string.Empty;
        public string Reasoning { get; set; } = string.Empty;
        public string SelectionMode { get; set; } = "concepts";
        public bool UsedFallback { get; set; }
        public bool AutoGenerateWithoutSelection { get; set; }
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
        public string ConceptKey { get; set; } = string.Empty;
        public string ConceptTitle { get; set; } = string.Empty;
        public string ConceptSummary { get; set; } = string.Empty;
        public string ConceptApproach { get; set; } = string.Empty;

        // New prompting flow: concepts already include a concrete ingredient plan (ids + quantities + units).
        public List<RecipeAiConceptIngredientPlanItem> ConceptIngredientPlan { get; set; } = new();
    }

    public sealed class RecipeAiConceptIngredientPlanItem
    {
        public int IngredientId { get; set; }

        // UI-only enrichment (server-resolved names). These are NOT required for prompting.
        public string? IngredientName { get; set; }
        public string? ReplacesIngredientName { get; set; }

        public string Action { get; set; } = "add"; // add | replace | remove
        public int? ReplacesIngredientId { get; set; }
        public string Quantity { get; set; } = string.Empty; // numeric only (e.g. 150, 0.5)
        public string Measure { get; set; } = string.Empty;  // unit only (e.g. g, ml, Stk.)
        public string Reason { get; set; } = string.Empty;

        public bool IsMainProtein { get; set; }
    }
}
