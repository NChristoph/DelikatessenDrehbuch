using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Data;
using Microsoft.EntityFrameworkCore;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    public class TradingStrategyService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TradingStrategyService> _logger;
        private readonly DexService _dexService;
        private readonly BlockchainService _blockchainService;

        public TradingStrategyService(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<TradingStrategyService> logger,
            DexService dexService,
            BlockchainService blockchainService)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _dexService = dexService;
            _blockchainService = blockchainService;
        }

        /// <summary>
        /// Analysiert Markt und gibt Trading Signal basierend auf ECHTEN Preis-Daten
        /// </summary>
        public async Task<TradingSignal> AnalyzeMarketAsync(int agentId)
        {
            var agent = await _context.WorldTradingAgents.FindAsync(agentId);
            if (agent == null)
            {
                return new TradingSignal { Action = "HOLD", Confidence = 0, Reason = "Agent not found" };
            }

            try
            {
                var wldTokenAddress = _configuration["WorldChain:WldTokenAddress"] ?? "";
                var wethAddress = _configuration["WorldChain:WethAddress"] ?? "";

                // 1. Hole aktuellen WLD/ETH Preis (von Blockchain oder geschätzt mit Variation)
                _logger.LogInformation("📊 Fetching current WLD/ETH price...");
                var currentPrice = await _dexService.GetTokenPriceAsync(wldTokenAddress, wethAddress, 1m, 3000);

                if (currentPrice <= 0)
                {
                    _logger.LogError("❌ Failed to get price - this should not happen!");
                    return new TradingSignal
                    {
                        Action = "HOLD",
                        Confidence = 30,
                        SuggestedAmount = 0,
                        Reason = "Critical: Price fetching failed"
                    };
                }

                _logger.LogInformation("✅ Current price retrieved: 1 WLD = {Price:F6} ETH", currentPrice);

                // 2. Speichere aktuellen Preis in DB
                var priceEntry = new WorldTokenPrice
                {
                    TokenIn = wldTokenAddress,
                    TokenOut = wethAddress,
                    Price = currentPrice,
                    Timestamp = DateTime.UtcNow
                };
                _context.WorldTokenPrices.Add(priceEntry);
                await _context.SaveChangesAsync();

                _logger.LogInformation("💰 Current price: 1 WLD = {Price} ETH", currentPrice);

                // 3. Hole historische Preise (letzte 2 Stunden)
                var twoHoursAgo = DateTime.UtcNow.AddHours(-2);
                var recentPrices = await _context.WorldTokenPrices
                    .Where(p => p.TokenIn == wldTokenAddress
                             && p.TokenOut == wethAddress
                             && p.Timestamp >= twoHoursAgo)
                    .OrderBy(p => p.Timestamp)
                    .ToListAsync();

                // 4. Berechne Preis-Trend (Momentum)
                decimal priceChange = 0;
                string trendReason = "";

                if (recentPrices.Count >= 2)
                {
                    // Vergleiche mit ältestem Preis in den letzten 2 Stunden
                    var oldestPrice = recentPrices.First().Price;
                    priceChange = ((currentPrice - oldestPrice) / oldestPrice) * 100;

                    trendReason = $"Price trend: {priceChange:+0.00;-0.00}% over {recentPrices.Count} samples ({oldestPrice:F6} → {currentPrice:F6} ETH)";
                    _logger.LogInformation("📈 {TrendReason}", trendReason);
                }
                else
                {
                    // Nicht genug Daten → Neutral bleiben
                    trendReason = $"Not enough price history ({recentPrices.Count} samples), current: {currentPrice:F6} ETH";
                    _logger.LogInformation("⏳ {TrendReason}", trendReason);
                }

                // 5. Risk Level adjustments
                var riskMultiplier = agent.RiskLevel switch
                {
                    "Low" => 0.5m,
                    "Medium" => 1.0m,
                    "High" => 1.5m,
                    _ => 1.0m
                };

                // 6. Generiere Trading Signal basierend auf Preis-Momentum
                if (recentPrices.Count < 2)
                {
                    // Cold Start: Nicht genug Daten für Trend-Analyse
                    return new TradingSignal
                    {
                        Action = "HOLD",
                        Confidence = 55,
                        SuggestedAmount = 0,
                        Reason = $"Collecting price data... ({recentPrices.Count}/2 samples)"
                    };
                }

                // Preis steigt stark → BUY Signal
                if (priceChange > 1.0m) // > 1% Anstieg
                {
                    var confidence = Math.Min(60 + (int)(priceChange * 10), 90); // 60-90%
                    var tradeSize = CalculatePositionSize(agent, riskMultiplier);

                    return new TradingSignal
                    {
                        Action = "BUY",
                        Confidence = confidence,
                        SuggestedAmount = tradeSize,
                        Reason = $"🚀 Strong uptrend: {priceChange:+0.00}% - Good entry point"
                    };
                }
                // Preis fällt stark → SELL Signal (falls WLD vorhanden)
                else if (priceChange < -1.0m && agent.CurrentBalanceWLD > 1m)
                {
                    var confidence = Math.Min(60 + (int)(Math.Abs(priceChange) * 10), 90);
                    var tradeSize = Math.Min(agent.CurrentBalanceWLD * 0.3m, CalculatePositionSize(agent, riskMultiplier));

                    return new TradingSignal
                    {
                        Action = "SELL",
                        Confidence = confidence,
                        SuggestedAmount = tradeSize,
                        Reason = $"📉 Strong downtrend: {priceChange:0.00}% - Take profits/cut losses"
                    };
                }
                // Leichter Aufwärtstrend → Mögliches BUY (niedrigere Confidence)
                else if (priceChange > 0.3m)
                {
                    var confidence = 50 + (int)(priceChange * 20); // 50-70%
                    var tradeSize = CalculatePositionSize(agent, riskMultiplier);

                    return new TradingSignal
                    {
                        Action = "BUY",
                        Confidence = confidence,
                        SuggestedAmount = tradeSize,
                        Reason = $"📊 Mild uptrend: {priceChange:+0.00}% - Conservative buy"
                    };
                }
                // Leichter Abwärtstrend → HOLD (kein Trade)
                else if (priceChange < -0.3m)
                {
                    return new TradingSignal
                    {
                        Action = "HOLD",
                        Confidence = 65,
                        SuggestedAmount = 0,
                        Reason = $"⚠️ Mild downtrend: {priceChange:0.00}% - Waiting for reversal"
                    };
                }
                // Seitwärtsbewegung → HOLD
                else
                {
                    return new TradingSignal
                    {
                        Action = "HOLD",
                        Confidence = 60,
                        SuggestedAmount = 0,
                        Reason = $"➡️ Sideways: {priceChange:+0.00;-0.00}% - No clear trend"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Market analysis failed for agent {AgentId}: {Message}", agentId, ex.Message);
                return new TradingSignal
                {
                    Action = "HOLD",
                    Confidence = 0,
                    Reason = $"Analysis error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Berechnet optimale Position Size basierend auf Risk Management
        /// </summary>
        private decimal CalculatePositionSize(WorldTradingAgent agent, decimal riskMultiplier)
        {
            // Risk 10% of current balance per trade (aggressiver als 2%)
            var riskPerTrade = agent.CurrentBalanceWLD * 0.10m * riskMultiplier;

            // Min 1 WLD, Max 30% of balance (erhöht von 20% auf 30%)
            var minSize = 1m;
            var maxSize = agent.CurrentBalanceWLD * 0.3m;

            var positionSize = Math.Max(minSize, Math.Min(riskPerTrade, maxSize));

            _logger.LogInformation("Position size calculated: {Size} WLD (Risk: {Risk}%, Balance: {Balance})",
                positionSize, riskPerTrade / agent.CurrentBalanceWLD * 100, agent.CurrentBalanceWLD);

            return positionSize;
        }

        /// <summary>
        /// Führt Trade basierend auf Signal aus
        /// </summary>
        public async Task<bool> ExecuteTradeAsync(
            int agentId,
            TradingSignal signal,
            string privateKey,
            string? worldIdHash)
        {
            if (signal.Action == "HOLD" || signal.SuggestedAmount <= 0)
            {
                _logger.LogInformation("No trade executed for agent {AgentId}: {Action}", agentId, signal.Action);
                return false;
            }

            var agent = await _context.WorldTradingAgents.FindAsync(agentId);
            if (agent == null) return false;

            try
            {
                _logger.LogInformation("📈 Executing {Action} trade for agent {AgentId}: {Amount} WLD",
                    signal.Action, agentId, signal.SuggestedAmount);

                var wldTokenAddress = _configuration["WorldChain:WldTokenAddress"]
                    ?? throw new Exception("WLD Token address not configured");
                var wethAddress = _configuration["WorldChain:WethAddress"]
                    ?? throw new Exception("WETH address not configured");

                string fromToken, toToken;
                decimal amountToSwap;

                if (signal.Action == "BUY")
                {
                    // Buy: Swap WETH to WLD
                    fromToken = wethAddress;
                    toToken = wldTokenAddress;

                    var currentPrice = await _dexService.GetTokenPriceAsync(wldTokenAddress, wethAddress, 1m, 3000);
                    if (currentPrice <= 0)
                    {
                        _logger.LogError("Failed to get WLD price for BUY trade");
                        return false;
                    }

                    // Berechne: Wenn ich X WLD kaufen will, brauche ich X * Preis ETH
                    amountToSwap = signal.SuggestedAmount * currentPrice;
                    _logger.LogInformation("💱 BUY: Want {WldAmount} WLD at {Price} ETH/WLD → Need {EthAmount} ETH",
                        signal.SuggestedAmount, currentPrice, amountToSwap);
                }
                else
                {
                    // Sell: Swap WLD to WETH
                    fromToken = wldTokenAddress;
                    toToken = wethAddress;
                    amountToSwap = signal.SuggestedAmount;
                    _logger.LogInformation("💱 SELL: Selling {WldAmount} WLD", signal.SuggestedAmount);
                }

                // ==================== AGENTKIT INTEGRATION ====================
                // Rufe Node.js AgentKit Service auf (zahlt Gas in WLD!)
                _logger.LogInformation("🤖 Calling AgentKit Service for swap execution...");
                _logger.LogInformation("⛽ Gas will be paid in WLD (via AgentKit)");

                var (success, amountOut, txHash, error) = await ExecuteSwapViaAgentKitAsync(
                    privateKey,
                    worldIdHash,
                    fromToken,
                    toToken,
                    amountToSwap
                );

                if (!success)
                {
                    _logger.LogError("Trade execution failed for agent {AgentId}: {Error}", agentId, error);

                    // Log failed trade WITH ERROR MESSAGE
                    var failedTrade = new WorldAgentTrade
                    {
                        AgentId = agentId,
                        TradeType = signal.Action,
                        FromToken = fromToken,
                        ToToken = toToken,
                        FromAmount = signal.SuggestedAmount,
                        ToAmount = 0,
                        Status = "Failed",
                        ErrorMessage = error,
                        ExecutedAt = DateTime.UtcNow,
                        DexUsed = "Uniswap-V3-WorldChain"
                    };
                    _context.WorldAgentTrades.Add(failedTrade);
                    await _context.SaveChangesAsync();

                    return false;
                }

                // Calculate P/L
                var profitLoss = signal.Action == "BUY"
                    ? amountOut - signal.SuggestedAmount // WLD gained
                    : amountOut * 1500 - signal.SuggestedAmount; // ETH value in WLD terms (rough estimate)

                // Update agent balances
                if (signal.Action == "BUY")
                {
                    agent.CurrentBalanceETH -= signal.SuggestedAmount;
                    agent.CurrentBalanceWLD += amountOut;
                }
                else
                {
                    agent.CurrentBalanceWLD -= signal.SuggestedAmount;
                    agent.CurrentBalanceETH += amountOut;
                }

                agent.TotalTrades++;
                if (profitLoss > 0)
                {
                    agent.SuccessfulTrades++;
                }
                agent.TotalProfitLossWLD += profitLoss;
                agent.ProfitLossPercentage = agent.InitialFundingWLD > 0
                    ? (agent.TotalProfitLossWLD / agent.InitialFundingWLD) * 100
                    : 0;
                agent.LastTradeAt = DateTime.UtcNow;

                // Log successful trade
                var trade = new WorldAgentTrade
                {
                    AgentId = agentId,
                    TradeType = signal.Action,
                    FromToken = fromToken,
                    ToToken = toToken,
                    FromAmount = signal.SuggestedAmount,
                    ToAmount = amountOut,
                    GasFee = 0.00001m,
                    ProfitLossWLD = profitLoss,
                    Status = "Success",
                    ExecutedAt = DateTime.UtcNow,
                    TxHash = txHash,
                    DexUsed = "Uniswap-V3-WorldChain"
                };

                _context.WorldAgentTrades.Add(trade);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Trade successful: {AmountOut} received, P/L: {ProfitLoss} WLD",
                    amountOut, profitLoss);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Trade execution exception: {Message}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Execute Swap via Node.js AgentKit Service (pays gas in WLD!)
        /// </summary>
        private async Task<(bool success, decimal amountOut, string txHash, string error)> ExecuteSwapViaAgentKitAsync(
            string privateKey,
            string? worldIdHash,
            string tokenIn,
            string tokenOut,
            decimal amountIn)
        {
            try
            {
                var agentKitUrl = _configuration["AgentKit:ServiceUrl"] ?? "http://localhost:3000";

                // 1. Initialize AgentKit with private key
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromMinutes(2);

                var initPayload = new
                {
                    privateKey = privateKey,
                    worldIdHash = string.IsNullOrWhiteSpace(worldIdHash) ? null : worldIdHash.Trim()
                };

                _logger.LogInformation("🔧 Initializing AgentKit...");
                var initResponse = await httpClient.PostAsJsonAsync($"{agentKitUrl}/api/agentkit/initialize", initPayload);

                if (!initResponse.IsSuccessStatusCode)
                {
                    var errorContent = await initResponse.Content.ReadAsStringAsync();
                    _logger.LogError("AgentKit initialization failed: {Error}", errorContent);
                    return (false, 0, "", $"AgentKit init failed: {errorContent}");
                }

                // 2. Execute swap via AgentKit
                var swapPayload = new
                {
                    tokenIn = tokenIn,
                    tokenOut = tokenOut,
                    amountIn = amountIn.ToString(),
                    slippagePercent = 3
                };

                _logger.LogInformation("🚀 Executing swap via AgentKit (Gas in WLD)...");
                var swapResponse = await httpClient.PostAsJsonAsync($"{agentKitUrl}/api/agentkit/swap", swapPayload);

                var swapResult = await swapResponse.Content.ReadFromJsonAsync<AgentKitSwapResponse>();

                if (swapResult == null)
                {
                    return (false, 0, "", "No response from AgentKit");
                }

                if (!swapResult.Success)
                {
                    _logger.LogError("AgentKit swap failed: {Error}", swapResult.Error);
                    return (false, 0, "", swapResult.Error ?? "Unknown error");
                }

                _logger.LogInformation("✅ AgentKit swap successful! TX: {TxHash}", swapResult.TxHash);
                _logger.LogInformation("⛽ Gas was paid in WLD");

                return (true, decimal.Parse(swapResult.AmountOut ?? "0"), swapResult.TxHash ?? "", "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AgentKit swap exception: {Message}", ex.Message);
                return (false, 0, "", ex.Message);
            }
        }
    }

    /// <summary>
    /// AgentKit Swap Response Model
    /// </summary>
    public class AgentKitSwapResponse
    {
        public bool Success { get; set; }
        public string? TxHash { get; set; }
        public string? AmountOut { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Trading Signal Model
    /// </summary>
    public class TradingSignal
    {
        public string Action { get; set; } = "HOLD"; // BUY, SELL, HOLD
        public decimal Confidence { get; set; } // 0-100
        public decimal SuggestedAmount { get; set; }
        public string Reason { get; set; } = "";
    }
}
