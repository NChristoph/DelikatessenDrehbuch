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
        public List<int> GetRandomIntsFromList(List<int> list, int count);

        public List<string> GetTrueBoolNamesFromModel(PersonalMealPlanSettings settings);
    }
}
