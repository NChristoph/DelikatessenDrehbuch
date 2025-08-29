using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public interface IUtilityService
    {
        string[] SplitLinesToArray(string convertToArray);

        List<string> GetListFromQueryString(string query);

        float? GetCaloriesByIngredientHandlers(List<IngredientHandlerModel> ingredientHandlerModels);

        string[] SplitToArrayBySeperator(string convertToArray, char separator);
    }
}
