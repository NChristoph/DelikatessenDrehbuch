using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using DelikatessenDrehbuch.Data;
using Microsoft.EntityFrameworkCore;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    public class WorldClipWatchService : IWorldClipWatchService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<WorldClipWatchService> _logger;

        public WorldClipWatchService(ApplicationDbContext context, ILogger<WorldClipWatchService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public decimal MinimumQualifiedWatchSeconds => 8m;

        public async Task<TrackClipWatchResult> TrackWatchAsync(string viewerUserHash, TrackClipWatchRequest request, bool allowCreatorSelfWatch = false)
        {
            if (string.IsNullOrWhiteSpace(viewerUserHash))
            {
                return Reject("missing-viewer-hash");
            }

            if (request == null || request.PostingId <= 0)
            {
                return Reject("invalid-posting-id");
            }

            var watchedSeconds = decimal.Round(request.WatchedSeconds, 2, MidpointRounding.AwayFromZero);
            if (watchedSeconds < MinimumQualifiedWatchSeconds)
            {
                return Reject($"below-threshold:{watchedSeconds}");
            }

            var posting = await _context.WorldUserPosting
                .AsNoTracking()
                .Include(x => x.Recipe)
                .FirstOrDefaultAsync(x => x.Id == request.PostingId);

            if (posting == null)
            {
                return Reject("posting-not-found");
            }

            if (string.IsNullOrWhiteSpace(posting.CreatorId) || posting.Recipe == null)
            {
                return Reject("posting-missing-creator-or-recipe");
            }

            if (!allowCreatorSelfWatch && string.Equals(posting.CreatorId, viewerUserHash, StringComparison.OrdinalIgnoreCase))
            {
                return Reject("self-watch-blocked");
            }

            var startedAt = request.SessionStartedAtUtc?.ToUniversalTime() ?? DateTime.UtcNow.AddSeconds(-(double)watchedSeconds);
            var endedAt = request.SessionEndedAtUtc?.ToUniversalTime() ?? DateTime.UtcNow;
            if (endedAt < startedAt)
            {
                endedAt = startedAt.AddSeconds((double)watchedSeconds);
            }

            var session = new WorldClipWatchSession
            {
                WorldUserPostingId = posting.Id,
                RecipeId = posting.Recipe.Id,
                ViewerUserHash = viewerUserHash,
                CreatorUserHash = posting.CreatorId,
                WatchedSeconds = watchedSeconds,
                IsQualifiedView = true,
                SessionStartedAtUtc = startedAt,
                SessionEndedAtUtc = endedAt,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _context.WorldClipWatchSessions.AddAsync(session);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "TrackClipWatch persisted session {SessionId} for posting {PostingId}, viewer {ViewerUserHash}, creator {CreatorUserHash}, watchedSeconds {WatchedSeconds}.",
                session.Id,
                session.WorldUserPostingId,
                session.ViewerUserHash,
                session.CreatorUserHash,
                session.WatchedSeconds);

            return new TrackClipWatchResult
            {
                Tracked = true,
                Reason = "tracked"
            };
        }

        public async Task<CreatorWatchAnalyticsViewModel> BuildDashboardAnalyticsAsync(string userHash)
        {
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return new CreatorWatchAnalyticsViewModel();
            }

            var creatorSessions = await _context.WorldClipWatchSessions
                .AsNoTracking()
                .Where(x => x.IsQualifiedView && x.CreatorUserHash == userHash)
                .ToListAsync();

            var viewerSessions = await _context.WorldClipWatchSessions
                .AsNoTracking()
                .Where(x => x.IsQualifiedView && x.ViewerUserHash == userHash)
                .ToListAsync();

            var topClipAggregates = await _context.WorldClipWatchSessions
                .AsNoTracking()
                .Where(x => x.IsQualifiedView && x.CreatorUserHash == userHash)
                .GroupBy(x => new { x.WorldUserPostingId, x.RecipeId })
                .Select(group => new
                {
                    group.Key.WorldUserPostingId,
                    group.Key.RecipeId,
                    QualifiedViews = group.Count(),
                    TotalWatchSeconds = group.Sum(x => x.WatchedSeconds),
                    AverageWatchSeconds = group.Average(x => x.WatchedSeconds)
                })
                .OrderByDescending(x => x.TotalWatchSeconds)
                .ThenByDescending(x => x.QualifiedViews)
                .Take(5)
                .ToListAsync();

            var topPostingIds = topClipAggregates.Select(x => x.WorldUserPostingId).Distinct().ToList();
            var postingMap = await _context.WorldUserPosting
                .AsNoTracking()
                .Where(x => topPostingIds.Contains(x.Id))
                .Include(x => x.Recipe)
                .ToDictionaryAsync(x => x.Id);

            return new CreatorWatchAnalyticsViewModel
            {
                QualifiedViewCount = creatorSessions.Count,
                UniqueQualifiedViewerCount = creatorSessions
                    .Select(x => x.ViewerUserHash)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),
                TotalQualifiedWatchMinutes = decimal.Round(creatorSessions.Sum(x => x.WatchedSeconds) / 60m, 2, MidpointRounding.AwayFromZero),
                AverageQualifiedWatchSeconds = creatorSessions.Count == 0
                    ? 0
                    : decimal.Round(creatorSessions.Average(x => x.WatchedSeconds), 2, MidpointRounding.AwayFromZero),
                ViewerQualifiedClipCount = viewerSessions.Count,
                ViewerQualifiedWatchMinutes = decimal.Round(viewerSessions.Sum(x => x.WatchedSeconds) / 60m, 2, MidpointRounding.AwayFromZero),
                TopClips = topClipAggregates.Select(x =>
                {
                    postingMap.TryGetValue(x.WorldUserPostingId, out var posting);
                    var thumb = posting?.ThumbnailUrl;
                    if (string.IsNullOrWhiteSpace(thumb))
                    {
                        thumb = posting?.Source ?? string.Empty;
                    }

                    return new TopCreatorClipViewModel
                    {
                        PostingId = x.WorldUserPostingId,
                        RecipeId = x.RecipeId,
                        RecipeTitle = posting?.Recipe?.Title ?? posting?.Title ?? $"Clip #{x.WorldUserPostingId}",
                        ThumbnailUrl = thumb ?? string.Empty,
                        QualifiedViews = x.QualifiedViews,
                        TotalWatchMinutes = decimal.Round(x.TotalWatchSeconds / 60m, 2, MidpointRounding.AwayFromZero),
                        AverageWatchSeconds = decimal.Round(x.AverageWatchSeconds, 2, MidpointRounding.AwayFromZero)
                    };
                }).ToList()
            };
        }

        private TrackClipWatchResult Reject(string reason)
        {
            _logger.LogInformation("TrackClipWatch rejected: {Reason}.", reason);
            return new TrackClipWatchResult
            {
                Tracked = false,
                Reason = reason
            };
        }
    }
}
