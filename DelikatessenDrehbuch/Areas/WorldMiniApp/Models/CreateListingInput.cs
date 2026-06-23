namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    /// <summary>
    /// Eingabedaten zum Erstellen eines Marktplatz-Angebots (typ-übergreifend).
    /// Welche Felder relevant sind, hängt von <see cref="ListingType"/> ab.
    /// </summary>
    public class CreateListingInput
    {
        public string ListingType { get; set; } = MarketplaceListingType.MealPlan;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string? SellerWalletAddress { get; set; }
        public string? SellerUsdtWalletAddress { get; set; }

        // ListingType = MealPlan
        public int? MealPlanId { get; set; }

        // ListingType = SingleRecipe
        public int? RecipeId { get; set; }

        // ListingType = DigitalProduct
        public string? DigitalFileUrl { get; set; }

        // ListingType = PhysicalObject
        public int? StockQuantity { get; set; }
        public bool RequiresShipping { get; set; }

        // Gemeinsam (Nicht-Essensplan)
        public string? CoverImageUrl { get; set; }
        public string? ImagesJson { get; set; }
    }
}
