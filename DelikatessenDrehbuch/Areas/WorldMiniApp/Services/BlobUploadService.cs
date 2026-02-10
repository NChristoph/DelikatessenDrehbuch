using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using System.Text;
using System.Text.Json;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    public class BlobUploadService : IBlobUploadService
    {
        private const long MaxImageBytes = 15L * 1024 * 1024;
        private const long MaxVideoBytes = 200L * 1024 * 1024;
        private const int MagicHeaderBytes = 16;
        private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private static readonly string[] AllowedVideoExtensions = { ".mp4", ".mov", ".webm" };
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

            ValidateFileUpload(file);

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
            var processedVideoName = GetProcessedVideoName(videoResult.BlobName);
            var processedVideoUrl = blobContainerClient.GetBlobClient(processedVideoName).Uri.ToString();

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
                SourceUrl = processedVideoUrl,
                ThumbnailUrl = processedVideoUrl
            };
        }

        public async Task<UploadContentResult> UploadContentToBlobFromUrl(string sourceUrl)
        {
            if (string.IsNullOrWhiteSpace(sourceUrl))
                throw new ArgumentException("Bildquelle fehlt.");

            using var httpClient = new HttpClient();
            using var response = await httpClient.GetAsync(sourceUrl);
            response.EnsureSuccessStatusCode();

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            var extension = GetExtensionFromContentType(contentType);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = Path.GetExtension(new Uri(sourceUrl).AbsolutePath);
            }
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".jpg";
            }

            // Blob/CDN Responses kommen teilweise als application/octet-stream zurück.
            // Dann leiten wir den Dateityp über die Extension her, damit die Upload-Validierung
            // (image/* / video/*) nicht fälschlich blockiert.
            if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                && !contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            {
                contentType = GetContentTypeFromExtension(extension) ?? contentType;
            }

            var fileName = $"recipe_{Guid.NewGuid():N}{extension}";
            await using var stream = new MemoryStream();
            await response.Content.CopyToAsync(stream);
            stream.Position = 0;

            var formFile = new FormFile(stream, 0, stream.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };

            return await UploadContentToBlob(formFile);
        }

        private static void ValidateFileUpload(IFormFile file)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var isImage = file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                          || AllowedImageExtensions.Contains(extension);
            var isVideo = file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)
                          || AllowedVideoExtensions.Contains(extension);

            if (!isImage && !isVideo)
            {
                throw new InvalidOperationException("Ungültiger Dateityp. Bitte ein Bild oder Video hochladen.");
            }

            if (isImage)
            {
                if (!AllowedImageExtensions.Contains(extension))
                    throw new InvalidOperationException("Ungültiges Bildformat. Bitte JPG, PNG oder WEBP nutzen.");

                if (file.Length > MaxImageBytes)
                    throw new InvalidOperationException("Bild ist zu groß. Maximal 15 MB erlaubt.");
            }

            if (isVideo)
            {
                if (!AllowedVideoExtensions.Contains(extension))
                    throw new InvalidOperationException("Ungültiges Videoformat. Bitte MP4, MOV oder WEBM nutzen.");

                if (file.Length > MaxVideoBytes)
                    throw new InvalidOperationException("Video ist zu groß. Maximal 200 MB erlaubt.");
            }

            if (!MatchesMagicBytes(file, extension, isImage, isVideo))
            {
                throw new InvalidOperationException("Dateisignatur stimmt nicht mit dem Dateityp überein.");
            }
        }

        private static bool MatchesMagicBytes(IFormFile file, string extension, bool isImage, bool isVideo)
        {
            Span<byte> header = stackalloc byte[MagicHeaderBytes];
            using var stream = file.OpenReadStream();
            var read = stream.Read(header);
            if (read < 4)
            {
                return false;
            }

            if (isImage)
            {
                if (extension is ".jpg" or ".jpeg")
                {
                    return header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
                }

                if (extension == ".png")
                {
                    return read >= 8
                        && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
                        && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;
                }

                if (extension == ".webp")
                {
                    return read >= 12
                        && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
                        && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;
                }
            }

            if (isVideo)
            {
                if (extension is ".mp4" or ".mov")
                {
                    return read >= 8
                        && header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70;
                }

                if (extension == ".webm")
                {
                    return header[0] == 0x1A && header[1] == 0x45 && header[2] == 0xDF && header[3] == 0xA3;
                }
            }

            return false;
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

        private static string GetProcessedVideoName(string rawVideoName)
        {
            return Path.GetFileNameWithoutExtension(rawVideoName) + "_processed.mp4";
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

        private static string? GetExtensionFromContentType(string contentType)
        {
            return contentType.ToLowerInvariant() switch
            {
                "image/jpeg" => ".jpg",
                "image/jpg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",
                "video/mp4" => ".mp4",
                "video/quicktime" => ".mov",
                "video/webm" => ".webm",
                _ => null
            };
        }

        private static string? GetContentTypeFromExtension(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".mp4" => "video/mp4",
                ".mov" => "video/quicktime",
                ".webm" => "video/webm",
                _ => null
            };
        }

        private record UploadedVideoResult(string SourceUrl, string BlobName);
    }
}
