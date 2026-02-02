using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using System.Text;
using System.Text.Json;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    public class BlobUploadService : IBlobUploadService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<BlobUploadService> _logger;

        public BlobUploadService(IConfiguration configuration, ILogger<BlobUploadService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<UploadContentResult> UploadContentToBlob(IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("Datei ist leer oder nicht vorhanden.");

            // Deine Keys (ich behalte sie bei)
            string storageConnectionString = _configuration["Blob_Conection_String"];
            if (string.IsNullOrWhiteSpace(storageConnectionString))
                throw new InvalidOperationException("Konfiguration fehlt: Blob_Conection_String");

            string containerName = _configuration["World-App-Blop-Name"] ?? "blob-world-mini-app";

            // Queue: optional eigener Key, fallback auf Storage-ConnString
            string queueConnectionString =
                _configuration["BlobStorageConnection"] // falls du den so nennen willst
                ?? _configuration["Queue_Connection_String"]
                ?? storageConnectionString;

            string queueName = _configuration["VideoProcessingQueueName"] ?? "video-processing-queue";

            var blobServiceClient = new BlobServiceClient(storageConnectionString);
            var blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);

            await blobContainerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

            string fileName = Path.GetFileNameWithoutExtension(file.FileName);
            string uniqueToken = Guid.NewGuid().ToString("N");

            // ---- IMAGES ----
            if (file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                var sourceName = $"{fileName}_{uniqueToken}.webp";
                var thumbName = $"{fileName}_{uniqueToken}_thumb.webp";

                using var sourceStream = await ConvertImageToWebpAsync(file, 1080, 1920);
                using var thumbStream = await ConvertImageToWebpAsync(file, 400, 711);

                var sourceUrl = await UploadStreamAsync(blobContainerClient, sourceName, sourceStream, "image/webp");
                var thumbUrl = await UploadStreamAsync(blobContainerClient, thumbName, thumbStream, "image/webp");

                _logger.LogInformation("✅ Image uploaded: {SourceBlob} + {ThumbBlob}", sourceName, thumbName);

                return new UploadContentResult
                {
                    SourceUrl = sourceUrl,
                    ThumbnailUrl = thumbUrl
                };
            }

            // ---- VIDEOS ----
            var videoResult = await UploadRawVideoAsync(blobContainerClient, file, fileName, uniqueToken);

            // Nach Video-Upload: Queue-Job erstellen
            try
            {
                await EnqueueVideoJobAsync(
                    queueConnectionString,
                    queueName,
                    containerName,
                    videoResult.BlobName // wichtig: BlobName fürs Processing
                );

                _logger.LogInformation("📩 Video job enqueued: {BlobName} -> {Queue}", videoResult.BlobName, queueName);
            }
            catch (Exception ex)
            {
                // Upload war erfolgreich, Queue aber nicht: das willst du sehen!
                _logger.LogError(ex, "❌ Video uploaded but enqueue failed for blob: {BlobName}", videoResult.BlobName);

                // Option A: trotzdem OK zurückgeben (Upload steht ja im Blob)
                // Option B: Exception werfen, damit UI es merkt
                // Ich mache: Exception werfen, weil sonst "still" nix passiert:
                throw;
            }

            // Ergebnis für UI/DB
            return new UploadContentResult
            {
                SourceUrl = videoResult.SourceUrl,
                ThumbnailUrl = videoResult.SourceUrl
            };
        }

        private static async Task<string> UploadStreamAsync(
            BlobContainerClient blobContainerClient,
            string blobName,
            Stream stream,
            string contentType)
        {
            stream.Position = 0;

            var blobClient = blobContainerClient.GetBlobClient(blobName);
            var blobHttpHeaders = new BlobHttpHeaders { ContentType = contentType };

            await blobClient.UploadAsync(stream, new BlobUploadOptions
            {
                HttpHeaders = blobHttpHeaders
            });

            return blobClient.Uri.ToString();
        }

        private static async Task<MemoryStream> ConvertImageToWebpAsync(IFormFile file, int width, int height)
        {
            using var inputStream = file.OpenReadStream();
            using var image = await Image.LoadAsync(inputStream);

            image.Mutate(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.Crop
            }));

            var outputStream = new MemoryStream();
            var encoder = new WebpEncoder { Quality = 90 };

            await image.SaveAsWebpAsync(outputStream, encoder);
            outputStream.Position = 0;

            return outputStream;
        }

        // Return-Typ erweitert, damit wir BlobName für Queue haben
        private static async Task<UploadedVideoResult> UploadRawVideoAsync(
            BlobContainerClient blobContainerClient,
            IFormFile file,
            string fileName,
            string uniqueToken)
        {
            var videoName = $"{fileName}_{uniqueToken}.mp4";

            await using var videoStream = file.OpenReadStream();
            var sourceUrl = await UploadStreamAsync(blobContainerClient, videoName, videoStream, "video/mp4");

            return new UploadedVideoResult(sourceUrl, videoName);
        }

        private static async Task EnqueueVideoJobAsync(
            string connectionString,
            string queueName,
            string containerName,
            string blobName)
        {
            var queueClient = new QueueClient(connectionString, queueName);

            await queueClient.CreateIfNotExistsAsync();

            // Nachricht (JSON)
            var payload = new
            {
                container = containerName,
                blobName = blobName,
                uploadedAt = DateTime.UtcNow
            };

            string json = JsonSerializer.Serialize(payload);

            // Queue braucht base64
            string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

            await queueClient.SendMessageAsync(base64);
        }

        private record UploadedVideoResult(string SourceUrl, string BlobName);
    }
}
