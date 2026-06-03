using System.Text.Json.Serialization;

namespace DelikatessenDrehbuch.Models
{
    public class SwapSuggestionRequest
    {
        public int OriginalIngredientId { get; set; }
        public int RecipeId { get; set; }
        public int? SwapVariantId { get; set; }
        public string? Goal { get; set; } // "low_carb", "high_protein", "vegan", null
        public string Language { get; set; } = "de";
        public string? AiProvider { get; set; } // "gemini", "openai", null (auto)

        // Optional: include current batch/variant swaps so the next suggestions respect already-selected changes.
        public List<IngredientSwap>? ContextSwaps { get; set; }

        // Optional: list of ingredient names already in the recipe to avoid suggesting them again
        public List<string>? ExistingIngredients { get; set; }
    }

    public class SwapSuggestionResponse
    {
        public List<IngredientSwapOption> Suggestions { get; set; }
        public OriginalIngredientInfo Original { get; set; }
        public string AiProvider { get; set; } // Which AI was used
        public int ProcessingTimeMs { get; set; }
    }

    public class IngredientSwapOption
    {
        public int IngredientId { get; set; }
        public string Name { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public string Reason { get; set; }
        public double CompatibilityScore { get; set; }
        public NutritionDelta Delta { get; set; }
        public string PreparationChange { get; set; }
        public string TasteImpact { get; set; }
        public List<string> Pros { get; set; }
        public List<string> Cons { get; set; }
        public decimal? CostPerKg { get; set; }
    }

    public class NutritionDelta
    {
        public decimal CaloriesDelta { get; set; }
        public decimal ProteinDelta { get; set; }
        public decimal CarbsDelta { get; set; }
        public decimal FatDelta { get; set; }
    }

    public class OriginalIngredientInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public NutritionInfo Nutrition { get; set; }
    }

    public class NutritionInfo
    {
        public decimal Calories { get; set; }
        public decimal Protein { get; set; }
        public decimal Carbs { get; set; }
        public decimal Fat { get; set; }
    }

    // AI Response Models
    public class GeminiSwapResponse
    {
        [JsonPropertyName("suggestions")]
        public List<GeminiSuggestion> Suggestions { get; set; }
    }

    public class GeminiSuggestion
    {
        [JsonPropertyName("ingredient_id")]
        public int IngredientId { get; set; }

        [JsonPropertyName("ingredient_name")]
        public string IngredientName { get; set; }

        [JsonPropertyName("quantity")]
        public decimal Quantity { get; set; }

        [JsonPropertyName("unit")]
        public string Unit { get; set; }

        [JsonPropertyName("reason")]
        public string Reason { get; set; }

        [JsonPropertyName("compatibility_score")]
        public double CompatibilityScore { get; set; }

        [JsonPropertyName("nutrition_delta")]
        public NutritionDelta NutritionDelta { get; set; }

        [JsonPropertyName("preparation_change")]
        public string PreparationChange { get; set; }

        [JsonPropertyName("taste_impact")]
        public string TasteImpact { get; set; }

        [JsonPropertyName("pros")]
        public List<string> Pros { get; set; }

        [JsonPropertyName("cons")]
        public List<string> Cons { get; set; }
    }
}
