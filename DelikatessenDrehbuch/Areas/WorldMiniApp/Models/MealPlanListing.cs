namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    /// <summary>
    /// Mögliche Angebotstypen im Marktplatz. Wird als einfacher String-Discriminator
    /// in <see cref="MealPlanListing.ListingType"/> gespeichert (KEIN EF-TPH-Discriminator).
    /// </summary>
    public static class MarketplaceListingType
    {
        public const string MealPlan = "MealPlan";          // Essensplan (bestehend, Default)
        public const string PhysicalObject = "PhysicalObject"; // Physisches, versendbares Objekt
        public const string DigitalProduct = "DigitalProduct"; // Digitaler Download/Link
        public const string SingleRecipe = "SingleRecipe";   // Einzelnes eigenes Rezept
        public const string Service = "Service";             // Dienstleistung/Beratung

        public static readonly string[] All =
        {
            MealPlan, PhysicalObject, DigitalProduct, SingleRecipe, Service
        };

        public static bool IsValid(string? type) =>
            !string.IsNullOrWhiteSpace(type) && Array.Exists(All, t => t == type);
    }

    public class MealPlanListing
    {
        public int Id { get; set; }
        public string SellerHash { get; set; }
        public string SellerName { get; set; }
        public string? SellerWalletAddress { get; set; }
        public string? SellerUsdtWalletAddress { get; set; }

        /// <summary>
        /// Angebotstyp (Discriminator). Siehe <see cref="MarketplaceListingType"/>.
        /// Default "MealPlan" deckt alle Bestandsdaten ab.
        /// </summary>
        public string ListingType { get; set; } = MarketplaceListingType.MealPlan;

        // --- Essensplan (ListingType = MealPlan) ---
        public int? MealPlanId { get; set; }
        public WorldUserMealPlan? MealPlan { get; set; }

        // --- Einzelrezept (ListingType = SingleRecipe) ---
        public int? RecipeId { get; set; }

        // --- Digitales Produkt (ListingType = DigitalProduct) ---
        public string? DigitalFileUrl { get; set; }

        // --- Physisches Objekt (ListingType = PhysicalObject) ---
        public int? StockQuantity { get; set; }   // null = unbegrenzt
        public bool RequiresShipping { get; set; }

        // --- Gemeinsam für Nicht-Essensplan-Typen ---
        public string? CoverImageUrl { get; set; } // Titelbild
        public string? ImagesJson { get; set; }    // optionale Bildergalerie (JSON-Array von URLs)

        public string Title { get; set; }
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int DayCount { get; set; }
        public int RecipeCount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;
        public int SoldCount { get; set; }
    }
}
