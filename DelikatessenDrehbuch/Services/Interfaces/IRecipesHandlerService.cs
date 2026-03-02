using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public interface IRecipesHandlerService
    {
       Task DeleteReciphandlerAsync(int recipesId);
       Task CreateRecipeAndIngredientHandlerAsync(int recipesId, List<IngredientHandlerModel> ingredientHandlers);
       Task<List<RecipesHandler>> GetRecipesHandlerByRecipesIdAsync(int recipesId);
       IQueryable<RecipesHandler> GetRecipesHandlerByRecipesIdsAsync(List<int> recipesIds);



    }
}
