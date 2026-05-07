namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces
{
    public class UploadContentResult
    {
        public string SourceUrl { get; set; }
        public string ThumbnailUrl { get; set; }

        /// <summary>
        /// Bunny Stream Video GUID (only set for video uploads to Bunny Stream)
        /// </summary>
        public string? VideoGuid { get; set; }
    }
}
