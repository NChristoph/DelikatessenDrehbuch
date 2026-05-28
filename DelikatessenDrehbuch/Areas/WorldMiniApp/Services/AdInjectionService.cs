using Microsoft.EntityFrameworkCore;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    public interface IAdInjectionService
    {
        Task<List<object>> InjectAdsIntoFeed(List<WorldUserPosting> postings, string? userVerificationLevel = null);
        Task<WorldAppAd?> GetNextActiveAd(string? userVerificationLevel = null);
    }

    public class AdInjectionService : IAdInjectionService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdInjectionService> _logger;

        public AdInjectionService(ApplicationDbContext context, ILogger<AdInjectionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Injects ads into a list of feed postings
        /// Returns a mixed list of postings and ads
        /// </summary>
        public async Task<List<object>> InjectAdsIntoFeed(List<WorldUserPosting> postings, string? userVerificationLevel = null)
        {
            var now = DateTime.UtcNow;

            // Get all active ads that are within their date range
            var activeAds = await _context.WorldAppAds
                .Where(a => a.IsActive)
                .Where(a => (a.StartDate == null || a.StartDate <= now) &&
                           (a.EndDate == null || a.EndDate >= now))
                .OrderByDescending(a => a.Priority)
                .ThenBy(a => a.ViewCount) // Rotate ads - show ones with fewer views first
                .ToListAsync();

            // Filter by target audience if specified
            if (!string.IsNullOrWhiteSpace(userVerificationLevel))
            {
                activeAds = activeAds
                    .Where(a => string.IsNullOrWhiteSpace(a.TargetAudience) ||
                               a.TargetAudience == "all" ||
                               a.TargetAudience == userVerificationLevel)
                    .ToList();
            }

            if (!activeAds.Any())
            {
                // No active ads, return postings as is (wrapped in objects for consistency)
                return postings.Cast<object>().ToList();
            }

            var result = new List<object>();
            var adIndex = 0;

            for (int i = 0; i < postings.Count; i++)
            {
                result.Add(postings[i]);

                // Check if we should inject an ad after this posting
                // Get the ad's ShowEveryXVideos setting
                var currentAd = activeAds[adIndex % activeAds.Count];
                var showEvery = currentAd.ShowEveryXVideos;

                if ((i + 1) % showEvery == 0 && (i + 1) < postings.Count)
                {
                    // Inject ad
                    result.Add(new AdSlot
                    {
                        Ad = currentAd,
                        Position = i + 1
                    });

                    // Move to next ad (rotate through available ads)
                    adIndex++;
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the next active ad to show (for single ad requests)
        /// </summary>
        public async Task<WorldAppAd?> GetNextActiveAd(string? userVerificationLevel = null)
        {
            var now = DateTime.UtcNow;

            var query = _context.WorldAppAds
                .Where(a => a.IsActive)
                .Where(a => (a.StartDate == null || a.StartDate <= now) &&
                           (a.EndDate == null || a.EndDate >= now))
                .AsQueryable();

            // Filter by target audience if specified
            if (!string.IsNullOrWhiteSpace(userVerificationLevel))
            {
                query = query.Where(a =>
                    string.IsNullOrWhiteSpace(a.TargetAudience) ||
                    a.TargetAudience == "all" ||
                    a.TargetAudience == userVerificationLevel);
            }

            var ad = await query
                .OrderByDescending(a => a.Priority)
                .ThenBy(a => a.ViewCount) // Show ads with fewer views first
                .FirstOrDefaultAsync();

            return ad;
        }

        /// <summary>
        /// Wrapper class for ad slots in the feed
        /// </summary>
        public class AdSlot
        {
            public WorldAppAd? Ad { get; set; }
            public int Position { get; set; }
            public string Type => "ad";
        }
    }
}
