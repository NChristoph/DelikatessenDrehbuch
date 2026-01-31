using System;
using System.IO;
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
        private readonly string _videoContainerName;
        private readonly string _ffmpegPath;

        public Function1(ILogger<Function1> logger, BlobServiceClient blobServiceClient, IConfiguration configuration)
        {
            _logger = logger;
            _blobServiceClient = blobServiceClient;
            _videoContainerName = configuration["VideoUploadContainer"] ?? "blob-world-mini-app";
            _ffmpegPath = configuration["FFMPEG_PATH"] ?? "ffmpeg";
        }

        [Function(nameof(Function1))]
        public async Task Run([BlobTrigger("%VideoUploadContainer%/{name}", Connection = "BlobStorageConnection")] Stream stream, string name)
        {
            if (!IsVideoFile(name))
            {
                _logger.LogInformation("Skipping non-video blob: {BlobName}", name);
                return;
            }

            var inputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{Path.GetExtension(name)}");
            var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_processed.mp4");

            try
            {
                await using (var fileStream = File.Create(inputPath))
                {
                    await stream.CopyToAsync(fileStream);
                }

                var ffmpegArgs = $"-y -i \"{inputPath}\" -vf scale=1280:-2 -c:v libx264 -preset fast -crf 28 -c:a aac -b:a 128k \"{outputPath}\"";
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = ffmpegArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(startInfo);
                if (process == null)
                {
                    _logger.LogError("Failed to start ffmpeg for blob {BlobName}", name);
                    return;
                }

                var stdOutTask = process.StandardOutput.ReadToEndAsync();
                var stdErrTask = process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync(CancellationToken.None);

                var stdOut = await stdOutTask;
                var stdErr = await stdErrTask;

                if (process.ExitCode != 0)
                {
                    _logger.LogError("ffmpeg failed for blob {BlobName}. ExitCode: {ExitCode}. Output: {StdOut}. Error: {StdErr}", name, process.ExitCode, stdOut, stdErr);
                    return;
                }

                var containerClient = _blobServiceClient.GetBlobContainerClient(_videoContainerName);
                var outputBlob = containerClient.GetBlobClient(name);

                await using var outputStream = File.OpenRead(outputPath);
                await outputBlob.UploadAsync(outputStream, new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = "video/mp4" }
                }, overwrite: true);

                _logger.LogInformation("Processed video overwritten: {BlobName} in {Container}", name, _videoContainerName);
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

        private void SafeDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete temp file {Path}", path);
            }
        }
    }
}
