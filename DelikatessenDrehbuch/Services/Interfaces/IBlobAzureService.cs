namespace DelikatessenDrehbuch.Services.Interfaces
{
    public interface IBlobAzureService
    {
        public Task UploadImageToAzureBlop(IFormFile file);
        public string GetImagePathFromAzure(IFormFile formFile);
    }
}
