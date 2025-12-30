using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    public class BlobUploadService : IBlobUploadService
    {
        private readonly IConfiguration _configuration;

        public BlobUploadService(IConfiguration configuration)
        {
            _configuration = configuration;
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

            // 3. Eindeutigen Dateinamen generieren
            // Aus "video.mp4" wird "video_8392-2382-3823.mp4" (Verhindert Überschreiben)
            string fileName = Path.GetFileNameWithoutExtension(file.FileName);
            string extension = Path.GetExtension(file.FileName);
            string uniqueFileName = $"{fileName}_{Guid.NewGuid()}{extension}";

            var blobClient = blobContainerClient.GetBlobClient(uniqueFileName);

            // 4. Hochladen mit korrekten Headern (WICHTIG FÜR VIDEOS!)
            var blobHttpHeaders = new BlobHttpHeaders
            {
                ContentType = file.ContentType // z.B. "video/mp4"
            };

            using (var stream = file.OpenReadStream())
            {
                await blobClient.UploadAsync(stream, new BlobUploadOptions
                {
                    HttpHeaders = blobHttpHeaders
                });
            }

            // 5. Die komplette URL zurückgeben (z.B. https://meinapp.blob.core.windows.net/videos/...)
            return blobClient.Uri.ToString();
        }
    }
}