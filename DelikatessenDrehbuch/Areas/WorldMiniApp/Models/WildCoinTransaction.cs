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

        // Sender == Buyer
        public int? SenderUserId { get; set; }
        public string BuyerHash { get; set; }
        public string? BuyerWalletAddress { get; set; }

        // Receiver == Seller
        public int? ReceiverUserId { get; set; }
        public string SellerHash { get; set; } = string.Empty;
        public string? SellerWalletAddress { get; set; }

        public int ListingId { get; set; }
        public MealPlanListing Listing { get; set; }
        public int CreatedMealPlanId { get; set; }

        public decimal PricePaid { get; set; }
        public decimal CreatorAmount { get; set; }
        public decimal PlatformFee { get; set; }

        // Legacy + neues Feld
        public string? ReferenceTxHash { get; set; }
        public string? PurchaseTransactionId { get; set; }
        public string? CashoutTransactionId { get; set; }

        public string Status { get; set; } = "ok"; // pending | ok | fehlgeschlagen
        public bool SellerCredited { get; set; }
        public DateTime? SellerCreditedAtUtc { get; set; }
        public DateTime OrderTimestampUtc { get; set; } = DateTime.UtcNow;

        public string PaymentToken { get; set; } = "WLD";  // "WLD", "USDT", "WildCoin"
        public DateTime PurchasedAt { get; set; } = DateTime.Now;
    }
}
