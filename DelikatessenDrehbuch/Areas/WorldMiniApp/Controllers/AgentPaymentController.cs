using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services;
using DelikatessenDrehbuch.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    [Area("WorldMiniApp")]
    [Route("WorldMiniApp/[controller]/[action]")]
    public class AgentPaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AgentPaymentController> _logger;
        private readonly IConfiguration _configuration;

        public AgentPaymentController(ApplicationDbContext context, ILogger<AgentPaymentController> logger, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Shared-Secret-Prüfung für den internen Node.js Agent-Service (gleiches Muster
        /// wie <c>AgentApiController</c>). Wird nur erzwungen, wenn TradingAgent:InternalApiKey
        /// konfiguriert ist (nicht-breaking für bestehende Deployments). InitiatePayment legt
        /// Auszahlungs-Aufträge an und MUSS in Produktion abgesichert sein.
        /// </summary>
        private bool IsServiceAuthorized()
        {
            var configuredKey = _configuration["TradingAgent:InternalApiKey"];
            if (string.IsNullOrWhiteSpace(configuredKey))
            {
                _logger.LogWarning(
                    "AgentPayment request not authenticated: TradingAgent:InternalApiKey is not configured. " +
                    "InitiatePayment must be locked down in production.");
                return true;
            }

            var providedKey = Request.Headers["X-Agent-Api-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(providedKey))
            {
                return false;
            }

            var expected = Encoding.UTF8.GetBytes(configuredKey);
            var actual = Encoding.UTF8.GetBytes(providedKey);
            return expected.Length == actual.Length
                && CryptographicOperations.FixedTimeEquals(expected, actual);
        }

        /// <summary>
        /// Agent initiiert Zahlung an User
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> InitiatePayment([FromBody] InitiateAgentPaymentRequest request)
        {
            try
            {
                if (!IsServiceAuthorized())
                {
                    return Unauthorized(new { error = "Nicht autorisiert" });
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(new { error = "Ungültige Request-Daten" });
                }

                // User prüfen
                var user = await _context.WorldAppUser
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserHash == request.RecipientUserHash);

                if (user == null)
                {
                    return NotFound(new { error = "User nicht gefunden" });
                }

                // Wallet-Adresse (TODO: Aus User-Profil oder Session holen)
                var walletAddress = user.WalletAddress ?? "";
                if (string.IsNullOrWhiteSpace(walletAddress))
                {
                    return BadRequest(new { error = "User hat keine Wallet-Adresse hinterlegt" });
                }

                // Token-Validierung: Nur native Token erlaubt
                var tokenSymbol = request.TokenSymbol.ToUpperInvariant();

                // WLD ist NICHT nativ auf World Chain - nur als ERC-20 verfügbar
                if (tokenSymbol == "WLD")
                {
                    _logger.LogWarning($"Agent versuchte WLD-Payment zu initiieren - WLD ist nicht nativ auf World Chain. Agent: {request.InitiatedBy}");
                    return BadRequest(new
                    {
                        error = "WLD ist nicht nativ auf World Chain verfügbar",
                        details = "WLD existiert nur als ERC-20 Token auf World Chain. Für native Payments bitte WETH verwenden.",
                        nativeTokens = new[] { "WETH" },
                        erc20Tokens = new[] { "WLD", "USDC", "USDCE" }
                    });
                }

                // Nur WETH ist nativ auf World Chain
                var supportedNativeTokens = new[] { "WETH" };
                if (!supportedNativeTokens.Contains(tokenSymbol))
                {
                    _logger.LogWarning($"Agent versuchte nicht-unterstütztes Token zu senden: {tokenSymbol}. Agent: {request.InitiatedBy}");
                    return BadRequest(new
                    {
                        error = $"Token '{tokenSymbol}' wird für Agent-Payments nicht unterstützt",
                        supportedTokens = supportedNativeTokens
                    });
                }

                // Payment Request erstellen
                var reference = $"agent-pay-{Guid.NewGuid():N}";
                var payment = new AgentPaymentRequest
                {
                    RecipientUserHash = request.RecipientUserHash,
                    RecipientWalletAddress = walletAddress,
                    TokenSymbol = request.TokenSymbol.ToUpperInvariant(),
                    Amount = request.Amount,
                    Description = request.Description,
                    Reference = reference,
                    Status = "pending",
                    InitiatedBy = request.InitiatedBy ?? "agent-system",
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30) // 30 Min gültig
                };

                _context.AgentPaymentRequests.Add(payment);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Agent Payment initiiert: {reference} → {request.RecipientUserHash} ({request.Amount} {request.TokenSymbol})");

                return Ok(new
                {
                    success = true,
                    paymentId = payment.Id,
                    reference = payment.Reference,
                    recipientWallet = payment.RecipientWalletAddress,
                    amount = payment.Amount,
                    tokenSymbol = payment.TokenSymbol,
                    description = payment.Description,
                    expiresAt = payment.ExpiresAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Initiieren von Agent Payment");
                return StatusCode(500, new { error = "Interner Server-Fehler" });
            }
        }

        /// <summary>
        /// User bestätigt Zahlung (nach MiniKit.pay() Success)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ConfirmPayment([FromBody] ConfirmAgentPaymentRequest request)
        {
            try
            {
                var currentUserHash = WorldMiniAppUserHashHelper.Resolve(HttpContext);
                if (string.IsNullOrWhiteSpace(currentUserHash))
                {
                    return Unauthorized(new { error = "Nicht angemeldet" });
                }

                var payment = await _context.AgentPaymentRequests
                    .FirstOrDefaultAsync(p => p.Reference == request.Reference);

                if (payment == null)
                {
                    return NotFound(new { error = "Payment nicht gefunden" });
                }

                // Nur der Empfänger darf seine eigene Zahlung bestätigen.
                if (!string.Equals(payment.RecipientUserHash, currentUserHash, StringComparison.Ordinal))
                {
                    return Forbid();
                }

                if (payment.Status != "pending")
                {
                    return BadRequest(new { error = $"Payment hat bereits Status: {payment.Status}" });
                }

                payment.Status = request.Status;
                payment.TransactionHash = request.TransactionHash;
                payment.ConfirmedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Agent Payment bestätigt: {request.Reference} → TxHash: {request.TransactionHash}");

                return Ok(new
                {
                    success = true,
                    reference = payment.Reference,
                    transactionHash = payment.TransactionHash,
                    confirmedAt = payment.ConfirmedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Bestätigen von Agent Payment");
                return StatusCode(500, new { error = "Interner Server-Fehler" });
            }
        }

        /// <summary>
        /// Payment als fehlgeschlagen markieren
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> FailPayment([FromBody] ConfirmAgentPaymentRequest request)
        {
            try
            {
                var currentUserHash = WorldMiniAppUserHashHelper.Resolve(HttpContext);
                if (string.IsNullOrWhiteSpace(currentUserHash))
                {
                    return Unauthorized(new { error = "Nicht angemeldet" });
                }

                var payment = await _context.AgentPaymentRequests
                    .FirstOrDefaultAsync(p => p.Reference == request.Reference);

                if (payment == null)
                {
                    return NotFound(new { error = "Payment nicht gefunden" });
                }

                // Nur der Empfänger darf seine eigene Zahlung als fehlgeschlagen markieren.
                if (!string.Equals(payment.RecipientUserHash, currentUserHash, StringComparison.Ordinal))
                {
                    return Forbid();
                }

                payment.Status = "failed";
                await _context.SaveChangesAsync();

                _logger.LogWarning($"Agent Payment fehlgeschlagen: {request.Reference}");

                return Ok(new { success = true, status = "failed" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Markieren von Payment als failed");
                return StatusCode(500, new { error = "Interner Server-Fehler" });
            }
        }

        /// <summary>
        /// Ausstehende Payments für User abrufen
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetPendingPayments()
        {
            try
            {
                var userHash = WorldMiniAppUserHashHelper.Resolve(HttpContext);
                if (string.IsNullOrWhiteSpace(userHash))
                {
                    return Unauthorized(new { error = "Nicht angemeldet" });
                }

                var pendingPayments = await _context.AgentPaymentRequests
                    .AsNoTracking()
                    .Where(p => p.RecipientUserHash == userHash && p.Status == "pending")
                    .Where(p => p.ExpiresAt == null || p.ExpiresAt > DateTime.UtcNow)
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(p => new
                    {
                        p.Id,
                        p.Reference,
                        p.TokenSymbol,
                        p.Amount,
                        p.Description,
                        p.RecipientWalletAddress,
                        p.InitiatedBy,
                        p.CreatedAt,
                        p.ExpiresAt
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    count = pendingPayments.Count,
                    payments = pendingPayments
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Abrufen von Pending Payments");
                return StatusCode(500, new { error = "Interner Server-Fehler" });
            }
        }

        /// <summary>
        /// Payment-Historie für User
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetPaymentHistory(int take = 20)
        {
            try
            {
                var userHash = WorldMiniAppUserHashHelper.Resolve(HttpContext);
                if (string.IsNullOrWhiteSpace(userHash))
                {
                    return Unauthorized(new { error = "Nicht angemeldet" });
                }

                if (take < 1) take = 1;
                if (take > 100) take = 100;

                var history = await _context.AgentPaymentRequests
                    .AsNoTracking()
                    .Where(p => p.RecipientUserHash == userHash)
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(take)
                    .Select(p => new
                    {
                        p.Reference,
                        p.TokenSymbol,
                        p.Amount,
                        p.Description,
                        p.Status,
                        p.TransactionHash,
                        p.InitiatedBy,
                        p.CreatedAt,
                        p.ConfirmedAt
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    count = history.Count,
                    payments = history
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Abrufen von Payment History");
                return StatusCode(500, new { error = "Interner Server-Fehler" });
            }
        }
    }
}
