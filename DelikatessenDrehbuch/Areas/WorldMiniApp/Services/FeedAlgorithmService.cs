using Microsoft.EntityFrameworkCore;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Data;

namespace WorldMiniApp.Services
{
    public class FeedAlgorithmService : IFeedAlgorithmService
    {
        private readonly ApplicationDbContext _context;

        // Scoring-Gewichte (können später per Config angepasst werden)
        private const double WEIGHT_LIKED_CREATOR = 3.0;
        private const double WEIGHT_FOLLOWED_CREATOR = 5.0;
        private const double WEIGHT_SIMILAR_KEYWORDS = 2.0;
        private const double WEIGHT_SIMILAR_CATEGORY = 1.5;
        private const double WEIGHT_POPULARITY = 0.5;
        private const double WEIGHT_RECENCY = 1.0;
        private const double WEIGHT_WATCHED_DURATION = 2.5;
        private const double DECAY_DAYS = 7; // Recency-Decay über 7 Tage

        public FeedAlgorithmService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<WorldUserPosting>> GetPersonalizedFeedAsync(
            string userHash,
            int take = 20,
            int skip = 0,
            string? searchQuery = null,
            string? categoryFilter = null,
            int? maxPrepTime = null)
        {
            // 1. Basis-Query mit Filtern
            var baseQuery = _context.WorldUserPosting
                .Include(p => p.Recipe)
                    .ThenInclude(r => r.RecipeKeywords)
                    .ThenInclude(rk => rk.Keyword)
                .AsQueryable();

            // Apply filters (wie im originalen FeedController)
            if (!string.IsNullOrEmpty(searchQuery))
            {
                var normalizedSearch = NormalizeString(searchQuery);
                baseQuery = baseQuery.Where(p =>
                    EF.Functions.Like(NormalizeString(p.Title ?? ""), $"%{normalizedSearch}%") ||
                    p.Recipe.RecipeKeywords.Any(rk =>
                        EF.Functions.Like(NormalizeString(rk.Keyword.Word_DE ?? ""), $"%{normalizedSearch}%") ||
                        EF.Functions.Like(NormalizeString(rk.Keyword.Word_EN ?? ""), $"%{normalizedSearch}%") ||
                        EF.Functions.Like(NormalizeString(rk.Keyword.Word_ESP ?? ""), $"%{normalizedSearch}%") ||
                        EF.Functions.Like(NormalizeString(rk.Keyword.Word_PRT ?? ""), $"%{normalizedSearch}%")
                    )
                );
            }

            if (!string.IsNullOrEmpty(categoryFilter))
            {
                var normalizedCategory = NormalizeString(categoryFilter);
                baseQuery = baseQuery.Where(p =>
                    EF.Functions.Like(NormalizeString(p.Recipe.Category ?? ""), $"%{normalizedCategory}%")
                );
            }

            if (maxPrepTime.HasValue)
            {
                baseQuery = baseQuery.Where(p => p.Recipe.PreparationTime <= maxPrepTime.Value);
            }

            // 2. Lade User-Präferenzen parallel
            var userPreferencesTask = LoadUserPreferencesAsync(userHash);

            // 3. Lade Postings (mehr als benötigt für besseres Ranking)
            var candidatePostings = await baseQuery
                .OrderByDescending(p => p.CreationTime)
                .Take((skip + take) * 3) // 3x mehr für bessere Auswahl
                .ToListAsync();

            var userPreferences = await userPreferencesTask;

            // 4. Score berechnen für alle Kandidaten
            var scoredPostings = new List<(WorldUserPosting posting, double score)>();

            foreach (var posting in candidatePostings)
            {
                var score = await CalculatePostingScoreAsync(posting, userHash, userPreferences);
                scoredPostings.Add((posting, score));
            }

            // 5. Sortieren nach Score und Pagination
            var result = scoredPostings
                .OrderByDescending(x => x.score)
                .Skip(skip)
                .Take(take)
                .Select(x => x.posting)
                .ToList();

            return result;
        }

        public async Task<double> CalculatePostingScoreAsync(WorldUserPosting posting, string userHash)
        {
            var userPreferences = await LoadUserPreferencesAsync(userHash);
            return await CalculatePostingScoreAsync(posting, userHash, userPreferences);
        }

        private async Task<double> CalculatePostingScoreAsync(
            WorldUserPosting posting,
            string userHash,
            UserPreferences userPreferences)
        {
            double score = 0;

            // 1. FOLLOWED CREATOR BOOST
            if (userPreferences.FollowedCreatorIds.Contains(posting.CreatorId ?? ""))
            {
                score += WEIGHT_FOLLOWED_CREATOR;
            }

            // 2. LIKED CREATOR BOOST (hat User schon andere Postings von diesem Creator geliked?)
            if (userPreferences.LikedCreatorIds.Contains(posting.CreatorId ?? ""))
            {
                score += WEIGHT_LIKED_CREATOR;
            }

            // 3. KEYWORD SIMILARITY (Content-Based Filtering)
            if (posting.Recipe?.RecipeKeywords != null)
            {
                var postingKeywords = posting.Recipe.RecipeKeywords
                    .Select(rk => rk.KeywordId)
                    .ToHashSet();

                var matchingKeywords = postingKeywords.Intersect(userPreferences.PreferredKeywordIds).Count();
                if (matchingKeywords > 0)
                {
                    score += WEIGHT_SIMILAR_KEYWORDS * matchingKeywords;
                }
            }

            // 4. CATEGORY SIMILARITY
            if (!string.IsNullOrEmpty(posting.Recipe?.Category))
            {
                var normalizedCategory = NormalizeString(posting.Recipe.Category);
                if (userPreferences.PreferredCategories.Contains(normalizedCategory))
                {
                    score += WEIGHT_SIMILAR_CATEGORY;
                }
            }

            // 5. POPULARITY SCORE (globale Like-Anzahl)
            var likeCount = posting.Recipe?.LikeCount ?? 0;
            score += WEIGHT_POPULARITY * Math.Log(likeCount + 1); // Logarithmic scaling

            // 6. RECENCY SCORE (neuere Posts bevorzugen mit Decay)
            var daysSinceCreation = (DateTime.Now - posting.CreationTime).TotalDays;
            var recencyScore = Math.Exp(-daysSinceCreation / DECAY_DAYS);
            score += WEIGHT_RECENCY * recencyScore;

            // 7. WATCH DURATION BOOST (wenn User ähnliche Postings lange angeschaut hat)
            if (userPreferences.AvgWatchDuration > 0)
            {
                score += WEIGHT_WATCHED_DURATION * (userPreferences.AvgWatchDuration / 60.0); // Normalisierung auf Minuten
            }

            // 8. DIVERSITY PENALTY (vermeidet zu viele Posts vom gleichen Creator hintereinander)
            // Wird später im Ranking-Algorithmus behandelt

            // 9. BEREITS GESEHEN? → Score = 0
            if (userPreferences.SeenPostingIds.Contains(posting.Id))
            {
                score *= 0.1; // Stark reduzieren, aber nicht komplett ausschließen
            }

            // 10. PREP TIME PREFERENCE (wenn User Pattern zeigt)
            if (userPreferences.PreferredPrepTimeRange != null && posting.Recipe != null)
            {
                var prepTime = posting.Recipe.PreparationTime;
                var (min, max) = userPreferences.PreferredPrepTimeRange.Value;
                if (prepTime >= min && prepTime <= max)
                {
                    score += 1.0;
                }
            }

            return score;
        }

        public async Task TrackInteractionAsync(
            string userHash,
            int postingId,
            InteractionType type,
            double? duration = null)
        {
            var interaction = new WorldUserInteraction
            {
                UserHash = userHash,
                PostingId = postingId,
                InteractionType = type,
                Duration = duration,
                CreatedAt = DateTime.Now
            };

            _context.WorldUserInteractions.Add(interaction);
            await _context.SaveChangesAsync();
        }

        private async Task<UserPreferences> LoadUserPreferencesAsync(string userHash)
        {
            var preferences = new UserPreferences();

            // 1. Gefolgte Creator
            var followedCreators = await _context.WorldUserAbo
                .Where(a => a.WorldUser.UserHash == userHash)
                .Select(a => a.Creator.UserHash)
                .ToListAsync();
            preferences.FollowedCreatorIds = followedCreators.ToHashSet();

            // 2. Creator von gelikten Postings
            var likedCreators = await _context.WorldUserLike
                .Where(l => l.WorldAppUser.UserHash == userHash)
                .Join(_context.WorldUserPosting,
                    like => like.Recipe.Id,
                    posting => posting.Recipe.Id,
                    (like, posting) => posting.CreatorId ?? "")
                .Distinct()
                .ToListAsync();
            preferences.LikedCreatorIds = likedCreators.ToHashSet();

            // 3. Bevorzugte Keywords (aus gelikten und lang angeschauten Postings)
            var likedRecipeIds = await _context.WorldUserLike
                .Where(l => l.WorldAppUser.UserHash == userHash)
                .Select(l => l.Recipe.Id)
                .ToListAsync();

            var watchedPostingIds = await _context.WorldUserInteractions
                .Where(i => i.UserHash == userHash &&
                           i.InteractionType == InteractionType.WatchComplete &&
                           i.Duration > 10) // Mind. 10 Sekunden
                .Select(i => i.PostingId)
                .ToListAsync();

            var relevantRecipeIds = await _context.WorldUserPosting
                .Where(p => watchedPostingIds.Contains(p.Id))
                .Select(p => p.Recipe.Id)
                .ToListAsync();

            relevantRecipeIds.AddRange(likedRecipeIds);

            // Keywords
            var keywordsList = await _context.RecipeBaseKeywords
                .Where(rk => relevantRecipeIds.Contains(rk.RecipeBaseDataId))
                .Select(rk => rk.KeywordId)
                .ToListAsync();

            preferences.PreferredKeywordIds = keywordsList
                .GroupBy(kid => kid)
                .OrderByDescending(g => g.Count())
                .Take(20)
                .Select(g => g.Key)
                .ToHashSet();

            // 4. Bevorzugte Kategorien
            var categoriesList = await _context.RecipeBaseData
                .Where(r => relevantRecipeIds.Contains(r.Id))
                .Select(r => NormalizeString(r.Category ?? ""))
                .Distinct()
                .ToListAsync();
            preferences.PreferredCategories = categoriesList.ToHashSet();

            // 5. Durchschnittliche Watch-Duration
            var watchDurations = await _context.WorldUserInteractions
                .Where(i => i.UserHash == userHash &&
                           i.InteractionType == InteractionType.WatchComplete &&
                           i.Duration.HasValue)
                .Select(i => i.Duration.Value)
                .ToListAsync();

            preferences.AvgWatchDuration = watchDurations.Any() ? watchDurations.Average() : 0;

            // 6. Bereits gesehene Postings (letzte 7 Tage)
            var sevenDaysAgo = DateTime.Now.AddDays(-7);
            var seenPostings = await _context.WorldUserInteractions
                .Where(i => i.UserHash == userHash &&
                           i.CreatedAt >= sevenDaysAgo &&
                           (i.InteractionType == InteractionType.View ||
                            i.InteractionType == InteractionType.WatchComplete))
                .Select(i => i.PostingId)
                .ToListAsync();
            preferences.SeenPostingIds = seenPostings.ToHashSet();

            // 7. Bevorzugte Prep-Time Range
            var likedPrepTimes = await _context.WorldUserLike
                .Where(l => l.WorldAppUser.UserHash == userHash)
                .Select(l => l.Recipe.PreparationTime)
                .ToListAsync();

            if (likedPrepTimes.Any())
            {
                var avg = likedPrepTimes.Average();
                var stdDev = CalculateStandardDeviation(likedPrepTimes);
                preferences.PreferredPrepTimeRange = ((int)(avg - stdDev), (int)(avg + stdDev));
            }

            return preferences;
        }

        private static string NormalizeString(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return input.ToLowerInvariant()
                .Replace(" ", "")
                .Replace("-", "")
                .Replace("ä", "a")
                .Replace("ö", "o")
                .Replace("ü", "u")
                .Replace("ß", "ss")
                .Replace("é", "e")
                .Replace("è", "e")
                .Replace("ê", "e")
                .Replace("á", "a")
                .Replace("à", "a")
                .Replace("ã", "a")
                .Replace("â", "a")
                .Replace("ó", "o")
                .Replace("õ", "o")
                .Replace("ô", "o");
        }

        private static double CalculateStandardDeviation(IEnumerable<int> values)
        {
            var enumerable = values.ToList();
            var avg = enumerable.Average();
            var sumOfSquares = enumerable.Sum(val => Math.Pow(val - avg, 2));
            return Math.Sqrt(sumOfSquares / enumerable.Count);
        }

        private class UserPreferences
        {
            public HashSet<string> FollowedCreatorIds { get; set; } = new();
            public HashSet<string> LikedCreatorIds { get; set; } = new();
            public HashSet<int> PreferredKeywordIds { get; set; } = new();
            public HashSet<string> PreferredCategories { get; set; } = new();
            public double AvgWatchDuration { get; set; }
            public HashSet<int> SeenPostingIds { get; set; } = new();
            public (int min, int max)? PreferredPrepTimeRange { get; set; }
        }
    }
}
