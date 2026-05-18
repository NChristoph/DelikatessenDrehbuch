using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using System.Numerics;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    public class BlockchainService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<BlockchainService> _logger;

        public BlockchainService(
            IConfiguration configuration,
            ILogger<BlockchainService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Erstellt eine Web3 Instanz mit dem Private Key des Agents
        /// </summary>
        public Web3 GetWeb3Instance(string privateKey)
        {
            var rpcUrl = _configuration["WorldChain:RpcUrl"] ?? "https://worldchain-mainnet.g.alchemy.com/public";
            var account = new Account(privateKey, 480); // Chain ID 480 = World Chain
            return new Web3(account, rpcUrl);
        }

        /// <summary>
        /// Estimiert Gas für eine Transaction
        /// </summary>
        public async Task<BigInteger> EstimateGasAsync(Web3 web3, TransactionInput txInput)
        {
            try
            {
                var gasEstimate = await web3.Eth.Transactions.EstimateGas.SendRequestAsync(txInput);
                // Add 20% buffer
                return gasEstimate.Value * 120 / 100;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Gas estimation failed, using default: {Error}", ex.Message);
                return new BigInteger(300000); // Default
            }
        }

        /// <summary>
        /// Wartet auf Transaction Confirmation mit Timeout
        /// </summary>
        public async Task<TransactionReceipt?> WaitForTransactionReceiptAsync(
            Web3 web3,
            string txHash,
            int timeoutSeconds = 30)
        {
            var receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);
            var waited = 0;

            while (receipt == null && waited < timeoutSeconds)
            {
                await Task.Delay(2000);
                waited += 2;
                receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);

                if (waited % 10 == 0)
                {
                    _logger.LogInformation("Waiting for TX {TxHash} confirmation... ({Waited}s)",
                        txHash.Substring(0, 10), waited);
                }
            }

            if (receipt != null && receipt.Status?.Value == 1)
            {
                _logger.LogInformation("Transaction {TxHash} confirmed successfully", txHash.Substring(0, 10));
            }
            else if (receipt != null)
            {
                _logger.LogError("Transaction {TxHash} failed with status {Status}",
                    txHash.Substring(0, 10), receipt.Status?.Value);
            }
            else
            {
                _logger.LogError("Transaction {TxHash} confirmation timeout after {Timeout}s",
                    txHash.Substring(0, 10), timeoutSeconds);
            }

            return receipt;
        }

        /// <summary>
        /// Sendet Transaction mit Retry Logic
        /// </summary>
        public async Task<string?> SendTransactionWithRetryAsync(
            Web3 web3,
            Func<Task<string>> sendTransaction,
            int maxRetries = 3)
        {
            Exception? lastException = null;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    var txHash = await sendTransaction();
                    _logger.LogInformation("Transaction sent: {TxHash} (attempt {Attempt})",
                        txHash.Substring(0, 10), i + 1);
                    return txHash;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogWarning("Transaction attempt {Attempt} failed: {Error}",
                        i + 1, ex.Message);

                    if (i < maxRetries - 1)
                    {
                        await Task.Delay(2000 * (i + 1)); // Exponential backoff
                    }
                }
            }

            _logger.LogError("Transaction failed after {MaxRetries} attempts: {Error}",
                maxRetries, lastException?.Message);
            return null;
        }

        /// <summary>
        /// Holt aktuellen Gas Price
        /// </summary>
        public async Task<BigInteger> GetGasPriceAsync(Web3 web3)
        {
            try
            {
                var gasPrice = await web3.Eth.GasPrice.SendRequestAsync();
                // Add 10% for faster confirmation
                return gasPrice.Value * 110 / 100;
            }
            catch
            {
                // Fallback for World Chain (very low fees)
                return Web3.Convert.ToWei(0.001, Nethereum.Util.UnitConversion.EthUnit.Gwei);
            }
        }

        /// <summary>
        /// Checkt ob genug ETH für Gas vorhanden ist
        /// </summary>
        public async Task<bool> HasSufficientGasAsync(Web3 web3, string address, BigInteger estimatedGas)
        {
            var balance = await web3.Eth.GetBalance.SendRequestAsync(address);
            var gasPrice = await GetGasPriceAsync(web3);
            var requiredEth = estimatedGas * gasPrice;

            _logger.LogInformation("Gas check: Balance={Balance} ETH, Required={Required} ETH",
                Web3.Convert.FromWei(balance.Value),
                Web3.Convert.FromWei(requiredEth));

            return balance.Value >= requiredEth;
        }
    }
}
