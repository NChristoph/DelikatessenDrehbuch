using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    public interface IWildCoinService
    {
        Task<decimal> GetBalanceAsync(string userHash);
        Task<bool> TransferAsync(string fromHash, string toHash, decimal amount, string referenceInfo);
        Task<bool> RewardAsync(string userHash, decimal amount, string referenceInfo);
        Task<List<WildCoinTransaction>> GetTransactionsAsync(string userHash, int take = 20);

        // Marketplace
        Task<MealPlanListing> CreateListingAsync(string sellerHash, int mealPlanId, string title, string? description, decimal price, string? sellerWalletAddress = null, string? sellerUsdtWalletAddress = null);
        Task<List<MealPlanListing>> GetActiveListingsAsync(int skip = 0, int take = 20);
        Task<List<MealPlanListing>> GetMyListingsAsync(string userHash);
        Task<MealPlanListing?> GetListingByIdAsync(int id);
        Task<bool> DeactivateListingAsync(string userHash, int listingId);
        Task<bool> ActivateListingAsync(string userHash, int listingId);
        Task<MealPlanPurchase?> FinalizeWorldChainPurchaseAsync(string buyerHash, int listingId, string txHash, string walletAddress, bool allowSelfPurchase = false, string paymentToken = "WLD");
        Task<List<MealPlanPurchase>> GetPurchasesByBuyerAsync(string buyerHash);
    }
}
