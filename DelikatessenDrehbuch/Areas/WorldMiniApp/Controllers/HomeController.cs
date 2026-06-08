using DelikatessenDrehbuch.Areas.WorldMiniApp.Extensions;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using DebugLog = DelikatessenDrehbuch.Areas.WorldMiniApp.Services.DebugLogger;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Newtonsoft.Json;
using System.Globalization;
using System.Resources;
using System.Text.Json;
using System.Threading;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    public class HomeController : WorldMiniAppBaseController
    {
        private const string WorldMiniAppId = "app_a8d8e00858f1e44ac3dcb9b2f6dfa1aa";
        private readonly ApplicationDbContext _context;
        private readonly IWorldClipWatchService _worldClipWatchService;
        private readonly IWorldAdPreferenceService _worldAdPreferenceService;
        private readonly IRecipeAiTransformService _recipeAiTransformService;
        private readonly IRecipeAiVariantJobService _recipeAiVariantJobService;
        private readonly IRecipeAiNutritionService _recipeAiNutritionService;
        private readonly RecipeTranslationService _recipeTranslationService;
        private readonly IStringLocalizer<SharedResources> _sharedLocalizer;
        private readonly ILogger<HomeController> _logger;

        private static readonly SemaphoreSlim EnsureNotificationsSchemaLock = new(1, 1);
        private static volatile bool NotificationsSchemaEnsured = false;

        // Azure-safe SQL (no GO). Creates the lightweight notifications table if missing.
        private const string EnsureWorldUserNotificationsSchemaSql = @"
IF OBJECT_ID(N'[dbo].[WorldUserNotifications]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[WorldUserNotifications](
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_WorldUserNotifications] PRIMARY KEY,
        [UserHash] NVARCHAR(256) NOT NULL,
        [Icon] NVARCHAR(64) NOT NULL CONSTRAINT [DF_WorldUserNotifications_Icon] DEFAULT (N'bi-bell'),
        [Sender] NVARCHAR(128) NOT NULL CONSTRAINT [DF_WorldUserNotifications_Sender] DEFAULT (N'system'),
        [Description] NVARCHAR(1000) NOT NULL,
        [Href] NVARCHAR(600) NULL,
        [NotificationKey] NVARCHAR(300) NULL,
        [EventType] NVARCHAR(64) NULL,
        [LatestActorName] NVARCHAR(128) NULL,
        [ContextText] NVARCHAR(400) NULL,
        [AggregateCount] INT NOT NULL CONSTRAINT [DF_WorldUserNotifications_AggregateCount] DEFAULT (1),
        [UnreadEventCount] INT NOT NULL CONSTRAINT [DF_WorldUserNotifications_UnreadEventCount] DEFAULT (1),
        [CreatedAtUtc] DATETIME2 NOT NULL CONSTRAINT [DF_WorldUserNotifications_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [IsSeen] BIT NOT NULL CONSTRAINT [DF_WorldUserNotifications_IsSeen] DEFAULT (0),
        [SeenAtUtc] DATETIME2 NULL
    );

    CREATE INDEX [IX_WorldUserNotifications_UserHash_IsSeen_CreatedAtUtc]
        ON [dbo].[WorldUserNotifications]([UserHash], [IsSeen], [CreatedAtUtc]);
END;

IF COL_LENGTH(N'[dbo].[WorldUserNotifications]', N'NotificationKey') IS NULL
BEGIN
    ALTER TABLE [dbo].[WorldUserNotifications]
        ADD [NotificationKey] NVARCHAR(300) NULL;
END;

IF COL_LENGTH(N'[dbo].[WorldUserNotifications]', N'EventType') IS NULL
BEGIN
    ALTER TABLE [dbo].[WorldUserNotifications]
        ADD [EventType] NVARCHAR(64) NULL;
END;

IF COL_LENGTH(N'[dbo].[WorldUserNotifications]', N'LatestActorName') IS NULL
BEGIN
    ALTER TABLE [dbo].[WorldUserNotifications]
        ADD [LatestActorName] NVARCHAR(128) NULL;
END;

IF COL_LENGTH(N'[dbo].[WorldUserNotifications]', N'ContextText') IS NULL
BEGIN
    ALTER TABLE [dbo].[WorldUserNotifications]
        ADD [ContextText] NVARCHAR(400) NULL;
END;

IF COL_LENGTH(N'[dbo].[WorldUserNotifications]', N'AggregateCount') IS NULL
BEGIN
    ALTER TABLE [dbo].[WorldUserNotifications]
        ADD [AggregateCount] INT NOT NULL CONSTRAINT [DF_WorldUserNotifications_AggregateCount_Legacy] DEFAULT (1);
END;

IF COL_LENGTH(N'[dbo].[WorldUserNotifications]', N'UnreadEventCount') IS NULL
BEGIN
    ALTER TABLE [dbo].[WorldUserNotifications]
        ADD [UnreadEventCount] INT NOT NULL CONSTRAINT [DF_WorldUserNotifications_UnreadEventCount_Legacy] DEFAULT (1);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_WorldUserNotifications_UserHash_NotificationKey'
      AND object_id = OBJECT_ID(N'[dbo].[WorldUserNotifications]')
)
BEGIN
    CREATE INDEX [IX_WorldUserNotifications_UserHash_NotificationKey]
        ON [dbo].[WorldUserNotifications]([UserHash], [NotificationKey]);
END;
";

        public HomeController(
            ApplicationDbContext context,
            IWorldClipWatchService worldClipWatchService,
            IWorldAdPreferenceService worldAdPreferenceService,
            IRecipeAiTransformService recipeAiTransformService,
            IRecipeAiVariantJobService recipeAiVariantJobService,
            IRecipeAiNutritionService recipeAiNutritionService,
            RecipeTranslationService recipeTranslationService,
            IStringLocalizer<SharedResources> sharedLocalizer,
            ILogger<HomeController> logger)
        {
            _context = context;
            _worldClipWatchService = worldClipWatchService;
            _worldAdPreferenceService = worldAdPreferenceService;
            _recipeAiTransformService = recipeAiTransformService;
            _recipeAiVariantJobService = recipeAiVariantJobService;
            _recipeAiNutritionService = recipeAiNutritionService;
            _recipeTranslationService = recipeTranslationService;
            _sharedLocalizer = sharedLocalizer;
            _logger = logger;
        }

        [HttpGet]
        [Route("/WorldMiniApp")]
        [Route("/WorldMiniApp/Home")]
        [Route("/WorldMiniApp/Home/Index")]
        [Route("/WorldMiniApp/Home/Index/{*extraPath}")]
        public IActionResult Index(string? extraPath = null, string? returnTo = null, string? path = null)
        {
            DebugLog.Log($"=== HOME INDEX === extra:{extraPath ?? "null"} returnTo:{returnTo ?? "null"} path:{path ?? "null"}");
            _logger.LogWarning("=== HOME INDEX CALLED === extraPath: {ExtraPath}, returnTo: {ReturnTo}, path: {Path}",
                extraPath ?? "NULL", returnTo ?? "NULL", path ?? "NULL");

            // World Mini App passes the path as part of the URL, not as a query parameter
            // Example: /WorldMiniApp/Home/Index/WorldMiniApp/Shared/Invite?token=...
            if (!string.IsNullOrWhiteSpace(extraPath))
            {
                DebugLog.Log($"Index: extraPath detected, treating as deep link: {extraPath}");
                _logger.LogWarning("WorldMiniApp Index called with trailing path segment (deep link): {ExtraPath}", extraPath);

                // Prepend "/" to make it a valid path
                var deepLinkPath = "/" + extraPath;
                var queryString = Request.QueryString.ToString();

                // Append query string if present (e.g., ?token=...)
                if (!string.IsNullOrEmpty(queryString))
                {
                    deepLinkPath += queryString;
                }

                DebugLog.Log($"Index: constructed deep link: {deepLinkPath}");

                var userHash = ResolveUserHash(string.Empty);
                DebugLog.Log($"Index: userHash for deep link: {userHash ?? "null"}");

                if (!string.IsNullOrWhiteSpace(userHash))
                {
                    // User is logged in, redirect immediately
                    DebugLog.Log($"Index: REDIRECTING logged-in user to: {deepLinkPath}");
                    return Redirect(deepLinkPath);
                }
                else
                {
                    // User not logged in, set as post-login redirect
                    DebugLog.Log($"Index: setting as post-login redirect: {deepLinkPath}");
                    returnTo = deepLinkPath;
                }
            }

            // If a path parameter is provided (from World Mini App deep link), use it for redirect
            if (!string.IsNullOrWhiteSpace(path))
            {
                DebugLog.Log($"Index: path detected: {path}");
                _logger.LogWarning("Path parameter detected: {Path}", path);
                var normalizedPath = NormalizeMiniAppReturnUrl(path);
                DebugLog.Log($"Index: normalized: {normalizedPath ?? "null"}");
                _logger.LogWarning("Normalized path: {NormalizedPath}", normalizedPath ?? "NULL");

                if (!string.IsNullOrWhiteSpace(normalizedPath))
                {
                    var userHash = ResolveUserHash(string.Empty);
                    DebugLog.Log($"Index: userHash: {userHash ?? "null"}");
                    _logger.LogWarning("UserHash for redirect: {UserHash}", userHash ?? "NULL");

                    if (!string.IsNullOrWhiteSpace(userHash))
                    {
                        // User is already logged in, redirect immediately
                        DebugLog.Log($"Index: REDIRECTING to {normalizedPath}");
                        _logger.LogWarning("Redirecting logged-in user to deep link: {Path}", normalizedPath);
                        return Redirect(normalizedPath);
                    }
                    else
                    {
                        // User not logged in, set as post-login redirect
                        DebugLog.Log($"Index: setting post-login redirect: {normalizedPath}");
                        _logger.LogWarning("Setting post-login redirect for deep link: {Path}", normalizedPath);
                        returnTo = path;
                    }
                }
                else
                {
                    DebugLog.Log($"Index: normalization FAILED for: {path}");
                    _logger.LogWarning("Path normalization failed for: {Path}", path);
                }
            }

            var finalRedirectUrl = NormalizeMiniAppReturnUrl(returnTo);
            _logger.LogWarning("Final PostLoginRedirectUrl: {Url}", finalRedirectUrl ?? "NULL");
            ViewData["PostLoginRedirectUrl"] = finalRedirectUrl;

            // ========== LOCALIZATION DEBUG LOGGING ==========
            var currentCulture = System.Globalization.CultureInfo.CurrentCulture;
            var currentUICulture = System.Globalization.CultureInfo.CurrentUICulture;

            _logger.LogWarning("=== LOCALIZATION DEBUG ===");
            _logger.LogWarning($"CurrentCulture: {currentCulture.Name}");
            _logger.LogWarning($"CurrentUICulture: {currentUICulture.Name}");

            // Test SharedLocalizer
            var testKey = "Home.WeeklyPlan";
            var testValue = _sharedLocalizer[testKey];
            _logger.LogWarning($"SharedLocalizer[\"{testKey}\"] = \"{testValue}\"");
            _logger.LogWarning($"ResourceNotFound: {testValue.ResourceNotFound}");

            // Check if resource manager exists
            try
            {
                var resourceManagerProperty = typeof(SharedResources)
                    .GetProperty("ResourceManager", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

                if (resourceManagerProperty != null)
                {
                    var resourceManager = resourceManagerProperty.GetValue(null) as System.Resources.ResourceManager;
                    if (resourceManager != null)
                    {
                        _logger.LogWarning($"ResourceManager found: {resourceManager.BaseName}");

                        // Try to get the value directly from ResourceManager
                        var directValue = resourceManager.GetString(testKey, currentUICulture);
                        _logger.LogWarning($"ResourceManager.GetString(\"{testKey}\") = \"{directValue}\"");

                        // Try getting the ResourceSet
                        var resourceSet = resourceManager.GetResourceSet(currentUICulture, true, false);
                        if (resourceSet != null)
                        {
                            _logger.LogWarning("ResourceSet found! Keys:");
                            var keys = new List<string>();
                            foreach (System.Collections.DictionaryEntry entry in resourceSet)
                            {
                                keys.Add(entry.Key?.ToString() ?? "null");
                            }
                            _logger.LogWarning($"Total keys: {keys.Count}");
                            _logger.LogWarning($"First 10 keys: {string.Join(", ", keys.Take(10))}");
                        }
                        else
                        {
                            _logger.LogWarning("ResourceSet is NULL!");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("ResourceManager is NULL!");
                    }
                }
                else
                {
                    _logger.LogWarning("ResourceManager property not found!");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking ResourceManager");
            }

            _logger.LogWarning("=== END DEBUG ===");
            // ================================================

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
                SalesHistory = sales
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

            // Sprache aus Cookie lesen
            var language = (Request.Cookies["deli-lang"] ?? "de").ToLowerInvariant();
            var langSuffix = language switch
            {
                "en" => "EN",
                "esp" => "ESP",   // Cookie verwendet "esp" statt "es"
                "prt" => "PRT",   // Cookie verwendet "prt" statt "pt"
                "id" => "ID",
                "nl" => "NL",
                "sv" => "SE",     // DB verwendet SE, Cookie sv
                "da" => "DK",     // DB verwendet DK, Cookie da
                "no" => "NO",     // Cookie verwendet "no" für Norwegisch
                "ms" => "MS",
                _ => "DE"
            };

            var ingredientLinks = recipe.Ingredients?
                .Where(ri => ri.Ingredient?.IngredientsAndNutrients != null)
                .ToList() ?? new();

            var ingredients = ingredientLinks
                .Select(ri =>
                {
                    var ing = ri.Ingredient?.IngredientsAndNutrients;
                    var measure = ri.Ingredient?.Measure;

                    // Zutatennamen in gewählter Sprache
                    var name = langSuffix switch
                    {
                        "EN" => ing?.Name_EN,
                        "ESP" => ing?.Name_ESP,
                        "PRT" => ing?.Name_PRT,
                        "ID" => ing?.Name_ID,
                        "NL" => ing?.Name_NL,
                        "SE" => ing?.Name_SE,     // Schwedisch
                        "DK" => ing?.Name_DK,     // Dänisch
                        "NO" => ing?.Name_NO,     // Norwegisch
                        "MS" => ing?.Name_MS,
                        _ => ing?.Name_DE
                    } ?? "";

                    // Maßeinheiten in gewählter Sprache
                    var unit = langSuffix switch
                    {
                        "EN" => measure?.Metrics_EN,
                        "ESP" => measure?.Metrics_ESP,
                        "PRT" => measure?.Metrics_PRT,
                        "ID" => measure?.Metrics_ID,
                        "NL" => measure?.Metrics_NL,
                        "SE" => measure?.Metrics_SE,     // Schwedisch
                        "DK" => measure?.Metrics_DK,     // Dänisch
                        "NO" => measure?.Metrics_NO,     // Norwegisch
                        "MS" => measure?.Metrics_MS,
                        _ => measure?.Metrics_DE
                    } ?? "";

                    return new
                    {
                        name,
                        quantity = ri.Ingredient?.Quantity?.Quantitys,
                        unit
                    };
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.name))
                .ToList();

            double totalProtein = 0, totalFat = 0, totalCarbs = 0, totalCalories = 0;
            foreach (var ri in ingredientLinks)
            {
                var n = ri.Ingredient!.IngredientsAndNutrients!;
                var qty = ri.Ingredient.Quantity?.Quantitys ?? 100;
                var factor = qty / 100.0;
                totalProtein += (double)n.Protein_a_100g * factor;
                totalFat += (double)n.Fat_a_100g * factor;
                totalCarbs += (double)n.Carbohydrates_a_100g * factor;
                totalCalories += n.Calories_a_100g * factor;
            }

            return Json(new
            {
                title = recipe.Title,
                category = recipe.Category,
                prepTime = recipe.PreparationTime,
                personCount = recipe.PersonCount,
                ingredients,
                protein = Math.Round(totalProtein, 1),
                fat = Math.Round(totalFat, 1),
                carbs = Math.Round(totalCarbs, 1),
                calories = Math.Round(totalCalories, 0)
            });
        }

        [HttpGet]
        [Route("/WorldMiniApp/Home/SharedRecipe")]
        public async Task<IActionResult> ShowRecipe(int id, int? aiVariantId = null, int? swapVariantId = null, int? communityVariantId = null, bool original = false, string share = null, string direct = null)
        {
            var model = await _context.RecipeBaseData
                .IncludeFullRecipeDetails()
                .FirstOrDefaultAsync(r => r.Id == id);

            if (model == null)
            {
                return NotFound();
            }

            // Wenn als Share-Link aufgerufen (share=1) und NICHT mit direct
            // UND NICHT bereits in World App → Landing Page
            var isWorldApp = IsWorldAppRequest();
            if (!string.IsNullOrEmpty(share) && string.IsNullOrEmpty(direct) && !isWorldApp)
            {
                ViewData["ShareType"] = "recipe";
                ViewData["Token"] = id.ToString();
                ViewData["Title"] = model.Title ?? "Rezept";
                ViewData["Description"] = $"Öffne dieses Rezept in der World App: {model.Title}";
                var queryParams = $"id={id}&direct=true";
                if (aiVariantId.HasValue) queryParams += $"&aiVariantId={aiVariantId}";
                if (swapVariantId.HasValue) queryParams += $"&swapVariantId={swapVariantId}";
                if (communityVariantId.HasValue) queryParams += $"&communityVariantId={communityVariantId}";
                if (original) queryParams += "&original=true";
                ViewData["TargetPath"] = $"/WorldMiniApp/Home/SharedRecipe?{queryParams}";
                return View("~/Areas/WorldMiniApp/Views/Home/SharedLinkLanding.cshtml");
            }

            // Wenn in World App oder mit direct=true → direkt zur View
            // (kein share Parameter oder bereits in App)

            var userHash = ResolveUserHash(string.Empty);
            var isSuperUser = userHash == SuperUserHash;
            ViewData["CanPublishToFeed"] = isSuperUser;
            var language = (Request.Cookies["deli-lang"] ?? "de").ToLowerInvariant();

            var posting = await _context.WorldUserPosting
                .Where(x => x.Recipe != null && x.Recipe.Id == id)
                .Select(x => new { x.Id, x.ThumbnailUrl, x.Source, x.CreatorName })
                .FirstOrDefaultAsync();

            if (posting != null)
            {
                ViewData["PostingThumbnailUrl"] = posting.ThumbnailUrl ?? posting.Source;
                ViewData["PostingCreatorName"] = posting.CreatorName;
                ViewData["PostingIdForShare"] = posting.Id;
                if (isSuperUser)
                {
                    ViewData["ExistingPostingId"] = (int?)posting.Id;
                }
            }

            // Load swaps: only when explicitly requested via query params.
            RecipeCommunityVariant communityVariant = null;
            RecipeUserVariant userVariant = null;

            if (communityVariantId.HasValue)
            {
                communityVariant = await _context.RecipeCommunityVariants
                    .FirstOrDefaultAsync(v => v.Id == communityVariantId.Value && v.OriginalRecipeId == id);
            }
            else if (swapVariantId.HasValue)
            {
                // Explicit variant requested
                userVariant = await _context.RecipeUserVariants
                    .FirstOrDefaultAsync(v => v.Id == swapVariantId.Value && v.OriginalRecipeId == id);
            }

            if (communityVariant != null)
            {
                ViewData["SwapsJson"] = communityVariant.SwapsJson;
                ViewData["VariantTitle"] = communityVariant.Title;
                ViewData["VariantSummary"] = communityVariant.Summary;
                ViewData["CommunityVariantId"] = communityVariant.Id;
                ViewData["IsCommunityVariant"] = true;
            }
            else if (userVariant != null)
            {
                ViewData["SwapsJson"] = userVariant.SwapsJson;
                ViewData["VariantTitle"] = userVariant.Title;
                // userVariant currently doesn't persist a summary, so keep it empty.
                ViewData["VariantSummary"] = null;
                ViewData["SwapVariantId"] = userVariant.Id;
                ViewData["IsCommunityVariant"] = false;
            }

            // Nutrition override for swap-variants: recompute macros from swapped ingredient IDs + quantities.
            if (ViewData["SwapsJson"] is string swapsJson && !string.IsNullOrWhiteSpace(swapsJson))
            {
                try
                {
                    var swaps = System.Text.Json.JsonSerializer.Deserialize<List<IngredientSwap>>(swapsJson)
                                ?? new List<IngredientSwap>();

                    if (swaps.Count > 0)
                    {
                        var swapByFrom = swaps
                            .Where(s => s.FromIngredientId > 0 && s.ToIngredientId > 0)
                            .GroupBy(s => s.FromIngredientId)
                            .ToDictionary(g => g.Key, g => g.Last());

                        var replacementIds = swapByFrom.Values
                            .Select(x => x.ToIngredientId)
                            .Distinct()
                            .ToList();

                        var replacements = await _context.IngredientsAndNutrients
                            .AsNoTracking()
                            .Where(i => replacementIds.Contains(i.Id))
                            .ToDictionaryAsync(i => i.Id);

                        decimal totalCalories = 0, totalProtein = 0, totalFat = 0, totalCarbs = 0, totalSugar = 0, totalFiber = 0;

                        foreach (var item in model.Ingredients)
                        {
                            var originalNutri = item.Ingredient?.IngredientsAndNutrients;
                            if (originalNutri == null) continue;

                            var ingredientId = originalNutri.Id;
                            var quantity = (decimal)(item.Ingredient?.Quantity?.Quantitys ?? 0);
                            var unit = (item.Ingredient?.Measure?.Metrics_DE ?? string.Empty).Trim().ToLowerInvariant();

                            // If swapped: use replacement ingredient + new quantity/unit.
                            if (swapByFrom.TryGetValue(ingredientId, out var swap) && replacements.TryGetValue(swap.ToIngredientId, out var replacementNutri))
                            {
                                quantity = swap.NewQuantity;
                                unit = (swap.Unit ?? "g").Trim().ToLowerInvariant();
                                // we treat ml like grams here (density not modeled) – consistent with your swap system prompt.
                                var factor = quantity / 100m;

                                totalCalories += replacementNutri.Calories_a_100g * factor;
                                totalProtein += replacementNutri.Protein_a_100g * factor;
                                totalFat += replacementNutri.Fat_a_100g * factor;
                                totalCarbs += replacementNutri.Carbohydrates_a_100g * factor;
                                totalSugar += replacementNutri.Sugar_a_100g * factor;
                                totalFiber += replacementNutri.Fiber_a_100g * factor;
                                continue;
                            }

                            // Original ingredient
                            decimal origFactor = (unit == "stk" || unit == "stück")
                                ? (quantity * originalNutri.Weight_per_piece) / 100m
                                : quantity / 100m;

                            totalCalories += originalNutri.Calories_a_100g * origFactor;
                            totalProtein += originalNutri.Protein_a_100g * origFactor;
                            totalFat += originalNutri.Fat_a_100g * origFactor;
                            totalCarbs += originalNutri.Carbohydrates_a_100g * origFactor;
                            totalSugar += originalNutri.Sugar_a_100g * origFactor;
                            totalFiber += originalNutri.Fiber_a_100g * origFactor;
                        }

                        ViewData["NutritionOverride"] = new
                        {
                            calories = totalCalories,
                            protein = totalProtein,
                            fat = totalFat,
                            carbs = totalCarbs,
                            sugar = totalSugar,
                            fiber = totalFiber
                        };
                    }
                }
                catch
                {
                    // Ignore nutrition override if swaps JSON is invalid.
                }
            }

            var communityVariantCount = await _context.RecipeCommunityVariants
                .AsNoTracking()
                .CountAsync(v => v.OriginalRecipeId == id && v.Language == language);
            ViewData["CommunityVariantCount"] = communityVariantCount;

            // NEW SYSTEM: Steps sind schon geladen via Include, filtern nach Sprache
            var stepsForLanguage = model.Steps?.FirstOrDefault(s => s.Culture == language);
            if (stepsForLanguage != null && !string.IsNullOrWhiteSpace(stepsForLanguage.Text))
            {
                ViewData["TranslatedSteps"] = stepsForLanguage.Text;
            }

            return View(model);
        }

        private static string BuildWorldMiniAppShareUrl(string targetPath)
        {
            var normalizedPath = string.IsNullOrWhiteSpace(targetPath)
                ? "/"
                : targetPath.StartsWith("/", StringComparison.Ordinal) ? targetPath : "/" + targetPath;

            return $"https://world.org/mini-app?app_id={WorldMiniAppId}&path={Uri.EscapeDataString(normalizedPath)}";
        }

        private static string? NormalizeMiniAppReturnUrl(string? returnTo)
        {
            if (string.IsNullOrWhiteSpace(returnTo))
            {
                return null;
            }

            var trimmed = Uri.UnescapeDataString(returnTo.Trim());
            if (!trimmed.StartsWith("/", StringComparison.Ordinal))
            {
                return null;
            }

            if (trimmed.StartsWith("//", StringComparison.Ordinal) || !trimmed.StartsWith("/WorldMiniApp", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return trimmed;
        }

        private IActionResult RecipeAiEditingDisabled()
        {
            return StatusCode(StatusCodes.Status410Gone, new
            {
                message = "Die AI-Rezeptbearbeitung ist vorerst deaktiviert."
            });
        }

      
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartAiRecipeVariantJob([FromBody] RecipeAiTransformRequest request, CancellationToken cancellationToken)
        {
            return RecipeAiEditingDisabled();
        }

        [HttpGet]
        public async Task<IActionResult> GetAiRecipeVariantJobStatus(string jobId, CancellationToken cancellationToken)
        {
            return RecipeAiEditingDisabled();
        }

        [HttpGet]
        public async Task<IActionResult> GetAiRecipeVariantJobResult(string jobId, CancellationToken cancellationToken)
        {
            return RecipeAiEditingDisabled();
        }

        public sealed class WorldUserNotificationListItemDto
        {
            public int Id { get; set; }
            public string Icon { get; set; } = "bi-bell";
            public string Sender { get; set; } = "system";
            public string Description { get; set; } = string.Empty;
            public string? Href { get; set; }
            public DateTime CreatedAtUtc { get; set; }
            public bool IsSeen { get; set; }
            public DateTime? SeenAtUtc { get; set; }
            public string Kind { get; set; } = "info";
            public string Title { get; set; } = "Info";
            public int AggregateCount { get; set; } = 1;
            public int UnreadEventCount { get; set; } = 0;
        }

        public sealed class WorldUserNotificationIdRequest
        {
            public int Id { get; set; }
        }

        [HttpGet]
        public async Task<IActionResult> GetWorldUserNotifications(int take = 30, bool includeSeen = true, CancellationToken cancellationToken = default)
        {
            var userHash = ResolveUserHash(string.Empty);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return Json(new { items = Array.Empty<WorldUserNotificationListItemDto>() });
            }

            take = Math.Clamp(take, 1, 100);
            await EnsureWorldUserNotificationsSchemaAsync(cancellationToken);

            var query = _context.WorldUserNotifications.AsNoTracking().Where(x => x.UserHash == userHash);
            if (!includeSeen)
            {
                query = query.Where(x => !x.IsSeen);
            }

            var items = await query
                .OrderBy(x => x.IsSeen)
                .ThenByDescending(x => x.CreatedAtUtc)
                .Take(take)
                .Select(x => new WorldUserNotificationListItemDto
                {
                    Id = x.Id,
                    Icon = x.Icon,
                    Sender = x.Sender,
                    Description = x.Description,
                    Href = x.Href,
                    CreatedAtUtc = x.CreatedAtUtc,
                    IsSeen = x.IsSeen,
                    SeenAtUtc = x.SeenAtUtc,
                    AggregateCount = x.AggregateCount > 0 ? x.AggregateCount : 1,
                    UnreadEventCount = x.UnreadEventCount > 0 ? x.UnreadEventCount : (x.IsSeen ? 0 : 1),
                    Kind = x.Sender == "ai" ? (x.Description.Contains("fehlgeschlagen") ? "error" : "success") : "info",
                    Title = x.Sender == "ai" ? "AI" : (string.IsNullOrWhiteSpace(x.Sender) ? "Info" : x.Sender)
                })
                .ToListAsync(cancellationToken);

            return Json(new { items });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkWorldUserNotificationSeen([FromBody] WorldUserNotificationIdRequest request, CancellationToken cancellationToken)
        {
            var userHash = ResolveUserHash(string.Empty);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return Unauthorized(new { message = "Nicht eingeloggt." });
            }

            if (request == null || request.Id <= 0)
            {
                return BadRequest(new { message = "Id fehlt." });
            }

            await EnsureWorldUserNotificationsSchemaAsync(cancellationToken);

            var item = await _context.WorldUserNotifications.FirstOrDefaultAsync(x => x.Id == request.Id && x.UserHash == userHash, cancellationToken);
            if (item == null)
            {
                return NotFound(new { message = "Benachrichtigung nicht gefunden." });
            }

            if (!item.IsSeen)
            {
                item.IsSeen = true;
                item.SeenAtUtc = DateTime.UtcNow;
                item.UnreadEventCount = 0;
                await _context.SaveChangesAsync(cancellationToken);
            }

            return Json(new { ok = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllWorldUserNotificationsSeen(CancellationToken cancellationToken)
        {
            var userHash = ResolveUserHash(string.Empty);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return Unauthorized(new { message = "Nicht eingeloggt." });
            }

            await EnsureWorldUserNotificationsSchemaAsync(cancellationToken);

            var items = await _context.WorldUserNotifications
                .Where(x => x.UserHash == userHash && !x.IsSeen)
                .ToListAsync(cancellationToken);

            if (items.Count > 0)
            {
                var now = DateTime.UtcNow;
                foreach (var it in items)
                {
                    it.IsSeen = true;
                    it.SeenAtUtc = now;
                    it.UnreadEventCount = 0;
                }
                await _context.SaveChangesAsync(cancellationToken);
            }

            return Json(new { ok = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearWorldUserNotifications(CancellationToken cancellationToken)
        {
            var userHash = ResolveUserHash(string.Empty);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return Unauthorized(new { message = "Nicht eingeloggt." });
            }

            await EnsureWorldUserNotificationsSchemaAsync(cancellationToken);

            var items = await _context.WorldUserNotifications.Where(x => x.UserHash == userHash).ToListAsync(cancellationToken);
            if (items.Count > 0)
            {
                _context.WorldUserNotifications.RemoveRange(items);
                await _context.SaveChangesAsync(cancellationToken);
            }

            return Json(new { ok = true });
        }

        private async Task EnsureWorldUserNotificationsSchemaAsync(CancellationToken cancellationToken)
        {
            if (NotificationsSchemaEnsured) return;

            await EnsureNotificationsSchemaLock.WaitAsync(cancellationToken);
            try
            {
                if (NotificationsSchemaEnsured) return;
                await _context.Database.ExecuteSqlRawAsync(EnsureWorldUserNotificationsSchemaSql, cancellationToken);
                NotificationsSchemaEnsured = true;
            }
            finally
            {
                EnsureNotificationsSchemaLock.Release();
            }
        }

       
        private static int? ResolveMeasureIdFromLookup(string? rawMeasure, IReadOnlyDictionary<string, int> lookup)
        {
            var key = NormalizeMeasureLookupKey(rawMeasure);
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            return lookup.TryGetValue(key, out var id) ? id : null;
        }

        private static string NormalizeMeasureLookupKey(string? rawMeasure)
        {
            return (rawMeasure ?? string.Empty)
                .Trim()
                .ToLowerInvariant()
                .Replace(".", string.Empty)
                .Replace(" ", string.Empty);
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

     
        private static decimal TryConvertToGramsOrMillilitersOrPieces(
            decimal amount,
            string? measure,
            string? quantityText,
            IngredientsAndNutrients nutrientSource)
        {
            var unit = (measure ?? string.Empty).Trim().ToLowerInvariant();
            unit = unit.Replace(".", string.Empty);

            // If measure is missing, try to infer common units from the quantity text.
            var q = (quantityText ?? string.Empty).Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(unit))
            {
                if (q.Contains("kg"))
                {
                    unit = "kg";
                }
                else if (q.Contains("ml"))
                {
                    unit = "ml";
                }
                else if (q.Contains(" l") || q.EndsWith("l") || q.Contains("liter"))
                {
                    unit = "l";
                }
                else if (q.Contains(" g") || q.EndsWith("g"))
                {
                    unit = "g";
                }
                else if (q.Contains("stk") || q.Contains("stück") || q.Contains("stueck") || q.Contains("piece") || q.Contains("pcs"))
                {
                    unit = "stk";
                }
            }

            if (unit is "stk" or "stuck" or "stück" or "stueck" or "piece" or "pcs")
            {
                var weight = nutrientSource?.Weight_per_piece ?? 0m;
                if (weight <= 0m)
                {
                    return 0m;
                }

                return amount * weight;
            }

            return TryConvertToGramsOrMilliliters(amount, unit, quantityText);
        }

        private static decimal TryConvertToGramsOrMilliliters(decimal amount, string? measure, string? quantityText)
        {
            var unit = (measure ?? string.Empty).Trim().ToLowerInvariant();
            unit = unit.Replace(".", string.Empty);

            // If measure is missing, try to infer common units from the quantity text.
            var q = (quantityText ?? string.Empty).ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(unit))
            {
                if (q.Contains("kg"))
                {
                    unit = "kg";
                }
                else if (q.Contains(" g") || q.EndsWith("g"))
                {
                    unit = "g";
                }
                else if (q.Contains("ml"))
                {
                    unit = "ml";
                }
                else if (q.Contains(" l") || q.EndsWith("l") || q.Contains("liter"))
                {
                    unit = "l";
                }
            }

            return unit switch
            {
                "g" or "gram" => amount,
                "kg" or "kilogramm" => amount * 1000m,
                "ml" or "milliliter" => amount,
                "l" or "liter" => amount * 1000m,
                _ => 0m
            };
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
        private bool IsWorldAppRequest()
        {
            var userAgent = Request.Headers["User-Agent"].ToString();
            return userAgent.Contains("WorldApp", StringComparison.OrdinalIgnoreCase) ||
                   userAgent.Contains("MiniKit", StringComparison.OrdinalIgnoreCase) ||
                   userAgent.Contains("Worldcoin", StringComparison.OrdinalIgnoreCase);
        }

        // SharedShoppingList moved to MealPlanController for consistency

        /// <summary>
        /// Returns all localized strings from SharedResources as JSON for client-side usage.
        /// Used by worldminiapp-i18n.js for client-side localization.
        /// </summary>
        [HttpGet]
        public IActionResult GetLocalizedStrings(string? culture)
        {
            var strings = new Dictionary<string, string>();

            try
            {
                // Get the resource manager via reflection
                var resourceManagerProperty = typeof(SharedResources)
                    .GetProperty("ResourceManager", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

                if (resourceManagerProperty != null)
                {
                    var resourceManager = resourceManagerProperty.GetValue(null) as ResourceManager;

                    if (resourceManager != null)
                    {
                        var targetCulture = string.IsNullOrWhiteSpace(culture)
                            ? CultureInfo.CurrentUICulture
                            : CultureInfo.GetCultureInfo(culture);

                        var resourceSet = resourceManager.GetResourceSet(targetCulture, true, false);

                        if (resourceSet != null)
                        {
                            foreach (System.Collections.DictionaryEntry entry in resourceSet)
                            {
                                if (entry.Key != null && entry.Value != null)
                                {
                                    strings[entry.Key.ToString()!] = entry.Value.ToString()!;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load localized strings for culture: {Culture}", culture);
                // Return empty dictionary on error
            }

            return Json(strings);
        }

        /// <summary>
        /// Neues System: Speichert Rezept und übersetzt Title + Steps in alle 10 Sprachen
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveRecipeWithTranslation([FromBody] SaveRecipeRequest request, CancellationToken cancellationToken)
        {
            try
            {
                // Validation
                if (string.IsNullOrWhiteSpace(request.Title))
                {
                    return BadRequest(new { success = false, message = "Titel fehlt" });
                }

                if (string.IsNullOrWhiteSpace(request.Steps))
                {
                    return BadRequest(new { success = false, message = "Zubereitungsschritte fehlen" });
                }

                if (request.Ingredients == null || !request.Ingredients.Any())
                {
                    return BadRequest(new { success = false, message = "Zutaten fehlen" });
                }

                _logger.LogInformation("💾 Saving recipe with AI translation: {Title}", request.Title);

                // 1. AI-Übersetzung (Titel + Steps in alle 10 Sprachen)
                var translation = await _recipeTranslationService.TranslateRecipeAsync(
                    request.Title,
                    request.Steps,
                    cancellationToken);

                if (!translation.Success)
                {
                    _logger.LogError("AI translation failed: {Error}", translation.ErrorMessage);
                    return StatusCode(500, new { success = false, message = "Übersetzung fehlgeschlagen: " + translation.ErrorMessage });
                }

                // 2. Datenbank-Transaktion starten
                using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

                // 3. RecipeBaseData erstellen (nur Basis-Daten, Steps kommen in eigene Tabelle)
                var recipe = new RecipeBaseData
                {
                    Title = request.Title,
                    Category = request.Category ?? "Hauptspeise",
                    PersonCount = request.PersonCount > 0 ? request.PersonCount : 2,
                    PreparationTime = request.PreparationTime > 0 ? request.PreparationTime : 30,
                    Preferences = request.Preferences ?? "",
                    LikeCount = 0
                };

                _context.RecipeBaseData.Add(recipe);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("✅ RecipeBaseData saved with Id: {RecipeId}", recipe.Id);

                // 4. RecipeSteps erstellen (10 Rows, eine pro Sprache)
                var cultures = new[] { "de", "en", "esp", "prt", "id", "nl", "sv", "da", "no", "ms" };
                var now = DateTime.UtcNow;

                foreach (var culture in cultures)
                {
                    var text = culture switch
                    {
                        "de" => translation.Steps.De,
                        "en" => translation.Steps.En,
                        "esp" => translation.Steps.Esp,
                        "prt" => translation.Steps.Prt,
                        "id" => translation.Steps.Id,
                        "nl" => translation.Steps.Nl,
                        "sv" => translation.Steps.Sv,
                        "da" => translation.Steps.Da,
                        "no" => translation.Steps.No,
                        "ms" => translation.Steps.Ms,
                        _ => translation.Steps.De
                    };

                    _context.RecipeSteps.Add(new RecipeSteps
                    {
                        RecipeId = recipe.Id,
                        Culture = culture,
                        Text = text,
                        CreatedAt = now
                    });
                }

                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("✅ RecipeSteps saved (10 languages)");

                // 5. Zutaten verknüpfen (werden nicht übersetzt, sind schon mehrsprachig in DB!)
                foreach (var ing in request.Ingredients)
                {
                    if (ing.IngredientMeasureQuantityId <= 0)
                    {
                        _logger.LogWarning("⚠️ Skipping ingredient with invalid IngredientMeasureQuantityId: {Id}", ing.IngredientMeasureQuantityId);
                        continue;
                    }

                    // Load the IngredientMeasureQuantity entity
                    var ingredientEntity = await _context.IngredientMeasureQuantity
                        .FirstOrDefaultAsync(i => i.Id == ing.IngredientMeasureQuantityId, cancellationToken);

                    if (ingredientEntity == null)
                    {
                        _logger.LogWarning("⚠️ IngredientMeasureQuantity not found: {Id}", ing.IngredientMeasureQuantityId);
                        continue;
                    }

                    _context.RecipeJoinIngredientMeasureQuantity.Add(new RecipeJoinIngredientMeasureQuantity
                    {
                        Recipe = recipe,
                        Ingredient = ingredientEntity
                    });
                }

                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("✅ Ingredients linked: {Count} items", request.Ingredients.Count);

                // 6. Commit
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation("🎉 Recipe saved successfully with Id: {RecipeId}", recipe.Id);

                return Json(new
                {
                    success = true,
                    recipeId = recipe.Id,
                    message = "Rezept erfolgreich gespeichert und übersetzt!",
                    translations = new
                    {
                        title = translation.Title,
                        steps = translation.Steps
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to save recipe with translation");
                return StatusCode(500, new { success = false, message = "Fehler beim Speichern: " + ex.Message });
            }
        }

        public async Task<IActionResult> GetMarketplacePreview(int listingId)
        {
            try
            {
                var listing = await _context.MealPlanListings
                    .Include(x => x.MealPlan)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == listingId && x.IsActive);

                if (listing == null)
                {
                    return NotFound(new { success = false, message = "Listing not found" });
                }

                // Extract recipe IDs from meal plan JSON
                var recipeIds = ExtractRecipeIdsFromMealPlanJson(listing.MealPlan?.MealPlan)
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList();

                // Build recipe media map (images + titles)
                var recipeMediaMap = await BuildRecipeMediaMapAsync(recipeIds);

                // Calculate nutrition totals
                var nutrition = await BuildNutritionTotalsAsync(recipeIds);

                // Build recipe list with images
                var recipes = recipeIds
                    .Where(recipeMediaMap.ContainsKey)
                    .Select(id =>
                    {
                        var media = recipeMediaMap[id];
                        return new
                        {
                            title = media.RecipeTitle,
                            imageUrl = media.ImageUrl
                        };
                    })
                    .ToList();

                // Calculate rating and active planner count
                var rating = listing.SoldCount > 0 ? 4.8m : 4.6m;
                var activePlannerCount = Math.Max(3, (listing.SoldCount % 17) + 3);

                // Calculate kcal per day
                var kcalPerDay = listing.DayCount > 0 ? (int)(nutrition.Calories / listing.DayCount) : 0;

                // Extract tags
                var desc = listing.Description ?? string.Empty;
                var tags = ExtractTags(desc, nutrition);

                return Json(new
                {
                    title = listing.Title,
                    creatorName = listing.SellerName,
                    dayCount = listing.DayCount,
                    recipeCount = listing.RecipeCount,
                    rating,
                    soldCount = listing.SoldCount,
                    activePlannerCount,
                    recipes,
                    kcalPerDay,
                    proteinGrams = (int)nutrition.Protein,
                    fatGrams = (int)nutrition.Fat,
                    carbsGrams = (int)nutrition.Carbohydrates,
                    tags
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading marketplace preview for listing {ListingId}", listingId);
                return StatusCode(500, new { success = false, message = "Error loading preview" });
            }
        }

        private static List<int> ExtractRecipeIdsFromMealPlanJson(string? mealPlanJson)
        {
            if (string.IsNullOrWhiteSpace(mealPlanJson)) return new List<int>();
            try
            {
                var indexIds = JsonConvert.DeserializeObject<Dictionary<int, List<int>>>(mealPlanJson);
                return indexIds?.Values.SelectMany(x => x).ToList() ?? new List<int>();
            }
            catch
            {
                return new List<int>();
            }
        }

        private async Task<Dictionary<int, (string ImageUrl, string RecipeTitle)>> BuildRecipeMediaMapAsync(List<int> recipeIds)
        {
            var result = new Dictionary<int, (string ImageUrl, string RecipeTitle)>();
            if (!recipeIds.Any()) return result;

            var postingMedia = await _context.WorldUserPosting
                .AsNoTracking()
                .Where(p => p.Recipe != null && recipeIds.Contains(p.Recipe.Id))
                .Select(p => new
                {
                    RecipeId = p.Recipe.Id,
                    p.ThumbnailUrl,
                    p.Source,
                    RecipeTitle = p.Recipe.Title,
                    p.CreationTime
                })
                .OrderByDescending(p => p.CreationTime)
                .ToListAsync();

            foreach (var group in postingMedia.GroupBy(x => x.RecipeId))
            {
                var media = group.First();
                var imageUrl = !string.IsNullOrWhiteSpace(media.ThumbnailUrl)
                    ? NormalizeRecipeImagePath(media.ThumbnailUrl)
                    : !string.IsNullOrWhiteSpace(media.Source) && !IsVideoPath(media.Source)
                        ? NormalizeRecipeImagePath(media.Source)
                        : string.Empty;

                result[group.Key] = (imageUrl, media.RecipeTitle ?? "");
            }

            return result;
        }

        private string NormalizeRecipeImagePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;

            const string oldDomain = "blobdelikatessendrehbuch.blob.core.windows.net";
            const string newCdnDomain = "DelekatesenDrehbuchCdn-beecexhdaghhacab.z01.azurefd.net";

            return path.Contains(oldDomain, StringComparison.OrdinalIgnoreCase)
                ? path.Replace(oldDomain, newCdnDomain, StringComparison.OrdinalIgnoreCase)
                : path;
        }

        private static bool IsVideoPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            var lower = path.ToLowerInvariant();
            return lower.Contains(".mp4")
                || lower.Contains(".mov")
                || lower.Contains(".webm")
                || lower.Contains(".m3u8")
                || lower.Contains("mediadelivery.net/play/");
        }

        private async Task<NutritionTotals> BuildNutritionTotalsAsync(List<int> recipeIds)
        {
            var recipes = await _context.RecipeBaseData
                .Include(r => r.Ingredients)
                    .ThenInclude(ri => ri.Ingredient)
                        .ThenInclude(i => i.IngredientsAndNutrients)
                .Include(r => r.Ingredients)
                    .ThenInclude(ri => ri.Ingredient)
                        .ThenInclude(i => i.Quantity)
                .Include(r => r.Ingredients)
                    .ThenInclude(ri => ri.Ingredient)
                        .ThenInclude(i => i.Measure)
                .Where(r => recipeIds.Contains(r.Id))
                .AsNoTracking()
                .ToListAsync();

            var totals = new NutritionTotals();

            foreach (var recipe in recipes)
            {
                if (recipe.Ingredients == null) continue;

                foreach (var ri in recipe.Ingredients)
                {
                    var ing = ri.Ingredient?.IngredientsAndNutrients;
                    var qtyDouble = ri.Ingredient?.Quantity?.Quantitys ?? 100.0;
                    var qty = (decimal)qtyDouble;

                    if (ing == null) continue;

                    var factor = qty / 100m;
                    totals.Protein += ing.Protein_a_100g * factor;
                    totals.Fat += ing.Fat_a_100g * factor;
                    totals.Carbohydrates += ing.Carbohydrates_a_100g * factor;
                    totals.Calories += ing.Calories_a_100g * factor;
                    totals.Fiber += ing.Fiber_a_100g * factor;
                }
            }

            totals.Calories = Math.Round(totals.Calories, 0);
            totals.Fat = Math.Round(totals.Fat, 1);
            totals.Carbohydrates = Math.Round(totals.Carbohydrates, 1);
            totals.Protein = Math.Round(totals.Protein, 1);
            totals.Fiber = Math.Round(totals.Fiber, 1);

            return totals;
        }

        private static List<string> ExtractTags(string description, NutritionTotals? nutrition)
        {
            var tags = new List<string>();
            var desc = description.ToLowerInvariant();

            if (desc.Contains("high protein") || desc.Contains("high-protein") || desc.Contains("proteinreich"))
                tags.Add("High-Protein");
            if (desc.Contains("low carb") || desc.Contains("low-carb"))
                tags.Add("Low Carb");
            if (desc.Contains("vegan"))
                tags.Add("Vegan");
            else if (desc.Contains("vegetarisch") || desc.Contains("vegetarian"))
                tags.Add("Vegetarisch");
            if (desc.Contains("keto"))
                tags.Add("Keto");
            if (desc.Contains("diät") || desc.Contains("diet") || desc.Contains("abnehm"))
                tags.Add("Diät");
            if (desc.Contains("muskelaufbau") || desc.Contains("muscle") || desc.Contains("mass gain"))
                tags.Add("Muskelaufbau");
            if (desc.Contains("schnell") || desc.Contains("quick") || desc.Contains("15 min"))
                tags.Add("Schnell");

            // Infer from nutrition if no tags found
            if (tags.Count == 0 && nutrition != null)
            {
                if (nutrition.Protein > 0 && nutrition.Calories > 0 && (nutrition.Protein * 4 / nutrition.Calories) > 0.30m)
                    tags.Add("High-Protein");
                if (nutrition.Carbohydrates > 0 && nutrition.Calories > 0 && (nutrition.Carbohydrates * 4 / nutrition.Calories) < 0.20m)
                    tags.Add("Low Carb");
            }

            return tags;
        }

        private class NutritionTotals
        {
            public decimal Calories { get; set; }
            public decimal Protein { get; set; }
            public decimal Fat { get; set; }
            public decimal Carbohydrates { get; set; }
            public decimal Fiber { get; set; }
        }

        // DTO für SaveRecipeWithTranslation
        public class SaveRecipeRequest
        {
            public string Title { get; set; }
            public string Steps { get; set; }
            public string? Category { get; set; }
            public int PersonCount { get; set; }
            public int PreparationTime { get; set; }
            public string? Preferences { get; set; }
            public List<IngredientItem> Ingredients { get; set; }
        }

        public class IngredientItem
        {
            public int IngredientMeasureQuantityId { get; set; }
        }
    }
}
