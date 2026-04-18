using DelikatessenDrehbuch.Areas.WorldMiniApp.Extensions;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    public class HomeController : WorldMiniAppBaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly IWorldClipWatchService _worldClipWatchService;
        private readonly IWorldAdPreferenceService _worldAdPreferenceService;
        private readonly IRecipeAiTransformService _recipeAiTransformService;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            ApplicationDbContext context,
            IWorldClipWatchService worldClipWatchService,
            IWorldAdPreferenceService worldAdPreferenceService,
            IRecipeAiTransformService recipeAiTransformService,
            ILogger<HomeController> logger)
        {
            _context = context;
            _worldClipWatchService = worldClipWatchService;
            _worldAdPreferenceService = worldAdPreferenceService;
            _recipeAiTransformService = recipeAiTransformService;
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
        public async Task<IActionResult> PreviewAiRecipeVariant([FromBody] RecipeAiTransformRequest request, CancellationToken cancellationToken)
        {
            if (request == null || request.RecipeId <= 0)
            {
                return BadRequest(new { message = "Rezeptdaten fehlen." });
            }

            if (request.AppliedChangeCount > 2)
            {
                return BadRequest(new { message = "Maximal zwei AI-Änderungen sind erlaubt." });
            }

            var recipe = await _context.RecipeBaseData
                .AsNoTracking()
                .IncludeFullRecipeDetails()
                .FirstOrDefaultAsync(r => r.Id == request.RecipeId, cancellationToken);

            if (recipe == null)
            {
                return NotFound(new { message = "Rezept wurde nicht gefunden." });
            }

            try
            {
                var language = (Request.Cookies["deli-lang"] ?? "de").ToLowerInvariant();
                var resolvedUserHash = ResolveUserHash(string.Empty);
                var cachedVariant = await TryGetCachedAiVariantAsync(
                    request.RecipeId,
                    request.VariantType,
                    language,
                    request.AppliedChangeCount,
                    request.UserNote,
                    resolvedUserHash,
                    cancellationToken);

                if (cachedVariant != null)
                {
                    return Json(cachedVariant);
                }

                var preview = await _recipeAiTransformService.BuildPreviewAsync(
                    recipe,
                    request.VariantType,
                    language,
                    request.UserNote,
                    request.AppliedChangeCount,
                    cancellationToken);

                return Json(preview);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "AI recipe preview failed for RecipeId={RecipeId}", request.RecipeId);
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAiRecipeVariant([FromBody] SaveAiRecipeVariantRequest request, CancellationToken cancellationToken)
        {
            if (request?.Preview == null || request.BaseRecipeId <= 0)
            {
                return BadRequest(new { message = "AI-Vorschau fehlt." });
            }

            var recipeExists = await _context.RecipeBaseData
                .AsNoTracking()
                .AnyAsync(r => r.Id == request.BaseRecipeId, cancellationToken);

            if (!recipeExists)
            {
                return NotFound(new { message = "Basisrezept wurde nicht gefunden." });
            }

            var preview = request.Preview;
            preview.StepPlan ??= new List<RecipeAiTransformStepPlanItem>();
            preview.Steps ??= new List<RecipeAiTransformStepPreview>();
            preview.Ingredients ??= new List<RecipeAiTransformIngredientPreview>();
            preview.Highlights ??= new List<string>();

            var userHash = ResolveUserHash(string.Empty);
            var language = (Request.Cookies["deli-lang"] ?? "de").ToLowerInvariant();
            var appliedChangeCount = Math.Max(0, 2 - preview.RemainingChanges);
            var isSharedCanonical = appliedChangeCount <= 0 && !preview.UserNoteApplied;

            RecipeAiVariant entity;
            if (isSharedCanonical)
            {
                entity = await _context.RecipeAiVariants
                    .FirstOrDefaultAsync(x =>
                        x.BaseRecipeId == request.BaseRecipeId &&
                        x.VariantType == preview.VariantType &&
                        x.Language == language &&
                        x.IsSharedCanonical,
                        cancellationToken)
                    ?? new RecipeAiVariant
                    {
                        BaseRecipeId = request.BaseRecipeId,
                        VariantType = preview.VariantType,
                        Language = language,
                        IsSharedCanonical = true,
                        CreatedAtUtc = DateTime.UtcNow
                    };
            }
            else
            {
                entity = new RecipeAiVariant
                {
                    BaseRecipeId = request.BaseRecipeId,
                    VariantType = preview.VariantType,
                    Language = language,
                    IsSharedCanonical = false,
                    CreatedAtUtc = DateTime.UtcNow
                };
            }

            entity.CreatedByUserHash = string.IsNullOrWhiteSpace(userHash) ? null : userHash;
            entity.AppliedChangeCount = appliedChangeCount;
            entity.LatestUserNote = preview.UserNoteApplied
                ? string.IsNullOrWhiteSpace(request.LatestUserNote)
                    ? null
                    : request.LatestUserNote.Trim()
                : null;
            entity.RenderedTitle = preview.Title ?? string.Empty;
            entity.RenderedSummary = preview.Summary ?? string.Empty;
            entity.StepPlanJson = JsonSerializer.Serialize(preview.StepPlan);
            entity.RenderedStepsJson = JsonSerializer.Serialize(preview.Steps);
            entity.RenderedIngredientsJson = JsonSerializer.Serialize(preview.Ingredients);
            entity.RenderedHighlightsJson = JsonSerializer.Serialize(preview.Highlights);
            entity.UpdatedAtUtc = DateTime.UtcNow;

            if (entity.Id == 0)
            {
                await _context.RecipeAiVariants.AddAsync(entity, cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);

            return Json(new
            {
                success = true,
                variantId = entity.Id,
                isSharedCanonical = entity.IsSharedCanonical
            });
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

        private async Task<RecipeAiTransformPreview?> TryGetCachedAiVariantAsync(
            int baseRecipeId,
            string variantType,
            string language,
            int appliedChangeCount,
            string? userNote,
            string? userHash,
            CancellationToken cancellationToken)
        {
            if (appliedChangeCount > 0 || !string.IsNullOrWhiteSpace(userNote))
            {
                return null;
            }

            var normalizedVariantType = (variantType ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedVariantType))
            {
                return null;
            }

            RecipeAiVariant? cached = null;
            if (!string.IsNullOrWhiteSpace(userHash))
            {
                cached = await _context.RecipeAiVariants
                    .AsNoTracking()
                    .Where(x =>
                        x.BaseRecipeId == baseRecipeId &&
                        x.VariantType == normalizedVariantType &&
                        x.Language == language &&
                        x.CreatedByUserHash == userHash &&
                        !x.IsSharedCanonical)
                    .OrderByDescending(x => x.UpdatedAtUtc)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            cached ??= await _context.RecipeAiVariants
                .AsNoTracking()
                .Where(x =>
                    x.BaseRecipeId == baseRecipeId &&
                    x.VariantType == normalizedVariantType &&
                    x.Language == language &&
                    x.IsSharedCanonical)
                .OrderByDescending(x => x.UpdatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            return cached == null ? null : MapAiVariantToPreview(cached);
        }

        private static RecipeAiTransformPreview MapAiVariantToPreview(RecipeAiVariant variant)
        {
            return new RecipeAiTransformPreview
            {
                VariantType = variant.VariantType,
                VariantLabel = GetVariantLabel(variant.VariantType),
                Title = variant.RenderedTitle,
                Summary = variant.RenderedSummary,
                UsedFallback = false,
                UserNoteApplied = variant.AppliedChangeCount > 0,
                RemainingChanges = Math.Max(0, 2 - variant.AppliedChangeCount),
                StepPlan = DeserializeJson<List<RecipeAiTransformStepPlanItem>>(variant.StepPlanJson) ?? new List<RecipeAiTransformStepPlanItem>(),
                Steps = DeserializeJson<List<RecipeAiTransformStepPreview>>(variant.RenderedStepsJson) ?? new List<RecipeAiTransformStepPreview>(),
                Ingredients = DeserializeJson<List<RecipeAiTransformIngredientPreview>>(variant.RenderedIngredientsJson) ?? new List<RecipeAiTransformIngredientPreview>(),
                Highlights = DeserializeJson<List<string>>(variant.RenderedHighlightsJson) ?? new List<string>()
            };
        }

        private static T? DeserializeJson<T>(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return default;
            }

            try
            {
                return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                return default;
            }
        }

        private static string GetVariantLabel(string? variantType)
        {
            return (variantType ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "vegan" => "Vegan",
                "mealprep" => "Meal Prep",
                "lowcarb" => "Low Carb",
                "highprotein" => "Mehr Protein",
                _ => "AI Vorschau"
            };
        }
        [HttpGet]
        public async Task<IActionResult> SharedShoppingList(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return NotFound();

            var list = await _context.WorldSharedShoppingList.FirstOrDefaultAsync(x => x.ShareToken == token);
            if (list == null)
                return NotFound();

            ViewData["Token"] = list.ShareToken;
            ViewData["Items"] = list.ItemsJson;
            ViewData["Checked"] = list.CheckedJson;

            return View("~/Areas/WorldMiniApp/Views/Home/SharedShoppingList.cshtml");
        }
    }
}
