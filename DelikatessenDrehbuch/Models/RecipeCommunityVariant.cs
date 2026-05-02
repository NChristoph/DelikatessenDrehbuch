namespace DelikatessenDrehbuch.Models
{
    // A public/shared variant of a base recipe created from ingredient swaps.
    // Other users can start their own edits from this variant.
    public class RecipeCommunityVariant
    {
        public int Id { get; set; }
        public int OriginalRecipeId { get; set; }
        public int? ParentCommunityVariantId { get; set; }

        public string Title { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public string Language { get; set; } = "de";
        public string SwapsJson { get; set; } = "[]";

        public string CreatedByUserHash { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }

        public int LikeCount { get; set; }

        public RecipeBaseData? OriginalRecipe { get; set; }
        public RecipeCommunityVariant? ParentCommunityVariant { get; set; }
    }
}
