namespace DelikatessenDrehbuch.Models
{
    // A private (per-user) ingredient-swap variant of a base recipe.
    // Serialized swaps are stored in SwapsJson.
    public sealed class RecipeUserVariant
    {
        public int Id { get; set; }

        public string UserHash { get; set; } = string.Empty;
        public int OriginalRecipeId { get; set; }

        public string Title { get; set; } = string.Empty;
        public string SwapsJson { get; set; } = "[]";

        public DateTime CreatedAtUtc { get; set; }
    }
}

