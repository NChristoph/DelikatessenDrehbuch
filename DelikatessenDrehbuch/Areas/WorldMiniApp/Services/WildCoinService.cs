using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Data;
using Microsoft.EntityFrameworkCore;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    public class WildCoinService : IWildCoinService
    {
        private readonly ApplicationDbContext _context;

        public WildCoinService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<decimal> GetBalance(string userHash)
        {
            var user = await _context.WorldAppUser.FirstOrDefaultAsync(u => u.UserHash == userHash);
            return user?.WildCoinBalance ?? 0;
        }

        public async Task<bool> Transfer(string fromHash, string toHash, decimal amount, string referenceInfo)
        {
            if (amount <= 0) return false;

            var sender = await _context.WorldAppUser.FirstOrDefaultAsync(u => u.UserHash == fromHash);
            var receiver = await _context.WorldAppUser.FirstOrDefaultAsync(u => u.UserHash == toHash);
            if (sender == null || receiver == null) return false;
            if (sender.WildCoinBalance < amount) return false;

            sender.WildCoinBalance -= amount;
            receiver.WildCoinBalance += amount;

            _context.Add(new WildCoinTransaction
            {
                UserHash = fromHash,
                Amount = -amount,
                BalanceAfter = sender.WildCoinBalance,
                Type = "purchase",
                ReferenceInfo = referenceInfo
            });
            _context.Add(new WildCoinTransaction
            {
                UserHash = toHash,
                Amount = amount,
                BalanceAfter = receiver.WildCoinBalance,
                Type = "sale",
                ReferenceInfo = referenceInfo
            });

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> Reward(string userHash, decimal amount, string referenceInfo)
        {
            if (amount <= 0) return false;

            var user = await _context.WorldAppUser.FirstOrDefaultAsync(u => u.UserHash == userHash);
            if (user == null) return false;

            user.WildCoinBalance += amount;

            _context.Add(new WildCoinTransaction
            {
                UserHash = userHash,
                Amount = amount,
                BalanceAfter = user.WildCoinBalance,
                Type = "reward",
                ReferenceInfo = referenceInfo
            });

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<WildCoinTransaction>> GetTransactions(string userHash, int take = 20)
        {
            return await _context.WildCoinTransactions
                .Where(t => t.UserHash == userHash)
                .OrderByDescending(t => t.CreatedAt)
                .Take(take)
                .ToListAsync();
        }

        // ---- Marketplace ----

        public async Task<MealPlanListing> CreateListing(string sellerHash, int mealPlanId, string title, string? description, decimal price)
        {
            var user = await _context.WorldAppUser.FirstOrDefaultAsync(u => u.UserHash == sellerHash);
            if (user == null) throw new InvalidOperationException("User nicht gefunden.");

            var mealPlan = await _context.WorldUserMealPlan.FirstOrDefaultAsync(m => m.Id == mealPlanId && m.UserHash == sellerHash);
            if (mealPlan == null) throw new InvalidOperationException("Essensplan nicht gefunden oder gehört nicht dir.");

            // Rezeptanzahl und Tage aus dem MealPlan JSON berechnen
            int dayCount = 0;
            int recipeCount = 0;
            if (!string.IsNullOrEmpty(mealPlan.MealPlan))
            {
                try
                {
                    var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, List<int>>>(mealPlan.MealPlan);
                    if (dict != null)
                    {
                        dayCount = dict.Count;
                        recipeCount = dict.Values.Sum(v => v.Count);
                    }
                }
                catch { }
            }

            var listing = new MealPlanListing
            {
                SellerHash = sellerHash,
                SellerName = user.UserName ?? "Anonym",
                MealPlanId = mealPlanId,
                MealPlan = mealPlan,
                Title = title,
                Description = description,
                Price = price,
                DayCount = dayCount,
                RecipeCount = recipeCount
            };

            await _context.MealPlanListings.AddAsync(listing);
            await _context.SaveChangesAsync();
            return listing;
        }

        public async Task<List<MealPlanListing>> GetActiveListings(int skip = 0, int take = 20)
        {
            return await _context.MealPlanListings
                .Where(l => l.IsActive)
                .OrderByDescending(l => l.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public async Task<List<MealPlanListing>> GetMyListings(string userHash)
        {
            return await _context.MealPlanListings
                .Where(l => l.SellerHash == userHash)
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();
        }

        public async Task<MealPlanListing?> GetListingById(int id)
        {
            return await _context.MealPlanListings
                .Include(l => l.MealPlan)
                .FirstOrDefaultAsync(l => l.Id == id);
        }

        public async Task<bool> DeactivateListing(string userHash, int listingId)
        {
            var listing = await _context.MealPlanListings.FirstOrDefaultAsync(l => l.Id == listingId && l.SellerHash == userHash);
            if (listing == null) return false;

            listing.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<MealPlanPurchase?> BuyListing(string buyerHash, int listingId)
        {
            var listing = await _context.MealPlanListings
                .Include(l => l.MealPlan)
                .FirstOrDefaultAsync(l => l.Id == listingId && l.IsActive);

            if (listing == null) return null;
            if (listing.SellerHash == buyerHash) return null; // Kann sich nicht selbst kaufen

            // Transfer
            var success = await Transfer(buyerHash, listing.SellerHash, listing.Price, $"MealPlanListing:{listing.Id}");
            if (!success) return null;

            listing.SoldCount++;

            // Kopie des MealPlans für den Käufer erstellen
            var copiedPlan = new WorldUserMealPlan
            {
                UserHash = buyerHash,
                Settings = listing.MealPlan?.Settings,
                MealPlan = listing.MealPlan?.MealPlan,
                Title = $"{listing.Title}",
                CreationTime = DateTime.Now
            };
            await _context.WorldUserMealPlan.AddAsync(copiedPlan);
            await _context.SaveChangesAsync();

            var purchase = new MealPlanPurchase
            {
                BuyerHash = buyerHash,
                ListingId = listing.Id,
                Listing = listing,
                CreatedMealPlanId = copiedPlan.Id,
                PricePaid = listing.Price
            };
            await _context.MealPlanPurchases.AddAsync(purchase);
            await _context.SaveChangesAsync();

            return purchase;
        }
    }
}
