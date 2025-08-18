using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public interface IMealPlanService
    {
        MealModel GenerateMealPlan(List<string> queries, int dayCount);
        List<int> GetAlternativeRecipes(List<int> usedIds, int excludeId);
        Dictionary<int, List<Recipes>> MapToMealPlanDictionary(string json);
        List<int> GetIntListByString(string Ids);
        List<string> GetMealPlanFilter();
        public Task CreateMealPlanAsync(int recipesId, string mealPlanName);

    }
}
