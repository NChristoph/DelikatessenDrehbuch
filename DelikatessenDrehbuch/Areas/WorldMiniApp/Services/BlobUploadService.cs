using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

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
        public async Task<UploadContentResult> UploadContentToBlob(IFormFile file)
        {
            string connectionString = _configuration["Blob_Conection_String"];
            string containerName = _configuration["World-App-Blop-Name"] ?? "blob-world-mini-app";

            var blobServiceClient = new BlobServiceClient(connectionString);
            var blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);

            await blobContainerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

            string fileName = Path.GetFileNameWithoutExtension(file.FileName);
            string uniqueToken = Guid.NewGuid().ToString("N");

            if (file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                var sourceName = $"{fileName}_{uniqueToken}.webp";
                var thumbName = $"{fileName}_{uniqueToken}_thumb.webp";

                using var sourceStream = await ConvertImageToWebpAsync(file, 1080, 1920);
                using var thumbStream = await ConvertImageToWebpAsync(file, 400, 711);

                var sourceUrl = await UploadStreamAsync(blobContainerClient, sourceName, sourceStream, "image/webp");
                var thumbUrl = await UploadStreamAsync(blobContainerClient, thumbName, thumbStream, "image/webp");

                return new UploadContentResult
                {
                    SourceUrl = sourceUrl,
                    ThumbnailUrl = thumbUrl
                };
            }

            var videoResult = await UploadRawVideoAsync(blobContainerClient, file, fileName, uniqueToken);
            return videoResult;
        }

        private static async Task<string> UploadStreamAsync(BlobContainerClient blobContainerClient, string blobName, Stream stream, string contentType)
        {
            stream.Position = 0;
            var blobClient = blobContainerClient.GetBlobClient(blobName);
            var blobHttpHeaders = new BlobHttpHeaders
            {
                ContentType = contentType
            };

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
            var encoder = new WebpEncoder
            {
                Quality = 90
            };
            await image.SaveAsWebpAsync(outputStream, encoder);
            outputStream.Position = 0;
            return outputStream;
        }

        private static async Task<UploadContentResult> UploadRawVideoAsync(BlobContainerClient blobContainerClient, IFormFile file, string fileName, string uniqueToken)
        {
            var videoName = $"{fileName}_{uniqueToken}.mp4";
            await using var videoStream = file.OpenReadStream();

            var sourceUrl = await UploadStreamAsync(blobContainerClient, videoName, videoStream, "video/mp4");

            return new UploadContentResult
            {
                SourceUrl = sourceUrl,
                ThumbnailUrl = sourceUrl
            };
        }
    }
}
