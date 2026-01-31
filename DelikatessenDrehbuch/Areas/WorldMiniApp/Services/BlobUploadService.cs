using System.Diagnostics;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;

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
        //TODO:Upload prüfen posting vervolständigen
        public async Task<string> UploadContentToBlob(IFormFile file)
        {
            // 1. Hole Config aus Umgebungsvariablen (oder appsettings.json)
            // Passe die Keys an, wie du sie genannt hast!
            string connectionString = _configuration["Blob_Conection_String"];
            string containerName = _configuration["World-App-Blop-Name"]?? "blob-world-mini-app";

            // 2. Client erstellen
            var blobServiceClient = new BlobServiceClient(connectionString);
            var blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);

            // Sicherstellen, dass der Container existiert (optional, aber sicher ist sicher)
            await blobContainerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

            string fileName = Path.GetFileNameWithoutExtension(file.FileName);
            string extension = Path.GetExtension(file.FileName);
            string contentType = file.ContentType;
            string blobExtension = extension;
            string? tempInputPath = null;
            string? tempOutputPath = null;

            Stream uploadStream;
            bool isVideo = IsVideoFile(file);

            try
            {
                if (isVideo)
                {
                    tempInputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{extension}");
                    await using (var tempInput = File.Create(tempInputPath))
                    {
                        await file.CopyToAsync(tempInput);
                    }

                    tempOutputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mp4");
                    if (await TryOptimizeVideoAsync(tempInputPath, tempOutputPath))
                    {
                        uploadStream = File.OpenReadStream(tempOutputPath);
                        contentType = "video/mp4";
                        blobExtension = ".mp4";
                    }
                    else
                    {
                        uploadStream = file.OpenReadStream();
                    }
                }
                else
                {
                    uploadStream = file.OpenReadStream();
                }

                string uniqueFileName = $"{fileName}_{Guid.NewGuid()}{blobExtension}";
                var blobClient = blobContainerClient.GetBlobClient(uniqueFileName);

                // 4. Hochladen mit korrekten Headern (WICHTIG FÜR VIDEOS!)
                var blobHttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType // z.B. "video/mp4"
                };

                using (uploadStream)
                {
                    await blobClient.UploadAsync(uploadStream, new BlobUploadOptions
                    {
                        HttpHeaders = blobHttpHeaders
                    });
                }

                // 5. Die komplette URL zurückgeben (z.B. https://meinapp.blob.core.windows.net/videos/...)
                return blobClient.Uri.ToString();
            }
            finally
            {
                CleanupTempFile(tempInputPath);
                CleanupTempFile(tempOutputPath);
            }
        }

        private static bool IsVideoFile(IFormFile file)
        {
            if (!string.IsNullOrWhiteSpace(file.ContentType)
                && file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string extension = Path.GetExtension(file.FileName);
            return extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".mov", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".m4v", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".avi", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".webm", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<bool> TryOptimizeVideoAsync(string inputPath, string outputPath)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-y -i \"{inputPath}\" -movflags +faststart -c:v libx264 -crf 22 -c:a aac -b:a 128k \"{outputPath}\"",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                string stderr = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    _logger.LogWarning("Video-Optimierung fehlgeschlagen (ExitCode {ExitCode}). FFmpeg-Fehler: {Error}", process.ExitCode, stderr);
                    return false;
                }

                if (!File.Exists(outputPath))
                {
                    _logger.LogWarning("Video-Optimierung lieferte keine Ausgabedatei: {OutputPath}", outputPath);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Video-Optimierung fehlgeschlagen. Upload erfolgt ohne Optimierung.");
                return false;
            }
        }

        private static void CleanupTempFile(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Ignorieren, wenn Temp-Dateien nicht gelöscht werden können.
            }
        }
    }
}
