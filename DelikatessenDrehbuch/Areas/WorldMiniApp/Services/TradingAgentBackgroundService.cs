using DelikatessenDrehbuch.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    public class TradingAgentBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<TradingAgentBackgroundService> _logger;
        private readonly IConfiguration _configuration;

        public TradingAgentBackgroundService(
            IServiceProvider services,
            ILogger<TradingAgentBackgroundService> logger,
            IConfiguration configuration)
        {
            _services = services;
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Trading Agent Background Service started");

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessActiveAgentsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in trading background service: {Message}", ex.Message);
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }

            _logger.LogInformation("Trading Agent Background Service stopped");
        }

        private async Task ProcessActiveAgentsAsync()
        {
            using var scope = _services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tradingAgentService = scope.ServiceProvider.GetRequiredService<TradingAgentService>();
            var tradingStrategyService = scope.ServiceProvider.GetRequiredService<TradingStrategyService>();

            var activeAgents = await context.WorldTradingAgents
                .Where(a => a.Status == "Active")
                .ToListAsync();

            _logger.LogInformation("Processing {Count} active trading agents", activeAgents.Count);

            foreach (var agent in activeAgents)
            {
                try
                {
                    _logger.LogInformation("Processing agent {AgentId} - {Name}", agent.Id, agent.AgentName);

                    // AgentKit-basierter Trading-Flow: kein automatischer ETH-Gas-Puffer noetig.
                    await tradingAgentService.CheckAndAutoSwapGasAsync(agent.Id);

                    var signal = await tradingStrategyService.AnalyzeMarketAsync(agent.Id);

                    _logger.LogInformation("Trading signal for agent {AgentId}: {Action} ({Confidence}% confidence)",
                        agent.Id, signal.Action, signal.Confidence);

                    agent.LastSignalAction = signal.Action;
                    agent.LastSignalConfidence = (int)signal.Confidence;
                    agent.LastSignalAt = DateTime.UtcNow;
                    agent.LastSignalReason = signal.Reason;
                    await context.SaveChangesAsync();

                    if (signal.Confidence >= 60 && signal.Action != "HOLD")
                    {
                        var privateKey = DecryptPrivateKey(agent.EncryptedPrivateKey);
                        await tradingStrategyService.ExecuteTradeAsync(agent.Id, signal, privateKey, agent.UserHash);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(2));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing agent {AgentId}: {Message}", agent.Id, ex.Message);
                }
            }

            _logger.LogInformation("Finished processing all active agents");
        }

        private string DecryptPrivateKey(string encryptedKey)
        {
            var fullCipher = Convert.FromBase64String(encryptedKey);

            var keyString = _configuration["TradingAgent:EncryptionKey"] ?? "CHANGE_THIS_TO_SECURE_KEY_32BYTES!";
            var key = Encoding.UTF8.GetBytes(keyString.PadRight(32).Substring(0, 32));

            using var aes = Aes.Create();
            aes.Key = key;

            var iv = new byte[16];
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
