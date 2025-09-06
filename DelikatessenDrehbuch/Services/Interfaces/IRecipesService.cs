using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public interface IRecipesService
    {
        Task DeleteRecipesByIdAsync(int id);
        Recipes GetOneRendomRecipeFromIdList(List<int> ids);
        List<int> GetRendomRecipesIds(List<int> recipesIds,int count);
        List<int> GetRecipesIdsByCategory(string category);
        Task<Recipes> GetRecipesFromDbByIdAsync(int id);
        int GetRecipesCount();
        Task<Recipes> SaveRecipesInDbAsync(Recipes recipes);
        Task EditRecipesAsync(int recipeToChangeId,Recipes newRecipesData);
        Task<int> GetRecipeIdByNameAndPreperation(string recipeName, string preperation);
        List<IngredientHandlerModel> GetOrdetIngredientHandler(List<RecipesHandler> recipesHandler);
        Task<FullRecipeData> GetFullRecipeDataByRecipesIdAsync(int id);
        public Task<List<FullRecipeData>> GetFullRecipeDataListByRecipesIdsAsync(List<int> resipesIds);

    }
}
