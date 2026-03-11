using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    [Area("WorldMiniApp")]
    public class WatchAnalyticsController : Controller
    {
        private const string SessionUserHashKey = "WorldMiniAppUserHash";
        private readonly IWorldClipWatchService _worldClipWatchService;

        public WatchAnalyticsController(IWorldClipWatchService worldClipWatchService)
        {
            _worldClipWatchService = worldClipWatchService;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TrackClipWatch([FromForm] TrackClipWatchRequest request)
        {
            var viewerUserHash = ResolveUserHash();
            if (string.IsNullOrWhiteSpace(viewerUserHash))
            {
                return Unauthorized();
            }

            var wasTracked = await _worldClipWatchService.TrackWatchAsync(viewerUserHash, request);
            return Ok(new
            {
                tracked = wasTracked,
                minimumQualifiedWatchSeconds = _worldClipWatchService.MinimumQualifiedWatchSeconds
            });
        }

        private string ResolveUserHash()
        {
            var sessionHash = HttpContext.Session.GetString(SessionUserHashKey)
                ?? HttpContext.Session.GetString("UserHash");

            return string.IsNullOrWhiteSpace(sessionHash) ? string.Empty : sessionHash;
        }
    }
}
