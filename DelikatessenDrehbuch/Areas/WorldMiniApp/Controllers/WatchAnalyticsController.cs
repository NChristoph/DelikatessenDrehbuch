using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    [Area("WorldMiniApp")]
    public class WatchAnalyticsController : WorldMiniAppBaseController
    {
        private readonly IWorldClipWatchService _worldClipWatchService;
        private readonly ILogger<WatchAnalyticsController> _logger;

        public WatchAnalyticsController(IWorldClipWatchService worldClipWatchService, ILogger<WatchAnalyticsController> logger)
        {
            _worldClipWatchService = worldClipWatchService;
            _logger = logger;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TrackClipWatch([FromForm] TrackClipWatchRequest request)
        {
            var rawWatchedSeconds = Request.Form["watchedSeconds"].ToString();
            request.WatchedSeconds = ParseWatchedSeconds(rawWatchedSeconds, request.WatchedSeconds);

            var viewerUserHash = ResolveUserHash();
            if (string.IsNullOrWhiteSpace(viewerUserHash))
            {
                _logger.LogWarning("TrackClipWatch rejected: no viewer hash resolved for posting {PostingId}.", request?.PostingId);
                return Unauthorized();
            }

            var allowCreatorSelfWatch = !string.IsNullOrWhiteSpace(Request.Cookies[WorldMiniAppUserHashHelper.TestUserHashCookieKey]);
            _logger.LogInformation(
                "TrackClipWatch request: postingId={PostingId}, viewer={ViewerUserHash}, rawWatchedSeconds={RawWatchedSeconds}, parsedWatchedSeconds={ParsedWatchedSeconds}, allowCreatorSelfWatch={AllowCreatorSelfWatch}.",
                request?.PostingId,
                viewerUserHash,
                rawWatchedSeconds,
                request?.WatchedSeconds,
                allowCreatorSelfWatch);

            var result = await _worldClipWatchService.TrackWatchAsync(viewerUserHash, request, allowCreatorSelfWatch);

            _logger.LogInformation(
                "TrackClipWatch result: postingId={PostingId}, viewer={ViewerUserHash}, tracked={Tracked}, reason={Reason}.",
                request?.PostingId,
                viewerUserHash,
                result.Tracked,
                result.Reason);

            return Ok(new
            {
                tracked = result.Tracked,
                reason = result.Reason,
                rawWatchedSeconds,
                parsedWatchedSeconds = request.WatchedSeconds,
                minimumQualifiedWatchSeconds = _worldClipWatchService.MinimumQualifiedWatchSeconds
            });
        }

        // ResolveUserHash() kommt jetzt aus WorldMiniAppBaseController.

        private static decimal ParseWatchedSeconds(string rawWatchedSeconds, decimal fallback)
        {
            if (string.IsNullOrWhiteSpace(rawWatchedSeconds))
            {
                return fallback;
            }

            var normalized = rawWatchedSeconds.Trim();
            if (normalized.Contains(','))
            {
                normalized = normalized.Replace(".", string.Empty).Replace(',', '.');
            }

            if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariantParsed))
            {
                return invariantParsed;
            }

            if (decimal.TryParse(rawWatchedSeconds, NumberStyles.Number, CultureInfo.GetCultureInfo("de-AT"), out var deParsed))
            {
                return deParsed;
            }

            return fallback;
        }
    }
}
