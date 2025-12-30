namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces
{
    public interface IBlobUploadService
    {
        public Task<string> UploadContentToBlob(IFormFile file);
    }
}
