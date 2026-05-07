namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces
{
    public interface IBunnyVideoTranscriptionService
    {
        Task QueueTranscriptionAsync(int postingId, string videoGuid, CancellationToken cancellationToken = default);
    }
}
