using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    [Area("WorldMiniApp")]
    public class AdPreferencesController : Controller
    {
                private readonly IWorldAdPreferenceService _worldAdPreferenceService;

        public AdPreferencesController(IWorldAdPreferenceService worldAdPreferenceService)
        {
            _worldAdPreferenceService = worldAdPreferenceService;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSettings(UpdateAdPreferenceSettingsRequest request)
        {
            var userHash = ResolveUserHash();
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return RedirectToAction("UserDashboard", "Home", new { area = "WorldMiniApp" });
            }

            var language = Request.Cookies["deli-lang"] ?? "de";
            await _worldAdPreferenceService.UpdateSettingsAsync(userHash, request, language);
            return RedirectToAction("UserDashboard", "Home", new { area = "WorldMiniApp" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RefreshProfile()
        {
            var userHash = ResolveUserHash();
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return RedirectToAction("UserDashboard", "Home", new { area = "WorldMiniApp" });
            }

            var language = Request.Cookies["deli-lang"] ?? "de";
            await _worldAdPreferenceService.RebuildProfileAsync(userHash, language);
            return RedirectToAction("UserDashboard", "Home", new { area = "WorldMiniApp" });
        }

        private string ResolveUserHash()
        {
            return WorldMiniAppUserHashHelper.Resolve(HttpContext);
        }
    }
}

