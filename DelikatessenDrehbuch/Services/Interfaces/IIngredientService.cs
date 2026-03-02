using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public interface IIngredientService
    {
        List<IngredientHandlerModel> GetIngredientsByRecipesIdsList(List<int> ids);
        Task<List<IngredientHandlerModel>> GetIngredientsByRecipesIdFromDbAsync(int id);
        Task<List<string>> GetIngredientsNamesByRecipesId(int id);
        Ingredient GetIngredientByNameFromDb(string name);
        Task<IngredientHandlerModel> GetOrCreateIngredientHandlerAsync(IngredientHandlerModel ingredientHandlerModel);
        public List<IngredientHandlerModel> GetIngredientHandlerListFromString(string mapToIngredientHandlers);
        public List<IngredientHandlerModel> GetIngredientHandlerModels(List<int> ids);
        public List<IngredientHandlerModel> GetIngredientHandlerByRecipesId(int recipesId);

    }
}
