using DelikatessenDrehbuch.Areas.WorldMiniApp.Services;
using DelikatessenDrehbuch.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    [Area("WorldMiniApp")]
    public class TradingAgentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly TradingAgentService _agentService;
        private readonly ILogger<TradingAgentController> _logger;

        public TradingAgentController(
            ApplicationDbContext context,
            TradingAgentService agentService,
            ILogger<TradingAgentController> logger)
        {
            _context = context;
            _agentService = agentService;
            _logger = logger;
        }

        private string ResolveUserHash()
        {
            return Request.Cookies["WorldMiniAppUserHash"] ?? string.Empty;
        }

        /// <summary>
        /// GET: Agent Status für User
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAgentStatus()
        {
            var userHash = ResolveUserHash();
            if (string.IsNullOrEmpty(userHash))
            {
                return Json(new { success = false, error = "Not authenticated" });
            }

            var agent = await _agentService.GetAgentByUserAsync(userHash);

            if (agent == null)
            {
                return Json(new { success = true, hasAgent = false });
            }

            // Trading History
            var trades = await _agentService.GetTradeHistoryAsync(agent.Id, 10);

            return Json(new
            {
                success = true,
                hasAgent = true,
                agent = new
                {
                    id = agent.Id,
                    name = agent.AgentName,
                    walletAddress = agent.WalletAddress,
                    status = agent.Status,
                    initialFunding = agent.InitialFundingWLD,
                    currentBalance = agent.CurrentBalanceWLD,
                    currentBalanceETH = agent.CurrentBalanceETH,
                    autoSwapThreshold = agent.AutoSwapThresholdETH,
                    autoSwapAmount = agent.AutoSwapAmountWLD,
                    profitLoss = agent.TotalProfitLossWLD,
                    profitLossPercent = agent.ProfitLossPercentage,
                    totalTrades = agent.TotalTrades,
                    successfulTrades = agent.SuccessfulTrades,
                    strategy = agent.Strategy,
                    riskLevel = agent.RiskLevel,
                    stopLossPercent = agent.StopLossPercentage,
                    createdAt = agent.CreatedAt,
                    startedAt = agent.StartedAt,
                    lastTradeAt = agent.LastTradeAt
                },
                recentTrades = trades.Select(t => new
                {
                    id = t.Id,
                    type = t.TradeType,
                    fromToken = t.FromToken,
                    toToken = t.ToToken,
                    fromAmount = t.FromAmount,
                    toAmount = t.ToAmount,
                    profitLoss = t.ProfitLossWLD,
                    status = t.Status,
                    executedAt = t.ExecutedAt,
                    txHash = t.TxHash
                })
            });
        }

        /// <summary>
        /// POST: Erstelle neuen Agent
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAgent([FromForm] string? agentName)
        {
            var userHash = ResolveUserHash();
            if (string.IsNullOrEmpty(userHash))
            {
                return Json(new { success = false, error = "Not authenticated" });
            }

            try
            {
                // Prüfe ob User bereits einen Agent hat
                var existingAgent = await _agentService.GetAgentByUserAsync(userHash);
                if (existingAgent != null)
                {
                    return Json(new { success = false, error = "Du hast bereits einen Trading Agent" });
                }

                // Erstelle Agent
                var agent = await _agentService.CreateAgentAsync(userHash, agentName);

                _logger.LogInformation("User {UserHash} created trading agent {AgentId}", userHash, agent.Id);

                return Json(new
                {
                    success = true,
                    agent = new
                    {
                        id = agent.Id,
                        name = agent.AgentName,
                        walletAddress = agent.WalletAddress,
                        status = agent.Status
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating trading agent for user {UserHash}", userHash);
                return Json(new { success = false, error = "Fehler beim Erstellen des Agenten" });
            }
        }

        /// <summary>
        /// POST: Starte Agent
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartAgent([FromForm] int agentId)
        {
            var userHash = ResolveUserHash();
            if (string.IsNullOrEmpty(userHash))
            {
                return Json(new { success = false, error = "Not authenticated" });
            }

            try
            {
                var result = await _agentService.StartAgentAsync(agentId, userHash);

                if (!result)
                {
                    return Json(new { success = false, error = "Agent konnte nicht gestartet werden. Prüfe Funding." });
                }

                _logger.LogInformation("User {UserHash} started trading agent {AgentId}", userHash, agentId);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting trading agent {AgentId}", agentId);
                return Json(new { success = false, error = "Fehler beim Starten" });
            }
        }

        /// <summary>
        /// POST: Pausiere Agent
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PauseAgent([FromForm] int agentId)
        {
            var userHash = ResolveUserHash();
            if (string.IsNullOrEmpty(userHash))
            {
                return Json(new { success = false, error = "Not authenticated" });
            }

            try
            {
                var result = await _agentService.PauseAgentAsync(agentId, userHash);

                if (!result)
                {
                    return Json(new { success = false, error = "Agent nicht gefunden" });
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pausing trading agent {AgentId}", agentId);
                return Json(new { success = false, error = "Fehler beim Pausieren" });
            }
        }

        /// <summary>
        /// POST: Stoppe Agent
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StopAgent([FromForm] int agentId)
        {
            var userHash = ResolveUserHash();
            if (string.IsNullOrEmpty(userHash))
            {
                return Json(new { success = false, error = "Not authenticated" });
            }

            try
            {
                var result = await _agentService.StopAgentAsync(agentId, userHash);

                if (!result)
                {
                    return Json(new { success = false, error = "Agent nicht gefunden" });
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping trading agent {AgentId}", agentId);
                return Json(new { success = false, error = "Fehler beim Stoppen" });
            }
        }

        /// <summary>
        /// POST: Update Funding (wird nach erfolgreichem Transfer aufgerufen)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmFunding([FromForm] int agentId, [FromForm] decimal amount, [FromForm] string txHash)
        {
            var userHash = ResolveUserHash();
            if (string.IsNullOrEmpty(userHash))
            {
                return Json(new { success = false, error = "Not authenticated" });
            }

            try
            {
                // Prüfe ob Agent dem User gehört
                var agent = await _context.WorldTradingAgents
                    .FirstOrDefaultAsync(a => a.Id == agentId && a.UserHash == userHash);

                if (agent == null)
                {
                    return Json(new { success = false, error = "Agent nicht gefunden" });
                }

                // Update Funding
                await _agentService.UpdateFundingAsync(agentId, amount);

                _logger.LogInformation("User {UserHash} funded agent {AgentId} with {Amount} WLD, TX: {TxHash}",
                    userHash, agentId, amount, txHash);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming funding for agent {AgentId}", agentId);
                return Json(new { success = false, error = "Fehler beim Bestätigen" });
            }
        }

        /// <summary>
        /// POST: Manual Auto-Swap (WLD → ETH für Gas)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SwapWLDtoETH([FromForm] int agentId, [FromForm] decimal amountWLD)
        {
            var userHash = ResolveUserHash();
            if (string.IsNullOrEmpty(userHash))
            {
                return Json(new { success = false, error = "Not authenticated" });
            }

            try
            {
                // Prüfe ob Agent dem User gehört
                var agent = await _context.WorldTradingAgents
                    .FirstOrDefaultAsync(a => a.Id == agentId && a.UserHash == userHash);

                if (agent == null)
                {
                    return Json(new { success = false, error = "Agent nicht gefunden" });
                }

                // Swap WLD zu ETH
                var success = await _agentService.SwapWLDtoETHAsync(agentId, amountWLD);

                if (!success)
                {
                    return Json(new { success = false, error = "Swap fehlgeschlagen" });
                }

                _logger.LogInformation("User {UserHash} manually swapped {Amount} WLD to ETH for agent {AgentId}",
                    userHash, amountWLD, agentId);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error swapping WLD to ETH for agent {AgentId}", agentId);
                return Json(new { success = false, error = "Fehler beim Swap" });
            }
        }

        /// <summary>
        /// POST: Withdraw Funds vom Agent
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> WithdrawFunds([FromForm] int agentId, [FromForm] decimal amount)
        {
            var userHash = ResolveUserHash();
            if (string.IsNullOrEmpty(userHash))
            {
                return Json(new { success = false, error = "Not authenticated" });
            }

            try
            {
                var (success, error) = await _agentService.WithdrawFundsAsync(agentId, userHash, amount);

                if (!success)
                {
                    return Json(new { success = false, error });
                }

                _logger.LogInformation("User {UserHash} withdrew {Amount} WLD from agent {AgentId}",
                    userHash, amount, agentId);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error withdrawing funds from agent {AgentId}", agentId);
                return Json(new { success = false, error = "Fehler beim Abheben" });
            }
        }
    }
}
