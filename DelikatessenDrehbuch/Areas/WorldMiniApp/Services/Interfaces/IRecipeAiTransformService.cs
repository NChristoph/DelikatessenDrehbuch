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
            IReadOnlyList<RecipeAiIngredientSuggestionItem>? selectedIngredients = null,
            CancellationToken cancellationToken = default);

        Task<RecipeAiIngredientSuggestionResponse> BuildIngredientSuggestionsAsync(
            RecipeBaseData recipe,
            string variantType,
            string language,
            string? userNote,
            CancellationToken cancellationToken = default);
    }
}
