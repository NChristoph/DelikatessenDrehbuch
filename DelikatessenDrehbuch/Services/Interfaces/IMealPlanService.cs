using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public interface IMealPlanService
    {
        Task<List<PersonalMealPlanRecipeModel>> GetMealPlanModels(string category,int count);
        string GetRecipeIdsByPreference();
        Task<List<PersonalMealPlanRecipeModel>> GetPersonalMealPlanModelListByIds(List<int> recipesIds, int dayCount);
        Task<PersonalMealPlanRecipeModel> CreatePersonalMealPlanRecipeModelByIdAsync(int id,int index);
        List<int> GetAlternativeRecipes(List<int> usedIds, int excludeId);
        Dictionary<int, List<PersonalMealPlanRecipeModel>> MapToMealPlanDictionary(string json);
        List<int> GetIntListByString(string Ids);
        List<string> GetMealPlanFilter();
        public Task CreateMealPlanAsync(int recipesId, string mealPlanName);

    }
}
