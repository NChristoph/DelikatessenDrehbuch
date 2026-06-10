using DelikatessenDrehbuch.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    /// <summary>
    /// API Controller for Node.js Trading Agent Service
    /// </summary>
    [ApiController]
    [Route("api")]
    public class AgentApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AgentApiController> _logger;

        public AgentApiController(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<AgentApiController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Shared-Secret-Pruefung fuer den internen Node.js Trading-Agent-Service.
        /// Wird nur erzwungen, wenn TradingAgent:InternalApiKey konfiguriert ist
        /// (nicht-breaking fuer bestehende Deployments). Diese Endpunkte geben u.a.
        /// verschluesselte Private Keys heraus und MUESSEN in Produktion abgesichert sein.
        /// </summary>
        private bool IsServiceAuthorized()
        {
            var configuredKey = _configuration["TradingAgent:InternalApiKey"];
            if (string.IsNullOrWhiteSpace(configuredKey))
            {
                _logger.LogWarning(
                    "AgentApi request not authenticated: TradingAgent:InternalApiKey is not configured. " +
                    "These endpoints expose wallet key material and must be locked down.");
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
        /// GET /api/agent/:agentId
        /// Get agent configuration for Node.js service
        /// </summary>
        [HttpGet("agent/{agentId}")]
        public async Task<IActionResult> GetAgent(int agentId)
        {
            if (!IsServiceAuthorized())
            {
                return Unauthorized(new { error = "Unauthorized" });
            }

            try
            {
                var agent = await _context.WorldTradingAgents.FindAsync(agentId);

                if (agent == null)
                {
                    return NotFound(new { error = "Agent not found" });
                }

                return Ok(new
                {
                    id = agent.Id,
                    walletAddress = agent.WalletAddress,
                    encryptedPrivateKey = agent.EncryptedPrivateKey,
                    currentBalanceWLD = agent.CurrentBalanceWLD,
                    currentBalanceETH = agent.CurrentBalanceETH,
                    initialFundingWLD = agent.InitialFundingWLD,
                    riskLevel = agent.RiskLevel,
                    strategy = agent.Strategy,
                    status = agent.Status
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get agent {AgentId}", agentId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// GET /api/prices/recent?limit=X
        /// Get recent price history
        /// </summary>
        [HttpGet("prices/recent")]
        public async Task<IActionResult> GetRecentPrices([FromQuery] int limit = 24)
        {
            if (!IsServiceAuthorized())
            {
                return Unauthorized(new { error = "Unauthorized" });
            }

            try
            {
                var prices = await _context.WorldTokenPrices
                    .OrderByDescending(p => p.Timestamp)
                    .Take(limit)
                    .OrderBy(p => p.Timestamp) // Oldest first for trend calculation
                    .Select(p => new
                    {
                        price = p.Price,
                        timestamp = p.Timestamp
                    })
                    .ToListAsync();

                return Ok(prices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get recent prices");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// POST /api/prices
        /// Save current price to database
        /// </summary>
        [HttpPost("prices")]
        public async Task<IActionResult> SavePrice([FromBody] SavePriceRequest request)
        {
            if (!IsServiceAuthorized())
            {
                return Unauthorized(new { error = "Unauthorized" });
            }

            try
            {
                var price = new Models.WorldTokenPrice
                {
                    TokenIn = request.TokenIn,
                    TokenOut = request.TokenOut,
                    Price = request.Price,
                    Timestamp = request.Timestamp ?? DateTime.UtcNow
                };

                _context.WorldTokenPrices.Add(price);
                await _context.SaveChangesAsync();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save price");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// POST /api/agent/:agentId/signal
        /// Update agent's last trading signal
        /// </summary>
        [HttpPost("agent/{agentId}/signal")]
        public async Task<IActionResult> UpdateSignal(int agentId, [FromBody] UpdateSignalRequest request)
        {
            if (!IsServiceAuthorized())
            {
                return Unauthorized(new { error = "Unauthorized" });
            }

            try
            {
                var agent = await _context.WorldTradingAgents.FindAsync(agentId);

                if (agent == null)
                {
                    return NotFound(new { error = "Agent not found" });
                }

                agent.LastSignalAction = request.Action;
                agent.LastSignalConfidence = request.Confidence;
                agent.LastSignalAt = DateTime.UtcNow;
                agent.LastSignalReason = request.Reason;

                await _context.SaveChangesAsync();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update signal for agent {AgentId}", agentId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// POST /api/agent/:agentId/balances
        /// Update agent balances after trade
        /// </summary>
        [HttpPost("agent/{agentId}/balances")]
        public async Task<IActionResult> UpdateBalances(int agentId, [FromBody] UpdateBalancesRequest request)
        {
            if (!IsServiceAuthorized())
            {
                return Unauthorized(new { error = "Unauthorized" });
            }

            try
            {
                var agent = await _context.WorldTradingAgents.FindAsync(agentId);

                if (agent == null)
                {
                    return NotFound(new { error = "Agent not found" });
                }

                agent.CurrentBalanceWLD = request.BalanceWLD;
                agent.CurrentBalanceETH = request.BalanceETH;
                agent.LastBalanceUpdate = DateTime.UtcNow;

                // Update profit/loss
                agent.TotalProfitLossWLD = agent.CurrentBalanceWLD - agent.InitialFundingWLD;
                agent.ProfitLossPercentage = agent.InitialFundingWLD > 0
                    ? (agent.TotalProfitLossWLD / agent.InitialFundingWLD) * 100
                    : 0;

                await _context.SaveChangesAsync();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update balances for agent {AgentId}", agentId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// POST /api/agent/:agentId/trades
        /// Log trade to database
        /// </summary>
        [HttpPost("agent/{agentId}/trades")]
        public async Task<IActionResult> LogTrade(int agentId, [FromBody] LogTradeRequest request)
        {
            if (!IsServiceAuthorized())
            {
                return Unauthorized(new { error = "Unauthorized" });
            }

            try
            {
                var agent = await _context.WorldTradingAgents.FindAsync(agentId);

                if (agent == null)
                {
                    return NotFound(new { error = "Agent not found" });
                }

                var trade = new Models.WorldAgentTrade
                {
                    AgentId = agentId,
                    TradeType = request.TradeType,
                    FromToken = request.TradeType == "BUY"
                        ? _configuration["WorldChain:WethAddress"] ?? ""
                        : _configuration["WorldChain:WldTokenAddress"] ?? "",
                    ToToken = request.TradeType == "BUY"
                        ? _configuration["WorldChain:WldTokenAddress"] ?? ""
                        : _configuration["WorldChain:WethAddress"] ?? "",
                    FromAmount = request.FromAmount,
                    ToAmount = request.ToAmount,
                    Status = request.Status,
                    TxHash = request.TxHash,
                    ErrorMessage = request.ErrorMessage,
                    ExecutedAt = DateTime.UtcNow,
                    DexUsed = "Uniswap-V3-WorldChain-NodeJS"
                };

                _context.WorldAgentTrades.Add(trade);

                // Update agent stats
                agent.TotalTrades++;
                agent.LastTradeAt = DateTime.UtcNow;

                if (request.Status == "Success")
                {
                    agent.SuccessfulTrades++;
                }

                await _context.SaveChangesAsync();

                return Ok(new { success = true, tradeId = trade.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log trade for agent {AgentId}", agentId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }

    // Request DTOs
    public class SavePriceRequest
    {
        public string TokenIn { get; set; } = string.Empty;
        public string TokenOut { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public DateTime? Timestamp { get; set; }
    }

    public class UpdateSignalRequest
    {
        public string Action { get; set; } = string.Empty;
        public int Confidence { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class UpdateBalancesRequest
    {
        public decimal BalanceWLD { get; set; }
        public decimal BalanceETH { get; set; }
    }

    public class LogTradeRequest
    {
        public string TradeType { get; set; } = string.Empty;
        public decimal FromAmount { get; set; }
        public decimal ToAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? TxHash { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
