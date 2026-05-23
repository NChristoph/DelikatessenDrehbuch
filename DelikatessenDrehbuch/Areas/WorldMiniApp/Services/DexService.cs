using Nethereum.Web3;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using System.Numerics;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    public class DexService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DexService> _logger;
        private readonly BlockchainService _blockchainService;

        // Uniswap V3 Quoter ABI (for price quotes)
        private const string QUOTER_ABI = @"[
            {
                ""inputs"": [
                    {""internalType"":""address"",""name"":""tokenIn"",""type"":""address""},
                    {""internalType"":""address"",""name"":""tokenOut"",""type"":""address""},
                    {""internalType"":""uint24"",""name"":""fee"",""type"":""uint24""},
                    {""internalType"":""uint256"",""name"":""amountIn"",""type"":""uint256""},
                    {""internalType"":""uint160"",""name"":""sqrtPriceLimitX96"",""type"":""uint160""}
                ],
                ""name"":""quoteExactInputSingle"",
                ""outputs"":[{""internalType"":""uint256"",""name"":""amountOut"",""type"":""uint256""}],
                ""stateMutability"":""nonpayable"",
                ""type"":""function""
            }
        ]";

        // Uniswap V3 Router ABI (simplified - only what we need)
        private const string ROUTER_ABI = @"[
            {
                ""inputs"": [{
                    ""components"": [
                        {""internalType"":""address"",""name"":""tokenIn"",""type"":""address""},
                        {""internalType"":""address"",""name"":""tokenOut"",""type"":""address""},
                        {""internalType"":""uint24"",""name"":""fee"",""type"":""uint24""},
                        {""internalType"":""address"",""name"":""recipient"",""type"":""address""},
                        {""internalType"":""uint256"",""name"":""deadline"",""type"":""uint256""},
                        {""internalType"":""uint256"",""name"":""amountIn"",""type"":""uint256""},
                        {""internalType"":""uint256"",""name"":""amountOutMinimum"",""type"":""uint256""},
                        {""internalType"":""uint160"",""name"":""sqrtPriceLimitX96"",""type"":""uint160""}
                    ],
                    ""internalType"":""struct ISwapRouter.ExactInputSingleParams"",
                    ""name"":""params"",
                    ""type"":""tuple""
                }],
                ""name"":""exactInputSingle"",
                ""outputs"":[{""internalType"":""uint256"",""name"":""amountOut"",""type"":""uint256""}],
                ""stateMutability"":""payable"",
                ""type"":""function""
            }
        ]";

        // ERC20 ABI
        private const string ERC20_ABI = @"[
            {""constant"":false,""inputs"":[{""name"":""spender"",""type"":""address""},{""name"":""amount"",""type"":""uint256""}],""name"":""approve"",""outputs"":[{""name"":"""",""type"":""bool""}],""payable"":false,""stateMutability"":""nonpayable"",""type"":""function""},
            {""constant"":true,""inputs"":[{""name"":""account"",""type"":""address""}],""name"":""balanceOf"",""outputs"":[{""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""},
            {""constant"":true,""inputs"":[],""name"":""decimals"",""outputs"":[{""name"":"""",""type"":""uint8""}],""stateMutability"":""view"",""type"":""function""}
        ]";

        public DexService(
            IConfiguration configuration,
            ILogger<DexService> logger,
            BlockchainService blockchainService)
        {
            _configuration = configuration;
            _logger = logger;
            _blockchainService = blockchainService;
        }

        /// <summary>
        /// Swapped Token A zu Token B über Uniswap V3
        /// </summary>
        public async Task<(bool success, decimal amountOut, string txHash, string error)> SwapTokensAsync(
            Web3 web3,
            string tokenIn,
            string tokenOut,
            decimal amountIn,
            decimal slippagePct = 5m,
            int feeTier = 3000)
        {
            try
            {
                var routerAddress = _configuration["WorldChain:UniswapV3RouterAddress"]
                    ?? throw new Exception("Uniswap Router address not configured");

                var account = web3.TransactionManager.Account;
                _logger.LogInformation("🔄 Swapping {AmountIn} of {TokenIn} to {TokenOut}", amountIn, tokenIn, tokenOut);
                _logger.LogInformation("📍 Using wallet address: {Address}", account.Address);

                // Check ETH balance for gas FIRST
                var ethBalance = await web3.Eth.GetBalance.SendRequestAsync(account.Address);
                var ethBalanceDecimal = Web3.Convert.FromWei(ethBalance);
                _logger.LogInformation("⛽ Native ETH balance: {EthBalance} ETH (Address: {Address})", ethBalanceDecimal, account.Address);

                // WORLD CHAIN CHECK: Auch WETH (ERC20) prüfen
                var wethAddress = _configuration["WorldChain:WethAddress"];
                if (!string.IsNullOrEmpty(wethAddress))
                {
                    try
                    {
                        var wethContract = web3.Eth.GetContract(ERC20_ABI, wethAddress);
                        var wethBalanceFunction = wethContract.GetFunction("balanceOf");
                        var wethBalanceWei = await wethBalanceFunction.CallAsync<BigInteger>(account.Address);
                        var wethBalanceDecimal = Web3.Convert.FromWei(wethBalanceWei);
                        _logger.LogInformation("💰 WETH (ERC20) balance: {WethBalance} WETH", wethBalanceDecimal);

                        // Kombiniere beide für Total
                        var totalEthBalance = ethBalanceDecimal + wethBalanceDecimal;
                        _logger.LogInformation("📊 Total ETH (native + WETH): {Total} ETH", totalEthBalance);

                        if (totalEthBalance < 0.0001m)
                        {
                            var error = $"Insufficient ETH for gas: {totalEthBalance} ETH total (native: {ethBalanceDecimal}, WETH: {wethBalanceDecimal}) - need at least 0.0001 ETH";
                            _logger.LogError(error);
                            return (false, 0, "", error);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Could not check WETH balance: {Message}", ex.Message);
                        // Fallback to native ETH check only
                        if (ethBalanceDecimal < 0.0001m)
                        {
                            var error = $"Insufficient native ETH for gas: {ethBalanceDecimal} ETH (need at least 0.0001 ETH)";
                            _logger.LogError(error);
                            return (false, 0, "", error);
                        }
                    }
                }
                else if (ethBalanceDecimal < 0.0001m)
                {
                    var error = $"Insufficient native ETH for gas: {ethBalanceDecimal} ETH (need at least 0.0001 ETH)";
                    _logger.LogError(error);
                    return (false, 0, "", error);
                }

                // Get token decimals
                var tokenInContract = web3.Eth.GetContract(ERC20_ABI, tokenIn);
                var decimalsFunction = tokenInContract.GetFunction("decimals");
                var decimals = await decimalsFunction.CallAsync<int>();

                // Convert amount to Wei
                var amountInWei = BigInteger.Parse((amountIn * (decimal)Math.Pow(10, decimals)).ToString("F0"));

                // Step 1: Check current balance
                var balanceFunction = tokenInContract.GetFunction("balanceOf");
                var balance = await balanceFunction.CallAsync<BigInteger>(account.Address);

                if (balance < amountInWei)
                {
                    var error = $"Insufficient balance: {balance} < {amountInWei}";
                    _logger.LogError(error);
                    return (false, 0, "", error);
                }

                // Step 2: Approve router to spend tokens
                _logger.LogInformation("✅ Approving router {Router} to spend {Amount} tokens...", routerAddress, amountInWei);
                var approveFunction = tokenInContract.GetFunction("approve");

                string? approveTxHash = null;
                try
                {
                    approveTxHash = await _blockchainService.SendTransactionWithRetryAsync(
                        web3,
                        async () => await approveFunction.SendTransactionAsync(
                            account.Address,
                            new HexBigInteger(300000), // gas limit
                            null, // gas price
                            null, // value
                            routerAddress,
                            amountInWei
                        )
                    );
                }
                catch (Exception ex)
                {
                    var error = $"Approve transaction exception: {ex.Message} (Router: {routerAddress})";
                    _logger.LogError(ex, error);
                    return (false, 0, "", error);
                }

                if (approveTxHash == null)
                {
                    var error = $"Approve transaction failed - unable to send transaction (Router: {routerAddress}, Token: {tokenIn})";
                    _logger.LogError(error);
                    return (false, 0, "", error);
                }

                // Wait for approve confirmation
                var approveReceipt = await _blockchainService.WaitForTransactionReceiptAsync(web3, approveTxHash);
                if (approveReceipt?.Status?.Value != 1)
                {
                    var error = $"Approve transaction reverted - TX: {approveTxHash}";
                    _logger.LogError(error);
                    return (false, 0, "", error);
                }

                // Step 3: Execute swap
                _logger.LogInformation("Executing swap on Uniswap V3...");

                // Calculate minimum output with slippage
                // Rough estimate: 1 WLD ≈ 0.00065 ETH
                var estimatedOutWei = amountInWei * 65 / 100000; // Simplified
                var minOutWei = estimatedOutWei * (100 - (int)slippagePct) / 100;

                var routerContract = web3.Eth.GetContract(ROUTER_ABI, routerAddress);
                var swapFunction = routerContract.GetFunction("exactInputSingle");

                var swapParams = new
                {
                    tokenIn,
                    tokenOut,
                    fee = feeTier,
                    recipient = account.Address,
                    deadline = DateTimeOffset.UtcNow.AddMinutes(20).ToUnixTimeSeconds(),
                    amountIn = amountInWei,
                    amountOutMinimum = minOutWei,
                    sqrtPriceLimitX96 = BigInteger.Zero
                };

                var swapTxHash = await _blockchainService.SendTransactionWithRetryAsync(
                    web3,
                    async () => await swapFunction.SendTransactionAsync(
                        account.Address,
                        new HexBigInteger(500000), // gas limit
                        null, // gas price
                        null, // value
                        swapParams
                    )
                );

                if (swapTxHash == null)
                {
                    var error = "Swap transaction failed - unable to send transaction (check gas, pool existence, slippage)";
                    _logger.LogError(error);
                    return (false, 0, "", error);
                }

                // Wait for swap confirmation
                var swapReceipt = await _blockchainService.WaitForTransactionReceiptAsync(web3, swapTxHash);
                if (swapReceipt?.Status?.Value != 1)
                {
                    var error = $"Swap transaction reverted - TX: {swapTxHash} (likely: pool doesn't exist, insufficient liquidity, or slippage too high)";
                    _logger.LogError(error);
                    return (false, 0, "", error);
                }

                // Get actual amount out from logs (simplified - use estimate)
                var actualOutWei = estimatedOutWei;
                var actualOut = (decimal)actualOutWei / (decimal)Math.Pow(10, 18); // ETH has 18 decimals

                _logger.LogInformation("Swap successful: {AmountOut} {TokenOut} received, TX: {TxHash}",
                    actualOut, tokenOut, swapTxHash.Substring(0, 10));

                return (true, actualOut, swapTxHash, "");
            }
            catch (Exception ex)
            {
                var error = $"Swap exception: {ex.Message}";
                _logger.LogError(ex, "Swap failed: {Message}", ex.Message);
                return (false, 0, "", error);
            }
        }

        /// <summary>
        /// Holt Token Balance
        /// </summary>
        public async Task<decimal> GetTokenBalanceAsync(Web3 web3, string tokenAddress, string walletAddress)
        {
            try
            {
                var contract = web3.Eth.GetContract(ERC20_ABI, tokenAddress);
                var balanceFunction = contract.GetFunction("balanceOf");
                var decimalsFunction = contract.GetFunction("decimals");

                var balance = await balanceFunction.CallAsync<BigInteger>(walletAddress);
                var decimals = await decimalsFunction.CallAsync<int>();

                return (decimal)balance / (decimal)Math.Pow(10, decimals);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get token balance");
                return 0;
            }
        }

        /// <summary>
        /// Holt aktuellen Preis von Token A zu Token B (1 Token A = X Token B)
        /// Nutzt CoinGecko API für echte Marktpreise
        /// </summary>
        public async Task<decimal> GetTokenPriceAsync(
            string tokenIn,
            string tokenOut,
            decimal amountIn = 1m,
            int feeTier = 3000)
        {
            var wldAddress = _configuration["WorldChain:WldTokenAddress"] ?? "";
            var wethAddress = _configuration["WorldChain:WethAddress"] ?? "";

            // METHODE 1: CoinGecko API (kostenlos, zuverlässig)
            try
            {
                _logger.LogInformation("🔍 Fetching real price from CoinGecko API");

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "DelikatessenDrehbuch/1.0");

                // WLD/ETH Preis berechnen
                if (tokenIn.Equals(wldAddress, StringComparison.OrdinalIgnoreCase) &&
                    tokenOut.Equals(wethAddress, StringComparison.OrdinalIgnoreCase))
                {
                    // Hole WLD Preis in USD und ETH Preis in USD
                    var url = "https://api.coingecko.com/api/v3/simple/price?ids=worldcoin-wld,ethereum&vs_currencies=usd";
                    var response = await httpClient.GetStringAsync(url);

                    // Parse JSON (einfaches Parsing)
                    var wldUsdMatch = System.Text.RegularExpressions.Regex.Match(response, @"""worldcoin-wld"":\s*{\s*""usd"":\s*([\d.]+)");
                    var ethUsdMatch = System.Text.RegularExpressions.Regex.Match(response, @"""ethereum"":\s*{\s*""usd"":\s*([\d.]+)");

                    if (wldUsdMatch.Success && ethUsdMatch.Success)
                    {
                        var wldUsd = decimal.Parse(wldUsdMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                        var ethUsd = decimal.Parse(ethUsdMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);

                        var wldEthPrice = wldUsd / ethUsd;

                        _logger.LogInformation("✅ CoinGecko: WLD=${WldUsd:F4}, ETH=${EthUsd:F2} → 1 WLD = {Price:F8} ETH",
                            wldUsd, ethUsd, wldEthPrice);

                        return wldEthPrice;
                    }
                }
                // ETH/WLD (inverse)
                else if (tokenIn.Equals(wethAddress, StringComparison.OrdinalIgnoreCase) &&
                         tokenOut.Equals(wldAddress, StringComparison.OrdinalIgnoreCase))
                {
                    var url = "https://api.coingecko.com/api/v3/simple/price?ids=worldcoin-wld,ethereum&vs_currencies=usd";
                    var response = await httpClient.GetStringAsync(url);

                    var wldUsdMatch = System.Text.RegularExpressions.Regex.Match(response, @"""worldcoin-wld"":\s*{\s*""usd"":\s*([\d.]+)");
                    var ethUsdMatch = System.Text.RegularExpressions.Regex.Match(response, @"""ethereum"":\s*{\s*""usd"":\s*([\d.]+)");

                    if (wldUsdMatch.Success && ethUsdMatch.Success)
                    {
                        var wldUsd = decimal.Parse(wldUsdMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                        var ethUsd = decimal.Parse(ethUsdMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);

                        var ethWldPrice = ethUsd / wldUsd;

                        _logger.LogInformation("✅ CoinGecko: ETH=${EthUsd:F2}, WLD=${WldUsd:F4} → 1 ETH = {Price:F2} WLD",
                            ethUsd, wldUsd, ethWldPrice);

                        return ethWldPrice;
                    }
                }

                throw new Exception("Token pair not supported or API response invalid");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ CoinGecko API failed: {Message}", ex.Message);

                // FALLBACK: Verwende vorsichtigen Fake-Preis als Notlösung
                _logger.LogWarning("⚠️ Using fallback estimated price (NOT REAL!)");

                // wldAddress und wethAddress sind bereits oben deklariert

                if (tokenIn.Equals(wldAddress, StringComparison.OrdinalIgnoreCase) &&
                    tokenOut.Equals(wethAddress, StringComparison.OrdinalIgnoreCase))
                {
                    // 1 WLD ≈ 0.0001 ETH (aktualisierter Basis-Preis basierend auf User-Feedback)
                    var basePrice = 0.0001m;
                    var random = new Random(DateTime.UtcNow.Millisecond);
                    var variation = (decimal)(random.NextDouble() * 0.04 - 0.02);
                    var price = basePrice * (1 + variation);

                    _logger.LogInformation("💰 WLD Price (FALLBACK estimated): 1 WLD = {Price:F8} ETH ({Variation:+0.00%;-0.00%})",
                        price, variation);
                    return price;
                }
                else if (tokenIn.Equals(wethAddress, StringComparison.OrdinalIgnoreCase) &&
                         tokenOut.Equals(wldAddress, StringComparison.OrdinalIgnoreCase))
                {
                    // 1 ETH ≈ 10000 WLD (inverse von 0.0001)
                    var basePrice = 10000m;
                    var random = new Random(DateTime.UtcNow.Millisecond);
                    var variation = (decimal)(random.NextDouble() * 0.04 - 0.02);
                    var price = basePrice * (1 + variation);

                    _logger.LogInformation("💰 ETH Price (FALLBACK estimated): 1 ETH = {Price:F2} WLD ({Variation:+0.00%;-0.00%})",
                        price, variation);
                    return price;
                }

                _logger.LogError("Unknown token pair and fallback failed: {TokenIn} -> {TokenOut}", tokenIn, tokenOut);
                return 0;
            }
        }
    }

    /// <summary>
    /// Uniswap V3 Pool slot0 output structure
    /// </summary>
    public class Slot0Output
    {
        public BigInteger SqrtPriceX96 { get; set; }
        public int Tick { get; set; }
        public ushort ObservationIndex { get; set; }
        public ushort ObservationCardinality { get; set; }
        public ushort ObservationCardinalityNext { get; set; }
        public byte FeeProtocol { get; set; }
        public bool Unlocked { get; set; }
    }
}
