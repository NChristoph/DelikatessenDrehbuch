using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces
{
    public interface IWorldClipWatchService
    {
        decimal MinimumQualifiedWatchSeconds { get; }
        Task<bool> TrackWatchAsync(string viewerUserHash, TrackClipWatchRequest request);
        Task<CreatorWatchAnalyticsViewModel> BuildDashboardAnalyticsAsync(string userHash);
    }
}
