using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    /// <summary>
    /// Service for managing the Meal Plan Marketplace.
    /// Handles listing creation, purchases via World Chain blockchain (WLD/USDT/USDCE),
    /// and marketplace operations. Does NOT handle virtual currency.
    /// </summary>
    public interface IMarketplaceService
    {
        // Marketplace Listings
        Task<MealPlanListing> CreateListingAsync(string sellerHash, int mealPlanId, string title, string? description, decimal price, string? sellerWalletAddress = null, string? sellerUsdtWalletAddress = null);
        Task<List<MealPlanListing>> GetActiveListingsAsync(int skip = 0, int take = 20);
        Task<List<MealPlanListing>> GetMyListingsAsync(string userHash);
        Task<MealPlanListing?> GetListingByIdAsync(int id);
        Task<bool> DeactivateListingAsync(string userHash, int listingId);
        Task<bool> ActivateListingAsync(string userHash, int listingId);

        // Marketplace Purchases (Blockchain)
        Task<MealPlanPurchase?> FinalizeWorldChainPurchaseAsync(string buyerHash, int listingId, string txHash, string walletAddress, bool allowSelfPurchase = false, string paymentToken = "WLD");
        Task<List<MealPlanPurchase>> GetPurchasesByBuyerAsync(string buyerHash);
    }
}
