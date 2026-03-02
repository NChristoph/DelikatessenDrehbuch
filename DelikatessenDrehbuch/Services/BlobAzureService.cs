using Azure.Storage.Blobs;
using DelikatessenDrehbuch.Services.Interfaces;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace DelikatessenDrehbuch.Services
{
    public class BlobAzureService:IBlobAzureService
    {
        private readonly string _connectionString = Environment.GetEnvironmentVariable("Blob_Conection_String");
        private readonly string _containerName = Environment.GetEnvironmentVariable("BigPic");
        private readonly string _containerNameSmall = Environment.GetEnvironmentVariable("SmalPic");
        private readonly string _containerNameMedium = Environment.GetEnvironmentVariable("MediumPic");
        private readonly string _azureAcoutName = Environment.GetEnvironmentVariable("Azure_Acount_Name");

        

        public async Task UploadImageToAzureBlop(IFormFile file)
        {

            string blobName = $"{file.FileName}";

            BlobServiceClient blobServiceClient = new(_connectionString);
            BlobContainerClient container = blobServiceClient.GetBlobContainerClient(_containerName);
            BlobContainerClient smallContainer = blobServiceClient.GetBlobContainerClient(_containerNameSmall);
            BlobContainerClient mediumContainer = blobServiceClient.GetBlobContainerClient(_containerNameMedium);


            await Task.WhenAll(

                   ResizeAndUploadImage(container, blobName, 1024, 1024, "", file),
                   ResizeAndUploadImage(smallContainer, blobName, 313, 313, "_small", file),
                   ResizeAndUploadImage(mediumContainer, blobName, 600, 600, "_medium", file)
            );

        }

        private static MemoryStream ResizeImageToWebP(SixLabors.ImageSharp.Image imageStream, int width, int height)
        {

            imageStream.Mutate(x => x.Resize(width, height));

            MemoryStream memoryStream = new()
            {
                Position = 0
            };

            imageStream.Save(memoryStream, new WebpEncoder { Quality = 100 });

            return new MemoryStream(memoryStream.ToArray());
        }
        private static async Task ResizeAndUploadImage(BlobContainerClient targetContainer, string fileName, int width, int height, string suffix, IFormFile file)
        {

            BlobClient resizedBlob = targetContainer.GetBlobClient(GetResizedFileName(fileName, suffix));

            using var ms = new MemoryStream();

            file.CopyTo(ms);
            ms.Position = 0;
            var byteArray = ms.ToArray();


            try
            {
                using var image = SixLabors.ImageSharp.Image.Load(byteArray);
                using var resizedStream = ResizeImageToWebP(image, width, height);


                BlobClient blob = targetContainer.GetBlobClient(fileName);
                await resizedBlob.UploadAsync(resizedStream, overwrite: true);


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

        }

        private static string GetResizedFileName(string originalFileName, string suffix)
        {
            return Path.GetFileNameWithoutExtension(originalFileName) + suffix + ".webp";
        }

        public string GetImagePathFromAzure(IFormFile formFile)
        {
            string blobName = $"{formFile.FileName}";

            return $"https://{_azureAcoutName}.blob.core.windows.net/{_containerName}/{blobName}";
        }
    }
}
