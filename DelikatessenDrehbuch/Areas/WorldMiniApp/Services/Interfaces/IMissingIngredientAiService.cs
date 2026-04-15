using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces
{
    public interface IMissingIngredientAiService
    {
        Task<MissingIngredientAiSuggestionResult> SuggestIngredientAsync(string ingredientName, CancellationToken cancellationToken = default);
    }
}
