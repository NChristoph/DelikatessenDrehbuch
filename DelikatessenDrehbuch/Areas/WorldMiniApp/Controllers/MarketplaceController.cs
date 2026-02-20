using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services;
using DelikatessenDrehbuch.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    [Area("WorldMiniApp")]
    public class MarketplaceController : Controller
    {
        private const string SessionUserHashKey = "WorldMiniAppUserHash";
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

        private string? ResolveUserHash(string? userHash)
        {
            if (!string.IsNullOrWhiteSpace(userHash)) return userHash;
            return HttpContext.Session.GetString(SessionUserHashKey)
                ?? HttpContext.Session.GetString("UserHash")
                ?? Request.Query["userHash"].FirstOrDefault();
        }

        // GET: Marketplace overview
        public async Task<IActionResult> Index(string? userHash)
        {
            userHash = ResolveUserHash(userHash);
            var listings = await _coinService.GetActiveListings(0, 50);
            var balance = !string.IsNullOrEmpty(userHash) ? await _coinService.GetBalance(userHash) : 0;

            ViewData["UserHash"] = userHash;
            ViewData["Balance"] = balance;
            SetWorldChainConfig();
            return View(listings);
        }

        // GET: Meine Angebote
        public async Task<IActionResult> MyListings(string? userHash)
        {
            userHash = ResolveUserHash(userHash);
            if (string.IsNullOrWhiteSpace(userHash)) return RedirectToAction("Index");

            var listings = await _coinService.GetMyListings(userHash);
            var balance = await _coinService.GetBalance(userHash);

            ViewData["UserHash"] = userHash;
            ViewData["Balance"] = balance;
            SetWorldChainConfig();
            return View(listings);
        }

        // GET: Sell-Formular
        public async Task<IActionResult> Sell(string? userHash)
        {
            userHash = ResolveUserHash(userHash);
            if (string.IsNullOrWhiteSpace(userHash)) return RedirectToAction("Index");

            var mealPlans = await _context.WorldUserMealPlan
                .Where(m => m.UserHash == userHash)
                .ToListAsync();

            ViewData["UserHash"] = userHash;
            return View(mealPlans);
        }

        // POST: Listing erstellen
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateListing(string userHash, int mealPlanId, string title, string? description, decimal price)
        {
            userHash = ResolveUserHash(userHash);
            if (string.IsNullOrWhiteSpace(userHash))
                return Json(new { success = false, error = "Nicht eingeloggt." });

            if (price < 1 || price > 1000)
                return Json(new { success = false, error = "Preis muss zwischen 1 und 1000 WildCoin liegen." });

            try
            {
                var listing = await _coinService.CreateListing(userHash, mealPlanId, title, description, price);
                return Json(new { success = true, listingId = listing.Id });
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // POST: Listing kaufen
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Buy(string userHash, int listingId)
        {
            userHash = ResolveUserHash(userHash);
            if (string.IsNullOrWhiteSpace(userHash))
                return Json(new { success = false, error = "Nicht eingeloggt." });

            var purchase = await _coinService.BuyListing(userHash, listingId, IsSelfPurchaseAllowedForTesting());
            if (purchase == null)
                return Json(new { success = false, error = "Kauf nicht möglich. Nicht genug WildCoin oder Angebot nicht verfügbar." });

            return Json(new { success = true, mealPlanId = purchase.CreatedMealPlanId });
        }

        // POST: Listing deaktivieren
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeactivateListing(string userHash, int listingId)
        {
            userHash = ResolveUserHash(userHash);
            if (string.IsNullOrWhiteSpace(userHash))
                return Json(new { success = false, error = "Nicht eingeloggt." });

            var success = await _coinService.DeactivateListing(userHash, listingId);
            return Json(new { success });
        }


        public class FinalizeWorldChainPurchaseRequest
        {
            public string UserHash { get; set; } = string.Empty;
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
            var userHash = ResolveUserHash(request.UserHash);
            if (string.IsNullOrWhiteSpace(userHash))
                return Json(new { success = false, error = "Nicht eingeloggt." });

            if (string.IsNullOrWhiteSpace(request.TxHash))
                return Json(new { success = false, error = "TxHash fehlt." });

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

        // GET: Transaktionshistorie
        [HttpGet]
        public async Task<IActionResult> Transactions(string? userHash)
        {
            userHash = ResolveUserHash(userHash);
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

        // GET: Balance abfragen
        [HttpGet]
        public async Task<IActionResult> Balance(string? userHash)
        {
            userHash = ResolveUserHash(userHash);
            if (string.IsNullOrWhiteSpace(userHash))
                return Json(new { balance = 0 });

            var balance = await _coinService.GetBalance(userHash);
            return Json(new { balance });
        }
    }
}
