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
        Task<Dictionary<int, List<PersonalMealPlanRecipeModel>>> MapToMealPlanDictionaryAsync(Dictionary<int, List<int>> indexAndIds,int personCount);
        List<int> GetIntListByString(string Ids);
        List<string> GetMealPlanFilter();
        public Task CreateMealPlanAsync(int recipesId, string mealPlanName);

    }
}
