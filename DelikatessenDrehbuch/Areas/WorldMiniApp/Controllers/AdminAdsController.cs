using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    [Area("WorldMiniApp")]
    [Route("WorldMiniApp/Admin/Ads")]
    public class AdminAdsController : WorldMiniAppBaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminAdsController> _logger;

        public AdminAdsController(ApplicationDbContext context, ILogger<AdminAdsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private bool IsAdmin()
        {
            var userHash = ResolveUserHash(string.Empty);
            return !string.IsNullOrWhiteSpace(SuperUserHash) && userHash == SuperUserHash;
        }

        /// <summary>
        /// List all ads with analytics
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!IsAdmin())
            {
                return Unauthorized();
            }

            var ads = await _context.WorldAppAds
                .OrderByDescending(a => a.IsActive)
                .ThenByDescending(a => a.Priority)
                .ThenByDescending(a => a.CreatedAt)
                .ToListAsync();

            return View(ads);
        }

        /// <summary>
        /// Create new ad form
        /// </summary>
        [HttpGet("Create")]
        public IActionResult Create()
        {
            if (!IsAdmin())
            {
                return Unauthorized();
            }

            return View(new WorldAppAd { ShowEveryXVideos = 5, Priority = 1, IsActive = true });
        }

        /// <summary>
        /// Create new ad
        /// </summary>
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(WorldAppAd ad)
        {
            if (!IsAdmin())
            {
                return Unauthorized();
            }

            try
            {
                var userHash = ResolveUserHash(string.Empty);
                ad.CreatedByUserHash = userHash;
                ad.CreatedAt = DateTime.UtcNow;
                ad.ViewCount = 0;
                ad.ClickCount = 0;

                _context.WorldAppAds.Add(ad);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating ad");
                ModelState.AddModelError("", "Failed to create ad");
                return View(ad);
            }
        }

        /// <summary>
        /// Edit ad form
        /// </summary>
        [HttpGet("Edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            if (!IsAdmin())
            {
                return Unauthorized();
            }

            var ad = await _context.WorldAppAds.FindAsync(id);
            if (ad == null)
            {
                return NotFound();
            }

            return View(ad);
        }

        /// <summary>
        /// Update ad
        /// </summary>
        [HttpPost("Edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, WorldAppAd updatedAd)
        {
            if (!IsAdmin())
            {
                return Unauthorized();
            }

            if (id != updatedAd.Id)
            {
                return BadRequest();
            }

            try
            {
                var ad = await _context.WorldAppAds.FindAsync(id);
                if (ad == null)
                {
                    return NotFound();
                }

                // Update fields
                ad.Title = updatedAd.Title;
                ad.Description = updatedAd.Description;
                ad.MediaUrl = updatedAd.MediaUrl;
                ad.ThumbnailUrl = updatedAd.ThumbnailUrl;
                ad.IsVideo = updatedAd.IsVideo;
                ad.TargetUrl = updatedAd.TargetUrl;
                ad.CtaText = updatedAd.CtaText;
                ad.IsActive = updatedAd.IsActive;
                ad.StartDate = updatedAd.StartDate;
                ad.EndDate = updatedAd.EndDate;
                ad.ShowEveryXVideos = updatedAd.ShowEveryXVideos;
                ad.Priority = updatedAd.Priority;
                ad.TargetAudience = updatedAd.TargetAudience;
                ad.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating ad {AdId}", id);
                ModelState.AddModelError("", "Failed to update ad");
                return View(updatedAd);
            }
        }

        /// <summary>
        /// Delete ad
        /// </summary>
        [HttpPost("Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!IsAdmin())
            {
                return Unauthorized();
            }

            try
            {
                var ad = await _context.WorldAppAds.FindAsync(id);
                if (ad == null)
                {
                    return NotFound();
                }

                _context.WorldAppAds.Remove(ad);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting ad {AdId}", id);
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Toggle ad active status
        /// </summary>
        [HttpPost("ToggleActive/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            if (!IsAdmin())
            {
                return Unauthorized();
            }

            try
            {
                var ad = await _context.WorldAppAds.FindAsync(id);
                if (ad == null)
                {
                    return NotFound();
                }

                ad.IsActive = !ad.IsActive;
                ad.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling ad {AdId}", id);
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// View detailed analytics for an ad
        /// </summary>
        [HttpGet("Analytics/{id}")]
        public async Task<IActionResult> Analytics(int id)
        {
            if (!IsAdmin())
            {
                return Unauthorized();
            }

            var ad = await _context.WorldAppAds.FindAsync(id);
            if (ad == null)
            {
                return NotFound();
            }

            // Get impressions for the last 30 days
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            var impressions = await _context.WorldAppAdImpressions
                .Where(i => i.AdId == id && i.ViewedAt >= thirtyDaysAgo)
                .OrderByDescending(i => i.ViewedAt)
                .Take(1000)
                .ToListAsync();

            // Group by date for chart
            var dailyStats = impressions
                .GroupBy(i => i.ViewedAt.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Views = g.Count(),
                    Clicks = g.Count(i => i.WasClicked),
                    CTR = g.Count() > 0 ? Math.Round((double)g.Count(i => i.WasClicked) / g.Count() * 100, 2) : 0
                })
                .OrderBy(s => s.Date)
                .ToList();

            ViewData["Ad"] = ad;
            ViewData["DailyStats"] = dailyStats;
            ViewData["TotalImpressions"] = impressions.Count;
            ViewData["TotalClicks"] = impressions.Count(i => i.WasClicked);
            ViewData["CTR"] = impressions.Count > 0 ? Math.Round((double)impressions.Count(i => i.WasClicked) / impressions.Count * 100, 2) : 0;

            return View(impressions);
        }
    }
}
