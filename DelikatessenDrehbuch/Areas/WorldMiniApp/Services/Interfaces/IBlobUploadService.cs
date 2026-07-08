namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces
{
    public interface IBlobUploadService
    {
        public Task<UploadContentResult> UploadContentToBlob(IFormFile file);
        public Task<UploadContentResult> UploadContentToBlobFromUrl(string sourceUrl);

        /// <summary>Deletes a Bunny Stream video by GUID (best-effort; used to clean up an orphaned
        /// upload when the recipe save fails). Never throws.</summary>
        public Task DeleteVideoAsync(string videoGuid);
    }
}
