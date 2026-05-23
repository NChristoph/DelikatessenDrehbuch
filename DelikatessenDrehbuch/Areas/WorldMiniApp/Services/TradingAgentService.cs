using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Data;
using Microsoft.EntityFrameworkCore;
using Nethereum.Hex.HexTypes;
using Nethereum.Web3;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    public class TradingAgentService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TradingAgentService> _logger;
        private readonly BlockchainService _blockchainService;
        private readonly DexService _dexService;

        // Encryption Key (sollte aus appsettings/environment kommen)
        private readonly byte[] _encryptionKey;

        public TradingAgentService(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<TradingAgentService> logger,
            BlockchainService blockchainService,
            DexService dexService)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _blockchainService = blockchainService;
            _dexService = dexService;

            // Encryption Key fuer Private Keys (32 bytes fuer AES-256)
            var keyString = _configuration["TradingAgent:EncryptionKey"] ?? "CHANGE_THIS_TO_SECURE_KEY_32BYTES!";
            _encryptionKey = Encoding.UTF8.GetBytes(keyString.PadRight(32).Substring(0, 32));
        }

        /// <summary>
        /// Erstellt einen neuen Trading Agent mit eigenem Wallet
        /// </summary>
        public async Task<WorldTradingAgent> CreateAgentAsync(string userHash, string? agentName = null)
        {
            var (walletAddress, privateKey) = GenerateEthereumWallet();
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

        public async Task<WorldTradingAgent?> GetAgentByUserAsync(string userHash)
        {
            return await _context.WorldTradingAgents
                .FirstOrDefaultAsync(a => a.UserHash == userHash);
        }

        public async Task<List<WorldTradingAgent>> GetAgentsByUserAsync(string userHash)
        {
            return await _context.WorldTradingAgents
                .Where(a => a.UserHash == userHash)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> StartAgentAsync(int agentId, string userHash)
        {
            var agent = await _context.WorldTradingAgents
                .FirstOrDefaultAsync(a => a.Id == agentId && a.UserHash == userHash);

            if (agent == null) return false;

            if (agent.Status == "Created" && agent.CurrentBalanceWLD < 1)
            {
                _logger.LogWarning("Cannot start agent {AgentId}: insufficient funding", agentId);
                return false;
            }

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

            return true;
        }

        /// <summary>
        /// Manueller WLD -> ETH Swap. Dieser Pfad braucht weiterhin native Gas-Mittel,
        /// weil er direkt ueber Web3/DexService ausgefuehrt wird.
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

            try
            {
                _logger.LogInformation("REAL BLOCKCHAIN SWAP: Agent {AgentId} swapping {Amount} WLD to ETH on-chain",
                    agentId, amountWLD);

                var wldTokenAddress = _configuration["WorldChain:WldTokenAddress"]
                    ?? throw new Exception("WLD Token address not configured");
                var wethAddress = _configuration["WorldChain:WethAddress"]
                    ?? throw new Exception("WETH address not configured");

                var privateKey = DecryptPrivateKey(agent.EncryptedPrivateKey);
                var web3 = _blockchainService.GetWeb3Instance(privateKey);

                var hasGas = await _blockchainService.HasSufficientGasAsync(
                    web3,
                    agent.WalletAddress,
                    new BigInteger(500000));

                if (!hasGas)
                {
                    _logger.LogError("Agent {AgentId} has insufficient ETH for manual WLD -> ETH swap", agentId);
                    return false;
                }

                var (success, amountOut, txHash, _) = await _dexService.SwapTokensAsync(
                    web3,
                    wldTokenAddress,
                    wethAddress,
                    amountWLD,
                    slippagePct: 5m,
                    feeTier: 3000);

                if (!success)
                {
                    var failedTrade = new WorldAgentTrade
                    {
                        AgentId = agentId,
                        TradeType = "AutoSwap",
                        FromToken = wldTokenAddress,
                        ToToken = "ETH",
                        FromAmount = amountWLD,
                        ToAmount = 0,
                        GasFee = 0,
                        ProfitLossWLD = 0,
                        Status = "Failed",
                        ExecutedAt = DateTime.UtcNow,
                        TxHash = "",
                        DexUsed = "Uniswap-V3-WorldChain"
                    };
                    _context.WorldAgentTrades.Add(failedTrade);
                    await _context.SaveChangesAsync();
                    return false;
                }

                agent.CurrentBalanceWLD -= amountWLD;
                agent.CurrentBalanceETH += amountOut;
                agent.LastBalanceUpdate = DateTime.UtcNow;

                var trade = new WorldAgentTrade
                {
                    AgentId = agentId,
                    TradeType = "AutoSwap",
                    FromToken = wldTokenAddress,
                    ToToken = "ETH",
                    FromAmount = amountWLD,
                    ToAmount = amountOut,
                    GasFee = 0.00002m,
                    ProfitLossWLD = -amountWLD,
                    Status = "Success",
                    ExecutedAt = DateTime.UtcNow,
                    TxHash = txHash,
                    DexUsed = "Uniswap-V3-WorldChain"
                };

                _context.WorldAgentTrades.Add(trade);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Manual swap completed: Agent {AgentId} now has {ETH} ETH for gas",
                    agentId, agent.CurrentBalanceETH);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Manual WLD -> ETH swap failed: {Message}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Der automatische Trading-Flow laeuft ueber AgentKit und braucht hier keinen separaten ETH-Gas-Puffer.
        /// </summary>
        public async Task<bool> CheckAndAutoSwapGasAsync(int agentId)
        {
            var agent = await _context.WorldTradingAgents.FindAsync(agentId);
            if (agent == null) return false;

            _logger.LogDebug(
                "Skipping ETH auto-swap for agent {AgentId}. Main trading flow uses AgentKit gas abstraction.",
                agentId);

            return true;
        }

        public async Task<bool> CheckProfitTargetAndWithdrawAsync(int agentId)
        {
            var agent = await _context.WorldTradingAgents.FindAsync(agentId);
            if (agent == null || agent.InitialFundingWLD == 0) return false;

            decimal profitPercent = ((agent.CurrentBalanceWLD - agent.InitialFundingWLD) / agent.InitialFundingWLD) * 100;

            if (profitPercent >= 100)
            {
                decimal totalGain = agent.CurrentBalanceWLD - agent.InitialFundingWLD;
                decimal withdrawAmount = totalGain * 0.5m;
                decimal remainingBalance = agent.CurrentBalanceWLD - withdrawAmount;

                _logger.LogInformation("Agent {AgentId} reached 100% profit target! Withdrawing 50% of gains: {Amount} WLD",
                    agentId, withdrawAmount);

                _logger.LogWarning("Auto-Withdraw is SIMULATED ONLY - no real blockchain transfer! Agent {AgentId}, Amount: {Amount} WLD",
                    agentId, withdrawAmount);

                agent.CurrentBalanceWLD = remainingBalance;
                agent.TotalProfitLossWLD = remainingBalance - agent.InitialFundingWLD;
                agent.ProfitLossPercentage = ((remainingBalance - agent.InitialFundingWLD) / agent.InitialFundingWLD) * 100;
                agent.LastBalanceUpdate = DateTime.UtcNow;

                var withdrawalTrade = new WorldAgentTrade
                {
                    AgentId = agentId,
                    TradeType = "AutoWithdraw",
                    FromToken = "Agent",
                    ToToken = "User",
                    FromAmount = withdrawAmount,
                    ToAmount = withdrawAmount,
                    GasFee = 0,
                    ProfitLossWLD = -withdrawAmount,
                    Status = "Simulated",
                    ExecutedAt = DateTime.UtcNow,
                    DexUsed = "Auto-Withdrawal-SIMULATED"
                };

                _context.WorldAgentTrades.Add(withdrawalTrade);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Auto-withdraw SIMULATED: {Amount} WLD marked as withdrawn in DB only, agent continues with {Remaining} WLD",
                    withdrawAmount, remainingBalance);

                return true;
            }

            return false;
        }

        public async Task<(bool success, string error)> WithdrawFundsAsync(int agentId, string userHash, decimal amountWLD)
        {
            var agent = await _context.WorldTradingAgents
                .FirstOrDefaultAsync(a => a.Id == agentId && a.UserHash == userHash);

            if (agent == null)
                return (false, "Agent nicht gefunden");

            if (amountWLD <= 0)
                return (false, "Betrag muss groesser als 0 sein");

            if (amountWLD > agent.CurrentBalanceWLD)
                return (false, $"Nicht genug Guthaben. Verfuegbar: {agent.CurrentBalanceWLD:F2} WLD");

            decimal remainingBalance = agent.CurrentBalanceWLD - amountWLD;
            if (remainingBalance < 0.1m && remainingBalance > 0)
            {
                return (false, "Mindestens 0.1 WLD muessen fuer Restbetrieb im Agent verbleiben. Nutze 'Alles abheben' um komplett zu leeren.");
            }

            _logger.LogInformation("Withdrawal fuer Agent {AgentId}: {Amount} WLD (Agent bleibt aktiv)", agentId, amountWLD);

            agent.CurrentBalanceWLD -= amountWLD;
            agent.TotalProfitLossWLD = agent.CurrentBalanceWLD - agent.InitialFundingWLD;
            agent.ProfitLossPercentage = agent.InitialFundingWLD > 0
                ? ((agent.CurrentBalanceWLD - agent.InitialFundingWLD) / agent.InitialFundingWLD) * 100
                : 0;
            agent.LastBalanceUpdate = DateTime.UtcNow;

            var withdrawalTrade = new WorldAgentTrade
            {
                AgentId = agentId,
                TradeType = "ManualWithdraw",
                FromToken = "Agent",
                ToToken = "User",
                FromAmount = amountWLD,
                ToAmount = amountWLD,
                GasFee = 0.00002m,
                ProfitLossWLD = -amountWLD,
                Status = "Pending",
                ExecutedAt = DateTime.UtcNow,
                DexUsed = "Manual-Withdrawal"
            };

            _context.WorldAgentTrades.Add(withdrawalTrade);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserHash} withdrew {Amount} WLD from agent {AgentId}. New balance: {Balance} WLD",
                userHash, amountWLD, agentId, agent.CurrentBalanceWLD);

            withdrawalTrade.Status = "Success";
            withdrawalTrade.TxHash = "manual-withdraw-" + DateTime.UtcNow.Ticks;
            await _context.SaveChangesAsync();

            return (true, string.Empty);
        }

        public async Task<(bool success, string error, string? txHash)> WithdrawEthReserveAsync(
            int agentId,
            string userHash,
            decimal amountEth,
            string targetWalletAddress)
        {
            var agent = await _context.WorldTradingAgents
                .FirstOrDefaultAsync(a => a.Id == agentId && a.UserHash == userHash);

            if (agent == null)
                return (false, "Agent nicht gefunden", null);

            if (amountEth <= 0)
                return (false, "Betrag muss groesser als 0 sein", null);

            if (!IsValidWalletAddress(targetWalletAddress))
                return (false, "Ungueltige Ziel-Wallet-Adresse", null);

            var normalizedTargetWallet = targetWalletAddress.Trim();
            var privateKey = DecryptPrivateKey(agent.EncryptedPrivateKey);
            var web3 = _blockchainService.GetWeb3Instance(privateKey);

            var wethTokenAddress = _configuration["WorldChain:WethAddress"]
                ?? throw new Exception("WETH Token address not configured");

            var nativeBalanceWei = await web3.Eth.GetBalance.SendRequestAsync(agent.WalletAddress);
            var wethBalance = await GetERC20BalanceAsync(web3, wethTokenAddress, agent.WalletAddress);
            var nativeBalance = Web3.Convert.FromWei(nativeBalanceWei.Value);

            string? txHash = null;
            string transferMode;

            if (wethBalance >= amountEth)
            {
                txHash = await TransferErc20Async(web3, wethTokenAddress, normalizedTargetWallet, amountEth);
                transferMode = "WETH";
            }
            else
            {
                var gasPrice = await _blockchainService.GetGasPriceAsync(web3);
                var gasLimit = new BigInteger(21000);
                var requiredGasWei = gasPrice * gasLimit;
                var sendAmountWei = Web3.Convert.ToWei(amountEth);

                if (nativeBalanceWei.Value < sendAmountWei + requiredGasWei)
                {
                    return (false, "Nicht genug ETH/WETH Reserve fuer diese Auszahlung", null);
                }

                txHash = await TransferNativeEthAsync(web3, normalizedTargetWallet, sendAmountWei, gasPrice, gasLimit);
                transferMode = "ETH";
            }

            if (string.IsNullOrWhiteSpace(txHash))
                return (false, "Transfer konnte nicht gesendet werden", null);

            agent.LastBalanceUpdate = DateTime.UtcNow;

            var trade = new WorldAgentTrade
            {
                AgentId = agentId,
                TradeType = "EthWithdraw",
                FromToken = transferMode,
                ToToken = normalizedTargetWallet,
                FromAmount = amountEth,
                ToAmount = amountEth,
                GasFee = 0,
                ProfitLossWLD = 0,
                Status = "Success",
                ExecutedAt = DateTime.UtcNow,
                TxHash = txHash,
                DexUsed = $"Agent-{transferMode}-Withdraw"
            };

            _context.WorldAgentTrades.Add(trade);
            await _context.SaveChangesAsync();
            await UpdateRealBalancesAsync(agentId);

            _logger.LogInformation(
                "User {UserHash} withdrew {Amount} {Mode} reserve from agent {AgentId} to {TargetWallet}. Tx: {TxHash}",
                userHash,
                amountEth,
                transferMode,
                agentId,
                normalizedTargetWallet,
                txHash);

            return (true, string.Empty, txHash);
        }

        public async Task<List<WorldAgentTrade>> GetTradeHistoryAsync(int agentId, int limit = 50, int skip = 0)
        {
            return await _context.WorldAgentTrades
                .Where(t => t.AgentId == agentId)
                .OrderByDescending(t => t.ExecutedAt)
                .Skip(skip)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<int> GetTradeCountAsync(int agentId)
        {
            return await _context.WorldAgentTrades
                .Where(t => t.AgentId == agentId)
                .CountAsync();
        }

        private (string address, string privateKey) GenerateEthereumWallet()
        {
            var ecKey = Nethereum.Signer.EthECKey.GenerateKey();
            var privateKeyHex = ecKey.GetPrivateKey();
            var address = ecKey.GetPublicAddress();

            _logger.LogInformation("Generated new Ethereum wallet: {Address}", address);

            return (address, privateKeyHex);
        }

        private string EncryptPrivateKey(string privateKey)
        {
            using var aes = Aes.Create();
            aes.Key = _encryptionKey;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(privateKey);
            var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            var result = new byte[aes.IV.Length + cipherBytes.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

            return Convert.ToBase64String(result);
        }

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

        public async Task<(decimal ethBalance, decimal wldBalance)> UpdateRealBalancesAsync(int agentId)
        {
            var agent = await _context.WorldTradingAgents.FindAsync(agentId);
            if (agent == null)
                throw new Exception($"Agent {agentId} not found");

            try
            {
                var privateKey = DecryptPrivateKey(agent.EncryptedPrivateKey);
                var web3 = _blockchainService.GetWeb3Instance(privateKey);

                var nativeEthBalanceWei = await web3.Eth.GetBalance.SendRequestAsync(agent.WalletAddress);
                var nativeEthBalance = Web3.Convert.FromWei(nativeEthBalanceWei.Value);

                var wethTokenAddress = _configuration["WorldChain:WethAddress"]
                    ?? throw new Exception("WETH Token address not configured");
                var wethBalance = await GetERC20BalanceAsync(web3, wethTokenAddress, agent.WalletAddress);
                var ethBalanceRaw = nativeEthBalance + wethBalance;

                var wldTokenAddress = _configuration["WorldChain:WldTokenAddress"]
                    ?? throw new Exception("WLD Token address not configured");
                var wldBalanceRaw = await GetERC20BalanceAsync(web3, wldTokenAddress, agent.WalletAddress);

                var ethBalance = SafeDecimalConvert(ethBalanceRaw, 9, 9);
                var wldBalance = SafeDecimalConvert(wldBalanceRaw, 12, 6);

                agent.CurrentBalanceETH = ethBalance;
                agent.CurrentBalanceWLD = wldBalance;
                agent.LastBalanceUpdate = DateTime.UtcNow;

                if (agent.InitialFundingWLD > 0)
                {
                    var profitLoss = wldBalance - agent.InitialFundingWLD;
                    var profitPercent = ((wldBalance - agent.InitialFundingWLD) / agent.InitialFundingWLD) * 100;

                    agent.TotalProfitLossWLD = SafeDecimalConvert(profitLoss, 12, 6);
                    agent.ProfitLossPercentage = SafeDecimalConvert(profitPercent, 3, 4);
                }

                await _context.SaveChangesAsync();
                return (ethBalance, wldBalance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update real balances for agent {AgentId}: {Message}",
                    agentId, ex.Message);
                throw;
            }
        }

        private decimal SafeDecimalConvert(decimal value, int maxDigitsBeforeDecimal, int maxDigitsAfterDecimal)
        {
            var rounded = Math.Round(value, maxDigitsAfterDecimal);
            var maxValue = (decimal)Math.Pow(10, maxDigitsBeforeDecimal) - (decimal)Math.Pow(10, -maxDigitsAfterDecimal);

            if (rounded > maxValue)
            {
                _logger.LogWarning("Value {Value} exceeds max {Max}, clamping to max", rounded, maxValue);
                return maxValue;
            }

            if (rounded < -maxValue)
            {
                _logger.LogWarning("Value {Value} below min {Min}, clamping to min", rounded, -maxValue);
                return -maxValue;
            }

            return rounded;
        }

        private async Task<decimal> GetERC20BalanceAsync(Web3 web3, string tokenAddress, string walletAddress)
        {
            try
            {
                var balanceOfFunction = web3.Eth.GetContract(
                    "[{\"constant\":true,\"inputs\":[{\"name\":\"_owner\",\"type\":\"address\"}],\"name\":\"balanceOf\",\"outputs\":[{\"name\":\"balance\",\"type\":\"uint256\"}],\"type\":\"function\"}]",
                    tokenAddress
                ).GetFunction("balanceOf");

                var balance = await balanceOfFunction.CallAsync<BigInteger>(walletAddress);
                return Web3.Convert.FromWei(balance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get ERC20 balance for token {Token}, wallet {Wallet}",
                    tokenAddress, walletAddress);
                return 0;
            }
        }

        private static bool IsValidWalletAddress(string? walletAddress)
        {
            if (string.IsNullOrWhiteSpace(walletAddress))
            {
                return false;
            }

            var value = walletAddress.Trim();
            return value.Length == 42
                && value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                && value.Skip(2).All(Uri.IsHexDigit);
        }

        private async Task<string?> TransferErc20Async(Web3 web3, string tokenAddress, string targetWalletAddress, decimal amount)
        {
            const string erc20Abi =
                "[{\"constant\":false,\"inputs\":[{\"name\":\"to\",\"type\":\"address\"},{\"name\":\"value\",\"type\":\"uint256\"}],\"name\":\"transfer\",\"outputs\":[{\"name\":\"\",\"type\":\"bool\"}],\"type\":\"function\"}]";

            var contract = web3.Eth.GetContract(erc20Abi, tokenAddress);
            var transferFunction = contract.GetFunction("transfer");
            var amountWei = Web3.Convert.ToWei(amount);
            return await transferFunction.SendTransactionAsync(web3.TransactionManager.Account.Address, new HexBigInteger(120000), null, targetWalletAddress, amountWei);
        }

        private async Task<string?> TransferNativeEthAsync(
            Web3 web3,
            string targetWalletAddress,
            BigInteger amountWei,
            BigInteger gasPrice,
            BigInteger gasLimit)
        {
            var tx = new Nethereum.RPC.Eth.DTOs.TransactionInput
            {
                From = web3.TransactionManager.Account.Address,
                To = targetWalletAddress,
                Value = new HexBigInteger(amountWei),
                Gas = new HexBigInteger(gasLimit),
                GasPrice = new HexBigInteger(gasPrice)
            };

            return await web3.TransactionManager.SendTransactionAsync(tx);
        }
    }
}
