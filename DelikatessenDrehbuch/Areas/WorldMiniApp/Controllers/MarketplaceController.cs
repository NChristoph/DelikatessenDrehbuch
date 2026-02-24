using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    [Area("WorldMiniApp")]
    public class MarketplaceController : Controller
    {
        private const string SessionUserHashKey = "WorldMiniAppUserHash";
        private const string SessionWalletWLD = "WorldWallet_WLD";
        private const string SessionWalletUSDT = "WorldWallet_USDT";
        private static readonly HashSet<string> AllowedWalletTokens = new(StringComparer.OrdinalIgnoreCase) { "WLD", "USDT", "USDCE" };
        private readonly IWildCoinService _coinService;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public MarketplaceController(IWildCoinService coinService, ApplicationDbContext context, IConfiguration configuration)
        {
            _coinService = coinService;
            _context = context;
            _configuration = configuration;
        }

        private void SetWorldChainConfig()
        {
            ViewData["WorldChainId"] = _configuration["WorldChain:ChainId"] ?? "480";
            ViewData["WorldChainWldToken"] = _configuration["WorldChain:WldTokenAddress"] ?? "";
            ViewData["WorldChainUsdtToken"] = _configuration["WorldChain:UsdtTokenAddress"] ?? "";
            ViewData["WorldChainMarketplace"] = _configuration["WorldChain:MarketplaceContractAddress"] ?? "";
            ViewData["WorldChainAllowSelfPurchaseForTesting"] =
                bool.TryParse(_configuration["WorldChain:AllowSelfPurchaseForTesting"], out var allowSelfPurchase)
                && allowSelfPurchase;
            ViewData["WorldChainTestMode"] =
                bool.TryParse(_configuration["WorldChain:TestMode"], out var testMode) && testMode;
        }

        private bool IsSelfPurchaseAllowedForTesting()
        {
            return bool.TryParse(_configuration["WorldChain:AllowSelfPurchaseForTesting"], out var allowSelfPurchase)
                && allowSelfPurchase;
        }

        /// <summary>
        /// NullifierHash kommt ausschließlich aus der Login-Session.
        /// </summary>
        private string? GetUserHash()
        {
            return HttpContext.Session.GetString(SessionUserHashKey);
        }

        // GET: Marketplace overview
        public async Task<IActionResult> Index()
        {
            var userHash = GetUserHash();
            var listings = await _context.MealPlanListings
                .Include(x => x.MealPlan)
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.CreatedAt)
                .Take(50)
                .ToListAsync();

            var recipeIds = listings
                .SelectMany(l => ExtractRecipeIdsFromMealPlanJson(l.MealPlan?.MealPlan))
                .Distinct()
                .ToList();
            var recipeMediaMap = await BuildRecipeMediaMapAsync(recipeIds);
            var creatorShopCards = listings.Select(listing =>
            {
                var listingRecipeIds = ExtractRecipeIdsFromMealPlanJson(listing.MealPlan?.MealPlan);
                var heroImages = listingRecipeIds
                    .Where(recipeMediaMap.ContainsKey)
                    .Select(id => recipeMediaMap[id])
                    .Where(media => !string.IsNullOrWhiteSpace(media.ImageUrl))
                    .ToList();

                return new PlanCardViewModel
                {
                    ListingId = listing.Id,
                    Title = listing.Title,
                    TitleJsSafe = (listing.Title ?? string.Empty).Replace("'", "\\'"),
                    Description = listing.Description,
                    CreatorName = listing.SellerName,
                    CreatorHash = listing.SellerHash,
                    SellerWalletAddress = listing.SellerWalletAddress,
                    DayCount = listing.DayCount,
                    RecipeCount = listing.RecipeCount,
                    CreatedDateLabel = listing.CreatedAt.ToString("dd.MM.yy"),
                    PriceWld = listing.Price,
                    Rating = listing.SoldCount > 0 ? 4.8m : 4.6m,
                    SoldCount = listing.SoldCount,
                    ActivePlannerCount = Math.Max(3, (listing.SoldCount % 17) + 3),
                    IsLowCarb = (listing.Description ?? string.Empty).Contains("low carb", StringComparison.OrdinalIgnoreCase),
                    IsDietFriendly = (listing.Description ?? string.Empty).Contains("diet", StringComparison.OrdinalIgnoreCase)
                        || (listing.Description ?? string.Empty).Contains("diät", StringComparison.OrdinalIgnoreCase),
                    HeroSlides = heroImages.Select(x => new PlanCardHeroSlideViewModel { ImageUrl = x.ImageUrl, RecipeTitle = x.RecipeTitle }).ToList(),
                    HeroImageUrls = heroImages.Select(x => x.ImageUrl).ToList(),
                    HeroImageUrl = heroImages.Select(x => x.ImageUrl).FirstOrDefault()
                };
            }).ToList();

            ViewData["CreatorShopCards"] = creatorShopCards;

            ViewData["UserHash"] = userHash ?? "";
            ViewData["WalletWLD"] = HttpContext.Session.GetString(SessionWalletWLD) ?? "";
            ViewData["WalletUSDT"] = HttpContext.Session.GetString(SessionWalletUSDT) ?? "";
            SetWorldChainConfig();
            return View(listings);
        }

        // GET: Creator Shop (Premium Karten Demo)
        [HttpGet]
        public async Task<IActionResult> CreatorShop()
        {
            var listings = await _context.MealPlanListings
                .Include(x => x.MealPlan)
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.CreatedAt)
                .Take(50)
                .ToListAsync();

            var recipeIds = listings
                .SelectMany(l => ExtractRecipeIdsFromMealPlanJson(l.MealPlan?.MealPlan))
                .Distinct()
                .ToList();
            var recipeMediaMap = await BuildRecipeMediaMapAsync(recipeIds);

            var cards = listings.Select(listing =>
            {
                var listingRecipeIds = ExtractRecipeIdsFromMealPlanJson(listing.MealPlan?.MealPlan);
                var heroImages = listingRecipeIds
                    .Where(recipeMediaMap.ContainsKey)
                    .Select(id => recipeMediaMap[id])
                    .Where(media => !string.IsNullOrWhiteSpace(media.ImageUrl))
                    .ToList();

                return new PlanCardViewModel
                {
                    ListingId = listing.Id,
                    Title = listing.Title,
                    TitleJsSafe = (listing.Title ?? string.Empty).Replace("'", "\\'"),
                    Description = listing.Description,
                    CreatorName = listing.SellerName,
                    CreatorHash = listing.SellerHash,
                    SellerWalletAddress = listing.SellerWalletAddress,
                    DayCount = listing.DayCount,
                    RecipeCount = listing.RecipeCount,
                    CreatedDateLabel = listing.CreatedAt.ToString("dd.MM.yy"),
                    PriceWld = listing.Price,
                    Rating = listing.SoldCount > 0 ? 4.8m : 4.6m,
                    SoldCount = listing.SoldCount,
                    ActivePlannerCount = Math.Max(3, (listing.SoldCount % 17) + 3),
                    IsLowCarb = (listing.Description ?? string.Empty).Contains("low carb", StringComparison.OrdinalIgnoreCase),
                    IsDietFriendly = (listing.Description ?? string.Empty).Contains("diet", StringComparison.OrdinalIgnoreCase)
                        || (listing.Description ?? string.Empty).Contains("diät", StringComparison.OrdinalIgnoreCase),
                    HeroSlides = heroImages.Select(x => new PlanCardHeroSlideViewModel { ImageUrl = x.ImageUrl, RecipeTitle = x.RecipeTitle }).ToList(),
                    HeroImageUrls = heroImages.Select(x => x.ImageUrl).ToList(),
                    HeroImageUrl = heroImages.Select(x => x.ImageUrl).FirstOrDefault()
                };
            }).ToList();

            return View(cards);
        }

        // GET: Meine Angebote
        public async Task<IActionResult> MyListings()
        {
            var userHash = GetUserHash();
            if (string.IsNullOrWhiteSpace(userHash)) return RedirectToAction("Index");

            var listings = await _coinService.GetMyListings(userHash);

            ViewData["UserHash"] = userHash;
            SetWorldChainConfig();
            return View(listings);
        }

        // GET: Sell-Formular
        public async Task<IActionResult> Sell()
        {
            var userHash = GetUserHash();
            if (string.IsNullOrWhiteSpace(userHash)) return RedirectToAction("Index");

            var mealPlans = await _context.WorldUserMealPlan
                .Where(m => m.UserHash == userHash)
                .ToListAsync();

            ViewData["UserHash"] = userHash;
            ViewData["WalletWLD"] = HttpContext.Session.GetString(SessionWalletWLD) ?? "";
            ViewData["WalletUSDT"] = HttpContext.Session.GetString(SessionWalletUSDT) ?? "";
            SetWorldChainConfig();
            return View(mealPlans);
        }

        // POST: Wallet-Adresse pro Coin in Session speichern (nach Wallet Auth)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveWalletAddress(string walletAddress, string token)
        {
            var userHash = GetUserHash();
            if (string.IsNullOrWhiteSpace(userHash))
                return Json(new { success = false, error = "Nicht eingeloggt." });

            if (string.IsNullOrWhiteSpace(walletAddress))
                return Json(new { success = false, error = "Wallet-Adresse fehlt." });

            token = (token ?? "WLD").ToUpperInvariant();
            if (!AllowedWalletTokens.Contains(token))
                return Json(new { success = false, error = "Unbekannter Token." });

            var sessionKey = token == "USDT" || token == "USDCE" ? SessionWalletUSDT : SessionWalletWLD;
            HttpContext.Session.SetString(sessionKey, walletAddress);

            return Json(new { success = true, token, walletAddress });
        }

        // GET: Gespeicherte Wallet-Adresse aus Session holen
        [HttpGet]
        public IActionResult GetWalletAddress(string? token)
        {
            var userHash = GetUserHash();
            if (string.IsNullOrWhiteSpace(userHash))
                return Json(new { walletAddress = "" });

            token = (token ?? "WLD").ToUpperInvariant();
            if (!AllowedWalletTokens.Contains(token))
                return Json(new { success = false, error = "Unbekannter Token." });

            var sessionKey = token == "USDT" || token == "USDCE" ? SessionWalletUSDT : SessionWalletWLD;
            var address = HttpContext.Session.GetString(sessionKey) ?? "";

            return Json(new { walletAddress = address, token });
        }

        // POST: Listing erstellen
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateListing(int mealPlanId, string title, string? description, decimal price, string? sellerWalletAddress, string? sellerUsdtWalletAddress)
        {
            var userHash = GetUserHash();
            if (string.IsNullOrWhiteSpace(userHash))
                return Json(new { success = false, error = "Nicht eingeloggt." });

            if (price < 1 || price > 1000)
                return Json(new { success = false, error = "Preis muss zwischen 1 und 1000 WLD liegen." });

            if (string.IsNullOrWhiteSpace(sellerWalletAddress) && string.IsNullOrWhiteSpace(sellerUsdtWalletAddress))
                return Json(new { success = false, error = "Bitte verbinde zuerst mindestens eine Wallet (WLD oder USDT), damit du Zahlungen empfangen kannst." });

            var sessionWalletWld = HttpContext.Session.GetString(SessionWalletWLD) ?? string.Empty;
            var sessionWalletUsdt = HttpContext.Session.GetString(SessionWalletUSDT) ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(sellerWalletAddress) && !string.Equals(sellerWalletAddress, sessionWalletWld, StringComparison.OrdinalIgnoreCase))
                return Json(new { success = false, error = "Die angegebene WLD-Wallet passt nicht zu deiner verbundenen World-Wallet." });

            if (!string.IsNullOrWhiteSpace(sellerUsdtWalletAddress) && !string.Equals(sellerUsdtWalletAddress, sessionWalletUsdt, StringComparison.OrdinalIgnoreCase))
                return Json(new { success = false, error = "Die angegebene USDT/USDC-Wallet passt nicht zu deiner verbundenen World-Wallet." });

            try
            {
                var listing = await _coinService.CreateListing(userHash, mealPlanId, title, description, price, sellerWalletAddress, sellerUsdtWalletAddress);
                return Json(new { success = true, listingId = listing.Id });
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // POST: Listing deaktivieren
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeactivateListing(int listingId)
        {
            var userHash = GetUserHash();
            if (string.IsNullOrWhiteSpace(userHash))
                return Json(new { success = false, error = "Nicht eingeloggt." });

            var success = await _coinService.DeactivateListing(userHash, listingId);
            return Json(new { success });
        }

        public class FinalizeWorldChainPurchaseRequest
        {
            public int ListingId { get; set; }
            public string TxHash { get; set; } = string.Empty;
            public string WalletAddress { get; set; } = string.Empty;
            public string PaymentToken { get; set; } = "WLD";
        }

        // POST: World Chain Kauf finalisieren (nach erfolgreicher On-Chain TX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FinalizeWorldChainPurchase([FromForm] FinalizeWorldChainPurchaseRequest request)
        {
            var userHash = GetUserHash();
            if (string.IsNullOrWhiteSpace(userHash))
                return Json(new { success = false, error = "Nicht eingeloggt." });

            if (string.IsNullOrWhiteSpace(request.TxHash))
                return Json(new { success = false, error = "TxHash fehlt." });

            var paymentToken = (request.PaymentToken ?? "WLD").ToUpperInvariant();
            if (!AllowedWalletTokens.Contains(paymentToken))
                return Json(new { success = false, error = "Unbekannter Payment-Token." });

            var buyerSessionWallet = paymentToken == "USDT" || paymentToken == "USDCE"
                ? HttpContext.Session.GetString(SessionWalletUSDT) ?? string.Empty
                : HttpContext.Session.GetString(SessionWalletWLD) ?? string.Empty;

            if (string.IsNullOrWhiteSpace(buyerSessionWallet) && !string.IsNullOrWhiteSpace(request.WalletAddress))
            {
                buyerSessionWallet = request.WalletAddress.Trim();
                var buyerSessionKey = paymentToken == "USDT" || paymentToken == "USDCE" ? SessionWalletUSDT : SessionWalletWLD;
                HttpContext.Session.SetString(buyerSessionKey, buyerSessionWallet);
            }

            if (string.IsNullOrWhiteSpace(buyerSessionWallet))
                return Json(new { success = false, error = "Keine verbundene Wallet in der Session gefunden." });

            if (!string.IsNullOrWhiteSpace(request.WalletAddress)
                && !string.Equals(request.WalletAddress, buyerSessionWallet, StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { success = false, error = "Wallet-Adresse passt nicht zur aktuellen Session." });
            }

            try
            {
                var purchase = await _coinService.FinalizeWorldChainPurchase(
                    userHash,
                    request.ListingId,
                    request.TxHash,
                    buyerSessionWallet,
                    IsSelfPurchaseAllowedForTesting(),
                    paymentToken);
                if (purchase == null)
                    return Json(new { success = false, error = "Kauf konnte nicht finalisiert werden." });

                return Json(new { success = true, mealPlanId = purchase.CreatedMealPlanId });
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // GET: Transaktionshistorie
        [HttpGet]
        public async Task<IActionResult> Transactions()
        {
            var userHash = GetUserHash();
            if (string.IsNullOrWhiteSpace(userHash))
                return Json(new List<object>());

            var transactions = await _coinService.GetTransactions(userHash);
            return Json(transactions.Select(t => new
            {
                t.Amount,
                t.BalanceAfter,
                t.Type,
                t.ReferenceInfo,
                date = t.CreatedAt.ToString("dd.MM.yyyy HH:mm")
            }));
        }

        // GET: Plan-Vorschau (Gerichte + Nährwerte) für ein Listing
        [HttpGet]
        public async Task<IActionResult> GetListingPreview(int listingId)
        {
            var listing = await _context.MealPlanListings
                .Include(l => l.MealPlan)
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == listingId && l.IsActive);

            if (listing?.MealPlan?.MealPlan == null)
                return Json(new { success = false, error = "Listing nicht gefunden." });

            var indexIds = JsonConvert.DeserializeObject<Dictionary<int, List<int>>>(listing.MealPlan.MealPlan);
            if (indexIds == null || !indexIds.Any())
                return Json(new { success = false, error = "Plan ist leer." });

            var allRecipeIds = indexIds.Values.SelectMany(x => x).Distinct().ToList();

            // Rezeptdaten laden (aus beiden Tabellen, wie in HomeController)
            var baseRecipes = await _context.RecipeBaseData
                .Include(r => r.Images)
                .AsNoTracking()
                .Where(r => allRecipeIds.Contains(r.Id))
                .ToListAsync();

            var classicRecipes = await _context.Recipes
                .AsNoTracking()
                .Where(r => allRecipeIds.Contains(r.Id))
                .ToListAsync();

            // Tage aufbauen
            var days = new List<object>();
            foreach (var entry in indexIds.OrderBy(e => e.Key))
            {
                var recipes = new List<object>();
                foreach (var recipeId in entry.Value)
                {
                    var baseR = baseRecipes.FirstOrDefault(r => r.Id == recipeId);
                    if (baseR != null)
                    {
                        var img = baseR.Images?.FirstOrDefault()?.Image ?? "";
                        if (!string.IsNullOrEmpty(img))
                            img = NormalizeRecipeImagePath(img);

                        recipes.Add(new
                        {
                            id = baseR.Id,
                            title = baseR.Title,
                            category = baseR.Category,
                            time = baseR.PreperationTime,
                            image = img
                        });
                        continue;
                    }

                    var classic = classicRecipes.FirstOrDefault(r => r.Id == recipeId);
                    if (classic != null)
                    {
                        var img = classic.ImagePath ?? "";
                        if (!string.IsNullOrEmpty(img))
                            img = NormalizeRecipeImagePath(img);

                        recipes.Add(new
                        {
                            id = classic.Id,
                            title = classic.Name,
                            category = classic.Category ?? "",
                            time = classic.PreparationTime ?? 0,
                            image = img
                        });
                    }
                }

                days.Add(new { day = entry.Key + 1, recipes });
            }

            // Nährwerte berechnen
            var nutrition = await BuildNutritionTotalsAsync(allRecipeIds, classicRecipes);

            return Json(new
            {
                success = true,
                title = listing.Title,
                description = listing.Description ?? string.Empty,
                dayCount = listing.DayCount,
                recipeCount = listing.RecipeCount,
                days,
                nutrition = new
                {
                    calories = nutrition.Calories,
                    protein = nutrition.Protein,
                    fat = nutrition.Fat,
                    carbs = nutrition.Carbohydrates,
                    fiber = nutrition.Fiber
                }
            });
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
                var preferredImage = group
                    .Select(x => x.ThumbnailUrl)
                    .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));

                preferredImage ??= group
                    .Select(x => x.Source)
                    .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && !IsVideoPath(path));

                preferredImage ??= group
                    .Select(x => x.Source)
                    .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));

                if (string.IsNullOrWhiteSpace(preferredImage))
                    continue;

                var title = group.Select(x => x.RecipeTitle).FirstOrDefault() ?? string.Empty;
                result[group.Key] = (NormalizeRecipeImagePath(preferredImage), title);
            }

            var missingBaseRecipeIds = recipeIds.Where(id => !result.ContainsKey(id)).ToList();

            var baseRecipes = await _context.RecipeBaseData
                .Include(r => r.Images)
                .AsNoTracking()
                .Where(r => missingBaseRecipeIds.Contains(r.Id))
                .ToListAsync();

            foreach (var recipe in baseRecipes)
            {
                var image = recipe.Images?.FirstOrDefault()?.Image;
                if (string.IsNullOrWhiteSpace(image)) continue;
                result[recipe.Id] = (NormalizeRecipeImagePath(image), recipe.Title ?? string.Empty);
            }

            var missingIds = recipeIds.Where(id => !result.ContainsKey(id)).ToList();
            if (missingIds.Any())
            {
                var classicRecipes = await _context.Recipes
                    .AsNoTracking()
                    .Where(r => missingIds.Contains(r.Id) && r.ImagePath != null)
                    .ToListAsync();

                foreach (var recipe in classicRecipes)
                {
                    if (string.IsNullOrWhiteSpace(recipe.ImagePath)) continue;
                    result[recipe.Id] = (NormalizeRecipeImagePath(recipe.ImagePath), recipe.Name ?? string.Empty);
                }
            }

            return result;
        }

        private static string NormalizeRecipeImagePath(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath)) return string.Empty;

            if (Uri.IsWellFormedUriString(imagePath, UriKind.Absolute))
                return ChangePath(imagePath);

            return ChangePath(FrontendFunctions.GetSmallImagePath(imagePath));
        }

        private static string ChangePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return path;

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
            return lower.Contains(".mp4") || lower.Contains(".mov") || lower.Contains(".webm") || lower.Contains(".m3u8");
        }

        private async Task<NutritionTotals> BuildNutritionTotalsAsync(List<int> recipeIds, List<Recipes>? classicRecipes = null)
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

                var basePersonCount = recipe.PersonCount == 0 ? 1 : recipe.PersonCount;

                foreach (var entry in recipe.Ingredients)
                {
                    var ingredient = entry.Ingredient;
                    var nutrient = ingredient?.IngredientsAndNutrients;
                    if (nutrient == null) continue;

                    var quantity = ingredient.Quantity?.Quantitys ?? 0;
                    var unit = ingredient.Measure?.UnitOfMeasurement ?? string.Empty;
                    var grams = (decimal)quantity;

                    if (string.Equals(unit, "Stk.", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(unit, "Stück", StringComparison.OrdinalIgnoreCase))
                    {
                        var weightPerPiece = nutrient.Weight_per_piece > 0 ? nutrient.Weight_per_piece : 0;
                        grams = (decimal)weightPerPiece * (decimal)quantity;
                    }

                    if (grams <= 0) continue;

                    totals.Calories += ((decimal)nutrient.Calories_a_100g * grams) / 100m;
                    totals.Fat += (nutrient.Fat_a_100g * grams) / 100m;
                    totals.Carbohydrates += (nutrient.Carbohydrates_a_100g * grams) / 100m;
                    totals.Protein += (nutrient.Protein_a_100g * grams) / 100m;
                    totals.Fiber += (nutrient.Fiber_a_100g * grams) / 100m;
                }
            }

            if (classicRecipes != null)
            {
                foreach (var classic in classicRecipes.Where(r => recipeIds.Contains(r.Id)))
                {
                    if (string.IsNullOrWhiteSpace(classic.Calories)) continue;
                    var normalized = classic.Calories.Replace(',', '.');
                    if (decimal.TryParse(normalized, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var classicCalories))
                    {
                        totals.Calories += Math.Max(0, classicCalories);
                    }
                }
            }

            totals.Calories = Math.Round(totals.Calories, 0);
            totals.Fat = Math.Round(totals.Fat, 1);
            totals.Carbohydrates = Math.Round(totals.Carbohydrates, 1);
            totals.Protein = Math.Round(totals.Protein, 1);
            totals.Fiber = Math.Round(totals.Fiber, 1);

            return totals;
        }

        private class NutritionTotals
        {
            public decimal Calories { get; set; }
            public decimal Protein { get; set; }
            public decimal Fat { get; set; }
            public decimal Carbohydrates { get; set; }
            public decimal Fiber { get; set; }
        }
    }
}
