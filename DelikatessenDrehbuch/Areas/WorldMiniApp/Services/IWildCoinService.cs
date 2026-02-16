using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    public interface IWildCoinService
    {
        Task<decimal> GetBalance(string userHash);
        Task<bool> Transfer(string fromHash, string toHash, decimal amount, string referenceInfo);
        Task<bool> Reward(string userHash, decimal amount, string referenceInfo);
        Task<List<WildCoinTransaction>> GetTransactions(string userHash, int take = 20);

        // Marketplace
        Task<MealPlanListing> CreateListing(string sellerHash, int mealPlanId, string title, string? description, decimal price);
        Task<List<MealPlanListing>> GetActiveListings(int skip = 0, int take = 20);
        Task<List<MealPlanListing>> GetMyListings(string userHash);
        Task<MealPlanListing?> GetListingById(int id);
        Task<bool> DeactivateListing(string userHash, int listingId);
        Task<MealPlanPurchase?> BuyListing(string buyerHash, int listingId);
    }
}
