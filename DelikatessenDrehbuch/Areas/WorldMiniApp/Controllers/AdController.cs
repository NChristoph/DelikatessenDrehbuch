using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    [Area("WorldMiniApp")]
    [ApiController]
    [Route("api/worldminiapp/ads")]
    public class AdController : WorldMiniAppBaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdController> _logger;

        public AdController(ApplicationDbContext context, ILogger<AdController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Track ad impression (view)
        /// </summary>
        [HttpPost("impression/{adId}")]
        public async Task<IActionResult> TrackImpression(int adId)
        {
            try
            {
                var ad = await _context.WorldAppAds.FindAsync(adId);
                if (ad == null)
                {
                    return NotFound();
                }

                var userHash = ResolveUserHash(string.Empty);
                var userAgent = Request.Headers["User-Agent"].ToString();

                // Increment view count
                ad.ViewCount++;

                // Create detailed impression record
                var impression = new WorldAppAdImpression
                {
                    AdId = adId,
                    UserHash = string.IsNullOrWhiteSpace(userHash) ? null : userHash,
                    WasClicked = false,
                    ViewedAt = DateTime.UtcNow,
                    UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent.Substring(0, Math.Min(200, userAgent.Length))
                };

                _context.WorldAppAdImpressions.Add(impression);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, impressionId = impression.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking ad impression for ad {AdId}", adId);
                return StatusCode(500, new { success = false, error = "Failed to track impression" });
            }
        }

        /// <summary>
        /// Track ad click
        /// </summary>
        [HttpPost("click/{adId}")]
        public async Task<IActionResult> TrackClick(int adId, [FromBody] TrackClickRequest? request = null)
        {
            try
            {
                var ad = await _context.WorldAppAds.FindAsync(adId);
                if (ad == null)
                {
                    return NotFound();
                }

                var userHash = ResolveUserHash(string.Empty);
                var impressionId = request?.ImpressionId;

                // Increment click count
                ad.ClickCount++;

                // If we have an impression ID, update that impression record
                if (impressionId.HasValue)
                {
                    var impression = await _context.WorldAppAdImpressions.FindAsync(impressionId.Value);
                    if (impression != null && impression.AdId == adId)
                    {
                        impression.WasClicked = true;
                        impression.ClickedAt = DateTime.UtcNow;
                    }
                }
                else
                {
                    // Create a new impression record for the click if we don't have one
                    var userAgent = Request.Headers["User-Agent"].ToString();
                    var impression = new WorldAppAdImpression
                    {
                        AdId = adId,
                        UserHash = string.IsNullOrWhiteSpace(userHash) ? null : userHash,
                        WasClicked = true,
                        ViewedAt = DateTime.UtcNow,
                        ClickedAt = DateTime.UtcNow,
                        UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent.Substring(0, Math.Min(200, userAgent.Length))
                    };
                    _context.WorldAppAdImpressions.Add(impression);
                }

                await _context.SaveChangesAsync();

                return Ok(new { success = true, targetUrl = ad.TargetUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking ad click for ad {AdId}", adId);
                return StatusCode(500, new { success = false, error = "Failed to track click" });
            }
        }

        /// <summary>
        /// Get ad analytics (admin only)
        /// </summary>
        [HttpGet("analytics")]
        public async Task<IActionResult> GetAnalytics()
        {
            var userHash = ResolveUserHash(string.Empty);
            if (userHash != SuperUserHash)
            {
                return Unauthorized();
            }

            try
            {
                var ads = await _context.WorldAppAds
                    .OrderByDescending(a => a.IsActive)
                    .ThenByDescending(a => a.CreatedAt)
                    .Select(a => new
                    {
                        a.Id,
                        a.Title,
                        a.IsActive,
                        a.ViewCount,
                        a.ClickCount,
                        CTR = a.ViewCount > 0 ? Math.Round((double)a.ClickCount / a.ViewCount * 100, 2) : 0,
                        a.CreatedAt,
                        a.StartDate,
                        a.EndDate
                    })
                    .ToListAsync();

                var totalViews = ads.Sum(a => a.ViewCount);
                var totalClicks = ads.Sum(a => a.ClickCount);
                var avgCTR = totalViews > 0 ? Math.Round((double)totalClicks / totalViews * 100, 2) : 0;

                return Ok(new
                {
                    ads,
                    summary = new
                    {
                        totalAds = ads.Count,
                        activeAds = ads.Count(a => a.IsActive),
                        totalViews,
                        totalClicks,
                        avgCTR
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting ad analytics");
                return StatusCode(500, new { success = false, error = "Failed to get analytics" });
            }
        }

        public class TrackClickRequest
        {
            public int? ImpressionId { get; set; }
        }
    }
}
