using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces
{
    public interface IWorldClipWatchService
    {
        decimal MinimumQualifiedWatchSeconds { get; }
        Task<TrackClipWatchResult> TrackWatchAsync(string viewerUserHash, TrackClipWatchRequest request, bool allowCreatorSelfWatch = false);
        Task<CreatorWatchAnalyticsViewModel> BuildDashboardAnalyticsAsync(string userHash);
    }

    public sealed class TrackClipWatchResult
    {
        public bool Tracked { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
