using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public interface IUtilityService
    {
        string[] SplitLinesToArray(string convertToArray);

        float? GetCaloriesByIngredientHandlers(List<IngredientHandlerModel> ingredientHandlerModels);
    }
}
    