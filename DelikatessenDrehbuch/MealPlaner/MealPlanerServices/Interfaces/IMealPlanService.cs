using DelikatessenDrehbuch.MealPlaner.Models;
using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.MealPlaner.MealPlanerServices.Interfaces
{
    public interface IMealPlanService
    {
        public Task<List<CreateNewMealPlanModel>> CreateMealPlanModels(string category, int count,string email,PersonalMealPlanSettings settings);

        public Task<Dictionary<int, List<CreatedMealPlanModel>>> CreateMealPlanModelsByIdsAsync(
                                                                               string indexAndIds,
                                                                               int personCount);
        Task<CreateNewMealPlanModel> CreatePersonalMealPlanRecipeModelByIdAsync(int id,int index);
        public Task CreateMealPlanAsync(int recipesId, string mealPlanName);

      



    }
}
