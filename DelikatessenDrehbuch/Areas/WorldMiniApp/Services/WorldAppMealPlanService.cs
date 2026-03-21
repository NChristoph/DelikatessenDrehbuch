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

        private readonly ApplicationDbContext _context;

        public WorldAppMealPlanService(ApplicationDbContext context)
        {
            _context = context;
        }

        private async Task<string> CheckTitleAsync(string title, string userHash)
        {

            var existingTitles = (await _context.WorldUserMealPlan
                .Where(x => x.UserHash == userHash)
                .Select(x => x.Title)
                .ToListAsync())
                .ToHashSet();


            if (!existingTitles.Contains(title))
            {
                return title;
            }


            int counter = 1;
            string newTitle = title;

            while (existingTitles.Contains(newTitle))
            {
                newTitle = $"{title} {counter}";
                counter++;
            }

            return newTitle;
        }


        public async Task CheckVerify(MiniAppSetupModel settings, string userHash, string title)
        {
            var user = await _context.WorldAppUser.FirstOrDefaultAsync(x => x.UserHash == userHash);

            if (user == null)
                throw new Exception("Bad Request");

            if (user.IsVerified == "orb")
                await CreateWorldUserMealPlan(settings, userHash, title);
            else
            {
                var plan = await _context.WorldUserMealPlan.FirstOrDefaultAsync(x => x.UserHash == userHash);
                if(plan==null)
                    await CreateWorldUserMealPlan(settings, userHash, title);
            }

        }
        public async Task CreateWorldUserMealPlan(MiniAppSetupModel settings, string UserHash, string title)
        {
            var checkedTitle = await CheckTitleAsync(title, UserHash);



            if (!string.IsNullOrEmpty(UserHash))
            {
                var newMealPlan = new WorldUserMealPlan()
                {
                    UserHash = UserHash,
                    Settings = JsonSerializer.Serialize(settings),
                    Title = checkedTitle,
                    CreationTime = DateTime.Now

                };

                await _context.WorldUserMealPlan.AddAsync(newMealPlan);
                await _context.SaveChangesAsync();
            }
            else
            {
                throw new Exception("User Hash Nicht gefunden");
            }


        }

        public async Task SaveNewMealPlan(string userHash, List<MealPlanerModel> mealPlanerModels,string title)
        {
            var user = await _context.WorldAppUser.FirstOrDefaultAsync(x => x.UserHash == userHash);
            if (user == null) throw new InvalidOperationException("User nicht gefunden.");
            if(user.IsVerified=="orb")
            {
                var existingPlan = await _context.WorldUserMealPlan.Where(x => x.UserHash == userHash && x.Title.Trim() == title.Trim()).FirstOrDefaultAsync();
                if (existingPlan == null) throw new InvalidOperationException("Essensplan nicht gefunden.");
                existingPlan.MealPlan = JsonSerializer.Serialize(GetDictionary(mealPlanerModels));
            }
            else
            {
                var plan = await _context.WorldUserMealPlan.Where(x => x.UserHash == userHash).FirstOrDefaultAsync();
                if (plan == null) throw new InvalidOperationException("Essensplan nicht gefunden.");
                plan.MealPlan = JsonSerializer.Serialize(GetDictionary(mealPlanerModels));
            }

                await _context.SaveChangesAsync();
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

        public async Task EditMealPlanAsync(string userHash, string title, List<MealPlanerModel> mealPlaner)
        {
            var mealPlanToEdit = await _context.WorldUserMealPlan.FirstOrDefaultAsync(x => x.UserHash == userHash && x.Title == title);
            if (mealPlanToEdit == null) throw new InvalidOperationException("Essensplan nicht gefunden.");

            mealPlanToEdit.MealPlan = JsonSerializer.Serialize(GetDictionary(mealPlaner));
            await _context.SaveChangesAsync();
        }

        public async Task<List<WorldUserMealPlan>> GetMealPlansByHash(string userHash)
        {
            return await _context.WorldUserMealPlan.Where(x => x.UserHash == userHash).ToListAsync();
        }

        public async Task DeleteMealPlanAsync(int id, string userHash)
        {
            var mealPlan = await _context.WorldUserMealPlan.SingleOrDefaultAsync(x => x.Id == id && x.UserHash == userHash);
            if (mealPlan == null)
                throw new Exception("Essensplan nicht gefunden oder keine Berechtigung.");

            var purchases = await _context.MealPlanPurchases
                .Where(x => x.CreatedMealPlanId == id && x.BuyerHash == userHash)
                .ToListAsync();

            if (purchases.Any())
            {
                _context.MealPlanPurchases.RemoveRange(purchases);
            }

            _context.WorldUserMealPlan.Remove(mealPlan);
            await _context.SaveChangesAsync();
        }

        public async Task<WorldUserMealPlan?> GetMealPlanByIdAsync(int id, string userHash)
        {
            return await _context.WorldUserMealPlan.SingleOrDefaultAsync(x => x.Id == id && x.UserHash == userHash);
        }
    }
}
