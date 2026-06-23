namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class WildCoinTransaction
    {
        public int Id { get; set; }
        public string UserHash { get; set; }
        public decimal Amount { get; set; }
        public decimal BalanceAfter { get; set; }
        public string Type { get; set; }          // "sale", "purchase", "reward"
        public string? ReferenceInfo { get; set; } // z.B. "MealPlanListing:42"
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class MealPlanPurchase
    {
        public int Id { get; set; }
        public string BuyerHash { get; set; }
        public string? BuyerWalletAddress { get; set; }
        public string SellerHash { get; set; } = string.Empty;
        public string? SellerWalletAddress { get; set; }
        public int ListingId { get; set; }
        public MealPlanListing Listing { get; set; }
        // Nur bei MealPlan/SingleRecipe gesetzt (kopierter Plan beim Käufer). Bei
        // Digital/Objekt/Dienstleistung null.
        public int? CreatedMealPlanId { get; set; }
        // Snapshot des Angebotstyps zum Kaufzeitpunkt (siehe MarketplaceListingType).
        public string? ListingType { get; set; }
        // Lieferadresse des Käufers (nur bei physischem Objekt).
        public string? BuyerShippingAddress { get; set; }
        public decimal PricePaid { get; set; }
        public decimal CreatorAmount { get; set; }
        public decimal PlatformFee { get; set; }
        public string? ReferenceTxHash { get; set; }
        public string PaymentToken { get; set; } = "WLD";  // "WLD", "USDT", "WildCoin"
        public DateTime PurchasedAt { get; set; } = DateTime.Now;
    }
}
