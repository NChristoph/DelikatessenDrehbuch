using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DelikatessenDrehbuchViedeoProzessor
{
    public class Function1
    {
        private readonly ILogger<Function1> _logger;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly IConfiguration _config;
        private readonly string _ffmpegPath;

        public Function1(ILogger<Function1> logger, BlobServiceClient blobServiceClient, IConfiguration configuration)
        {
            _logger = logger;
            _blobServiceClient = blobServiceClient;
            _config = configuration;
            _ffmpegPath = ResolveFfmpegPath(configuration);
        }

        private sealed class VideoJob
        {
            public string? Container { get; set; }
            public string? BlobName { get; set; }
        }

        [Function("Function1")]
        public async Task Run(
            [QueueTrigger("%VideoProcessingQueue%", Connection = "QueueStorageConnection")] string message,
            FunctionContext context)
        {
          

            VideoJob job;
            try
            {
                job = JsonSerializer.Deserialize<VideoJob>(message, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? throw new InvalidOperationException("Message deserialized to null.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Invalid JSON in queue message. Moving to poison automatically.");
                throw; 
            }

            var containerName = job.Container ?? _config["VideoUploadContainer"] ?? "blob-world-mini-app";
            var blobName = job.BlobName;

            if (string.IsNullOrWhiteSpace(blobName))
            {
                _logger.LogError("❌ blobName missing in message.");
                throw new InvalidOperationException("blobName missing in message.");
            }

            if (!IsVideoFile(blobName))
            {
                _logger.LogInformation("Skipping non-video blob: {BlobName}", blobName);
                return;
            }

            var inputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{Path.GetExtension(blobName)}");
            var outputBlobName = Path.GetFileNameWithoutExtension(blobName) + "_processed.mp4";
            var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_processed.mp4");

            try
            {
                _logger.LogInformation("⬇️ Downloading blob: {Container}/{Blob}", containerName, blobName);

                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                var inputBlob = containerClient.GetBlobClient(blobName);

                // Existenzcheck
                if (!await inputBlob.ExistsAsync())
                {
                    _logger.LogError("❌ Blob not found: {Container}/{Blob}", containerName, blobName);
                    throw new FileNotFoundException($"Blob not found: {containerName}/{blobName}");
                }

                await using (var fs = File.Create(inputPath))
                {
                    await inputBlob.DownloadToAsync(fs);
                }
                string ffmpegToUse = _ffmpegPath;

                if (OperatingSystem.IsLinux())
                {
                    ffmpegToUse = await PrepareLinuxFfmpegAsync(_ffmpegPath, _logger);
                }

           
                var ffmpegArgs = $"-y -i \"{inputPath}\" -vf scale='min(1080,iw)':-2 -c:v libx264 -preset veryfast -crf 26 -tune fastdecode -movflags +faststart -c:a aac -b:a 128k -ac 2 \"{outputPath}\"";

                var startInfo = new ProcessStartInfo
                {
                    FileName = ffmpegToUse,
                    Arguments = ffmpegArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                    throw new InvalidOperationException("Failed to start ffmpeg process.");

                var stdOutTask = process.StandardOutput.ReadToEndAsync();
                var stdErrTask = process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync(CancellationToken.None);

                var stdOut = await stdOutTask;
                var stdErr = await stdErrTask;

                if (process.ExitCode != 0)
                {
                    _logger.LogError("❌ ffmpeg failed. ExitCode={ExitCode}\nSTDOUT={StdOut}\nSTDERR={StdErr}",
                        process.ExitCode, stdOut, stdErr);
                    throw new InvalidOperationException($"ffmpeg failed with ExitCode {process.ExitCode}");
                }

                _logger.LogInformation("⬆️ Uploading processed video as new blob: {Container}/{Blob}", containerName, outputBlobName);

                var outputBlob = containerClient.GetBlobClient(outputBlobName);
                await using var outputStream = File.OpenRead(outputPath);

                await outputBlob.UploadAsync(outputStream, overwrite: true);
                await outputBlob.SetHttpHeadersAsync(new BlobHttpHeaders { ContentType = "video/mp4" });

                _logger.LogInformation("✅ Done. Output: {Url}", outputBlob.Uri.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔥 Processing failed for blob {Container}/{Blob}", containerName, blobName);
                throw; // wichtig: damit Azure es in poison schiebt, falls es wiederholt failt
            }
            finally
            {
                SafeDeleteFile(inputPath);
                SafeDeleteFile(outputPath);
            }
        }

        private static bool IsVideoFile(string name)
        {
            var extension = Path.GetExtension(name).ToLowerInvariant();
            return extension is ".mp4" or ".mov" or ".mkv" or ".avi" or ".webm";
        }

        private static void SafeDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* egal */ }
        }

        private static string ResolveFfmpegPath(IConfiguration configuration)
        {
            var configuredPath = configuration["FFMPEG_PATH"];
            if (!string.IsNullOrWhiteSpace(configuredPath)) return configuredPath;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var windowsExePath = Path.Combine(AppContext.BaseDirectory, "Bins", "ffmpeg.exe");
                if (File.Exists(windowsExePath)) return windowsExePath;

                var windowsPlainPath = Path.Combine(AppContext.BaseDirectory, "Bins", "ffmpeg");
                if (File.Exists(windowsPlainPath)) return windowsPlainPath;

                return windowsExePath;
            }

            return Path.Combine("/home/site/wwwroot", "Bins", "ffmpeg");
        }

        private static async Task<string> PrepareLinuxFfmpegAsync(string configuredPath, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
                throw new InvalidOperationException("FFMPEG path is empty.");

            // If the path points to a real file, copy it to /tmp and make it executable.
            if (File.Exists(configuredPath))
            {
                var tmpFfmpeg = "/tmp/ffmpeg";

                if (!File.Exists(tmpFfmpeg))
                {
                    File.Copy(configuredPath, tmpFfmpeg, overwrite: true);
                }

                logger.LogInformation("🔐 Setze Execute-Rechte für ffmpeg in /tmp: {Path}", tmpFfmpeg);

                var chmod = Process.Start(new ProcessStartInfo
                {
                    FileName = "/bin/chmod",
                    Arguments = $"+x \"{tmpFfmpeg}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                });

                if (chmod == null)
                    throw new InvalidOperationException("chmod process could not be started.");

                await chmod.WaitForExitAsync(CancellationToken.None);

                var chmodErr = await chmod.StandardError.ReadToEndAsync();
                if (!string.IsNullOrWhiteSpace(chmodErr))
                    logger.LogWarning("chmod stderr: {err}", chmodErr);

                return tmpFfmpeg;
            }

            // If the configured value is a command like 'ffmpeg', use it directly and rely on PATH.
            logger.LogInformation("Using ffmpeg from PATH/command name: {Path}", configuredPath);
            return configuredPath;
        }
    }
}
