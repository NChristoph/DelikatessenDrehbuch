using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces
{
    public interface IRecipeAiVariantJobService
    {
        Task<RecipeAiVariantJobStartResponse> StartJobAsync(
            RecipeAiTransformRequest request,
            string language,
            string? userHash,
            CancellationToken cancellationToken);

        Task<RecipeAiVariantJobStatusResponse?> GetStatusAsync(string jobId, CancellationToken cancellationToken);

        Task<RecipeAiTransformPreview?> GetResultAsync(string jobId, CancellationToken cancellationToken);
    }
}

