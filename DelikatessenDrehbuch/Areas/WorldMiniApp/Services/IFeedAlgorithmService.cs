using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;

namespace WorldMiniApp.Services
{
    public interface IFeedAlgorithmService
    {
        /// <summary>
        /// Berechnet personalisierten Feed basierend auf User-Interaktionen
        /// </summary>
        Task<List<WorldUserPosting>> GetPersonalizedFeedAsync(
            string userHash,
            int take = 20,
            int skip = 0,
            string? searchQuery = null,
            string? categoryFilter = null,
            int? maxPrepTime = null
        );

        /// <summary>
        /// Berechnet Score für ein Posting basierend auf User-Präferenzen
        /// </summary>
        Task<double> CalculatePostingScoreAsync(WorldUserPosting posting, string userHash);

        /// <summary>
        /// Trackt User-Interaktion für zukünftige Empfehlungen
        /// </summary>
        Task TrackInteractionAsync(string userHash, int postingId, InteractionType type, double? duration = null);
    }

    public enum InteractionType
    {
        View = 1,
        Like = 2,
        Unlike = 3,
        Skip = 4,
        WatchComplete = 5,
        AddToMealPlan = 6,
        FollowCreator = 7,
        UnfollowCreator = 8
    }
}
