namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces
{
    public interface IBlobUploadService
    {
        public Task<UploadContentResult> UploadContentToBlob(IFormFile file);
        public Task<UploadContentResult> UploadContentToBlobFromUrl(string sourceUrl);
    }
}
