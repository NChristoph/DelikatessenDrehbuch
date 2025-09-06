using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Services.Interfaces;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Http;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq.Expressions;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace DelikatessenDrehbuch.Services
{

    public class MealPlanService : IMealPlanService
    {
        private readonly ApplicationDbContext _context;
        private readonly HelpfulMethods _helper;
        private readonly IHttpContextAccessor _httpContext;
        private readonly IRecipesService _recipesService;
        private readonly IRecipesHandlerService _recipesHandlerService;
        private readonly IIngredientService _ingredientService;
        private readonly ISessionService _sessionService;

        public MealPlanService(ApplicationDbContext context, HelpfulMethods helper, IHttpContextAccessor httpContext,
                               IRecipesService recipesService, IIngredientService ingredientService,
                               ISessionService sessionService, IRecipesHandlerService recipesHandlerService)
        {
            _context = context;
            _helper = helper;
            _httpContext = httpContext;
            _recipesService = recipesService;
            _ingredientService = ingredientService;
            _sessionService = sessionService;
            _recipesHandlerService = recipesHandlerService;
        }

        public async Task<List<PersonalMealPlanRecipeModel>> GetMealPlanModels(string category, int count)
        {

            var recipesIdsFromDb = _recipesService.GetRecipesIdsByCategory(category);
            var sortedRecipesIds = SortRecipesIdsBySettings(recipesIdsFromDb);
            var rendomRecipesIds = _recipesService.GetRendomRecipesIds(sortedRecipesIds, count);
            var personalRecipes = await CreatePersonalMealPlanRecipeModelByIdsAsync(rendomRecipesIds);

            return personalRecipes;

        }

        private List<int> SortRecipesIdsBySettings(List<int> recipeIdsFromDb)
        {
            var settings = _sessionService.GetMealPlanSettingsFromSession();

            var queryable = _context.QueryHandler.Where(x => recipeIdsFromDb.Contains(x.Recipe.Id))
                                                 .Include(x=>x.Recipe)
                                                 .AsQueryable()
                                                 .AsNoTracking();

            var bools = typeof(PersonalMealPlanSettings)
                        .GetProperties()
                        .Where(p => p.PropertyType == typeof(bool));

            foreach (var filter in bools)
            {
                var isChecked = (bool)filter.GetValue(settings);
                if (isChecked)
                {
                    var name = filter.Name;
                    
                    if (name.StartsWith("No"))
                    {
                        var subName = name.Substring(2);
                        if (subName == "Pork")
                            subName = "Schweinefleisch";

                        var removeIds = queryable.Where(r => r.Query.Query.ToLower() == subName.ToLower())
                                                 .Select(x => x.Recipe.Id)
                                                 .ToList();

                        queryable = queryable.Where(r => !removeIds.Contains(r.Recipe.Id));

                    }
                    if (!name.StartsWith("No")&& name != "CookingTimeOne")
                    {
                        queryable = queryable.Where(r => r.Query.Query.ToLower() == name.ToLower()).Include(x => x.Query);
                    }
                    if (name == "CookingTimeOne")
                    {
                        
                        queryable = queryable.Where(r => r.Recipe.PreparationTime < 45);
                    }
                }
            }
            return queryable.Select(x => x.Recipe.Id).ToHashSet().ToList();
        }




        public async Task<List<PersonalMealPlanRecipeModel>> GetPersonalMealPlanModelListByIds(List<int> recipesIds, int dayCount)
        {

            var recipesIdsFromDb = _context.Recipes.Where(x => x.Category == "Hauptspeise" && recipesIds.Contains(x.Id))
                                                .Select(x => x.Id)
                                                .ToList();

            var randomRecipesIds = _recipesService.GetRendomRecipesIds(recipesIdsFromDb, dayCount);
            recipesIds.RemoveAll(x => randomRecipesIds.Contains(x));
            _sessionService.SaveRecipesIdToSession(recipesIds);

            var model = await CreatePersonalMealPlanRecipeModelByIdsAsync(randomRecipesIds);


            return model;
        }

        public async Task<List<PersonalMealPlanRecipeModel>> CreatePersonalMealPlanRecipeModelByIdsAsync(List<int> recipesIds)
        {
            List<PersonalMealPlanRecipeModel> modelList = new();
            foreach (var recipeId in recipesIds)
            {
                var model = await CreatePersonalMealPlanRecipeModelByIdAsync(recipeId);

                modelList.Add(model);
            }
            return modelList;
        }

        public async Task<PersonalMealPlanRecipeModel> CreatePersonalMealPlanRecipeModelByIdAsync(int recipeId)
        {
            var recipehandlers = await _recipesHandlerService.GetRecipesHandlerByRecipesIdAsync(recipeId);

            PersonalMealPlanRecipeModel model = new()
            {
                Index = 0,
                Id = recipeId,
                Recipes = _context.Recipes.FirstOrDefault(x => x.Id == recipeId),
                Ingredients = _recipesService.GetOrdetIngredientHandler(recipehandlers)
            };

            return model;
        }



        public Dictionary<int, List<PersonalMealPlanRecipeModel>> MapToMealPlanDictionary(string json)
        {
            var model = new Dictionary<int, List<PersonalMealPlanRecipeModel>>();

            var decoded = Uri.UnescapeDataString(json);
            var dictionary = JsonSerializer.Deserialize<Dictionary<int, List<int>>>(decoded);

            var allRecipeIds = dictionary.Values.SelectMany(list => list).Distinct().ToList();
            var personalMealPlanFromSession = _sessionService.GetPersonalMealPlanFromSession();


            foreach (var key in dictionary.Keys)
            {
                var recipeIds = dictionary[key];


                var matchingRecipes = personalMealPlanFromSession
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
        //TODO:Mach das ordentlicher und in der view ein dropdown zum auswählen des plans
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
