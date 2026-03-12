using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using DelikatessenDrehbuch.Data;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    public class WorldAdPreferenceService : IWorldAdPreferenceService
    {
        private readonly ApplicationDbContext _context;

        public WorldAdPreferenceService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<AdPreferenceDashboardViewModel> GetDashboardProfileAsync(string userHash, string preferredLanguage)
        {
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return new AdPreferenceDashboardViewModel();
            }

            var profile = await EnsureProfileAsync(userHash, preferredLanguage);
            var shouldRefresh = profile.AllowPersonalizedAds
                && (!profile.LastProfileRefreshUtc.HasValue || profile.LastProfileRefreshUtc.Value < DateTime.UtcNow.AddHours(-6));

            if (shouldRefresh)
            {
                await RebuildProfileAsync(userHash, preferredLanguage);
                profile = await EnsureProfileAsync(userHash, preferredLanguage);
            }

            var topInterests = await _context.WorldAdPreferenceInterests
                .AsNoTracking()
                .Where(x => x.UserHash == userHash)
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.LastSignalAtUtc)
                .Take(12)
                .Select(x => new AdPreferenceInterestViewModel
                {
                    InterestType = x.InterestType,
                    DisplayLabel = x.DisplayLabel,
                    Score = x.Score,
                    SignalCount = x.SignalCount,
                    LastSignalAtUtc = x.LastSignalAtUtc
                })
                .ToListAsync();

            return new AdPreferenceDashboardViewModel
            {
                AllowPersonalizedAds = profile.AllowPersonalizedAds,
                AllowCategoryTargeting = profile.AllowCategoryTargeting,
                AllowKeywordTargeting = profile.AllowKeywordTargeting,
                AllowCreatorTargeting = profile.AllowCreatorTargeting,
                PreferredLanguage = profile.PreferredLanguage,
                QualifiedWatchCount = profile.QualifiedWatchCount,
                LikedRecipeCount = profile.LikedRecipeCount,
                FollowedCreatorCount = profile.FollowedCreatorCount,
                LastProfileRefreshUtc = profile.LastProfileRefreshUtc,
                TopCategories = DeserializeList(profile.TopCategoriesJson),
                TopKeywords = DeserializeList(profile.TopKeywordsJson),
                TopCreators = DeserializeList(profile.TopCreatorsJson),
                SegmentLabels = DeserializeList(profile.SegmentLabelsJson),
                TopInterests = topInterests
            };
        }

        public async Task UpdateSettingsAsync(string userHash, UpdateAdPreferenceSettingsRequest request, string preferredLanguage)
        {
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return;
            }

            var profile = await EnsureProfileAsync(userHash, preferredLanguage);
            profile.AllowPersonalizedAds = request.AllowPersonalizedAds;
            profile.AllowCategoryTargeting = request.AllowPersonalizedAds && request.AllowCategoryTargeting;
            profile.AllowKeywordTargeting = request.AllowPersonalizedAds && request.AllowKeywordTargeting;
            profile.AllowCreatorTargeting = request.AllowPersonalizedAds && request.AllowCreatorTargeting;
            profile.PreferredLanguage = NormalizeLanguage(preferredLanguage);
            profile.UpdatedAtUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            if (profile.AllowPersonalizedAds)
            {
                await RebuildProfileAsync(userHash, preferredLanguage);
            }
            else
            {
                _context.WorldAdPreferenceInterests.RemoveRange(_context.WorldAdPreferenceInterests.Where(x => x.UserHash == userHash));
                profile.TopCategoriesJson = "[]";
                profile.TopKeywordsJson = "[]";
                profile.TopCreatorsJson = "[]";
                profile.SegmentLabelsJson = "[]";
                profile.LastProfileRefreshUtc = DateTime.UtcNow;
                profile.QualifiedWatchCount = 0;
                profile.LikedRecipeCount = 0;
                profile.FollowedCreatorCount = 0;
                await _context.SaveChangesAsync();
            }
        }

        public async Task RebuildProfileAsync(string userHash, string preferredLanguage)
        {
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return;
            }

            var profile = await EnsureProfileAsync(userHash, preferredLanguage);
            if (!profile.AllowPersonalizedAds)
            {
                return;
            }

            var likes = await _context.WorldUserLike
                .AsNoTracking()
                .Where(x => x.WorldAppUser.UserHash == userHash)
                .Include(x => x.Recipe)
                    .ThenInclude(r => r.RecipeKeywords)
                    .ThenInclude(link => link.Keyword)
                .ToListAsync();

            var qualifiedWatchSessions = await _context.WorldClipWatchSessions
                .AsNoTracking()
                .Where(x => x.ViewerUserHash == userHash && x.IsQualifiedView)
                .ToListAsync();

            var watchedPostingIds = qualifiedWatchSessions.Select(x => x.WorldUserPostingId).Distinct().ToList();
            var watchedPostings = await _context.WorldUserPosting
                .AsNoTracking()
                .Where(x => watchedPostingIds.Contains(x.Id))
                .Include(x => x.Recipe)
                    .ThenInclude(r => r.RecipeKeywords)
                    .ThenInclude(link => link.Keyword)
                .ToDictionaryAsync(x => x.Id);

            var follows = await _context.WorldUserAbo
                .AsNoTracking()
                .Where(x => x.WorldUser.UserHash == userHash)
                .Include(x => x.Creator)
                .ToListAsync();

            var interestMap = new Dictionary<string, InterestAccumulator>(StringComparer.OrdinalIgnoreCase);

            foreach (var like in likes)
            {
                AddRecipeSignals(interestMap, like.Recipe, 4m, DateTime.UtcNow, profile);
            }

            foreach (var watch in qualifiedWatchSessions)
            {
                if (!watchedPostings.TryGetValue(watch.WorldUserPostingId, out var posting) || posting.Recipe == null)
                {
                    continue;
                }

                var watchScore = Math.Max(1m, decimal.Round(watch.WatchedSeconds / 12m, 2, MidpointRounding.AwayFromZero));
                AddRecipeSignals(interestMap, posting.Recipe, watchScore, watch.CreatedAtUtc, profile);

                if (profile.AllowCreatorTargeting && !string.IsNullOrWhiteSpace(posting.CreatorId))
                {
                    AddInterest(interestMap, "Creator", posting.CreatorId, posting.CreatorName ?? posting.CreatorId, watchScore + 1m, watch.CreatedAtUtc);
                }
            }

            foreach (var follow in follows)
            {
                if (profile.AllowCreatorTargeting && follow.Creator != null && !string.IsNullOrWhiteSpace(follow.Creator.UserHash))
                {
                    AddInterest(interestMap, "Creator", follow.Creator.UserHash, follow.Creator.UserName ?? follow.Creator.UserHash, 6m, DateTime.UtcNow);
                }
            }

            var orderedInterests = interestMap.Values
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.LastSignalAtUtc)
                .ToList();

            var existing = _context.WorldAdPreferenceInterests.Where(x => x.UserHash == userHash);
            _context.WorldAdPreferenceInterests.RemoveRange(existing);

            var newRows = orderedInterests.Select(x => new WorldAdPreferenceInterest
            {
                UserHash = userHash,
                InterestType = x.InterestType,
                InterestKey = x.InterestKey,
                DisplayLabel = x.DisplayLabel,
                Score = decimal.Round(x.Score, 2, MidpointRounding.AwayFromZero),
                SignalCount = x.SignalCount,
                LastSignalAtUtc = x.LastSignalAtUtc
            }).ToList();

            await _context.WorldAdPreferenceInterests.AddRangeAsync(newRows);

            profile.PreferredLanguage = NormalizeLanguage(preferredLanguage);
            profile.QualifiedWatchCount = qualifiedWatchSessions.Count;
            profile.LikedRecipeCount = likes.Count;
            profile.FollowedCreatorCount = follows.Count;
            profile.TopCategoriesJson = JsonConvert.SerializeObject(orderedInterests.Where(x => x.InterestType == "Category").Take(5).Select(x => x.DisplayLabel).ToList());
            profile.TopKeywordsJson = JsonConvert.SerializeObject(orderedInterests.Where(x => x.InterestType == "Keyword").Take(8).Select(x => x.DisplayLabel).ToList());
            profile.TopCreatorsJson = JsonConvert.SerializeObject(orderedInterests.Where(x => x.InterestType == "Creator").Take(5).Select(x => x.DisplayLabel).ToList());
            profile.SegmentLabelsJson = JsonConvert.SerializeObject(BuildSegments(profile, orderedInterests));
            profile.LastProfileRefreshUtc = DateTime.UtcNow;
            profile.UpdatedAtUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        private void AddRecipeSignals(Dictionary<string, InterestAccumulator> interestMap, RecipeBaseData? recipe, decimal score, DateTime lastSignalAtUtc, WorldAdPreferenceProfile profile)
        {
            if (recipe == null)
            {
                return;
            }

            if (profile.AllowCategoryTargeting && !string.IsNullOrWhiteSpace(recipe.Category))
            {
                AddInterest(interestMap, "Category", recipe.Category.Trim(), recipe.Category.Trim(), score, lastSignalAtUtc);
            }

            if (profile.AllowKeywordTargeting && recipe.RecipeKeywords != null)
            {
                foreach (var keywordLink in recipe.RecipeKeywords)
                {
                    var label = keywordLink.Keyword?.Word_DE;
                    if (string.IsNullOrWhiteSpace(label))
                    {
                        continue;
                    }

                    AddInterest(interestMap, "Keyword", label.Trim(), label.Trim(), Math.Max(1m, score * 0.8m), lastSignalAtUtc);
                }
            }

            foreach (var diet in ExtractDietLabels(recipe.Preferences))
            {
                AddInterest(interestMap, "Diet", diet, diet, Math.Max(1m, score * 0.9m), lastSignalAtUtc);
            }
        }

        private static IEnumerable<string> ExtractDietLabels(string? preferences)
        {
            if (string.IsNullOrWhiteSpace(preferences))
            {
                yield break;
            }

            var tokens = preferences
                .Split(new[] { ',', ';', '|', '/' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var token in tokens)
            {
                yield return token;
            }
        }

        private static void AddInterest(Dictionary<string, InterestAccumulator> interestMap, string interestType, string interestKey, string displayLabel, decimal score, DateTime lastSignalAtUtc)
        {
            var mapKey = $"{interestType}:{interestKey}";
            if (!interestMap.TryGetValue(mapKey, out var existing))
            {
                existing = new InterestAccumulator
                {
                    InterestType = interestType,
                    InterestKey = interestKey,
                    DisplayLabel = displayLabel,
                    Score = 0,
                    SignalCount = 0,
                    LastSignalAtUtc = lastSignalAtUtc
                };
                interestMap[mapKey] = existing;
            }

            existing.Score += score;
            existing.SignalCount += 1;
            if (lastSignalAtUtc > existing.LastSignalAtUtc)
            {
                existing.LastSignalAtUtc = lastSignalAtUtc;
            }
        }

        private async Task<WorldAdPreferenceProfile> EnsureProfileAsync(string userHash, string preferredLanguage)
        {
            var profile = await _context.WorldAdPreferenceProfiles.FirstOrDefaultAsync(x => x.UserHash == userHash);
            if (profile != null)
            {
                return profile;
            }

            profile = new WorldAdPreferenceProfile
            {
                UserHash = userHash,
                PreferredLanguage = NormalizeLanguage(preferredLanguage),
                AllowPersonalizedAds = false,
                AllowCategoryTargeting = true,
                AllowKeywordTargeting = true,
                AllowCreatorTargeting = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _context.WorldAdPreferenceProfiles.AddAsync(profile);
            await _context.SaveChangesAsync();
            return profile;
        }

        private static List<string> BuildSegments(WorldAdPreferenceProfile profile, List<InterestAccumulator> orderedInterests)
        {
            var segments = new List<string>();

            if (profile.QualifiedWatchCount >= 12)
            {
                segments.Add("High Watch Intent");
            }

            if (profile.LikedRecipeCount >= 5)
            {
                segments.Add("Active Taste Explorer");
            }

            if (profile.FollowedCreatorCount >= 3)
            {
                segments.Add("Creator Loyalist");
            }

            if (orderedInterests.Any(x => x.InterestType == "Diet" && x.DisplayLabel.Contains("vegan", StringComparison.OrdinalIgnoreCase)))
            {
                segments.Add("Vegan Friendly");
            }
            else if (orderedInterests.Any(x => x.InterestType == "Diet" && x.DisplayLabel.Contains("vegetar", StringComparison.OrdinalIgnoreCase)))
            {
                segments.Add("Vegetarian Friendly");
            }

            if (orderedInterests.Any(x => x.InterestType == "Category" && x.DisplayLabel.Contains("dess", StringComparison.OrdinalIgnoreCase)))
            {
                segments.Add("Sweet Tooth");
            }

            return segments.Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToList();
        }

        private static string NormalizeLanguage(string? preferredLanguage)
        {
            if (string.IsNullOrWhiteSpace(preferredLanguage))
            {
                return "de";
            }

            return preferredLanguage.Trim().ToLowerInvariant();
        }

        private static List<string> DeserializeList(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<string>();
            }

            try
            {
                return JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        private sealed class InterestAccumulator
        {
            public string InterestType { get; set; } = string.Empty;
            public string InterestKey { get; set; } = string.Empty;
            public string DisplayLabel { get; set; } = string.Empty;
            public decimal Score { get; set; }
            public int SignalCount { get; set; }
            public DateTime LastSignalAtUtc { get; set; }
        }
    }
}

