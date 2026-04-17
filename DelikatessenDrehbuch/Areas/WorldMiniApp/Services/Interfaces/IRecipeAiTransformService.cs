using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces
{
    public interface IRecipeAiTransformService
    {
        Task<RecipeAiTransformPreview> BuildPreviewAsync(
            RecipeBaseData recipe,
            string variantType,
            string language,
            string? userNote,
            int appliedChangeCount,
            CancellationToken cancellationToken = default);
    }
}
