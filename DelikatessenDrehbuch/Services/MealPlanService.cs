using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Services.Interfaces;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Http;
using System;
using System.Text.Json;

namespace DelikatessenDrehbuch.Services
{
    public class MealPlanService : IMealPlanService
    {
        private readonly ApplicationDbContext _context;
        private readonly HelpfulMethods _helper;
        private readonly IHttpContextAccessor _httpContext;
        private readonly IRecipesService _recipesService;
        private readonly IIngredientService _ingredientService;

        public MealPlanService(ApplicationDbContext context, HelpfulMethods helper, IHttpContextAccessor httpContext, IRecipesService recipesService, IIngredientService ingredientService)
        {
            _context = context;
            _helper = helper;
            _httpContext = httpContext;
            _recipesService = recipesService;
            _ingredientService = ingredientService;
        }

        public MealModel GenerateMealPlan(List<string> queries, int dayCount)
        {
            var random = new Random();

            var cleanedList = queries
                .Where(q => !string.IsNullOrWhiteSpace(q))
                .Select(q => q.Trim().ToLower())
                .ToList();

            var recipeIds = _context.QueryHandler
                .Where(q => cleanedList.Contains(q.Query.Query.ToLower()) && q.Recipe.Category == "Hauptspeise")
                .Select(q => q.Recipe.Id)
                .ToList();

            var randomRecipesIds = recipeIds.OrderBy(_ => random.Next())
                                            .Take(dayCount)
                                            .ToList();

            // Session speichern
            _httpContext.HttpContext?.Session.SetString("RecipesFromDbIds", string.Join(";", recipeIds));

            var model = new MealModel
            {
                Recipes = _context.Recipes.Where(r => randomRecipesIds.Contains(r.Id)).ToList(),
                Ingredients = _ingredientService.GetIngredientsByRecipesIdsList(randomRecipesIds)
            };

            return model;
        }

        public Dictionary<int, List<Recipes>> MapToMealPlanDictionary(string json)
        {
            var model = new Dictionary<int, List<Recipes>>();

            var decoded = Uri.UnescapeDataString(json);
            var dictionary = JsonSerializer.Deserialize<Dictionary<int, List<int>>>(decoded);

            var allRecipeIds = dictionary.Values.SelectMany(list => list).Distinct().ToList();

            var recipesFromDb = _context.Recipes.Where(x => allRecipeIds.Contains(x.Id)).ToList();

            foreach (var key in dictionary.Keys)
            {
                var recipeIds = dictionary[key];


                var matchingRecipes = recipesFromDb
                    .Where(r => recipeIds.Contains(r.Id))
                    .OrderBy(r => recipeIds.IndexOf(r.Id))
                    .ToList();

                model.Add(key, matchingRecipes);
            }

            return model;

        }

        public List<int> GetAlternativeRecipes(List<int> usedIds, int excludeId)
        {

            var idsString = GetIntListByString(_httpContext.HttpContext?.Session
                           .GetString("RecipesFromDbIds"));


            if (idsString == null) return new List<int>();

            return _context.Recipes
                .Where(x => idsString.Contains(x.Id) &&
                            x.Id != excludeId &&
                            !usedIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToList();
        }



        public List<int> GetIntListByString(string idString)
        {
            try
            {
                return idString.Split(';', StringSplitOptions.RemoveEmptyEntries)
                                    .Select(id => int.Parse(id))
                                    .ToList();
            }
            catch (Exception ex)
            {
                throw new Exception($"Fehler beim Parsen der Zeichenkette{idString}" + ex.Message);

            }
        }

        public List<string> GetMealPlanFilter()
        {
            return _context.MealplanFilter.Select(x => x.Filter).ToList();
        }
        //TODO:Mach das ordentlicher und in der vie ein dropdown zum auswählen des plans
        public async Task CreateMealPlanAsync(int recipesId, string mealPlanName)
        {
            var melplanArry = mealPlanName.Split(";");
            MealPlan mealPlan = new()
            {
                Name = melplanArry[0],
                MyMealModel = _context.MyMealModel.Single(x => x.Id == Int32.Parse(melplanArry[1]))
            };

            if (mealPlan != null)
            {
                

                var exist = _context.MealPlan.FirstOrDefault(x => x.Name.ToLower() == mealPlan.Name.ToLower());
                if (exist == null)
                    _context.MealPlan.Add(mealPlan);

                _context.SaveChanges();
                try
                {
                    MealPlanHandler mealPlanHandler = new()
                    {
                        Id = 0,
                        MealPlan = _context.MealPlan.SingleOrDefault(x => x.Name.ToLower() == mealPlan.Name.ToLower()),
                        Recipes = await _recipesService.GetRecipesFromDbByIdAsync(recipesId),
                    };

                    await _context.MealPlanHandler.AddAsync(mealPlanHandler);
                    await _context.SaveChangesAsync();


                }

                catch (Exception ex)
                {

                    throw new Exception("Fehler bei den Menüplans", ex);

                }



            }

        }
    }
}
