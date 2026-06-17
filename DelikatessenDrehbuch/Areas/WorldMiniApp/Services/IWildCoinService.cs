using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    public interface IWildCoinService
    {
        // Marketplace (only - WildCoin virtual currency removed)
        Task<MealPlanListing> CreateListingAsync(string sellerHash, int mealPlanId, string title, string? description, decimal price, string? sellerWalletAddress = null, string? sellerUsdtWalletAddress = null);
        Task<List<MealPlanListing>> GetActiveListingsAsync(int skip = 0, int take = 20);
        Task<List<MealPlanListing>> GetMyListingsAsync(string userHash);
        Task<MealPlanListing?> GetListingByIdAsync(int id);
        Task<bool> DeactivateListingAsync(string userHash, int listingId);
        Task<bool> ActivateListingAsync(string userHash, int listingId);
        Task<MealPlanPurchase?> FinalizeWorldChainPurchaseAsync(string buyerHash, int listingId, string txHash, string walletAddress, bool allowSelfPurchase = false, string paymentToken = "WLD");
        Task<List<MealPlanPurchase>> GetPurchasesByBuyerAsync(string buyerHash);

        // --- Verkäufer-Auszahlung (Modell B Cash-out) ---
        Task<decimal> GetAvailableBalanceAsync(string sellerHash);
        Task<(bool success, string? error, MarketplacePayoutRequest? request)> RequestPayoutAsync(string sellerHash, decimal amount, string token, string walletAddress);
        Task<List<MarketplacePayoutRequest>> GetPayoutsForSellerAsync(string sellerHash);
        Task<List<MarketplacePayoutRequest>> GetPendingPayoutsAsync();
        Task<bool> MarkPayoutPaidAsync(int payoutId, string txHash, string adminHash);
        Task<bool> RejectPayoutAsync(int payoutId, string note, string adminHash);
    }
}
