using DelikatessenDrehbuch.Areas.WorldMiniApp.Extensions;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    public class HomeController : WorldMiniAppBaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly IWorldClipWatchService _worldClipWatchService;
        private readonly IWorldAdPreferenceService _worldAdPreferenceService;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            ApplicationDbContext context,
            IWorldClipWatchService worldClipWatchService,
            IWorldAdPreferenceService worldAdPreferenceService,
            ILogger<HomeController> logger)
        {
            _context = context;
            _worldClipWatchService = worldClipWatchService;
            _worldAdPreferenceService = worldAdPreferenceService;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> UserDashboard()
        {
            var userHash = WorldMiniApp.Services.WorldMiniAppUserHashHelper.Resolve(HttpContext);

            if (string.IsNullOrWhiteSpace(userHash))
            {
                return View(new UserDashboardViewModel
                {
                    IsLoggedInWithWorldMiniApp = false
                });
            }

            var sales = await _context.MealPlanPurchases
                .Include(x => x.Listing)
                .Where(x => x.SellerHash == userHash)
                .OrderByDescending(x => x.PurchasedAt)
                .Take(100)
                .ToListAsync();

            var transactionHistory = await _context.WildCoinTransactions
                .Where(x => x.UserHash == userHash)
                .OrderByDescending(x => x.CreatedAt)
                .Take(100)
                .ToListAsync();

            var openWldAmount = sales
                .Where(x => (x.PaymentToken ?? "WLD").Equals("WLD", StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.CreatorAmount);

            var openUsdcAmount = sales
                .Where(x => (x.PaymentToken ?? "WLD").Equals("USDC", StringComparison.OrdinalIgnoreCase)
                         || (x.PaymentToken ?? "WLD").Equals("USDCE", StringComparison.OrdinalIgnoreCase)
                         || (x.PaymentToken ?? "WLD").Equals("USDT", StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.CreatorAmount);

            var watchAnalytics = await _worldClipWatchService.BuildDashboardAnalyticsAsync(userHash);
            var preferredLanguage = Request.Cookies["deli-lang"] ?? "de";
            var adPreferences = await _worldAdPreferenceService.GetDashboardProfileAsync(userHash, preferredLanguage);

            var model = new UserDashboardViewModel
            {
                IsLoggedInWithWorldMiniApp = true,
                UserHash = userHash,
                SoldMealPlanCount = sales.Count,
                TotalCreatorRevenue = sales.Sum(x => x.CreatorAmount),
                OpenWldAmount = openWldAmount,
                OpenUsdcAmount = openUsdcAmount,
                WatchAnalytics = watchAnalytics,
                AdPreferences = adPreferences,
                SalesHistory = sales,
                WildCoinHistory = transactionHistory
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GetRecipePreview(int id)
        {
            var recipe = await _context.RecipeBaseData
                .AsNoTracking()
                .Include(r => r.Ingredients)
                    .ThenInclude(ri => ri.Ingredient)
                        .ThenInclude(i => i.IngredientsAndNutrients)
                .Include(r => r.Ingredients)
                    .ThenInclude(ri => ri.Ingredient)
                        .ThenInclude(i => i.Quantity)
                .Include(r => r.Ingredients)
                    .ThenInclude(ri => ri.Ingredient)
                        .ThenInclude(i => i.Measure)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (recipe == null) return NotFound();

            var ingredients = recipe.Ingredients?
                .Select(ri => new
                {
                    name = ri.Ingredient?.IngredientsAndNutrients?.Name_DE ?? "",
                    quantity = ri.Ingredient?.Quantity?.Quantitys,
                    unit = ri.Ingredient?.Measure?.Metrics_DE ?? ""
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.name))
                .ToList() ?? new();

            return Json(new
            {
                title = recipe.Title,
                category = recipe.Category,
                prepTime = recipe.PreparationTime,
                personCount = recipe.PersonCount,
                ingredients
            });
        }

        public async Task<IActionResult> ShowRecipe(int id)
        {
            var model = await _context.RecipeBaseData
                .IncludeFullRecipeDetails()
                .FirstOrDefaultAsync(r => r.Id == id);

            if (model == null)
            {
                return NotFound();
            }

            var userHash = ResolveUserHash(string.Empty);
            var isSuperUser = userHash == SuperUserHash;
            ViewData["CanPublishToFeed"] = isSuperUser;

            var posting = await _context.WorldUserPosting
                .Where(x => x.Recipe != null && x.Recipe.Id == id)
                .Select(x => new { x.Id, x.ThumbnailUrl, x.Source })
                .FirstOrDefaultAsync();

            if (posting != null)
            {
                ViewData["PostingThumbnailUrl"] = posting.ThumbnailUrl ?? posting.Source;
                if (isSuperUser)
                {
                    ViewData["ExistingPostingId"] = (int?)posting.Id;
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PublishRecipeBaseDataToFeed(int recipeBaseDataId, string userHash)
        {
            userHash = ResolveUserHash(userHash);
            if (userHash != SuperUserHash)
            {
                return Forbid();
            }

            var recipe = await _context.RecipeBaseData
                .Include(r => r.Images)
                .FirstOrDefaultAsync(r => r.Id == recipeBaseDataId);
            if (recipe == null)
            {
                return NotFound();
            }

            var existingPosting = await _context.WorldUserPosting
                .Include(x => x.Recipe)
                .FirstOrDefaultAsync(x => x.Recipe != null && x.Recipe.Id == recipeBaseDataId);

            if (existingPosting != null)
            {
                return RedirectToAction("EditRecipe", "Recipe", new { area = "WorldMiniApp", postingId = existingPosting.Id, userHash });
            }

            var sourceImage = recipe.Images?.FirstOrDefault()?.Image
                ?? "https://cdn.pixabay.com/photo/2014/12/21/23/28/recipe-575434_640.png";

            var posting = new WorldUserPosting
            {
                CreatorId = userHash,
                CreatorName = "Avocado",
                Title = recipe.Title,
                Recipe = recipe,
                Source = sourceImage,
                ThumbnailUrl = sourceImage,
                CreationTime = DateTime.Now
            };

            await _context.WorldUserPosting.AddAsync(posting);
            await _context.SaveChangesAsync();

            return RedirectToAction("EditRecipe", "Recipe", new { area = "WorldMiniApp", postingId = posting.Id, userHash });
        }
    }
}
