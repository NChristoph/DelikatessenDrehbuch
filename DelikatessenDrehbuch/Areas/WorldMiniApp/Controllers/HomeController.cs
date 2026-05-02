using DelikatessenDrehbuch.Areas.WorldMiniApp.Extensions;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.Globalization;
using System.Resources;
using System.Text.Json;
using System.Threading;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    public class HomeController : WorldMiniAppBaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly IWorldClipWatchService _worldClipWatchService;
        private readonly IWorldAdPreferenceService _worldAdPreferenceService;
        private readonly IRecipeAiTransformService _recipeAiTransformService;
        private readonly IRecipeAiVariantJobService _recipeAiVariantJobService;
        private readonly IRecipeAiNutritionService _recipeAiNutritionService;
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
            IStringLocalizer<SharedResources> sharedLocalizer,
            ILogger<HomeController> logger)
        {
            _context = context;
            _worldClipWatchService = worldClipWatchService;
            _worldAdPreferenceService = worldAdPreferenceService;
            _recipeAiTransformService = recipeAiTransformService;
            _recipeAiVariantJobService = recipeAiVariantJobService;
            _recipeAiNutritionService = recipeAiNutritionService;
            _sharedLocalizer = sharedLocalizer;
            _logger = logger;
        }

        public IActionResult Index()
        {
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

            var ingredientLinks = recipe.Ingredients?
                .Where(ri => ri.Ingredient?.IngredientsAndNutrients != null)
                .ToList() ?? new();

            var ingredients = ingredientLinks
                .Select(ri => new
                {
                    name = ri.Ingredient?.IngredientsAndNutrients?.Name_DE ?? "",
                    quantity = ri.Ingredient?.Quantity?.Quantitys,
                    unit = ri.Ingredient?.Measure?.Metrics_DE ?? ""
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

        public async Task<IActionResult> ShowRecipe(int id, int? aiVariantId = null, int? swapVariantId = null, bool original = false)
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
            var language = (Request.Cookies["deli-lang"] ?? "de").ToLowerInvariant();

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

            // Load ingredient swaps if swapVariantId is provided OR auto-load user's latest variant
            RecipeUserVariant variant = null;

            if (swapVariantId.HasValue)
            {
                // Explicit variant requested
                variant = await _context.RecipeUserVariants
                    .FirstOrDefaultAsync(v => v.Id == swapVariantId.Value && v.OriginalRecipeId == id);
            }
            else if (!original && !aiVariantId.HasValue && !string.IsNullOrEmpty(userHash))
            {
                // Auto-load user's latest variant for this recipe (only if no AI variant requested)
                variant = await _context.RecipeUserVariants
                    .Where(v => v.UserHash == userHash && v.OriginalRecipeId == id)
                    .OrderByDescending(v => v.CreatedAtUtc)
                    .FirstOrDefaultAsync();
            }

            if (variant != null)
            {
                ViewData["SwapsJson"] = variant.SwapsJson;
                ViewData["VariantTitle"] = variant.Title;
                ViewData["SwapVariantId"] = variant.Id;
            }

            return View(model);
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
    }
}
