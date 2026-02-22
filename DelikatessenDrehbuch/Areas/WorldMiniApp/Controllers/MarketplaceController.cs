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
        public async Task<IActionResult> CreateListing(int mealPlanId, string title, string? description, decimal price)
        {
            var userHash = GetUserHash();
            if (string.IsNullOrWhiteSpace(userHash))
                return Json(new { success = false, error = "Nicht eingeloggt." });

            if (price < 1 || price > 1000)
                return Json(new { success = false, error = "Preis muss zwischen 1 und 1000 WLD liegen." });

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
    }
}
