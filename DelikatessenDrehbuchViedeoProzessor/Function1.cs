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
            var thumbBlobName = Path.GetFileNameWithoutExtension(blobName) + "_thumb.webp";
            var thumbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_thumb.webp");

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
                    // Ziel in /tmp (schreibbar)
                    var tmpFfmpeg = "/tmp/ffmpeg";

                    try
                    {
                        // Falls nicht vorhanden oder neu deployt: kopieren
                        if (!File.Exists(tmpFfmpeg))
                        {
                           
                            File.Copy(_ffmpegPath, tmpFfmpeg, overwrite: true);
                        }

                        // Execute-Rechte auf /tmp setzen
                        _logger.LogInformation("🔐 Setze Execute-Rechte für ffmpeg in /tmp: {Path}", tmpFfmpeg);

                        var chmod = Process.Start(new ProcessStartInfo
                        {
                            FileName = "/bin/chmod",
                            Arguments = $"+x \"{tmpFfmpeg}\"",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false
                        });

                        chmod!.WaitForExit();

                        var chmodErr = chmod.StandardError.ReadToEnd();
                        if (!string.IsNullOrWhiteSpace(chmodErr))
                            _logger.LogWarning("chmod stderr: {err}", chmodErr);

                        ffmpegToUse = tmpFfmpeg;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ Konnte ffmpeg nicht nach /tmp vorbereiten");
                        throw;
                    }
                }

           
                var ffmpegArgs = $"-y -i \"{inputPath}\" -vf \"scale=720:1280:force_original_aspect_ratio=decrease:force_divisible_by=2,setsar=1\" -pix_fmt yuv420p -c:v libx264 -preset veryfast -crf 28 -profile:v high -level 4.1 -threads 2 -movflags +faststart -c:a aac -b:a 96k -ac 1 \"{outputPath}\"";

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

                // Nur die letzten Zeilen von stderr behalten (ffmpeg schreibt Progress dorthin)
                const int maxStderrChars = 4000;
                var stderrBuffer = new System.Text.StringBuilder(maxStderrChars + 200);
                var stdoutBuffer = new System.Text.StringBuilder(1000);

                var stdOutTask = Task.Run(async () =>
                {
                    string? line;
                    while ((line = await process.StandardOutput.ReadLineAsync()) != null)
                    {
                        if (stdoutBuffer.Length < 1000) stdoutBuffer.AppendLine(line);
                    }
                });

                var stdErrTask = Task.Run(async () =>
                {
                    string? line;
                    while ((line = await process.StandardError.ReadLineAsync()) != null)
                    {
                        stderrBuffer.AppendLine(line);
                        // Ring-Buffer: nur die letzten Zeilen behalten
                        if (stderrBuffer.Length > maxStderrChars * 2)
                        {
                            var s = stderrBuffer.ToString();
                            stderrBuffer.Clear();
                            stderrBuffer.Append(s.Substring(s.Length - maxStderrChars));
                        }
                    }
                });

                // Timeout: 8 Minuten pro Video (preset medium braucht mehr Zeit)
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(8));
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    try { process.Kill(entireProcessTree: true); } catch { }
                    _logger.LogError("❌ ffmpeg timeout after 8 minutes for blob: {Blob}", blobName);
                    throw new TimeoutException($"ffmpeg timed out after 8 minutes for {blobName}");
                }

                await Task.WhenAll(stdOutTask, stdErrTask);
                var stdOut = stdoutBuffer.ToString();
                var stdErr = stderrBuffer.ToString();

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

                // Thumbnail: Frame bei Sekunde 1 als WEBP (400x711)
                _logger.LogInformation("🖼️ Generating thumbnail: {Thumb}", thumbBlobName);
                var thumbArgs = $"-y -ss 1 -i \"{outputPath}\" -frames:v 1 -vf \"scale=400:711:force_original_aspect_ratio=decrease:force_divisible_by=2\" -c:v libwebp -quality 80 \"{thumbPath}\"";

                var thumbStartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegToUse,
                    Arguments = thumbArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var thumbProcess = Process.Start(thumbStartInfo);
                if (thumbProcess != null)
                {
                    using var thumbCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    try
                    {
                        await thumbProcess.WaitForExitAsync(thumbCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        try { thumbProcess.Kill(entireProcessTree: true); } catch { }
                    }

                    if (thumbProcess.ExitCode == 0 && File.Exists(thumbPath))
                    {
                        var thumbBlob = containerClient.GetBlobClient(thumbBlobName);
                        await using var thumbStream = File.OpenRead(thumbPath);
                        await thumbBlob.UploadAsync(thumbStream, overwrite: true);
                        await thumbBlob.SetHttpHeadersAsync(new BlobHttpHeaders { ContentType = "image/webp" });
                        _logger.LogInformation("🖼️ Thumbnail uploaded: {Url}", thumbBlob.Uri.ToString());
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ Thumbnail generation failed (ExitCode={ExitCode}), continuing without thumbnail.", thumbProcess.ExitCode);
                    }
                }

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
                SafeDeleteFile(thumbPath);
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
                return Path.Combine(AppContext.BaseDirectory, "Bins", "ffmpeg.exe");

            return Path.Combine("/home/site/wwwroot", "Bins", "ffmpeg");
        }
    }
}
