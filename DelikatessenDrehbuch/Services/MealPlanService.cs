using Azure.Storage.Blobs.Models;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Services.Interfaces;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Http;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using NuGet.Packaging;
using System;
using System.Linq.Expressions;
using System.Reflection;
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
            var settings = _sessionService.GetMealPlanSettingsFromSession();

            var recipesIdsFromDb = _recipesService.GetRecipesIdsByCategory(category);
            var sortRecipesByQuery = GetSortedListByQuerys(recipesIdsFromDb, settings, category);

            var rendomRecipesIds = _recipesService.GetRendomRecipesIds(sortRecipesByQuery, count);
            var personalRecipes = await CreatePersonalMealPlanRecipeModelByIdsAsync(rendomRecipesIds);

            return personalRecipes;

        }

        private List<int> GetSortedListByQuerys(List<int> recipeIdsFromDb, PersonalMealPlanSettings settings, string category)
        {
            var filterBool = GetTrueBoolsFromSetting(settings).Select(x => x.Name);
            bool ingredientsIdontLike = settings.IngredientIds != null;

            HashSet<int> recipesToRemove = new();
            HashSet<int> recipesToAdd = new();

            bool cookingTimeOne = filterBool.Contains("CookingTimeOne");

            var queryable = _context.QueryHandler.Where(x => recipeIdsFromDb.Contains(x.Recipe.Id)
                                                  && x.Recipe.Category == category
                                                  && (cookingTimeOne
                                                      ? x.Recipe.PreparationTime < 45
                                                      : x.Recipe.PreparationTime > 1))
                                                 .Include(x => x.Recipe)
                                                 .AsNoTracking();


            foreach (var filter in filterBool)
            {
                if (filter.StartsWith("No"))
                {
                    var subName = filter.Substring(2);
                    recipesToRemove.AddRange(queryable.Where(r => r.Query.Query.ToLower() == subName.ToLower())
                                                       .Select(x => x.Recipe.Id)
                                                       .ToHashSet()
                    );

                }
                else
                {
                    recipesToAdd.AddRange(queryable.Where(r => r.Query.Query.ToLower() == filter.ToLower())
                                                       .Select(x => x.Recipe.Id)
                                                       .ToHashSet()
                    );
                }
            }

            if (recipesToAdd.Any())
            {
                return  SortByIngredientIdontLike(settings,recipesToAdd.ToList());
            }
            else
            {
                return SortByIngredientIdontLike(settings,queryable.Where(x => !recipesToRemove.Contains(x.Recipe.Id))
                                            .Select(x => x.Recipe.Id)
                                            .ToHashSet()
                                            .ToList());
            }


        }

        private List<PropertyInfo> GetTrueBoolsFromSetting(PersonalMealPlanSettings settings)
        {
            var bools = typeof(PersonalMealPlanSettings)
                       .GetProperties()
                       .Where(p => p.PropertyType == typeof(bool));

            return bools.Where(b => (bool)b.GetValue(settings) == true).ToList();
        }

        private List<int> SortByIngredientIdontLike(PersonalMealPlanSettings settings, List<int> sortedRecipes)
        {
            if (settings.IngredientIds == null)
                return sortedRecipes;

            var recipes = _context.RecipesHandlers.Where(x => sortedRecipes.Contains(x.Recipe.Id))
                                                     .Include(x => x.IngredientHandler)
                                                     .AsNoTracking()
                                                     .AsQueryable();

            var sort = recipes.Where(x => settings.IngredientIds.Contains(x.IngredientHandler.Ingredient.Id))
                            .Select(x => x.Recipe.Id)
                            .ToHashSet()
                            .ToList();

            return sortedRecipes.Except(sort).ToList();
        }




        private List<int> SortListByIngredient(List<int> sortedRecipesByQuery, PersonalMealPlanSettings settings)
        {
            var hasWithout = settings.IngredientIds?.Any() ?? false;
            var hasAtHome = settings.IngredientsAtHome?.Any() ?? false;

            return (hasWithout, hasAtHome) switch
            {
                (false, false) => sortedRecipesByQuery, // nichts gesetzt

                (true, true) =>
                    GetSortetListWhithIngredients(
                        GetSortetListWhithOutIngredients(sortedRecipesByQuery, settings),
                        settings),

                (true, false) =>
                    GetSortetListWhithOutIngredients(sortedRecipesByQuery, settings),

                (false, true) => GetSortetListWhithIngredients(sortedRecipesByQuery, settings)


            };

        }

        private List<int> GetSortetListWhithOutIngredients(List<int> sortedList, PersonalMealPlanSettings settings)
        {

            var ingredientsIdsIDontLike = settings.IngredientIds;

            var idsFromRecipesIDontLike = _context.RecipesHandlers
                                          .Where(x => sortedList.Contains(x.Recipe.Id) && ingredientsIdsIDontLike.Contains(x.IngredientHandler.Ingredient.Id))
                                          .Select(x => x.Recipe.Id)
                                          .Distinct()
                                          .ToList();

            var machedRecipes = _context.Recipes
                                .Where(x => !idsFromRecipesIDontLike.Contains(x.Id))
                                .Select(x => x.Id)
                                .ToList();


            return machedRecipes;

        }
        //ToDo:Noch so umschreiben das 1-2 rezepte mit den treffenden zutaten zurück kommen
        //Nicht nur die dabei
        //Todo:2 In der db Stehen 100 gerichte mit der category hauptgericht das auf hauptspeise ändern

        private List<int> GetSortetListWhithIngredients(List<int> sortedList, PersonalMealPlanSettings settings)
        {

            var ingredientsIdsILike = settings.IngredientsAtHome;

            var idsFromRecipesIDontLike = _context.RecipesHandlers
                                          .Where(x => sortedList.Contains(x.Recipe.Id) && ingredientsIdsILike.Contains(x.IngredientHandler.Ingredient.Id))
                                          .Select(x => x.Recipe.Id)
                                          .Distinct()
                                          .ToList();

            var machedRecipes = _context.Recipes
                                .Where(x => idsFromRecipesIDontLike.Contains(x.Id))
                                .Select(x => x.Id)
                                .ToList();


            return machedRecipes;

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
