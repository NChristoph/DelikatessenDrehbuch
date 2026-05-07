using System.Text.Json;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    public sealed class BunnyVideoTranscriptionService : IBunnyVideoTranscriptionService
    {
        private const int MaxStatusPollAttempts = 60;
        private static readonly TimeSpan StatusPollDelay = TimeSpan.FromSeconds(5);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IBackgroundTaskQueue _queue;
        private readonly ILogger<BunnyVideoTranscriptionService> _logger;

        public BunnyVideoTranscriptionService(
            IServiceScopeFactory scopeFactory,
            IBackgroundTaskQueue queue,
            ILogger<BunnyVideoTranscriptionService> logger)
        {
            _scopeFactory = scopeFactory;
            _queue = queue;
            _logger = logger;
        }

        public Task QueueTranscriptionAsync(int postingId, string videoGuid, CancellationToken cancellationToken = default)
        {
            if (postingId <= 0 || string.IsNullOrWhiteSpace(videoGuid))
            {
                return Task.CompletedTask;
            }

            return _queue.QueueAsync(async stoppingToken =>
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, stoppingToken);
                await RequestTranscriptionWhenReadyAsync(postingId, videoGuid.Trim(), linkedCts.Token);
            }).AsTask();
        }

        private async Task RequestTranscriptionWhenReadyAsync(int postingId, string videoGuid, CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

            var apiKey = configuration["Bunny_Net_Api_Stream"];
            var libraryId = configuration["Bunny_Net_Stream_ID"];
            var sourceLanguage = (configuration["Bunny_Net_Transcribe_SourceLanguage"] ?? "de").Trim().ToLowerInvariant();
            var targetLanguages = ParseTargetLanguages(configuration["Bunny_Net_Transcribe_TargetLanguages"], sourceLanguage);

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(libraryId))
            {
                _logger.LogWarning("Skipping Bunny transcription for posting {PostingId}. Stream transcription config is incomplete.", postingId);
                return;
            }

            var client = httpClientFactory.CreateClient("BunnyStorage");

            for (var attempt = 1; attempt <= MaxStatusPollAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var status = await GetVideoStatusAsync(client, apiKey, libraryId, videoGuid, cancellationToken);
                if (status == 4)
                {
                    await RequestTranscriptionAsync(client, apiKey, libraryId, videoGuid, sourceLanguage, targetLanguages, cancellationToken);
                    _logger.LogInformation("Queued Bunny transcription for posting {PostingId} / video {VideoGuid}.", postingId, videoGuid);
                    return;
                }

                if (status == 5)
                {
                    _logger.LogWarning("Skipping Bunny transcription for posting {PostingId}. Video {VideoGuid} failed encoding.", postingId, videoGuid);
                    return;
                }

                await Task.Delay(StatusPollDelay, cancellationToken);
            }

            _logger.LogWarning("Timed out waiting for Bunny video {VideoGuid} to finish before requesting transcription.", videoGuid);
        }

        private static async Task<int> GetVideoStatusAsync(HttpClient client, string apiKey, string libraryId, string videoGuid, CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://video.bunnycdn.com/library/{libraryId}/videos/{videoGuid}");
            request.Headers.Add("AccessKey", apiKey);

            using var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await JsonSerializer.DeserializeAsync<JsonElement>(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);

            return payload.TryGetProperty("status", out var statusElement)
                ? statusElement.GetInt32()
                : 0;
        }

        private static async Task RequestTranscriptionAsync(
            HttpClient client,
            string apiKey,
            string libraryId,
            string videoGuid,
            string sourceLanguage,
            IReadOnlyList<string> targetLanguages,
            CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"https://video.bunnycdn.com/library/{libraryId}/videos/{videoGuid}/transcribe");
            request.Headers.Add("AccessKey", apiKey);
            request.Content = JsonContent.Create(new
            {
                sourceLanguage,
                targetLanguages = targetLanguages.Count > 0 ? targetLanguages : null,
                generateTitle = false,
                generateDescription = false,
                generateChapters = false,
                generateMoments = false
            });

            using var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        private static IReadOnlyList<string> ParseTargetLanguages(string? raw, string sourceLanguage)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return Array.Empty<string>();
            }

            return raw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => x.ToLowerInvariant())
                .Where(x => x.Length == 2 && !string.Equals(x, sourceLanguage, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
