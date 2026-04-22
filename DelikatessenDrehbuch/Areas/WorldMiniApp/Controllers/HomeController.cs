using DelikatessenDrehbuch.Areas.WorldMiniApp.Extensions;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
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
        [CreatedAtUtc] DATETIME2 NOT NULL CONSTRAINT [DF_WorldUserNotifications_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [IsSeen] BIT NOT NULL CONSTRAINT [DF_WorldUserNotifications_IsSeen] DEFAULT (0),
        [SeenAtUtc] DATETIME2 NULL
    );

    CREATE INDEX [IX_WorldUserNotifications_UserHash_IsSeen_CreatedAtUtc]
        ON [dbo].[WorldUserNotifications]([UserHash], [IsSeen], [CreatedAtUtc]);
END;
";

        public HomeController(
            ApplicationDbContext context,
            IWorldClipWatchService worldClipWatchService,
            IWorldAdPreferenceService worldAdPreferenceService,
            IRecipeAiTransformService recipeAiTransformService,
            IRecipeAiVariantJobService recipeAiVariantJobService,
            IRecipeAiNutritionService recipeAiNutritionService,
            ILogger<HomeController> logger)
        {
            _context = context;
            _worldClipWatchService = worldClipWatchService;
            _worldAdPreferenceService = worldAdPreferenceService;
            _recipeAiTransformService = recipeAiTransformService;
            _recipeAiVariantJobService = recipeAiVariantJobService;
            _recipeAiNutritionService = recipeAiNutritionService;
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

            // New canonical AI storage (v2)
            var savedBaseRecipesQuery = _context.RecipeAiBaseRecipes
                .AsNoTracking()
                .Include(x => x.Ingredients)
                    .ThenInclude(x => x.IngredientMeasureQuantity)
                        .ThenInclude(x => x.IngredientsAndNutrients)
                .Include(x => x.Ingredients)
                    .ThenInclude(x => x.IngredientMeasureQuantity)
                        .ThenInclude(x => x.Quantity)
                .Include(x => x.Ingredients)
                    .ThenInclude(x => x.IngredientMeasureQuantity)
                        .ThenInclude(x => x.Measure)
                .Include(x => x.Steps)
                .Include(x => x.SelectedIngredients)
                    .ThenInclude(x => x.Ingredient)
                .Where(x => x.BaseRecipeId == id && x.Language == language);

            if (!string.IsNullOrWhiteSpace(userHash))
            {
                savedBaseRecipesQuery = savedBaseRecipesQuery.Where(x => x.CreatedByUserHash == userHash || x.IsSharedCanonical);
            }
            else
            {
                savedBaseRecipesQuery = savedBaseRecipesQuery.Where(x => x.IsSharedCanonical);
            }

            var savedBaseRecipes = await savedBaseRecipesQuery
                .OrderByDescending(x => x.UpdatedAtUtc)
                .ToListAsync();

            var savedVariantPayloadV2 = savedBaseRecipes
                .GroupBy(x => new
                {
                    x.VariantType,
                    x.AiProvider,
                    x.SelectedIngredientKey,
                    OwnerScope = !string.IsNullOrWhiteSpace(userHash) && x.CreatedByUserHash == userHash ? "user" : "shared"
                })
                .Select(g => g.OrderByDescending(x => x.UpdatedAtUtc).First())
                .OrderByDescending(x => !string.IsNullOrWhiteSpace(userHash) && x.CreatedByUserHash == userHash)
                .ThenByDescending(x => x.UpdatedAtUtc)
                .Take(12)
                .Select(x => new
                {
                    variantId = x.Id,
                    variantType = x.VariantType,
                    variantLabel = GetVariantLabel(x.VariantType),
                    aiProvider = x.AiProvider,
                    title = x.Title,
                    summary = x.Summary,
                    selectedIngredientKey = x.SelectedIngredientKey,
                    updatedAtUtc = x.UpdatedAtUtc,
                    isOwnedByCurrentUser = !string.IsNullOrWhiteSpace(userHash) && x.CreatedByUserHash == userHash,
                    preview = MapAiBaseRecipeToPreview(x, model, language)
                })
                .ToList();

            var savedVariantPayload = savedVariantPayloadV2
                .OrderByDescending(x => x.isOwnedByCurrentUser)
                .ThenByDescending(x => x.updatedAtUtc)
                .Take(12)
                .ToList();

            ViewData["SavedAiVariantsJson"] = JsonSerializer.Serialize(savedVariantPayload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            ViewData["SavedAiVariantCount"] = savedVariantPayload.Count;

            return View(model);
        }

        private static RecipeAiTransformPreview MapAiBaseRecipeToPreview(RecipeAiBaseRecipe variant, RecipeBaseData baseRecipe, string language)
        {
            var normalizedLanguage = (language ?? "de").Trim().ToLowerInvariant();

            var ingredients = (variant.Ingredients ?? new List<RecipeAiBaseRecipeIngredient>())
                .OrderBy(x => x.SortOrder)
                .Select(link =>
                {
                    var imq = link.IngredientMeasureQuantity;
                    var nutrient = imq?.IngredientsAndNutrients;
                    var qty = imq?.Quantity?.Quantitys ?? 0d;
                    var measureLabel = imq?.Measure?.GetLocalized(normalizedLanguage) ?? (imq?.Measure?.Metrics_DE ?? string.Empty);

                    var name = nutrient == null
                        ? string.Empty
                        : normalizedLanguage switch
                        {
                            "en" => nutrient.Name_EN ?? nutrient.Name_DE ?? string.Empty,
                            "pt" => nutrient.Name_PRT ?? nutrient.Name_DE ?? string.Empty,
                            "es" => nutrient.Name_ESP ?? nutrient.Name_DE ?? string.Empty,
                            _ => nutrient.Name_DE ?? string.Empty
                        };

                    var quantityText = qty > 0
                        ? $"{qty.ToString(CultureInfo.InvariantCulture)} {measureLabel}".Trim()
                        : string.Empty;

                    return new RecipeAiTransformIngredientPreview
                    {
                        IngredientId = nutrient?.Id ?? 0,
                        Name = name,
                        Quantity = quantityText,
                        Measure = measureLabel,
                        ChangeHint = link.ChangeHint,
                        IsModified = link.IsModified
                    };
                })
                .Where(x => x.IngredientId > 0 && !string.IsNullOrWhiteSpace(x.Name))
                .ToList();

            var steps = (variant.Steps ?? new List<RecipeAiBaseRecipeStep>())
                .OrderBy(x => x.StepIndex)
                .Select(x => new RecipeAiTransformStepPreview
                {
                    Index = x.StepIndex,
                    Text = x.Text ?? string.Empty
                })
                .Where(x => x.Index > 0 && !string.IsNullOrWhiteSpace(x.Text))
                .ToList();

            var preview = new RecipeAiTransformPreview
            {
                VariantType = variant.VariantType,
                VariantLabel = GetVariantLabel(variant.VariantType),
                AiProvider = variant.AiProvider,
                LanguageCode = variant.Language,
                SelectedIngredientKey = variant.SelectedIngredientKey,
                Title = variant.Title,
                Summary = variant.Summary,
                PreparationText = variant.PreparationText ?? string.Empty,
                IngredientsText = string.Join("\n", ingredients.Select(x => $"{x.Quantity} {x.Name}".Trim())),
                UsedFallback = false,
                UserNoteApplied = variant.AppliedChangeCount > 0,
                RemainingChanges = Math.Max(0, 2 - variant.AppliedChangeCount),
                StepPlan = new List<RecipeAiTransformStepPlanItem>(),
                Steps = steps,
                Ingredients = ingredients,
                Highlights = new List<string>(),
                Nutrition = BuildNutritionFromAiBaseRecipeIngredients(variant.Ingredients ?? new List<RecipeAiBaseRecipeIngredient>(), normalizedLanguage)
            };

            return preview;
        }

        private static RecipeAiTransformNutritionPreview BuildNutritionFromAiBaseRecipeIngredients(
            IReadOnlyList<RecipeAiBaseRecipeIngredient> ingredientLinks,
            string language)
        {
            if (ingredientLinks == null || ingredientLinks.Count == 0)
            {
                return new RecipeAiTransformNutritionPreview();
            }

            var normalizedLanguage = (language ?? "de").Trim().ToLowerInvariant();

            decimal totalCalories = 0m;
            decimal totalProtein = 0m;
            decimal totalCarbs = 0m;
            decimal totalFat = 0m;
            decimal totalSugar = 0m;

            foreach (var link in ingredientLinks)
            {
                var imq = link.IngredientMeasureQuantity;
                var nutrient = imq?.IngredientsAndNutrients;
                if (nutrient == null)
                {
                    continue;
                }

                var amount = (decimal)(imq?.Quantity?.Quantitys ?? 0d);
                if (amount <= 0m)
                {
                    continue;
                }

                var measureLabel = imq?.Measure?.GetLocalized(normalizedLanguage)
                    ?? imq?.Measure?.Metrics_DE
                    ?? string.Empty;

                var quantityText = $"{amount.ToString(CultureInfo.InvariantCulture)} {measureLabel}".Trim();
                var gramsOrMl = TryConvertToGramsOrMillilitersOrPieces(amount, measureLabel, quantityText, nutrient);
                if (gramsOrMl <= 0m)
                {
                    continue;
                }

                var factor = gramsOrMl / 100m;
                totalCalories += nutrient.Calories_a_100g * factor;
                totalProtein += nutrient.Protein_a_100g * factor;
                totalCarbs += nutrient.Carbohydrates_a_100g * factor;
                totalFat += nutrient.Fat_a_100g * factor;
                totalSugar += nutrient.Sugar_a_100g * factor;
            }

            return new RecipeAiTransformNutritionPreview
            {
                Calories = totalCalories,
                Protein = totalProtein,
                Carbs = totalCarbs,
                Fat = totalFat,
                Sugar = totalSugar
            };
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PreviewAiRecipeVariant([FromBody] RecipeAiTransformRequest request, CancellationToken cancellationToken)
        {
            return await GenerateAiRecipeVariantCoreAsync(request, cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateAiRecipeVariant([FromBody] RecipeAiTransformRequest request, CancellationToken cancellationToken)
        {
            return await GenerateAiRecipeVariantCoreAsync(request, cancellationToken);
        }

        private async Task<IActionResult> GenerateAiRecipeVariantCoreAsync(RecipeAiTransformRequest request, CancellationToken cancellationToken)
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
                var selectedIngredientIds = (request.SelectedIngredientIds ?? new List<int>())
                    .Where(x => x > 0)
                    .Distinct()
                    .Take(5)
                    .ToList();

                if (selectedIngredientIds.Count == 0 && request.SelectedIngredientId.GetValueOrDefault() > 0)
                {
                    selectedIngredientIds.Add(request.SelectedIngredientId.Value);
                }

                List<RecipeAiIngredientSuggestionItem>? selectedIngredients = null;
                if (selectedIngredientIds.Count > 0)
                {
                    var ingredients = await _context.IngredientsAndNutrients
                        .AsNoTracking()
                        .Include(x => x.FoodCategory)
                        .Where(x => selectedIngredientIds.Contains(x.Id))
                        .ToListAsync(cancellationToken);

                    if (ingredients.Count != selectedIngredientIds.Count)
                    {
                        return NotFound(new { message = "Mindestens eine gewählte Zutat wurde nicht gefunden." });
                    }

                    selectedIngredients = ingredients
                        .OrderBy(x => selectedIngredientIds.IndexOf(x.Id))
                        .Select(ingredient =>
                        {
                            var ingredientName = language switch
                            {
                                "en" => ingredient.Name_EN,
                                "pt" => ingredient.Name_PRT,
                                "es" => ingredient.Name_ESP,
                                _ => ingredient.Name_DE
                            };

                            var categoryLabel = language switch
                            {
                                "en" => ingredient.FoodCategory?.Name_EN,
                                "pt" => ingredient.FoodCategory?.Name_PRT,
                                "es" => ingredient.FoodCategory?.Name_ESP,
                                _ => ingredient.FoodCategory?.Name_DE
                            };

                            return new RecipeAiIngredientSuggestionItem
                            {
                                IngredientId = ingredient.Id,
                                Name = ingredientName ?? ingredient.Name_DE ?? ingredient.Name_EN ?? "Zutat",
                                CategoryKey = ingredient.FoodCategory?.CategoryKey ?? string.Empty,
                                CategoryLabel = categoryLabel ?? ingredient.FoodCategory?.Name_DE ?? string.Empty,
                                ProteinPer100g = ingredient.Protein_a_100g,
                                CarbsPer100g = ingredient.Carbohydrates_a_100g,
                                FatPer100g = ingredient.Fat_a_100g,
                                FiberPer100g = ingredient.Fiber_a_100g,
                                CaloriesPer100g = ingredient.Calories_a_100g,
                                AiReason = "user-selected"
                            };
                        })
                        .ToList();
                }

                var resolvedUserHash = ResolveUserHash(string.Empty);
                var selectedConcept = string.IsNullOrWhiteSpace(request.SelectedConceptKey)
                    ? null
                    : new RecipeAiIngredientSuggestionItem
                    {
                        ConceptKey = request.SelectedConceptKey?.Trim() ?? string.Empty,
                        ConceptTitle = request.SelectedConceptTitle?.Trim() ?? string.Empty,
                        ConceptSummary = request.SelectedConceptSummary?.Trim() ?? string.Empty,
                        ConceptApproach = request.SelectedConceptApproach?.Trim() ?? string.Empty,
                        ConceptIngredientPlan = request.SelectedConceptIngredientPlan ?? new List<RecipeAiConceptIngredientPlanItem>()
                    };

                var selectedIngredientKey = selectedConcept != null
                    ? $"concept:{selectedConcept.ConceptKey}"
                    : BuildSelectedIngredientKey(selectedIngredientIds);
                RecipeAiTransformPreview? cachedVariant = null;
                if (selectedIngredients == null || selectedIngredients.Count == 0)
                {
                    cachedVariant = await TryGetCachedAiVariantAsync(
                        request.RecipeId,
                        request.VariantType,
                        language,
                        request.AiProvider ?? string.Empty,
                        selectedIngredientKey,
                        request.AppliedChangeCount,
                        request.UserNote,
                        resolvedUserHash,
                        cancellationToken);
                }

                if (cachedVariant != null)
                {
                    // Enforce high-protein minimum even for cached variants (older cached items may predate the rule).
                    if (string.Equals(request.VariantType?.Trim(), "highprotein", StringComparison.OrdinalIgnoreCase))
                    {
                        cachedVariant.Nutrition = await BuildAiPreviewNutritionAsync(cachedVariant, cancellationToken);
                        var baselineNutrition = BuildRecipeNutritionFromRecipe(recipe, language);
                        if (!MeetsHighProteinMinimum(recipe.PersonCount, baselineNutrition.Protein, cachedVariant.Nutrition.Protein))
                        {
                            cachedVariant = null;
                        }
                    }

                    if (cachedVariant != null)
                    {
                        return Json(cachedVariant);
                    }
                }

                var preview = await _recipeAiTransformService.BuildPreviewAsync(
                    recipe,
                    request.VariantType,
                    request.AiProvider ?? string.Empty,
                    language,
                    request.UserNote,
                    request.AppliedChangeCount,
                    selectedConcept: selectedConcept,
                    selectedIngredients: selectedIngredients,
                    cancellationToken: cancellationToken);

                preview.AiProvider = string.IsNullOrWhiteSpace(request.AiProvider) ? "openai" : request.AiProvider.Trim().ToLowerInvariant();
                preview.LanguageCode = language;
                preview.SelectedIngredientKey = selectedIngredientKey;
                preview.Nutrition = await _recipeAiNutritionService.BuildAiPreviewNutritionAsync(preview, language, cancellationToken);

                // Hard requirement: high-protein must actually hit +7% protein per portion.
                // If the first result doesn't, we do one internal repair pass (without consuming a user "change round").
                var userProvidedNote = !string.IsNullOrWhiteSpace(request.UserNote);
                if (string.Equals(request.VariantType?.Trim(), "highprotein", StringComparison.OrdinalIgnoreCase)
                    && !preview.UsedFallback
                    && selectedConcept != null)
                {
                    var baselineNutrition = BuildRecipeNutritionFromRecipe(recipe, language);
                    if (!MeetsHighProteinMinimum(recipe.PersonCount, baselineNutrition.Protein, preview.Nutrition.Protein))
                    {
                        var portions = Math.Max(1, recipe.PersonCount);
                        var baselinePer = baselineNutrition.Protein / portions;
                        var currentPer = preview.Nutrition.Protein / portions;
                        var targetMin = baselinePer * 1.15m;

                        var proteinValidatorNote =
                            $"INTERN: Protein-Ziel nicht erreicht. Aktuell ca. {currentPer:0.#} g Protein/Portion, Ziel mind. {targetMin:0.#} g. " +
                            "Erhoehe Protein durch echte, mengenrelevante Anpassungen (keine Mini-Mengen wie 2 g Mehl). " +
                            "Wenn du Huelsenfruechte/Mehl als Proteinhebel nutzt, nimm eine realistische Menge oder arbeite mit Eiern/Proteinbeilage; halte das Rezept kochbar.";

                        var mergedNote = string.IsNullOrWhiteSpace(request.UserNote)
                            ? proteinValidatorNote
                            : (request.UserNote!.Trim() + "\n" + proteinValidatorNote);

                        var repaired = await _recipeAiTransformService.BuildPreviewAsync(
                            recipe,
                            request.VariantType,
                            request.AiProvider ?? string.Empty,
                            language,
                            mergedNote,
                            request.AppliedChangeCount,
                            selectedConcept: selectedConcept,
                            selectedIngredients: selectedIngredients,
                            cancellationToken: cancellationToken);

                        repaired.AiProvider = preview.AiProvider;
                        repaired.LanguageCode = language;
                        repaired.SelectedIngredientKey = selectedIngredientKey;
                        repaired.Nutrition = await _recipeAiNutritionService.BuildAiPreviewNutritionAsync(repaired, language, cancellationToken);

                        // Keep UX honest: don't show "User-Wunsch drin" when the user didn't type anything.
                        if (!userProvidedNote)
                        {
                            repaired.UserNoteApplied = false;
                        }

                        // Only accept repaired preview if it meets the minimum, otherwise keep the better one but add a hint.
                        if (MeetsHighProteinMinimum(recipe.PersonCount, baselineNutrition.Protein, repaired.Nutrition.Protein))
                        {
                            preview = repaired;
                        }
                        else
                        {
                            preview.Highlights ??= new List<string>();
                            preview.Highlights.Add($"Hinweis: Protein-Ziel (+7%) wurde nicht sicher erreicht ({currentPer:0.#} g/Portion).");
                        }
                    }
                }

                if (!userProvidedNote)
                {
                    preview.UserNoteApplied = false;
                }

                return Json(preview);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "AI recipe preview failed for RecipeId={RecipeId}", request.RecipeId);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected AI recipe preview failure for RecipeId={RecipeId}", request.RecipeId);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SuggestAiVariantIngredients([FromBody] RecipeAiIngredientSuggestionRequest request, CancellationToken cancellationToken)
        {
            if (request == null || request.RecipeId <= 0 || string.IsNullOrWhiteSpace(request.VariantType))
            {
                return BadRequest(new { message = "Rezept oder AI-Variante fehlt." });
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
                var suggestions = await _recipeAiTransformService.BuildIngredientSuggestionsAsync(
                    recipe,
                    request.VariantType,
                    request.AiProvider ?? string.Empty,
                    language,
                    request.UserNote,
                    cancellationToken);

                return Json(suggestions);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "AI ingredient suggestions failed for RecipeId={RecipeId}", request.RecipeId);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected AI ingredient suggestions failure for RecipeId={RecipeId}", request.RecipeId);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartAiRecipeVariantJob([FromBody] RecipeAiTransformRequest request, CancellationToken cancellationToken)
        {
            if (request == null || request.RecipeId <= 0 || string.IsNullOrWhiteSpace(request.VariantType))
            {
                return BadRequest(new { message = "Rezept oder AI-Variante fehlt." });
            }

            try
            {
                var language = (Request.Cookies["deli-lang"] ?? "de").ToLowerInvariant();
                var userHash = ResolveUserHash(string.Empty);
                var response = await _recipeAiVariantJobService.StartJobAsync(request, language, userHash, cancellationToken);
                return Json(response);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "AI job start failed for RecipeId={RecipeId}", request?.RecipeId);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected AI job start failure for RecipeId={RecipeId}", request?.RecipeId);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAiRecipeVariantJobStatus(string jobId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(jobId))
            {
                return BadRequest(new { message = "jobId fehlt." });
            }

            try
            {
                var status = await _recipeAiVariantJobService.GetStatusAsync(jobId.Trim(), cancellationToken);
                if (status == null)
                {
                    return NotFound(new { message = "Job nicht gefunden (abgelaufen)." });
                }

                return Json(status);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "AI job status failed for JobId={JobId}", jobId);
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAiRecipeVariantJobResult(string jobId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(jobId))
            {
                return BadRequest(new { message = "jobId fehlt." });
            }

            try
            {
                var result = await _recipeAiVariantJobService.GetResultAsync(jobId.Trim(), cancellationToken);
                if (result == null)
                {
                    return NotFound(new { message = "Ergebnis nicht bereit (oder Job abgelaufen)." });
                }

                return Json(result);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "AI job result failed for JobId={JobId}", jobId);
                return BadRequest(new { message = ex.Message });
            }
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

            var baseRecipeMeta = await _context.RecipeBaseData
                .AsNoTracking()
                .Where(r => r.Id == request.BaseRecipeId)
                .Select(r => new { r.PersonCount, Prep = r.PreparationTime })
                .FirstAsync(cancellationToken);

            var preview = request.Preview;
            preview.StepPlan ??= new List<RecipeAiTransformStepPlanItem>();
            preview.Steps ??= new List<RecipeAiTransformStepPreview>();
            preview.Ingredients ??= new List<RecipeAiTransformIngredientPreview>();
            preview.Highlights ??= new List<string>();

            var userHash = ResolveUserHash(string.Empty);
            var language = (Request.Cookies["deli-lang"] ?? "de").ToLowerInvariant();
            var normalizedProvider = string.IsNullOrWhiteSpace(request.AiProvider)
                ? (string.IsNullOrWhiteSpace(preview.AiProvider) ? "openai" : preview.AiProvider.Trim().ToLowerInvariant())
                : request.AiProvider.Trim().ToLowerInvariant();
            var selectedIngredientIds = (request.SelectedIngredientIds ?? new List<int>())
                .Where(x => x > 0)
                .Distinct()
                .OrderBy(x => x)
                .ToList();
            var selectedConceptKey = string.IsNullOrWhiteSpace(request.SelectedConceptKey)
                ? string.Empty
                : request.SelectedConceptKey.Trim();
            var selectedIngredientKey = string.IsNullOrWhiteSpace(selectedConceptKey)
                ? BuildSelectedIngredientKey(selectedIngredientIds)
                : $"concept:{selectedConceptKey}";
            var appliedChangeCount = Math.Max(0, 2 - preview.RemainingChanges);
            var isSharedCanonical = appliedChangeCount <= 0 && !preview.UserNoteApplied;

            // === New canonical storage (v2): RecipeAiBaseRecipes + IngredientMeasureQuantity bridge ===
            RecipeAiBaseRecipe entity;
            if (isSharedCanonical)
            {
                entity = await _context.RecipeAiBaseRecipes
                    .FirstOrDefaultAsync(x =>
                        x.BaseRecipeId == request.BaseRecipeId &&
                        x.VariantType == preview.VariantType &&
                        x.Language == language &&
                        x.AiProvider == normalizedProvider &&
                        x.SelectedIngredientKey == selectedIngredientKey &&
                        x.IsSharedCanonical,
                        cancellationToken)
                    ?? new RecipeAiBaseRecipe
                    {
                        BaseRecipeId = request.BaseRecipeId,
                        VariantType = preview.VariantType,
                        Language = language,
                        AiProvider = normalizedProvider,
                        SelectedIngredientKey = selectedIngredientKey,
                        IsSharedCanonical = true,
                        CreatedAtUtc = DateTime.UtcNow
                    };
            }
            else
            {
                entity = new RecipeAiBaseRecipe
                {
                    BaseRecipeId = request.BaseRecipeId,
                    VariantType = preview.VariantType,
                    Language = language,
                    AiProvider = normalizedProvider,
                    SelectedIngredientKey = selectedIngredientKey,
                    IsSharedCanonical = false,
                    CreatedAtUtc = DateTime.UtcNow
                };
            }

            entity.AiProvider = normalizedProvider;
            entity.SelectedIngredientKey = selectedIngredientKey;
            entity.CreatedByUserHash = string.IsNullOrWhiteSpace(userHash) ? null : userHash;
            entity.AppliedChangeCount = appliedChangeCount;
            entity.LatestUserNote = preview.UserNoteApplied
                ? string.IsNullOrWhiteSpace(request.LatestUserNote)
                    ? null
                    : request.LatestUserNote.Trim()
                : null;
            entity.Title = preview.Title ?? string.Empty;
            entity.Summary = preview.Summary ?? string.Empty;
            entity.PersonCount = Math.Max(1, baseRecipeMeta.PersonCount);
            entity.PreparationTimeMinutes = Math.Max(0, baseRecipeMeta.Prep);
            entity.PreparationText = string.IsNullOrWhiteSpace(preview.PreparationText)
                ? string.Join("\n", preview.Steps.OrderBy(x => x.Index).Select(x => x.Text))
                : preview.PreparationText;
            entity.UpdatedAtUtc = DateTime.UtcNow;

            if (entity.Id == 0)
            {
                await _context.RecipeAiBaseRecipes.AddAsync(entity, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }
            else
            {
                await _context.SaveChangesAsync(cancellationToken);
            }

            // Clear and recreate links for deterministic ordering.
            var existingSelected = await _context.RecipeAiBaseRecipeSelectedIngredients
                .Where(x => x.RecipeAiBaseRecipeId == entity.Id)
                .ToListAsync(cancellationToken);
            if (existingSelected.Count > 0)
            {
                _context.RecipeAiBaseRecipeSelectedIngredients.RemoveRange(existingSelected);
            }

            var existingSteps = await _context.RecipeAiBaseRecipeSteps
                .Where(x => x.RecipeAiBaseRecipeId == entity.Id)
                .ToListAsync(cancellationToken);
            if (existingSteps.Count > 0)
            {
                _context.RecipeAiBaseRecipeSteps.RemoveRange(existingSteps);
            }

            var existingIngredients = await _context.RecipeAiBaseRecipeIngredients
                .Where(x => x.RecipeAiBaseRecipeId == entity.Id)
                .ToListAsync(cancellationToken);
            if (existingIngredients.Count > 0)
            {
                _context.RecipeAiBaseRecipeIngredients.RemoveRange(existingIngredients);
            }

            if (selectedIngredientIds.Count > 0)
            {
                await _context.RecipeAiBaseRecipeSelectedIngredients.AddRangeAsync(
                    selectedIngredientIds.Select((ingredientId, index) => new RecipeAiBaseRecipeSelectedIngredient
                    {
                        RecipeAiBaseRecipeId = entity.Id,
                        IngredientId = ingredientId,
                        SortOrder = index
                    }),
                    cancellationToken);
            }

            // IngredientMeasureQuantity bridge
            var measureLookup2 = await LoadMeasureLookupAsync(language, cancellationToken);
            var ingredientLinks = new List<RecipeAiBaseRecipeIngredient>();
            for (var index = 0; index < preview.Ingredients.Count; index++)
            {
                var item = preview.Ingredients[index];
                if (item == null)
                {
                    continue;
                }

                var imqId = await ResolveOrCreateIngredientMeasureQuantityId(item, measureLookup2, cancellationToken);
                if (imqId == null)
                {
                    continue;
                }

                ingredientLinks.Add(new RecipeAiBaseRecipeIngredient
                {
                    RecipeAiBaseRecipeId = entity.Id,
                    IngredientMeasureQuantityId = imqId.Value,
                    SortOrder = index,
                    IsModified = item.IsModified,
                    ChangeHint = item.ChangeHint
                });
            }

            if (ingredientLinks.Count > 0)
            {
                await _context.RecipeAiBaseRecipeIngredients.AddRangeAsync(ingredientLinks, cancellationToken);
            }

            // Steps
            var stepRows = preview.Steps
                .Where(x => x != null && x.Index > 0 && !string.IsNullOrWhiteSpace(x.Text))
                .OrderBy(x => x.Index)
                .Select(x => new RecipeAiBaseRecipeStep
                {
                    RecipeAiBaseRecipeId = entity.Id,
                    StepIndex = x.Index,
                    Text = x.Text ?? string.Empty
                })
                .ToList();
            if (stepRows.Count > 0)
            {
                await _context.RecipeAiBaseRecipeSteps.AddRangeAsync(stepRows, cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);

            return Json(new
            {
                success = true,
                variantId = entity.Id,
                isSharedCanonical = entity.IsSharedCanonical
            });
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

        private async Task<Dictionary<string, int>> LoadMeasureLookupAsync(string language, CancellationToken cancellationToken)
        {
            // Build a lookup across all localized metric strings (e.g. "g", "ml", "stk") to Measure.Id.
            // This keeps the AI output lightweight (it can just say "g") while we store normalized FK IDs.
            var measures = await _context.Metrics
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var lookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var m in measures)
            {
                if (m == null)
                {
                    continue;
                }

                foreach (var unit in new[]
                {
                    m.Metrics_DE, m.Metrics_EN, m.Metrics_ESP, m.Metrics_PRT,
                    m.Metrics_ID, m.Metrics_MS, m.Metrics_NL, m.Metrics_SE,
                    m.Metrics_DK, m.Metrics_NO
                })
                {
                    var key = NormalizeMeasureLookupKey(unit);
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }

                    if (!lookup.ContainsKey(key))
                    {
                        lookup[key] = m.Id;
                    }
                }
            }

            // Common user/AI variants.
            if (!lookup.ContainsKey("stk") && lookup.TryGetValue("stuck", out var pieceId))
            {
                lookup["stk"] = pieceId;
            }
            if (!lookup.ContainsKey("stueck") && lookup.TryGetValue("stuck", out var pieceId2))
            {
                lookup["stueck"] = pieceId2;
            }

            return lookup;
        }

        private async Task<int?> ResolveOrCreateIngredientMeasureQuantityId(
            RecipeAiTransformIngredientPreview item,
            IReadOnlyDictionary<string, int> measureLookup,
            CancellationToken cancellationToken)
        {
            if (item == null || item.IngredientId <= 0)
            {
                return null;
            }

            var quantity = TryParseLeadingQuantity(item.Quantity);
            if (quantity <= 0m)
            {
                return null;
            }

            var measureId = ResolveMeasureIdFromLookup(item.Measure, measureLookup);
            if (measureId == null)
            {
                return null;
            }

            // Reuse existing Quantity rows where possible (Quantity table is intentionally tiny).
            var quantityValue = (double)quantity;
            var existingQuantity = await _context.Quantities
                .FirstOrDefaultAsync(x => Math.Abs(x.Quantitys - quantityValue) < 0.0001, cancellationToken);

            if (existingQuantity == null)
            {
                existingQuantity = new Quantity { Quantitys = quantityValue };
                await _context.Quantities.AddAsync(existingQuantity, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }

            // Reuse existing IngredientMeasureQuantity rows to avoid DB bloat.
            var existingImq = await _context.IngredientMeasureQuantity
                .FirstOrDefaultAsync(x =>
                    EF.Property<int>(x, "IngredientsAndNutrientsId") == item.IngredientId
                    && EF.Property<int>(x, "QuantityId") == existingQuantity.Id
                    && EF.Property<int>(x, "MeasureId") == measureId.Value,
                    cancellationToken);

            if (existingImq != null)
            {
                return existingImq.Id;
            }

            var ingredient = await _context.IngredientsAndNutrients
                .FirstOrDefaultAsync(x => x.Id == item.IngredientId, cancellationToken);
            var measure = await _context.Metrics
                .FirstOrDefaultAsync(x => x.Id == measureId.Value, cancellationToken);

            if (ingredient == null || measure == null)
            {
                return null;
            }

            var created = new IngredientMeasureQuantity
            {
                IngredientsAndNutrients = ingredient,
                Quantity = existingQuantity,
                Measure = measure
            };

            await _context.IngredientMeasureQuantity.AddAsync(created, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return created.Id;
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
            string aiProvider,
            string selectedIngredientKey,
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
            var normalizedProvider = string.IsNullOrWhiteSpace(aiProvider) ? "openai" : aiProvider.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedVariantType))
            {
                return null;
            }

            RecipeAiBaseRecipe? cached = null;
            if (!string.IsNullOrWhiteSpace(userHash))
            {
                cached = await _context.RecipeAiBaseRecipes
                    .AsNoTracking()
                    .Include(x => x.Ingredients)
                        .ThenInclude(x => x.IngredientMeasureQuantity)
                            .ThenInclude(x => x.IngredientsAndNutrients)
                    .Include(x => x.Ingredients)
                        .ThenInclude(x => x.IngredientMeasureQuantity)
                            .ThenInclude(x => x.Quantity)
                    .Include(x => x.Ingredients)
                        .ThenInclude(x => x.IngredientMeasureQuantity)
                            .ThenInclude(x => x.Measure)
                    .Include(x => x.Steps)
                    .Include(x => x.SelectedIngredients)
                        .ThenInclude(x => x.Ingredient)
                    .Where(x =>
                        x.BaseRecipeId == baseRecipeId &&
                        x.VariantType == normalizedVariantType &&
                        x.Language == language &&
                        x.AiProvider == normalizedProvider &&
                        x.SelectedIngredientKey == selectedIngredientKey &&
                        x.CreatedByUserHash == userHash &&
                        !x.IsSharedCanonical)
                    .OrderByDescending(x => x.UpdatedAtUtc)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            cached ??= await _context.RecipeAiBaseRecipes
                .AsNoTracking()
                .Include(x => x.Ingredients)
                    .ThenInclude(x => x.IngredientMeasureQuantity)
                        .ThenInclude(x => x.IngredientsAndNutrients)
                .Include(x => x.Ingredients)
                    .ThenInclude(x => x.IngredientMeasureQuantity)
                        .ThenInclude(x => x.Quantity)
                .Include(x => x.Ingredients)
                    .ThenInclude(x => x.IngredientMeasureQuantity)
                        .ThenInclude(x => x.Measure)
                .Include(x => x.Steps)
                .Include(x => x.SelectedIngredients)
                    .ThenInclude(x => x.Ingredient)
                .Where(x =>
                    x.BaseRecipeId == baseRecipeId &&
                    x.VariantType == normalizedVariantType &&
                    x.Language == language &&
                    x.AiProvider == normalizedProvider &&
                    x.SelectedIngredientKey == selectedIngredientKey &&
                    x.IsSharedCanonical)
                .OrderByDescending(x => x.UpdatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            var baseRecipe = await _context.RecipeBaseData
                .AsNoTracking()
                .Include(x => x.Ingredients)
                    .ThenInclude(x => x.Ingredient)
                        .ThenInclude(x => x.IngredientsAndNutrients)
                .FirstOrDefaultAsync(x => x.Id == baseRecipeId, cancellationToken);

            return cached == null || baseRecipe == null ? null : MapAiBaseRecipeToPreview(cached, baseRecipe, language);
        }
        private static IngredientsAndNutrients? FindMatchingIngredient(
            int ingredientId,
            string? displayName,
            IReadOnlyList<IngredientMeasureQuantity> originalIngredients,
            IReadOnlyList<IngredientsAndNutrients> selectedIngredients)
        {
            if (ingredientId > 0)
            {
                var selectedById = selectedIngredients.FirstOrDefault(x => x.Id == ingredientId);
                if (selectedById != null)
                {
                    return selectedById;
                }

                var originalById = originalIngredients
                    .Select(x => x.IngredientsAndNutrients)
                    .FirstOrDefault(x => x != null && x.Id == ingredientId);
                if (originalById != null)
                {
                    return originalById;
                }
            }

            var normalizedDisplayName = NormalizeIngredientLookupName(displayName);
            if (string.IsNullOrWhiteSpace(normalizedDisplayName))
            {
                return null;
            }

            var selectedMatch = selectedIngredients.FirstOrDefault(x => NormalizeIngredientLookupName(x.Name_DE) == normalizedDisplayName);
            if (selectedMatch != null)
            {
                return selectedMatch;
            }

            var originalExactMatch = originalIngredients
                .Select(x => x.IngredientsAndNutrients)
                .FirstOrDefault(x => x != null && NormalizeIngredientLookupName(x.Name_DE) == normalizedDisplayName);

            if (originalExactMatch != null)
            {
                return originalExactMatch;
            }

            return selectedIngredients.FirstOrDefault(x => NormalizeIngredientLookupName(x.Name_DE).Contains(normalizedDisplayName, StringComparison.OrdinalIgnoreCase))
                ?? originalIngredients.Select(x => x.IngredientsAndNutrients)
                    .FirstOrDefault(x => x != null && NormalizeIngredientLookupName(x.Name_DE).Contains(normalizedDisplayName, StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeIngredientLookupName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value
                .Trim()
                .ToLowerInvariant()
                .Replace("ä", "ae")
                .Replace("ö", "oe")
                .Replace("ü", "ue")
                .Replace("ß", "ss")
                .Replace("(gemahlen)", string.Empty)
                .Replace("(in öl)", string.Empty)
                .Replace("(in oel)", string.Empty)
                .Replace("filets", "filet")
                .Replace("zehen", "zehe")
                .Replace(".", string.Empty)
                .Replace(",", string.Empty)
                .Trim();
        }

        private static decimal TryParseLeadingQuantity(string? quantityText)
        {
            var text = (quantityText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0m;
            }

            var firstToken = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            var fractionParts = firstToken.Split('/');
            if (fractionParts.Length == 2
                && decimal.TryParse(fractionParts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var numerator)
                && decimal.TryParse(fractionParts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var denominator)
                && denominator != 0)
            {
                return numerator / denominator;
            }

            var numericToken = new string(text.TakeWhile(ch => char.IsDigit(ch) || ch is ',' or '.').ToArray());
            if (string.IsNullOrWhiteSpace(numericToken))
            {
                return 0m;
            }

            return decimal.TryParse(numericToken.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var result)
                ? result
                : 0m;
        }

        private async Task<RecipeAiTransformNutritionPreview> BuildAiPreviewNutritionAsync(
            RecipeAiTransformPreview preview,
            CancellationToken cancellationToken)
        {
            if (preview?.Ingredients == null || preview.Ingredients.Count == 0)
            {
                return new RecipeAiTransformNutritionPreview();
            }

            var ingredientIds = preview.Ingredients
                .Select(x => x?.IngredientId ?? 0)
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            if (ingredientIds.Count == 0)
            {
                return new RecipeAiTransformNutritionPreview();
            }

            var nutrients = await _context.IngredientsAndNutrients
                .AsNoTracking()
                .Where(x => ingredientIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, cancellationToken);

            decimal totalCalories = 0m;
            decimal totalProtein = 0m;
            decimal totalCarbs = 0m;
            decimal totalFat = 0m;
            decimal totalSugar = 0m;

            foreach (var item in preview.Ingredients)
            {
                if (item == null || item.IngredientId <= 0)
                {
                    continue;
                }

                if (!nutrients.TryGetValue(item.IngredientId, out var nutrient))
                {
                    continue;
                }

                var amount = TryParseLeadingQuantity(item.Quantity);
                if (amount <= 0m)
                {
                    continue;
                }

                var gramsOrMl = TryConvertToGramsOrMillilitersOrPieces(amount, item.Measure, item.Quantity, nutrient);
                if (gramsOrMl <= 0m)
                {
                    continue;
                }

                var factor = gramsOrMl / 100m;
                totalCalories += nutrient.Calories_a_100g * factor;
                totalProtein += nutrient.Protein_a_100g * factor;
                totalCarbs += nutrient.Carbohydrates_a_100g * factor;
                totalFat += nutrient.Fat_a_100g * factor;
                totalSugar += nutrient.Sugar_a_100g * factor;
            }

            return new RecipeAiTransformNutritionPreview
            {
                Calories = totalCalories,
                Protein = totalProtein,
                Carbs = totalCarbs,
                Fat = totalFat,
                Sugar = totalSugar
            };
        }

        private static bool MeetsHighProteinMinimum(int portions, decimal baselineProteinTotal, decimal candidateProteinTotal)
        {
            var safePortions = Math.Max(1, portions);
            var baselinePerPortion = baselineProteinTotal / safePortions;
            if (baselinePerPortion <= 0m)
            {
                return true;
            }

            var candidatePerPortion = candidateProteinTotal / safePortions;
            return candidatePerPortion + 0.05m >= baselinePerPortion * 1.15m;
        }

        private RecipeAiTransformNutritionPreview BuildRecipeNutritionFromRecipe(RecipeBaseData recipe, string language)
        {
            if (recipe?.Ingredients == null || recipe.Ingredients.Count == 0)
            {
                return new RecipeAiTransformNutritionPreview();
            }

            var normalizedLanguage = (language ?? "de").Trim().ToLowerInvariant();

            decimal totalCalories = 0m;
            decimal totalProtein = 0m;
            decimal totalCarbs = 0m;
            decimal totalFat = 0m;
            decimal totalSugar = 0m;

            foreach (var link in recipe.Ingredients)
            {
                var imq = link?.Ingredient;
                var nutrient = imq?.IngredientsAndNutrients;
                if (imq == null || nutrient == null)
                {
                    continue;
                }

                var amount = (decimal)(imq.Quantity?.Quantitys ?? 0d);
                if (amount <= 0m)
                {
                    continue;
                }

                var measureLabel = imq.Measure?.GetLocalized(normalizedLanguage)
                    ?? imq.Measure?.Metrics_DE
                    ?? string.Empty;

                var quantityText = $"{amount.ToString(CultureInfo.InvariantCulture)} {measureLabel}".Trim();
                var gramsOrMl = TryConvertToGramsOrMillilitersOrPieces(amount, measureLabel, quantityText, nutrient);
                if (gramsOrMl <= 0m)
                {
                    continue;
                }

                var factor = gramsOrMl / 100m;
                totalCalories += nutrient.Calories_a_100g * factor;
                totalProtein += nutrient.Protein_a_100g * factor;
                totalCarbs += nutrient.Carbohydrates_a_100g * factor;
                totalFat += nutrient.Fat_a_100g * factor;
                totalSugar += nutrient.Sugar_a_100g * factor;
            }

            return new RecipeAiTransformNutritionPreview
            {
                Calories = totalCalories,
                Protein = totalProtein,
                Carbs = totalCarbs,
                Fat = totalFat,
                Sugar = totalSugar
            };
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

        private static string BuildSelectedIngredientKey(IEnumerable<int>? ingredientIds)
        {
            if (ingredientIds == null)
            {
                return string.Empty;
            }

            return string.Join(",",
                ingredientIds
                    .Where(x => x > 0)
                    .Distinct()
                    .OrderBy(x => x));
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
