using DelikatessenDrehbuch.Areas.WorldMiniApp.Exceptions;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Data;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    /// <summary>
    /// Service for managing the Meal Plan Marketplace.
    /// Handles listing creation, purchases via World Chain blockchain (WLD/USDT/USDCE),
    /// and marketplace operations.
    /// Implements both IMarketplaceService and IWildCoinService (legacy) for backwards compatibility.
    /// </summary>
    public class MarketplaceService : IMarketplaceService, IWildCoinService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MarketplaceService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private static readonly Regex TxHashRegex = new("^0x[a-fA-F0-9]{64}$", RegexOptions.Compiled);
        private const string WorldChainRpcUrl = "https://worldchain-mainnet.g.alchemy.com/public";

        public MarketplaceService(ApplicationDbContext context, IConfiguration configuration, ILogger<MarketplaceService> logger, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        // ---- Marketplace Listings ----

        public async Task<MealPlanListing> CreateListingAsync(string sellerHash, int mealPlanId, string title, string? description, decimal price, string? sellerWalletAddress = null, string? sellerUsdtWalletAddress = null)
        {
            var user = await _context.WorldAppUser.FirstOrDefaultAsync(u => u.UserHash == sellerHash);
            if (user == null) throw new WorldMiniAppNotFoundException("User nicht gefunden.");

            var mealPlan = await _context.WorldUserMealPlan.FirstOrDefaultAsync(m => m.Id == mealPlanId && m.UserHash == sellerHash);
            if (mealPlan == null) throw new WorldMiniAppNotFoundException("Essensplan nicht gefunden oder gehört nicht dir.");

            var mealPlanDays = ExtractMealPlanDays(mealPlan.MealPlan);
            var recipeIds = mealPlanDays
                .Values
                .SelectMany(v => v)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (recipeIds.Count > 0)
            {
                var ownedRecipeIds = await _context.WorldUserPosting
                    .Where(p => p.CreatorId == sellerHash && p.Recipe != null && recipeIds.Contains(p.Recipe.Id))
                    .Select(p => p.Recipe.Id)
                    .Distinct()
                    .ToListAsync();

                if (recipeIds.Except(ownedRecipeIds).Any())
                {
                    throw new WorldMiniAppValidationException("Du kannst nur Essenspläne verkaufen, die ausschließlich deine eigenen Rezepte enthalten.");
                }
            }

            // Rezeptanzahl und Tage aus dem MealPlan JSON berechnen
            int dayCount = mealPlanDays.Count;
            int recipeCount = recipeIds.Count;

            var listing = new MealPlanListing
            {
                SellerHash = sellerHash,
                SellerName = user.UserName ?? "Anonym",
                SellerWalletAddress = sellerWalletAddress,
                SellerUsdtWalletAddress = sellerUsdtWalletAddress,
                MealPlanId = mealPlanId,
                MealPlan = mealPlan,
                Title = title,
                Description = description,
                Price = price,
                DayCount = dayCount,
                RecipeCount = recipeCount
            };

            await _context.MealPlanListings.AddAsync(listing);
            await _context.SaveChangesAsync();
            return listing;
        }

        private static Dictionary<int, List<int>> ExtractMealPlanDays(string? mealPlanJson)
        {
            if (string.IsNullOrWhiteSpace(mealPlanJson))
            {
                return new Dictionary<int, List<int>>();
            }

            try
            {
                return JsonSerializer.Deserialize<Dictionary<int, List<int>>>(mealPlanJson) ?? new Dictionary<int, List<int>>();
            }
            catch
            {
                return new Dictionary<int, List<int>>();
            }
        }

        public async Task<List<MealPlanListing>> GetActiveListingsAsync(int skip = 0, int take = 20)
        {
            return await _context.MealPlanListings
                .Where(l => l.IsActive)
                .OrderByDescending(l => l.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public async Task<List<MealPlanListing>> GetMyListingsAsync(string userHash)
        {
            return await _context.MealPlanListings
                .Include(l => l.MealPlan)
                .Where(l => l.SellerHash == userHash)
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();
        }

        public async Task<MealPlanListing?> GetListingByIdAsync(int id)
        {
            return await _context.MealPlanListings
                .Include(l => l.MealPlan)
                .FirstOrDefaultAsync(l => l.Id == id);
        }

        public async Task<bool> DeactivateListingAsync(string userHash, int listingId)
        {
            var listing = await _context.MealPlanListings.FirstOrDefaultAsync(l => l.Id == listingId && l.SellerHash == userHash);
            if (listing == null) return false;

            listing.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ActivateListingAsync(string userHash, int listingId)
        {
            var listing = await _context.MealPlanListings.FirstOrDefaultAsync(l => l.Id == listingId && l.SellerHash == userHash);
            if (listing == null) return false;

            listing.IsActive = true;
            await _context.SaveChangesAsync();
            return true;
        }

        // ---- Marketplace Purchases (Blockchain) ----

        public async Task<MealPlanPurchase?> FinalizeWorldChainPurchaseAsync(string buyerHash, int listingId, string txHash, string walletAddress, bool allowSelfPurchase = false, string paymentToken = "WLD")
        {
            if (string.IsNullOrWhiteSpace(txHash))
            {
                _logger.LogWarning("FinalizeWorldChainPurchase: TxHash ist leer.");
                return null;
            }

            // SICHERHEIT: Die Zahlung wird jetzt BLOCKIEREND und fail-closed verifiziert
            // (siehe unten, nach dem Laden des Listings). Früher lief die Prüfung als
            // fire-and-forget und der Kauf wurde IMMER durchgelassen -> jeder eingeloggte
            // Nutzer konnte mit einer erfundenen txHash Bezahlinhalte gratis bekommen.

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var listing = await _context.MealPlanListings
                    .Include(l => l.MealPlan)
                    .FirstOrDefaultAsync(l => l.Id == listingId && l.IsActive);

                if (listing == null) return null;
                if (!allowSelfPurchase && listing.SellerHash == buyerHash) return null;

                // Duplikatschutz: gleiche TX nicht doppelt finalisieren
                var existing = await _context.MealPlanPurchases
                    .FirstOrDefaultAsync(p => p.ReferenceTxHash == txHash);
                if (existing != null) return existing;

                // Modell B: Der Käufer zahlt den VOLLEN Preis in EINER Zahlung an die
                // PLATTFORM-Wallet (atomar). Plattform-Wallet je nach Token bestimmen.
                var isStableToken = (paymentToken ?? "WLD").Equals("USDT", StringComparison.OrdinalIgnoreCase)
                    || (paymentToken ?? "WLD").Equals("USDCE", StringComparison.OrdinalIgnoreCase);
                var platformWallet = isStableToken
                    ? (_configuration["Marketplace:PlatformWalletAddressUsdt"] ?? _configuration["Marketplace:PlatformWalletAddress"])
                    : _configuration["Marketplace:PlatformWalletAddress"];
                if (string.IsNullOrWhiteSpace(platformWallet))
                {
                    _logger.LogError("Kauf abgelehnt: Marketplace:PlatformWalletAddress nicht konfiguriert (fail-closed).");
                    return null;
                }

                // SICHERHEIT: Zahlung serverseitig verifizieren (fail-closed) — sie MUSS an die
                // Plattform-Wallet gegangen sein. Schließt den "gratis via erfundener txHash"-Exploit.
                var paymentVerified = await VerifyMiniKitPaymentAsync(txHash, listingId, platformWallet);
                if (!paymentVerified)
                {
                    _logger.LogWarning("Kauf abgelehnt: Zahlung nicht verifiziert. Listing={Listing} Tx={Tx}", listingId, txHash);
                    return null;
                }

                listing.SoldCount++;

                var copiedPlan = new WorldUserMealPlan
                {
                    UserHash = buyerHash,
                    Settings = listing.MealPlan?.Settings,
                    MealPlan = listing.MealPlan?.MealPlan,
                    Title = listing.Title,
                    CreationTime = DateTime.Now
                };
                await _context.WorldUserMealPlan.AddAsync(copiedPlan);
                await _context.SaveChangesAsync();

                var creatorAmount = Math.Round(listing.Price * 0.80m, 6);
                var platformFee = listing.Price - creatorAmount;

                var sellerWalletForPurchase = (paymentToken ?? "WLD").Equals("USDT", StringComparison.OrdinalIgnoreCase)
                    || (paymentToken ?? "WLD").Equals("USDCE", StringComparison.OrdinalIgnoreCase)
                    ? (listing.SellerUsdtWalletAddress ?? listing.SellerWalletAddress)
                    : listing.SellerWalletAddress;

                var purchase = new MealPlanPurchase
                {
                    BuyerHash = buyerHash,
                    BuyerWalletAddress = walletAddress,
                    SellerHash = listing.SellerHash,
                    SellerWalletAddress = sellerWalletForPurchase,
                    ListingId = listing.Id,
                    Listing = listing,
                    CreatedMealPlanId = copiedPlan.Id,
                    PricePaid = listing.Price,
                    CreatorAmount = creatorAmount,
                    PlatformFee = platformFee,
                    ReferenceTxHash = txHash,
                    PaymentToken = paymentToken
                };
                await _context.MealPlanPurchases.AddAsync(purchase);
                await _context.SaveChangesAsync();

                // Modell B: Die Plattform hat den vollen Betrag erhalten. Der Verkäufer-Anteil
                // (80 %) wird als internes Guthaben (WildCoinBalance) gutgeschrieben; die echte
                // Auszahlung erfolgt separat (manuell / späteres Cash-out-Feature). Der Token
                // wird in ReferenceInfo vermerkt (Guthaben ist token-agnostisch, WLD-denominiert).
                var seller = await _context.WorldAppUser.FirstOrDefaultAsync(u => u.UserHash == listing.SellerHash);
                if (seller != null)
                {
                    seller.WildCoinBalance += creatorAmount;
                    await _context.WildCoinTransactions.AddAsync(new WildCoinTransaction
                    {
                        UserHash = seller.UserHash,
                        Amount = creatorAmount,
                        BalanceAfter = seller.WildCoinBalance,
                        Type = "sale_credit",
                        ReferenceInfo = $"listing:{listingId};token:{paymentToken};tx:{txHash}",
                        CreatedAt = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
                return purchase;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<MealPlanPurchase>> GetPurchasesByBuyerAsync(string buyerHash)
        {
            return await _context.MealPlanPurchases
                .Include(p => p.Listing)
                    .ThenInclude(l => l.MealPlan)
                .Where(p => p.BuyerHash == buyerHash)
                .OrderByDescending(p => p.PurchasedAt)
                .ToListAsync();
        }

        // ---- Verkäufer-Auszahlung (Modell B Cash-out) ----

        public async Task<decimal> GetAvailableBalanceAsync(string sellerHash)
        {
            var user = await _context.WorldAppUser.AsNoTracking().FirstOrDefaultAsync(u => u.UserHash == sellerHash);
            return user?.WildCoinBalance ?? 0m;
        }

        public async Task<(bool success, string? error, MarketplacePayoutRequest? request)> RequestPayoutAsync(string sellerHash, decimal amount, string token, string walletAddress)
        {
            if (string.IsNullOrWhiteSpace(sellerHash)) return (false, "Nicht eingeloggt.", null);
            if (string.IsNullOrWhiteSpace(walletAddress)) return (false, "Auszahlungs-Wallet fehlt.", null);
            if (amount <= 0) return (false, "Betrag muss größer 0 sein.", null);

            var normalizedToken = (token ?? "WLD").ToUpperInvariant();
            if (normalizedToken == "USDT") normalizedToken = "USDCE";
            if (normalizedToken != "WLD" && normalizedToken != "USDCE") return (false, "Unbekannter Token.", null);

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var user = await _context.WorldAppUser.FirstOrDefaultAsync(u => u.UserHash == sellerHash);
                if (user == null) return (false, "Nutzer nicht gefunden.", null);
                if (amount > user.WildCoinBalance) return (false, "Betrag übersteigt dein verfügbares Guthaben.", null);

                // Guthaben sofort reservieren (vom verfügbaren Saldo abziehen).
                user.WildCoinBalance -= amount;

                var request = new MarketplacePayoutRequest
                {
                    SellerHash = sellerHash,
                    Amount = amount,
                    Token = normalizedToken,
                    WalletAddress = walletAddress.Trim(),
                    Status = "pending",
                    RequestedAt = DateTime.UtcNow
                };
                await _context.MarketplacePayoutRequests.AddAsync(request);

                await _context.WildCoinTransactions.AddAsync(new WildCoinTransaction
                {
                    UserHash = sellerHash,
                    Amount = -amount,
                    BalanceAfter = user.WildCoinBalance,
                    Type = "payout_request",
                    ReferenceInfo = $"token:{normalizedToken};wallet:{walletAddress.Trim()}",
                    CreatedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                return (true, null, request);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task<List<MarketplacePayoutRequest>> GetPayoutsForSellerAsync(string sellerHash)
        {
            return await _context.MarketplacePayoutRequests.AsNoTracking()
                .Where(p => p.SellerHash == sellerHash)
                .OrderByDescending(p => p.RequestedAt)
                .Take(100)
                .ToListAsync();
        }

        public async Task<List<MarketplacePayoutRequest>> GetPendingPayoutsAsync()
        {
            return await _context.MarketplacePayoutRequests.AsNoTracking()
                .Where(p => p.Status == "pending")
                .OrderBy(p => p.RequestedAt)
                .Take(200)
                .ToListAsync();
        }

        public async Task<bool> MarkPayoutPaidAsync(int payoutId, string txHash, string adminHash)
        {
            var request = await _context.MarketplacePayoutRequests.FirstOrDefaultAsync(p => p.Id == payoutId && p.Status == "pending");
            if (request == null) return false;

            request.Status = "paid";
            request.TxHash = txHash?.Trim();
            request.ProcessedAt = DateTime.UtcNow;
            request.ProcessedBy = adminHash;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RejectPayoutAsync(int payoutId, string note, string adminHash)
        {
            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var request = await _context.MarketplacePayoutRequests.FirstOrDefaultAsync(p => p.Id == payoutId && p.Status == "pending");
                if (request == null) return false;

                // Reserviertes Guthaben zurückbuchen.
                var user = await _context.WorldAppUser.FirstOrDefaultAsync(u => u.UserHash == request.SellerHash);
                if (user != null)
                {
                    user.WildCoinBalance += request.Amount;
                    await _context.WildCoinTransactions.AddAsync(new WildCoinTransaction
                    {
                        UserHash = request.SellerHash,
                        Amount = request.Amount,
                        BalanceAfter = user.WildCoinBalance,
                        Type = "payout_refund",
                        ReferenceInfo = $"payout:{request.Id}",
                        CreatedAt = DateTime.UtcNow
                    });
                }

                request.Status = "rejected";
                request.Note = note;
                request.ProcessedAt = DateTime.UtcNow;
                request.ProcessedBy = adminHash;
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                return true;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Verifiziert eine MiniKit-Zahlung serverseitig über die World-Payment-API
        /// (https://developer.worldcoin.org/api/v2/minikit/transaction/{id}?app_id=…&type=payment).
        /// FAIL-CLOSED: liefert nur true, wenn die Transaktion zu DIESER App gehört
        /// (App-scoped API-Key), die reference zum Listing passt ("listing-{id}-…"),
        /// die Zahlung nicht fehlgeschlagen ist und der Empfänger (falls geliefert) die
        /// erwartete (Plattform-)Wallet ist. Schließt den "gratis via erfundener txHash"-Exploit.
        /// Notfall-Schalter: Config Marketplace:VerifyPayments=false (NICHT in Produktion).
        /// </summary>
        private async Task<bool> VerifyMiniKitPaymentAsync(string transactionId, int listingId, string? expectedRecipient)
        {
            var verifyEnabled = !string.Equals(_configuration["Marketplace:VerifyPayments"], "false", StringComparison.OrdinalIgnoreCase);
            if (!verifyEnabled)
            {
                _logger.LogWarning("Marketplace:VerifyPayments=false -> Zahlung NICHT verifiziert (unsicher!). Tx={Tx}", transactionId);
                return true;
            }

            var appId = _configuration["WorldId:AppId"];
            // Bestehender Azure-App-Setting-Name "WorldApiKey" wird bevorzugt;
            // Fallback auf WorldId:DevPortalApiKey.
            var apiKey = _configuration["WorldApiKey"] ?? _configuration["WorldId:DevPortalApiKey"];
            if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogError("Zahlungsverifizierung nicht möglich: WorldId:AppId oder WorldApiKey fehlt -> fail-closed.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return false;
            }

            try
            {
                var url = $"https://developer.worldcoin.org/api/v2/minikit/transaction/{Uri.EscapeDataString(transactionId)}?app_id={Uri.EscapeDataString(appId)}&type=payment";
                var client = _httpClientFactory.CreateClient();
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Headers.UserAgent.ParseAdd("DelikatessenDrehbuch/1.0");
                request.Headers.Accept.ParseAdd("application/json");

                var response = await client.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    // Nicht-2xx = TX gehört nicht zu dieser App / existiert nicht.
                    _logger.LogWarning("World-Payment-API {Status} für Tx {Tx}", (int)response.StatusCode, transactionId);
                    return false;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var status = root.TryGetProperty("transaction_status", out var st) ? st.GetString() : null;
                var reference = root.TryGetProperty("reference", out var rf) ? rf.GetString() : null;
                var to = root.TryGetProperty("to", out var toEl) ? toEl.GetString() : null;

                // 1) Darf nicht fehlgeschlagen sein (pending/mined sind ok).
                if (string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Zahlung status=failed Tx={Tx}", transactionId);
                    return false;
                }

                // 2) reference muss zu DIESEM Listing gehören (Frontend: "listing-{id}-{ts}").
                if (string.IsNullOrWhiteSpace(reference) ||
                    !reference.StartsWith($"listing-{listingId}-", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Zahlungs-reference passt nicht zum Listing. ref={Ref} listing={Listing}", reference, listingId);
                    return false;
                }

                // 3) Empfänger (falls von der API geliefert) muss die erwartete Wallet sein
                //    (Modell B: die Plattform-Wallet). Verhindert Zahlungen an beliebige Adressen.
                if (!string.IsNullOrWhiteSpace(to) && !string.IsNullOrWhiteSpace(expectedRecipient)
                    && !string.Equals(to.Trim(), expectedRecipient.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Zahlungs-Empfänger != erwartete Wallet. to={To}", to);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Zahlungsverifizierung Fehler für Tx {Tx} -> fail-closed.", transactionId);
                return false;
            }
        }

        /// <summary>
        /// Prueft via World Chain RPC ob die Transaktion existiert und erfolgreich war (status=0x1).
        /// Versucht bis zu 3x mit je 3s Wartezeit (TX koennte noch pending sein).
        /// HINWEIS: Aktuell ungenutzt – die Zahlungsprüfung läuft über VerifyMiniKitPaymentAsync.
        /// </summary>
        private async Task<bool> VerifyTransactionOnChainAsync(string txHash)
        {
            const int maxAttempts = 3;
            const int delayMs = 3000;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                    var rpcRequest = new
                    {
                        jsonrpc = "2.0",
                        method = "eth_getTransactionReceipt",
                        @params = new[] { txHash },
                        id = 1
                    };

                    var response = await httpClient.PostAsJsonAsync(WorldChainRpcUrl, rpcRequest);
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError("World Chain RPC Fehler: HTTP {StatusCode} (Versuch {Attempt}/{Max})", response.StatusCode, attempt, maxAttempts);
                        if (attempt < maxAttempts) await Task.Delay(delayMs);
                        continue;
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    // Kein result oder null = TX noch pending oder existiert nicht
                    if (!root.TryGetProperty("result", out var result) || result.ValueKind == JsonValueKind.Null)
                    {
                        _logger.LogWarning("TX {TxHash} noch nicht bestaetigt (Versuch {Attempt}/{Max}).", txHash, attempt, maxAttempts);
                        if (attempt < maxAttempts) await Task.Delay(delayMs);
                        continue;
                    }

                    // Status pruefen: 0x1 = success, 0x0 = reverted
                    if (result.TryGetProperty("status", out var status))
                    {
                        var statusValue = status.GetString();
                        if (statusValue != "0x1")
                        {
                            _logger.LogWarning("TX {TxHash} ist fehlgeschlagen (status={Status}).", txHash, statusValue);
                            return false;
                        }
                    }

                    _logger.LogInformation("TX {TxHash} auf World Chain bestaetigt.", txHash);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fehler bei On-Chain Verifizierung fuer TX {TxHash} (Versuch {Attempt}/{Max})", txHash, attempt, maxAttempts);
                    if (attempt < maxAttempts) await Task.Delay(delayMs);
                }
            }

            _logger.LogError("TX {TxHash} konnte nach {Max} Versuchen nicht auf World Chain bestaetigt werden.", txHash, maxAttempts);
            return false;
        }
    }
}
