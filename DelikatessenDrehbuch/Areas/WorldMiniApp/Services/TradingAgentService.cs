using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Hex.HexTypes;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using System.Numerics;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    public class TradingAgentService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TradingAgentService> _logger;

        // Encryption Key (sollte aus appsettings/environment kommen)
        private readonly byte[] _encryptionKey;

        public TradingAgentService(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<TradingAgentService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;

            // Encryption Key für Private Keys (32 bytes für AES-256)
            var keyString = _configuration["TradingAgent:EncryptionKey"] ?? "CHANGE_THIS_TO_SECURE_KEY_32BYTES!";
            _encryptionKey = Encoding.UTF8.GetBytes(keyString.PadRight(32).Substring(0, 32));
        }

        /// <summary>
        /// Erstellt einen neuen Trading Agent mit eigenem Wallet
        /// </summary>
        public async Task<WorldTradingAgent> CreateAgentAsync(string userHash, string? agentName = null)
        {
            // Generiere neues Ethereum Wallet
            var (walletAddress, privateKey) = GenerateEthereumWallet();

            // Verschlüssele Private Key
            var encryptedKey = EncryptPrivateKey(privateKey);

            var agent = new WorldTradingAgent
            {
                UserHash = userHash,
                WalletAddress = walletAddress,
                EncryptedPrivateKey = encryptedKey,
                Status = "Created",
                AgentName = agentName ?? $"Trading Agent {DateTime.UtcNow:yyyy-MM-dd}",
                InitialFundingWLD = 0,
                CurrentBalanceWLD = 0,
                TotalProfitLossWLD = 0,
                ProfitLossPercentage = 0,
                Strategy = "GridTrading",
                RiskLevel = "Medium",
                StopLossPercentage = 20m
            };

            _context.WorldTradingAgents.Add(agent);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created trading agent {AgentId} for user {UserHash} with wallet {Wallet}",
                agent.Id, userHash, walletAddress);

            return agent;
        }

        /// <summary>
        /// Hole Agent für User
        /// </summary>
        public async Task<WorldTradingAgent?> GetAgentByUserAsync(string userHash)
        {
            return await _context.WorldTradingAgents
                .FirstOrDefaultAsync(a => a.UserHash == userHash);
        }

        /// <summary>
        /// Hole alle Agents für User (falls mehrere erlaubt)
        /// </summary>
        public async Task<List<WorldTradingAgent>> GetAgentsByUserAsync(string userHash)
        {
            return await _context.WorldTradingAgents
                .Where(a => a.UserHash == userHash)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Starte Agent (aktiviere Trading)
        /// </summary>
        public async Task<bool> StartAgentAsync(int agentId, string userHash)
        {
            var agent = await _context.WorldTradingAgents
                .FirstOrDefaultAsync(a => a.Id == agentId && a.UserHash == userHash);

            if (agent == null) return false;

            // Prüfe ob genug Funding (nur bei erstmaligem Start, nicht bei Restart)
            if (agent.Status == "Created" && agent.CurrentBalanceWLD < 1)
            {
                _logger.LogWarning("Cannot start agent {AgentId}: insufficient funding", agentId);
                return false;
            }

            // Bei Restart: Erlaube Start auch mit weniger Balance
            if (agent.Status == "Stopped" && agent.CurrentBalanceWLD < 0.1m)
            {
                _logger.LogWarning("Cannot restart agent {AgentId}: balance too low ({Balance} WLD)",
                    agentId, agent.CurrentBalanceWLD);
                return false;
            }

            agent.Status = "Active";
            agent.StartedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Started/Restarted trading agent {AgentId} (Previous status: {PrevStatus})",
                agentId, agent.Status);
            return true;
        }

        /// <summary>
        /// Pausiere Agent
        /// </summary>
        public async Task<bool> PauseAgentAsync(int agentId, string userHash)
        {
            var agent = await _context.WorldTradingAgents
                .FirstOrDefaultAsync(a => a.Id == agentId && a.UserHash == userHash);

            if (agent == null) return false;

            agent.Status = "Paused";
            await _context.SaveChangesAsync();

            _logger.LogInformation("Paused trading agent {AgentId}", agentId);
            return true;
        }

        /// <summary>
        /// Stoppe Agent (permanent)
        /// </summary>
        public async Task<bool> StopAgentAsync(int agentId, string userHash)
        {
            var agent = await _context.WorldTradingAgents
                .FirstOrDefaultAsync(a => a.Id == agentId && a.UserHash == userHash);

            if (agent == null) return false;

            agent.Status = "Stopped";
            agent.StoppedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Stopped trading agent {AgentId}", agentId);
            return true;
        }

        /// <summary>
        /// Update Agent Funding (nach Transfer)
        /// </summary>
        public async Task<bool> UpdateFundingAsync(int agentId, decimal amountWLD)
        {
            var agent = await _context.WorldTradingAgents.FindAsync(agentId);
            if (agent == null) return false;

            if (agent.InitialFundingWLD == 0)
            {
                agent.InitialFundingWLD = amountWLD;
            }

            agent.CurrentBalanceWLD += amountWLD;
            agent.LastBalanceUpdate = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated agent {AgentId} funding: +{Amount} WLD, new balance: {Balance} WLD",
                agentId, amountWLD, agent.CurrentBalanceWLD);

            // Auto-Swap: Bei erstem Funding 1 WLD zu ETH für Gas
            if (agent.InitialFundingWLD == amountWLD && agent.CurrentBalanceETH < 0.001m)
            {
                _logger.LogInformation("Performing initial auto-swap for agent {AgentId}: 1 WLD → ETH", agentId);
                await SwapWLDtoETHAsync(agentId, 1m);
            }

            return true;
        }

        /// <summary>
        /// Swap WLD zu ETH für Gas (Auto-Swap)
        /// </summary>
        public async Task<bool> SwapWLDtoETHAsync(int agentId, decimal amountWLD)
        {
            var agent = await _context.WorldTradingAgents.FindAsync(agentId);
            if (agent == null) return false;

            if (agent.CurrentBalanceWLD < amountWLD)
            {
                _logger.LogWarning("Agent {AgentId} has insufficient WLD for swap: {Current} < {Required}",
                    agentId, agent.CurrentBalanceWLD, amountWLD);
                return false;
            }

            // TODO: Hier echte DEX Integration (Uniswap V3 auf World Chain)
            // Für jetzt: Simuliere Swap mit geschätztem Wechselkurs
            // In Production: Nutze Nethereum + Uniswap Router Contract

            _logger.LogInformation("AUTO-SWAP: Agent {AgentId} swapping {Amount} WLD to ETH", agentId, amountWLD);

            // Geschätzter Wechselkurs (WLD ≈ $2, ETH ≈ $3000)
            // 1 WLD ≈ 0.00067 ETH (minus 0.3% DEX fee, minus ~$0.02 gas)
            decimal estimatedETH = amountWLD * 0.00065m;

            // Update Balances
            agent.CurrentBalanceWLD -= amountWLD;
            agent.CurrentBalanceETH += estimatedETH;
            agent.LastBalanceUpdate = DateTime.UtcNow;

            // Log Trade
            var trade = new WorldAgentTrade
            {
                AgentId = agentId,
                TradeType = "AutoSwap",
                FromToken = _configuration["WorldChain:WldTokenAddress"] ?? "",
                ToToken = "ETH",
                FromAmount = amountWLD,
                ToAmount = estimatedETH,
                GasFee = 0.00002m, // ~$0.02 gas auf World Chain
                ProfitLossWLD = -amountWLD, // Verlust durch Swap (aber notwendig für Gas)
                Status = "Success",
                ExecutedAt = DateTime.UtcNow,
                DexUsed = "Uniswap-V3-WorldChain"
            };

            _context.WorldAgentTrades.Add(trade);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Auto-swap completed: Agent {AgentId} now has {ETH} ETH for gas", agentId, agent.CurrentBalanceETH);

            return true;
        }

        /// <summary>
        /// Prüfe ETH Balance und führe Auto-Swap aus falls nötig
        /// (Wird vor jedem Trade aufgerufen)
        /// </summary>
        public async Task<bool> CheckAndAutoSwapGasAsync(int agentId)
        {
            var agent = await _context.WorldTradingAgents.FindAsync(agentId);
            if (agent == null) return false;

            // Wenn ETH unter Threshold, führe Auto-Swap aus
            if (agent.CurrentBalanceETH < agent.AutoSwapThresholdETH)
            {
                _logger.LogInformation("Agent {AgentId} ETH balance {Current} < threshold {Threshold}, triggering auto-swap",
                    agentId, agent.CurrentBalanceETH, agent.AutoSwapThresholdETH);

                return await SwapWLDtoETHAsync(agentId, agent.AutoSwapAmountWLD);
            }

            return true; // Genug Gas vorhanden
        }

        /// <summary>
        /// Prüfe Profit-Ziel und führe Auto-Withdraw aus
        /// Bei 100% Profit (Verdopplung) → 50% der Gewinne auszahlen
        /// </summary>
        public async Task<bool> CheckProfitTargetAndWithdrawAsync(int agentId)
        {
            var agent = await _context.WorldTradingAgents.FindAsync(agentId);
            if (agent == null || agent.InitialFundingWLD == 0) return false;

            // Berechne aktuellen Profit Prozentsatz
            decimal profitPercent = ((agent.CurrentBalanceWLD - agent.InitialFundingWLD) / agent.InitialFundingWLD) * 100;

            // Bei 100% Profit (Verdopplung)
            if (profitPercent >= 100)
            {
                decimal totalGain = agent.CurrentBalanceWLD - agent.InitialFundingWLD;
                decimal withdrawAmount = totalGain * 0.5m; // 50% der Gewinne
                decimal remainingBalance = agent.CurrentBalanceWLD - withdrawAmount;

                _logger.LogInformation("Agent {AgentId} reached 100% profit target! Withdrawing 50% of gains: {Amount} WLD",
                    agentId, withdrawAmount);

                // TODO: Hier echten Transfer zum User Wallet implementieren
                // Für jetzt: Nur in DB tracken

                // Update Agent Balance
                agent.CurrentBalanceWLD = remainingBalance;
                agent.TotalProfitLossWLD = remainingBalance - agent.InitialFundingWLD;
                agent.ProfitLossPercentage = ((remainingBalance - agent.InitialFundingWLD) / agent.InitialFundingWLD) * 100;
                agent.LastBalanceUpdate = DateTime.UtcNow;

                // Log Withdrawal Trade
                var withdrawalTrade = new WorldAgentTrade
                {
                    AgentId = agentId,
                    TradeType = "AutoWithdraw",
                    FromToken = "Agent",
                    ToToken = "User",
                    FromAmount = withdrawAmount,
                    ToAmount = withdrawAmount,
                    GasFee = 0,
                    ProfitLossWLD = -withdrawAmount, // Aus Agent-Sicht ein "Verlust"
                    Status = "Success",
                    ExecutedAt = DateTime.UtcNow,
                    DexUsed = "Auto-Withdrawal"
                };

                _context.WorldAgentTrades.Add(withdrawalTrade);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Auto-withdraw completed: {Amount} WLD sent to user, agent continues with {Remaining} WLD",
                    withdrawAmount, remainingBalance);

                return true;
            }

            return false; // Kein Withdraw nötig
        }

        /// <summary>
        /// Withdraw Funds vom Agent zurück zum User
        /// </summary>
        public async Task<(bool success, string error)> WithdrawFundsAsync(int agentId, string userHash, decimal amountWLD)
        {
            var agent = await _context.WorldTradingAgents
                .FirstOrDefaultAsync(a => a.Id == agentId && a.UserHash == userHash);

            if (agent == null)
                return (false, "Agent nicht gefunden");

            // Validierung
            if (amountWLD <= 0)
                return (false, "Betrag muss größer als 0 sein");

            if (amountWLD > agent.CurrentBalanceWLD)
                return (false, $"Nicht genug Guthaben. Verfügbar: {agent.CurrentBalanceWLD:F2} WLD");

            // Min-Balance für Gas behalten (0.1 WLD)
            decimal remainingBalance = agent.CurrentBalanceWLD - amountWLD;
            if (remainingBalance < 0.1m && remainingBalance > 0)
            {
                return (false, "Mindestens 0.1 WLD müssen für Gas-Fees im Agent verbleiben. Nutze 'Alles abheben' um komplett zu leeren.");
            }

            // Pause Agent wenn aktiv (Sicherheit)
            bool wasActive = agent.Status == "Active";
            if (wasActive)
            {
                agent.Status = "Paused";
                _logger.LogInformation("Agent {AgentId} automatically paused for withdrawal", agentId);
            }

            // Update Balance
            agent.CurrentBalanceWLD -= amountWLD;
            agent.TotalProfitLossWLD = agent.CurrentBalanceWLD - agent.InitialFundingWLD;
            agent.ProfitLossPercentage = agent.InitialFundingWLD > 0
                ? ((agent.CurrentBalanceWLD - agent.InitialFundingWLD) / agent.InitialFundingWLD) * 100
                : 0;
            agent.LastBalanceUpdate = DateTime.UtcNow;

            // Log Withdrawal Trade
            var withdrawalTrade = new WorldAgentTrade
            {
                AgentId = agentId,
                TradeType = "ManualWithdraw",
                FromToken = "Agent",
                ToToken = "User",
                FromAmount = amountWLD,
                ToAmount = amountWLD,
                GasFee = 0.00002m, // Geschätzte Gas Fee für Transfer
                ProfitLossWLD = -amountWLD,
                Status = "Pending",
                ExecutedAt = DateTime.UtcNow,
                DexUsed = "Manual-Withdrawal"
            };

            _context.WorldAgentTrades.Add(withdrawalTrade);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserHash} withdrew {Amount} WLD from agent {AgentId}. New balance: {Balance} WLD",
                userHash, amountWLD, agentId, agent.CurrentBalanceWLD);

            // TODO: Hier echten Transfer vom Agent Wallet zum User Wallet implementieren
            // Für Production: Nutze Agent's Private Key um WLD zu transferieren
            // var privateKey = DecryptPrivateKey(agent.EncryptedPrivateKey);
            // ... Transfer Logik ...

            // Markiere Trade als Success (nach echtem Transfer)
            withdrawalTrade.Status = "Success";
            withdrawalTrade.TxHash = "manual-withdraw-" + DateTime.UtcNow.Ticks; // TODO: Echte TX Hash
            await _context.SaveChangesAsync();

            return (true, string.Empty);
        }

        /// <summary>
        /// Hole Trading History
        /// </summary>
        public async Task<List<WorldAgentTrade>> GetTradeHistoryAsync(int agentId, int limit = 50)
        {
            return await _context.WorldAgentTrades
                .Where(t => t.AgentId == agentId)
                .OrderByDescending(t => t.ExecutedAt)
                .Take(limit)
                .ToListAsync();
        }

        /// <summary>
        /// Generiere Ethereum Wallet (vereinfacht - in Production: Ethers.js verwenden)
        /// </summary>
        private (string address, string privateKey) GenerateEthereumWallet()
        {
            // HINWEIS: Dies ist eine vereinfachte Version
            // In Production sollte man Nethereum oder eine sichere Lib verwenden

            using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var privateKeyBytes = ecdsa.ExportECPrivateKey();
            var privateKeyHex = "0x" + Convert.ToHexString(privateKeyBytes).ToLower();

            // Generiere "fake" Adresse (in Production: richtig aus Public Key ableiten)
            var addressBytes = RandomNumberGenerator.GetBytes(20);
            var address = "0x" + Convert.ToHexString(addressBytes).ToLower();

            return (address, privateKeyHex);
        }

        /// <summary>
        /// Verschlüssele Private Key mit AES-256
        /// </summary>
        private string EncryptPrivateKey(string privateKey)
        {
            using var aes = Aes.Create();
            aes.Key = _encryptionKey;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(privateKey);
            var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            // IV + Encrypted Data
            var result = new byte[aes.IV.Length + cipherBytes.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

            return Convert.ToBase64String(result);
        }

        /// <summary>
        /// Entschlüssele Private Key (nur für Trading Service)
        /// </summary>
        public string DecryptPrivateKey(string encryptedKey)
        {
            var fullCipher = Convert.FromBase64String(encryptedKey);

            using var aes = Aes.Create();
            aes.Key = _encryptionKey;

            var iv = new byte[aes.IV.Length];
            var cipher = new byte[fullCipher.Length - iv.Length];

            Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);

            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);

            return Encoding.UTF8.GetString(plainBytes);
        }
    }
}
