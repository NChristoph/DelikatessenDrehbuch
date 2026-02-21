using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public interface INutrientService
    {
        Task CreateNutrienHandlersAsync(string Nutrients, string Ingredient);
        Task<List<NutrienHandler>> GetNutrienHandlersByIngredientNamesAsync(List<string> ingredientNames);
        public Task<List<NutrienHandler>> GetNutrienHandlersByIngredientIdsAsync(List<int> ingredientIds);
    }
}
