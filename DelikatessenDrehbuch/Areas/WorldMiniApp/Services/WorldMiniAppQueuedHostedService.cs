using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    public sealed class WorldMiniAppQueuedHostedService : BackgroundService
    {
        private readonly IBackgroundTaskQueue _queue;
        private readonly ILogger<WorldMiniAppQueuedHostedService> _logger;

        public WorldMiniAppQueuedHostedService(
            IBackgroundTaskQueue queue,
            ILogger<WorldMiniAppQueuedHostedService> logger)
        {
            _queue = queue;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                Func<CancellationToken, ValueTask> workItem;
                try
                {
                    workItem = await _queue.DequeueAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background queue dequeue failed.");
                    await Task.Delay(500, stoppingToken);
                    continue;
                }

                try
                {
                    await workItem(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background queue work item failed.");
                }
            }
        }
    }
}

