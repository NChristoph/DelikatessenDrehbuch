using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Data;
using Microsoft.EntityFrameworkCore;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    public interface IMarketplaceRankingService
    {
        Task<List<MealPlanListing>> RankListingsAsync(List<MealPlanListing> listings, string? userHash);
    }

    public class MarketplaceRankingService : IMarketplaceRankingService
    {
        private readonly ApplicationDbContext _context;

        // Weights for scoring (total = 1.0)
        private const double PopularityWeight = 0.30;
        private const double QualityWeight = 0.25;
        private const double FreshnessWeight = 0.20;
        private const double CreatorWeight = 0.15;
        private const double UserAffinityWeight = 0.10;

        public MarketplaceRankingService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<MealPlanListing>> RankListingsAsync(List<MealPlanListing> listings, string? userHash)
        {
            if (!listings.Any())
                return listings;

            // Load necessary data for scoring
            var creatorHashes = listings.Select(l => l.SellerHash).Distinct().ToList();

            // Get creator verification status
            var creatorVerifications = await _context.WorldAppUser
                .AsNoTracking()
                .Where(u => creatorHashes.Contains(u.UserHash))
                .Select(u => new { u.UserHash, u.IsVerified })
                .ToListAsync();
            var verificationMap = creatorVerifications.ToDictionary(v => v.UserHash, v => v.IsVerified);

            // Get follow status for user
            HashSet<string> followedCreators = new();
            if (!string.IsNullOrWhiteSpace(userHash))
            {
                followedCreators = (await _context.WorldUserAbo
                    .AsNoTracking()
                    .Where(a => a.WorldUser.UserHash == userHash && creatorHashes.Contains(a.Creator.UserHash))
                    .Select(a => a.Creator.UserHash)
                    .ToListAsync())
                    .ToHashSet();
            }

            // Get creator follower counts
            var followerCounts = await _context.WorldUserAbo
                .AsNoTracking()
                .Where(a => creatorHashes.Contains(a.Creator.UserHash))
                .GroupBy(a => a.Creator.UserHash)
                .Select(g => new { CreatorHash = g.Key, Count = g.Count() })
                .ToListAsync();
            var followerCountMap = followerCounts.ToDictionary(f => f.CreatorHash, f => f.Count);

            // Score each listing
            var scoredListings = listings.Select(listing =>
            {
                var popularityScore = CalculatePopularityScore(listing);
                var qualityScore = CalculateQualityScore(listing);
                var freshnessScore = CalculateFreshnessScore(listing);
                var creatorScore = CalculateCreatorScore(listing, verificationMap, followerCountMap);
                var userAffinityScore = CalculateUserAffinityScore(listing, userHash, followedCreators);

                var totalScore =
                    (popularityScore * PopularityWeight) +
                    (qualityScore * QualityWeight) +
                    (freshnessScore * FreshnessWeight) +
                    (creatorScore * CreatorWeight) +
                    (userAffinityScore * UserAffinityWeight);

                return new
                {
                    Listing = listing,
                    Score = totalScore,
                    // Debug info (can be removed in production)
                    PopularityScore = popularityScore,
                    QualityScore = qualityScore,
                    FreshnessScore = freshnessScore,
                    CreatorScore = creatorScore,
                    UserAffinityScore = userAffinityScore
                };
            }).OrderByDescending(x => x.Score).ToList();

            return scoredListings.Select(x => x.Listing).ToList();
        }

        /// <summary>
        /// Popularity Score (0-100) based on sales and engagement
        /// </summary>
        private double CalculatePopularityScore(MealPlanListing listing)
        {
            // More sales = higher score (logarithmic scale to avoid huge outliers)
            var salesScore = Math.Min(100, Math.Log10(listing.SoldCount + 1) * 40);

            // New listings with 0 sales get a small boost (cold start problem)
            if (listing.SoldCount == 0 && (DateTime.Now - listing.CreatedAt).TotalDays < 7)
            {
                salesScore = 30; // New listing boost
            }

            return salesScore;
        }

        /// <summary>
        /// Quality Score (0-100) based on listing completeness
        /// </summary>
        private double CalculateQualityScore(MealPlanListing listing)
        {
            double score = 0;

            // Recipe count (more recipes = better, max at 21 recipes for 7 days * 3 meals)
            var recipeRatio = Math.Min(1.0, listing.RecipeCount / 21.0);
            score += recipeRatio * 40; // Max 40 points

            // Day count (7 days is optimal)
            var dayRatio = Math.Min(1.0, listing.DayCount / 7.0);
            score += dayRatio * 20; // Max 20 points

            // Description present and meaningful (>50 chars)
            if (!string.IsNullOrWhiteSpace(listing.Description) && listing.Description.Length > 50)
            {
                score += 20;
            }
            else if (!string.IsNullOrWhiteSpace(listing.Description))
            {
                score += 10; // Partial credit for short description
            }

            // Price is reasonable (between 5-50 WLD)
            if (listing.Price >= 5 && listing.Price <= 50)
            {
                score += 20;
            }
            else if (listing.Price > 0)
            {
                score += 10; // Partial credit
            }

            return Math.Min(100, score);
        }

        /// <summary>
        /// Freshness Score (0-100) - newer listings get higher scores
        /// </summary>
        private double CalculateFreshnessScore(MealPlanListing listing)
        {
            var ageInDays = (DateTime.Now - listing.CreatedAt).TotalDays;

            // Decay function: Fresh listings (0-7 days) = 100, then exponential decay
            if (ageInDays <= 7)
                return 100;

            if (ageInDays <= 14)
                return 80;

            if (ageInDays <= 30)
                return 60;

            if (ageInDays <= 60)
                return 40;

            if (ageInDays <= 90)
                return 20;

            return 10; // Old listings still get a minimum score
        }

        /// <summary>
        /// Creator Score (0-100) based on creator reputation
        /// </summary>
        private double CalculateCreatorScore(
            MealPlanListing listing,
            Dictionary<string, string?> verificationMap,
            Dictionary<string, int> followerCountMap)
        {
            double score = 0;

            // Verification status (orb = 50 points, device = 25 points)
            if (verificationMap.TryGetValue(listing.SellerHash, out var verification))
            {
                if (string.Equals(verification, "orb", StringComparison.OrdinalIgnoreCase))
                {
                    score += 50;
                }
                else if (string.Equals(verification, "device", StringComparison.OrdinalIgnoreCase))
                {
                    score += 25;
                }
            }

            // Follower count (logarithmic scale)
            if (followerCountMap.TryGetValue(listing.SellerHash, out var followerCount))
            {
                score += Math.Min(30, Math.Log10(followerCount + 1) * 10);
            }

            // Creator has multiple successful listings (>5 sales total)
            // Note: This would need aggregation across all creator's listings
            // For now, use this listing's sales as proxy
            if (listing.SoldCount >= 5)
            {
                score += 20;
            }

            return Math.Min(100, score);
        }

        /// <summary>
        /// User Affinity Score (0-100) - personalized for the user
        /// </summary>
        private double CalculateUserAffinityScore(
            MealPlanListing listing,
            string? userHash,
            HashSet<string> followedCreators)
        {
            if (string.IsNullOrWhiteSpace(userHash))
                return 50; // Neutral score for anonymous users

            double score = 50; // Start neutral

            // User follows this creator (+50 points = max score)
            if (followedCreators.Contains(listing.SellerHash))
            {
                score += 50;
            }

            // TODO: Add more personalization:
            // - User likes similar recipes (diet type, category)
            // - User has purchased similar plans
            // - User's typical price range

            return Math.Min(100, score);
        }
    }
}
