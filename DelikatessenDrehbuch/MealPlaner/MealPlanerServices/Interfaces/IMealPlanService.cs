using DelikatessenDrehbuch.MealPlaner;
using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.MealPlaner.MealPlanerServices.Interfaces
{
    public interface IMealPlanService
    {
        public Task<List<MealPlanerModel>> CreateMealPlanModels(string category, int count,string email,PersonalMealPlanSettings settings);


        Task<MealPlanerModel> CreatePersonalMealPlanRecipeModelByIdAsync(int id,int index);
        public Task CreateMealPlanAsync(int recipesId, string mealPlanName);

        public Task CreateSavedMealPlanAsync(string email, PersonalMealPlanSettings settings, Dictionary<int, List<int>> indexAndIds);




    }
}
