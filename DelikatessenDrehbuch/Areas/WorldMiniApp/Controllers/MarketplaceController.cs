using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services;
using DelikatessenDrehbuch.Data;
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
            var listings = await _coinService.GetActiveListings(0, 50);

            ViewData["UserHash"] = userHash ?? "";
            ViewData["WalletWLD"] = HttpContext.Session.GetString(SessionWalletWLD) ?? "";
            ViewData["WalletUSDT"] = HttpContext.Session.GetString(SessionWalletUSDT) ?? "";
            SetWorldChainConfig();
            return View(listings);
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
            var sessionKey = token == "USDT" ? SessionWalletUSDT : SessionWalletWLD;
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
            var sessionKey = token == "USDT" ? SessionWalletUSDT : SessionWalletWLD;
            var address = HttpContext.Session.GetString(sessionKey) ?? "";

            return Json(new { walletAddress = address, token });
        }

        // POST: Listing erstellen
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateListing(int mealPlanId, string title, string? description, decimal price, string? sellerWalletAddress)
        {
            var userHash = GetUserHash();
            if (string.IsNullOrWhiteSpace(userHash))
                return Json(new { success = false, error = "Nicht eingeloggt." });

            if (price < 1 || price > 1000)
                return Json(new { success = false, error = "Preis muss zwischen 1 und 1000 WLD liegen." });

            if (string.IsNullOrWhiteSpace(sellerWalletAddress))
                return Json(new { success = false, error = "Bitte verbinde zuerst deine Wallet, damit du Zahlungen empfangen kannst." });

            try
            {
                var listing = await _coinService.CreateListing(userHash, mealPlanId, title, description, price, sellerWalletAddress);
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

            try
            {
                var purchase = await _coinService.FinalizeWorldChainPurchase(
                    userHash,
                    request.ListingId,
                    request.TxHash,
                    request.WalletAddress,
                    IsSelfPurchaseAllowedForTesting(),
                    request.PaymentToken);
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
                            img = FrontendFunctions.GetSmallImagePath(img);

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
                            img = FrontendFunctions.GetSmallImagePath(img);

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
            var nutrition = await BuildNutritionTotalsAsync(allRecipeIds);

            return Json(new
            {
                success = true,
                title = listing.Title,
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
