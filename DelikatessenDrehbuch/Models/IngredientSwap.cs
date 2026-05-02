namespace DelikatessenDrehbuch.Models
{
    // Serialized into RecipeUserVariant.SwapsJson / RecipeCommunityVariant.SwapsJson.
    // Represents a single ingredient substitution within a recipe.
    public sealed class IngredientSwap
    {
        public int FromIngredientId { get; set; }
        public string FromName { get; set; } = string.Empty;

        public int ToIngredientId { get; set; }
        public string ToName { get; set; } = string.Empty;

        // Quantity in the provided Unit after the swap is applied.
        public decimal NewQuantity { get; set; }
        public string Unit { get; set; } = "g";
    }
}

