using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public interface IUtilityService
    {
        string[] SplitLinesToArray(string convertToArray);

        List<string> GetListFromQueryString(string query);

        string GenerateRandomToken(int length);

        float? GetCaloriesByIngredientHandlers(List<IngredientHandlerModel> ingredientHandlerModels);

        string[] SplitToArrayBySeperator(string convertToArray, char separator);

        public List<int> ConvertStringListToIntList(List<string>idsString);
        public int GetRandomIntFromList(List<int> list);
        public Task<List<IngredientHandlerModel>> SumIngredients(List<IngredientHandlerModel> ingredients);
    }
}
