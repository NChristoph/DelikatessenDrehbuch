using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Text.Json;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces
{
    public class WorldAppMealPlanService : IWorldAppMealPlanService
    {

        private readonly ApplicationDbContext _contex;

        public WorldAppMealPlanService(ApplicationDbContext context)
        {
            _contex = context;
        }

        private string CheckTitle(string title, string userHash)
        {

            var existingTitles = _contex.WorldUserMealPlan
                .Where(x => x.UserHash == userHash)
                .Select(x => x.Title)
                .ToHashSet();


            if (!existingTitles.Contains(title))
            {
                return title;
            }


            int counter = 1;
            string newTitle = title;

            // PrÃ¼fe: "Test 1", "Test 2", "Test 3"...
            while (existingTitles.Contains(newTitle))
            {
                newTitle = $"{title} {counter}"; // FÃ¼gt Leerzeichen und Zahl an
                counter++;
            }

            return newTitle;
        }


        public async Task<Task> CheckVerifie(MiniAppSetupModel settings, string userHash, string title)
        {
            var user = await _contex.WorldAppUser.FirstOrDefaultAsync(x => x.UserHash == userHash);

            if (user == null)
                throw new Exception("Bad Reguest");

            if (user.IsVerified == "orb")
                _ =  await CreateWorldUserMealPlan(settings, userHash, title);
            else
            {
                var plan = _contex.WorldUserMealPlan.FirstOrDefault(x => x.UserHash == userHash);
                if(plan==null)
                    _ = await CreateWorldUserMealPlan(settings, userHash, title);
            }

            return Task.CompletedTask;

        }
        public async Task<Task> CreateWorldUserMealPlan(MiniAppSetupModel settings, string UserHash, string title)
        {
            var checkedTitle = CheckTitle(title, UserHash);

           

            if (!string.IsNullOrEmpty(UserHash))
            {
                var newMealPlan = new WorldUserMealPlan()
                {
                    UserHash = UserHash,
                    Settings = JsonSerializer.Serialize(settings),
                    Title = checkedTitle,
                    CreationTime = DateTime.Now

                };

                await _contex.WorldUserMealPlan.AddAsync(newMealPlan);
                await _contex.SaveChangesAsync();
            }
            else
            {
                throw new Exception("User Hash Nicht gefunden");
            }


            return Task.CompletedTask;


        }

        public async Task<Task> SaveNewMealPlan(string userHash, List<MealPlanerModel> mealPlanerModels,string title)
        {
            var user = await _contex.WorldAppUser.FirstOrDefaultAsync(x => x.UserHash == userHash);
            if(user.IsVerified=="orb")
            {
                var existigPlan = _contex.WorldUserMealPlan.Where(x => x.UserHash == userHash && x.Title.Trim() == title.Trim()).FirstOrDefault();
                existigPlan.MealPlan = JsonSerializer.Serialize(GetDictionary(mealPlanerModels));
            }
            else
            {
                var plan= _contex.WorldUserMealPlan.Where(x => x.UserHash == userHash).FirstOrDefault();
                plan.MealPlan = JsonSerializer.Serialize(GetDictionary(mealPlanerModels));
            }

                await _contex.SaveChangesAsync();

            return Task.CompletedTask;

        }

        private Dictionary<int, List<int>> GetDictionary(List<MealPlanerModel> mealPlanerModels)
        {
            var dic = mealPlanerModels.GroupBy(x => x.Index)
                                      .ToDictionary(
                                         g => g.Key,
                                         g => g.Select(x => x.Recipes.Id).ToList()
                                      );


            return dic;
        }

        public Task EditeMealPlan(string userHash, string title, List<MealPlanerModel> mealPlaner)
        {
            var mealPlanToEdit = _contex.WorldUserMealPlan.FirstOrDefault(x => x.UserHash == userHash && x.Title == title);

            mealPlanToEdit.MealPlan = JsonSerializer.Serialize(GetDictionary(mealPlaner));

            return Task.CompletedTask;
        }

        public async Task<List<WorldUserMealPlan>> GetMealPlansByHash(string userHasch)
        {
            return await _contex.WorldUserMealPlan.Where(x => x.UserHash == userHasch).ToListAsync();
        }

        public void DeleteMealPlan(int id, string userHash)
        {
            var mealPlan = _contex.WorldUserMealPlan.SingleOrDefault(x => x.Id == id && x.UserHash == userHash);
            if (mealPlan == null)
                throw new Exception("Essensplan nicht gefunden oder keine Berechtigung.");

            var purchases = _contex.MealPlanPurchases
                .Where(x => x.CreatedMealPlanId == id && x.BuyerHash == userHash)
                .ToList();

            if (purchases.Any())
            {
                _contex.MealPlanPurchases.RemoveRange(purchases);
            }

            _contex.WorldUserMealPlan.Remove(mealPlan);
            _contex.SaveChanges();
        }

        public WorldUserMealPlan GetMealPlanById(int id, string userHash)
        {
            return _contex.WorldUserMealPlan.SingleOrDefault(x => x.Id == id && x.UserHash == userHash);
        }
    }
}
