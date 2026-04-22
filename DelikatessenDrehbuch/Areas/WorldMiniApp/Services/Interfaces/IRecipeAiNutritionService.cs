using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces
{
    public interface IRecipeAiNutritionService
    {
        Task<RecipeAiTransformNutritionPreview> BuildAiPreviewNutritionAsync(
            RecipeAiTransformPreview preview,
            string language,
            CancellationToken cancellationToken);
    }
}

