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
        public int ListingId { get; set; }
        public MealPlanListing Listing { get; set; }
        public int CreatedMealPlanId { get; set; }
        public decimal PricePaid { get; set; }
        public string? ReferenceTxHash { get; set; }
        public string? BuyerWalletAddress { get; set; }
        public DateTime PurchasedAt { get; set; } = DateTime.Now;
    }
}
