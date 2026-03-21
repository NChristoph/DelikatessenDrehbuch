using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Models;
using System.Security.Cryptography;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces
{
    public interface IWorldAppMealPlanService
    {
        public Task EditMealPlanAsync(string UserHash,string title, List<MealPlanerModel> mealPlanerModels);
        public Task CheckVerify(MiniAppSetupModel settings, string userHash, string title);
        public Task SaveNewMealPlan(string userHash, List<MealPlanerModel> mealPlanerModels,string title);
        public Task DeleteMealPlanAsync(int id, string userHash);
        public Task<WorldUserMealPlan?> GetMealPlanByIdAsync(int id, string userHash);
        public Task<List<WorldUserMealPlan>> GetMealPlansByHash(string userHash);
    }
}
