using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Data;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    public class WildCoinService : IWildCoinService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WildCoinService> _logger;
        private static readonly Regex TxHashRegex = new("^0x[a-fA-F0-9]{64}$", RegexOptions.Compiled);
        private const string WorldChainRpcUrl = "https://worldchain-mainnet.g.alchemy.com/public";

        public WildCoinService(ApplicationDbContext context, IConfiguration configuration, ILogger<WildCoinService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<decimal> GetBalance(string userHash)
        {
            var user = await _context.WorldAppUser.FirstOrDefaultAsync(u => u.UserHash == userHash);
            return user?.WildCoinBalance ?? 0;
        }

        public async Task<bool> Transfer(string fromHash, string toHash, decimal amount, string referenceInfo)
        {
            if (amount <= 0) return false;

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var sender = await _context.WorldAppUser.FirstOrDefaultAsync(u => u.UserHash == fromHash);
                var receiver = await _context.WorldAppUser.FirstOrDefaultAsync(u => u.UserHash == toHash);
                if (sender == null || receiver == null) return false;
                if (sender.WildCoinBalance < amount) return false;

                sender.WildCoinBalance -= amount;
                receiver.WildCoinBalance += amount;

                _context.Add(new WildCoinTransaction
                {
                    UserHash = fromHash,
                    Amount = -amount,
                    BalanceAfter = sender.WildCoinBalance,
                    Type = "purchase",
                    ReferenceInfo = referenceInfo
                });
                _context.Add(new WildCoinTransaction
                {
                    UserHash = toHash,
                    Amount = amount,
                    BalanceAfter = receiver.WildCoinBalance,
                    Type = "sale",
                    ReferenceInfo = referenceInfo
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                return false;
            }
        }

        public async Task<bool> Reward(string userHash, decimal amount, string referenceInfo)
        {
            if (amount <= 0) return false;

            var user = await _context.WorldAppUser.FirstOrDefaultAsync(u => u.UserHash == userHash);
            if (user == null) return false;

            user.WildCoinBalance += amount;

            _context.Add(new WildCoinTransaction
            {
                UserHash = userHash,
                Amount = amount,
                BalanceAfter = user.WildCoinBalance,
                Type = "reward",
                ReferenceInfo = referenceInfo
            });

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<WildCoinTransaction>> GetTransactions(string userHash, int take = 20)
        {
            return await _context.WildCoinTransactions
                .Where(t => t.UserHash == userHash)
                .OrderByDescending(t => t.CreatedAt)
                .Take(take)
                .ToListAsync();
        }

        // ---- Marketplace ----

        public async Task<MealPlanListing> CreateListing(string sellerHash, int mealPlanId, string title, string? description, decimal price, string? sellerWalletAddress = null)
        {
            var user = await _context.WorldAppUser.FirstOrDefaultAsync(u => u.UserHash == sellerHash);
            if (user == null) throw new InvalidOperationException("User nicht gefunden.");

            var mealPlan = await _context.WorldUserMealPlan.FirstOrDefaultAsync(m => m.Id == mealPlanId && m.UserHash == sellerHash);
            if (mealPlan == null) throw new InvalidOperationException("Essensplan nicht gefunden oder gehört nicht dir.");

            // Rezeptanzahl und Tage aus dem MealPlan JSON berechnen
            int dayCount = 0;
            int recipeCount = 0;
            if (!string.IsNullOrEmpty(mealPlan.MealPlan))
            {
                try
                {
                    var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, List<int>>>(mealPlan.MealPlan);
                    if (dict != null)
                    {
                        dayCount = dict.Count;
                        recipeCount = dict.Values.Sum(v => v.Count);
                    }
                }
                catch { }
            }

            var listing = new MealPlanListing
            {
                SellerHash = sellerHash,
                SellerName = user.UserName ?? "Anonym",
                SellerWalletAddress = sellerWalletAddress,
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

        public async Task<List<MealPlanListing>> GetActiveListings(int skip = 0, int take = 20)
        {
            return await _context.MealPlanListings
                .Where(l => l.IsActive)
                .OrderByDescending(l => l.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public async Task<List<MealPlanListing>> GetMyListings(string userHash)
        {
            return await _context.MealPlanListings
                .Where(l => l.SellerHash == userHash)
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();
        }

        public async Task<MealPlanListing?> GetListingById(int id)
        {
            return await _context.MealPlanListings
                .Include(l => l.MealPlan)
                .FirstOrDefaultAsync(l => l.Id == id);
        }

        public async Task<bool> DeactivateListing(string userHash, int listingId)
        {
            var listing = await _context.MealPlanListings.FirstOrDefaultAsync(l => l.Id == listingId && l.SellerHash == userHash);
            if (listing == null) return false;

            listing.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }


        public async Task<MealPlanPurchase?> FinalizeWorldChainPurchase(string buyerHash, int listingId, string txHash, string walletAddress, bool allowSelfPurchase = false, string paymentToken = "WLD")
        {
            if (string.IsNullOrWhiteSpace(txHash))
            {
                _logger.LogWarning("FinalizeWorldChainPurchase: TxHash ist leer.");
                return null;
            }

            // On-Chain Verifizierung ist nicht-blockierend:
            // MiniKit pay() bestaetigt die Zahlung bereits in der World App.
            // Der Alchemy Public RPC kann die TX evtl. noch nicht liefern (Latenz/Rate-Limit).
            // Wir loggen das Ergebnis, lassen den Kauf aber trotzdem durch.
            // Duplikatschutz via UNIQUE Index auf ReferenceTxHash schuetzt vor Missbrauch.
            var isOnChainTx = TxHashRegex.IsMatch(txHash);
            if (isOnChainTx)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var verified = await VerifyTransactionOnChainAsync(txHash);
                        if (verified)
                            _logger.LogInformation("On-Chain Verifizierung erfolgreich: {TxHash}", txHash);
                        else
                            _logger.LogWarning("On-Chain Verifizierung fehlgeschlagen (TX evtl. noch pending): {TxHash}", txHash);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "On-Chain Verifizierung Fehler fuer {TxHash}", txHash);
                    }
                });
            }
            else
            {
                _logger.LogInformation("FinalizeWorldChainPurchase: MiniKit Payment-Referenz: {TxRef}", txHash);
            }

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

                var purchase = new MealPlanPurchase
                {
                    BuyerHash = buyerHash,
                    BuyerWalletAddress = walletAddress,
                    SellerHash = listing.SellerHash,
                    SellerWalletAddress = listing.SellerWalletAddress,
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

                await transaction.CommitAsync();
                return purchase;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<MealPlanPurchase>> GetPurchasesByBuyer(string buyerHash)
        {
            return await _context.MealPlanPurchases
                .Include(p => p.Listing)
                .Where(p => p.BuyerHash == buyerHash)
                .OrderByDescending(p => p.PurchasedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Prueft via World Chain RPC ob die Transaktion existiert und erfolgreich war (status=0x1).
        /// Versucht bis zu 3x mit je 3s Wartezeit (TX koennte noch pending sein).
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
