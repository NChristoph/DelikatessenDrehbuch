using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Models;
using System.Security.Cryptography;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces
{
    public interface IWorldAppMealPlanService
    {
        public Task EditeMealPlan(string UserHash,string title, List<MealPlanerModel> mealPlanerModels);
        public Task<Task> CheckVerifie(MiniAppSetupModel settings, string userHash, string title);
        public Task<Task> SaveNewMealPlan(string userHash, List<MealPlanerModel> mealPlanerModels,string title);
        public void DeleteMealPlan(int id, string userHash);
        public WorldUserMealPlan GetMealPlanById(int id, string userHash);
        public Task<List<WorldUserMealPlan>> GetMealPlansByHash(string userHasch);
    }
}
