using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using System.Text.Json;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    public class BunnyUploadService : IBlobUploadService
    {
        private const long MaxImageBytes = 15L * 1024 * 1024;
        private const long MaxVideoBytes = 200L * 1024 * 1024;
        private const long TargetSourceImageBytes = 850_000; // ~0.85 MB
        private const long TargetThumbImageBytes = 250_000;
        private const int MagicHeaderBytes = 16;
        private const int MaxStreamPollAttempts = 60; // 60 Sekunden max Wartezeit
        private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private static readonly string[] AllowedVideoExtensions = { ".mp4", ".mov", ".webm" };
        private readonly IConfiguration _configuration;
        private readonly ILogger<BunnyUploadService> _logger;
        private readonly HttpClient _httpClient;

        public BunnyUploadService(IConfiguration configuration, ILogger<BunnyUploadService> logger, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("BunnyStorage");
        }

        public async Task DeleteVideoAsync(string videoGuid)
        {
            if (string.IsNullOrWhiteSpace(videoGuid))
            {
                return;
            }

            try
            {
                var apiKey = _configuration["Bunny_Net_Api_Stream"];
                var libraryId = _configuration["Bunny_Net_Stream_ID"];
                if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(libraryId))
                {
                    _logger.LogWarning("Cannot delete Bunny video {VideoGuid}: stream credentials missing.", videoGuid);
                    return;
                }

                var url = $"https://video.bunnycdn.com/library/{libraryId}/videos/{videoGuid}";
                using var request = new HttpRequestMessage(HttpMethod.Delete, url);
                request.Headers.Add("AccessKey", apiKey);
                using var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Deleted orphaned Bunny video {VideoGuid}.", videoGuid);
                }
                else
                {
                    _logger.LogWarning("Failed to delete Bunny video {VideoGuid}: {Status}", videoGuid, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error deleting Bunny video {VideoGuid}.", videoGuid);
            }
        }

        public async Task<UploadContentResult> UploadContentToBlob(IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("Datei ist leer oder nicht vorhanden.");

            ValidateFileUpload(file);

            // Bunny.net Config
            string bunnyStoragePassword = _configuration["Bunny_Net_Passwort_Lager"];
            string bunnyStorageAddress = _configuration["Bunny_Net_Storage_Adres"];
            string bunnyCdnHostname = _configuration["Bunny_net_host_name"];
            string bunnyStreamApiKey = _configuration["Bunny_Net_Api_Stream"];
            string bunnyStreamLibraryId = _configuration["Bunny_Net_Stream_ID"];
            string bunnyStreamHostname = _configuration["Bunny_Net_Stream_Host_Name"];

            if (string.IsNullOrWhiteSpace(bunnyStoragePassword))
                throw new InvalidOperationException("Konfiguration fehlt: Bunny_Net_Passwort_Lager");

            if (string.IsNullOrWhiteSpace(bunnyStorageAddress))
                throw new InvalidOperationException("Konfiguration fehlt: Bunny_Net_Storage_Adres");

            if (string.IsNullOrWhiteSpace(bunnyCdnHostname))
                throw new InvalidOperationException("Konfiguration fehlt: Bunny_net_host_name");

            string fileName = Path.GetFileNameWithoutExtension(file.FileName);
            string uniqueToken = Guid.NewGuid().ToString("N");

            // ---- IMAGES ----
            if (file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                var sourceName = $"{fileName}_{uniqueToken}.webp";
                var thumbName = $"{fileName}_{uniqueToken}_thumb.webp";

                using var sourceStream = await ConvertImageToWebpAsync(file, 1080, 1920, TargetSourceImageBytes);
                using var thumbStream = await ConvertImageToWebpAsync(file, 400, 711, TargetThumbImageBytes);

                var sourceUrl = await UploadToBunnyAsync(bunnyStorageAddress, bunnyStoragePassword, bunnyCdnHostname, sourceName, sourceStream, "image/webp");
                var thumbUrl = await UploadToBunnyAsync(bunnyStorageAddress, bunnyStoragePassword, bunnyCdnHostname, thumbName, thumbStream, "image/webp");


                return new UploadContentResult
                {
                    SourceUrl = sourceUrl,
                    ThumbnailUrl = thumbUrl
                };
            }

            // ---- VIDEOS ----
            // Bunny Stream ist optional - wenn nicht konfiguriert, wird Bunny Storage verwendet
            bool useBunnyStream = !string.IsNullOrWhiteSpace(bunnyStreamApiKey)
                && !string.IsNullOrWhiteSpace(bunnyStreamLibraryId)
                && !string.IsNullOrWhiteSpace(bunnyStreamHostname);

            if (useBunnyStream)
            {
                // Bunny Stream: Automatisches Transcoding & Thumbnails (non-blocking)
                var streamResult = await UploadToBunnyStreamAsync(bunnyStreamApiKey, bunnyStreamLibraryId, bunnyStreamHostname, file, fileName, waitForTranscoding: false);

                return new UploadContentResult
                {
                    SourceUrl = streamResult.VideoUrl,
                    ThumbnailUrl = streamResult.ThumbnailUrl,
                    VideoGuid = streamResult.VideoGuid
                };
            }
            else
            {
                // Fallback: Bunny Storage (ohne automatisches Processing)
                _logger.LogWarning("Bunny Stream not configured. Video will be uploaded to Bunny Storage without automatic processing.");

                var videoName = $"{fileName}_{uniqueToken}.mp4";
                await using var videoStream = file.OpenReadStream();
                var videoUrl = await UploadToBunnyAsync(bunnyStorageAddress, bunnyStoragePassword, bunnyCdnHostname, videoName, videoStream, "video/mp4");

                return new UploadContentResult
                {
                    SourceUrl = videoUrl,
                    ThumbnailUrl = videoUrl  // Use video URL as thumbnail fallback
                };
            }
        }

        // Bunny.net CDN Hosts für SSRF-Schutz (Wildcard-Check in Methode)
        private static readonly string[] AllowedUrlHostPatterns = {
            ".b-cdn.net",
            "storage.bunnycdn.com",
            "mediadelivery.net"
        };

        public async Task<UploadContentResult> UploadContentToBlobFromUrl(string sourceUrl)
        {
            if (string.IsNullOrWhiteSpace(sourceUrl))
                throw new ArgumentException("Bildquelle fehlt.");

            // Security: SSRF-Schutz - nur HTTPS und bekannte Hosts erlauben
            if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var parsedUri))
                throw new ArgumentException("Ungueltige URL.");

            if (parsedUri.Scheme != Uri.UriSchemeHttps)
                throw new ArgumentException("Nur HTTPS-URLs sind erlaubt.");

            if (!AllowedUrlHostPatterns.Any(pattern =>
                parsedUri.Host.Equals(pattern.TrimStart('.'), StringComparison.OrdinalIgnoreCase) ||
                parsedUri.Host.EndsWith(pattern, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning("SSRF-Schutz: Abgelehnte URL-Host: {Host}", parsedUri.Host);
                throw new ArgumentException("URL-Host ist nicht erlaubt.");
            }

            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            using var response = await httpClient.GetAsync(parsedUri);
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

        private async Task<string> UploadToBunnyAsync(
            string storageAddress,
            string accessKey,
            string cdnHostname,
            string fileName,
            Stream stream,
            string contentType)
        {
            stream.Position = 0;

            // Bunny Storage API: PUT zu https://storage.bunnycdn.com/avocadon/{fileName}
            var uploadUrl = $"{storageAddress.TrimEnd('/')}/{fileName}";

            using var request = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
            request.Headers.Add("AccessKey", accessKey);
            request.Content = new StreamContent(stream);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("❌ Bunny Upload failed: {StatusCode} - {Error}", response.StatusCode, error);
                throw new InvalidOperationException($"Bunny Upload failed: {response.StatusCode}");
            }

            // CDN URL zurückgeben (nicht Storage URL)
            return GetBunnyCdnUrl(cdnHostname, fileName);
        }

        private static string GetBunnyCdnUrl(string cdnHostname, string fileName)
        {
            // CDN URL: https://{cdn-hostname}/{fileName}
            // Hostname kann sein: avocadon.b-cdn.net oder custom domain wie cdn.example.com
            var hostname = cdnHostname.Replace("https://", "").Replace("http://", "").TrimEnd('/');
            return $"https://{hostname}/{fileName}";
        }

        private static async Task<MemoryStream> ConvertImageToWebpAsync(IFormFile file, int width, int height, long targetMaxBytes)
        {
            using var inputStream = file.OpenReadStream();
            using var image = await Image.LoadAsync(inputStream);

            image.Mutate(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.Crop
            }));

            var quality = 88;
            for (var attempt = 0; attempt < 8; attempt++)
            {
                var outputStream = new MemoryStream();
                var encoder = new WebpEncoder { Quality = quality };

                await image.SaveAsWebpAsync(outputStream, encoder);

                if (outputStream.Length <= targetMaxBytes || quality <= 35)
                {
                    outputStream.Position = 0;
                    return outputStream;
                }

                outputStream.Dispose();
                quality -= 8;
            }

            var fallbackStream = new MemoryStream();
            await image.SaveAsWebpAsync(fallbackStream, new WebpEncoder { Quality = 35 });
            fallbackStream.Position = 0;
            return fallbackStream;
        }

        private async Task<BunnyStreamResult> UploadToBunnyStreamAsync(
            string apiKey,
            string libraryId,
            string streamHostname,
            IFormFile file,
            string fileName,
            bool waitForTranscoding = true)
        {
            // Schritt 1: Video-Objekt in Bunny Stream erstellen
            var createUrl = $"https://video.bunnycdn.com/library/{libraryId}/videos";
            var createRequest = new HttpRequestMessage(HttpMethod.Post, createUrl);
            createRequest.Headers.Add("AccessKey", apiKey);
            createRequest.Content = JsonContent.Create(new { title = fileName });

            var createResponse = await _httpClient.SendAsync(createRequest);
            createResponse.EnsureSuccessStatusCode();

            var createResult = await JsonSerializer.DeserializeAsync<JsonElement>(
                await createResponse.Content.ReadAsStreamAsync());

            var videoGuid = createResult.GetProperty("guid").GetString();
            var videoId = createResult.GetProperty("videoLibraryId").GetInt64();

            _logger.LogInformation("📹 Created Bunny Stream video: {VideoGuid}", videoGuid);

            // Schritt 2: Video hochladen (mit Retry-Logic für Timeouts)
            const int maxRetries = 3;
            Exception? lastException = null;

            for (int retry = 0; retry < maxRetries; retry++)
            {
                try
                {
                    _logger.LogInformation("⬆️ Uploading video to Bunny Stream (Attempt {Retry}/{Max})...", retry + 1, maxRetries);

                    var uploadUrl = $"https://video.bunnycdn.com/library/{libraryId}/videos/{videoGuid}";
                    var uploadRequest = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
                    uploadRequest.Headers.Add("AccessKey", apiKey);

                    await using var videoStream = file.OpenReadStream();
                    uploadRequest.Content = new StreamContent(videoStream);
                    uploadRequest.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

                    var uploadResponse = await _httpClient.SendAsync(uploadRequest);
                    uploadResponse.EnsureSuccessStatusCode();

                    _logger.LogInformation("✅ Video uploaded to Bunny Stream: {VideoGuid}", videoGuid);
                    break; // Erfolg - raus aus Retry-Loop
                }
                catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
                {
                    lastException = ex;
                    _logger.LogWarning("⚠️ Upload timeout (Attempt {Retry}/{Max}): {Message}",
                        retry + 1, maxRetries, ex.Message);

                    if (retry < maxRetries - 1)
                    {
                        var delaySeconds = (retry + 1) * 2; // Exponential backoff: 2s, 4s, 6s
                        _logger.LogInformation("⏳ Waiting {Delay}s before retry...", delaySeconds);
                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                    }
                }
                catch (HttpRequestException ex)
                {
                    lastException = ex;
                    _logger.LogWarning("⚠️ Upload network error (Attempt {Retry}/{Max}): {Message}",
                        retry + 1, maxRetries, ex.Message);

                    if (retry < maxRetries - 1)
                    {
                        await Task.Delay(TimeSpan.FromSeconds((retry + 1) * 2));
                    }
                }
            }

            // Wenn alle Retries fehlgeschlagen sind
            if (lastException != null)
            {
                _logger.LogError(lastException,
                    "❌ Video upload failed after {MaxRetries} attempts. File size: {FileSize:N0} bytes",
                    maxRetries, file.Length);
                throw new InvalidOperationException(
                    $"Video-Upload fehlgeschlagen nach {maxRetries} Versuchen. " +
                    $"Datei zu groß ({file.Length / 1024 / 1024:N1} MB) oder Verbindung zu langsam. " +
                    "Bitte versuchen Sie es später erneut oder verwenden Sie eine schnellere Internetverbindung.",
                    lastException);
            }

            // Hostname einmal vorbereiten
            var hostname = streamHostname.Replace("https://", "").Replace("http://", "").TrimEnd('/');

            // Return immediately if not waiting for transcoding
            if (!waitForTranscoding)
            {
                var pendingVideoUrl = $"https://{hostname}/{videoGuid}/playlist.m3u8";
                var pendingThumbUrl = $"https://{hostname}/{videoGuid}/thumbnail.jpg";
                _logger.LogInformation("⏩ Video uploaded, not waiting for transcoding: {VideoGuid}", videoGuid);
                return new BunnyStreamResult(pendingVideoUrl, pendingThumbUrl, videoGuid);
            }

            // Schritt 3: Auf Transcoding warten (Polling)
            for (int attempt = 0; attempt < MaxStreamPollAttempts; attempt++)
            {
                await Task.Delay(1000); // 1 Sekunde warten

                var statusUrl = $"https://video.bunnycdn.com/library/{libraryId}/videos/{videoGuid}";
                var statusRequest = new HttpRequestMessage(HttpMethod.Get, statusUrl);
                statusRequest.Headers.Add("AccessKey", apiKey);

                var statusResponse = await _httpClient.SendAsync(statusRequest);
                statusResponse.EnsureSuccessStatusCode();

                var statusResult = await JsonSerializer.DeserializeAsync<JsonElement>(
                    await statusResponse.Content.ReadAsStreamAsync());

                var status = statusResult.GetProperty("status").GetInt32();

                // Status: 0=Created, 1=Uploading, 2=Processing, 3=Transcoding, 4=Finished, 5=Error
                if (status == 4) // Finished
                {
                    var videoUrl = $"https://{hostname}/{videoGuid}/playlist.m3u8";
                    var thumbnailUrl = $"https://{hostname}/{videoGuid}/thumbnail.jpg";

                    _logger.LogInformation("🎉 Video transcoding finished: {VideoUrl}", videoUrl);

                    return new BunnyStreamResult(videoUrl, thumbnailUrl, videoGuid);
                }

                if (status == 5) // Error
                {
                    throw new InvalidOperationException("Bunny Stream Transcoding failed");
                }

                _logger.LogInformation("⏳ Waiting for transcoding... (Status: {Status}, Attempt: {Attempt}/{Max})",
                    status, attempt + 1, MaxStreamPollAttempts);
            }

            // Timeout - aber Video ist hochgeladen, nur noch nicht fertig
            _logger.LogWarning("⚠️ Transcoding timeout - returning placeholder URLs");
            var placeholderVideoUrl = $"https://{hostname}/{videoGuid}/playlist.m3u8";
            var placeholderThumbUrl = $"https://{hostname}/{videoGuid}/thumbnail.jpg";

            return new BunnyStreamResult(placeholderVideoUrl, placeholderThumbUrl, videoGuid);
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

        private record BunnyStreamResult(string VideoUrl, string ThumbnailUrl, string VideoGuid);
    }
}
