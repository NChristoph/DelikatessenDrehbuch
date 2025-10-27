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
using System.Threading.Tasks;

namespace DelikatessenDrehbuch.Services
{

    public class MealPlanService : IMealPlanService
    {
        private readonly ApplicationDbContext _context;
        private readonly HelpfulMethods _helper;
        private readonly IMealPlanUtilityService _mealPlanUtilityService;
        private readonly IHttpContextAccessor _httpContext;
        private readonly IRecipesService _recipesService;
        private readonly IRecipesHandlerService _recipesHandlerService;
        private readonly IIngredientService _ingredientService;
        private readonly ISessionService _sessionService;
       

        public MealPlanService(ApplicationDbContext context, HelpfulMethods helper, IHttpContextAccessor httpContext,
                               IRecipesService recipesService, IIngredientService ingredientService,
                               ISessionService sessionService, IRecipesHandlerService recipesHandlerService, IMealPlanUtilityService helperUtility)
        {
            _context = context;
            _helper = helper;
            _httpContext = httpContext;
            _recipesService = recipesService;
            _ingredientService = ingredientService;
            _sessionService = sessionService;
            _recipesHandlerService = recipesHandlerService;
            _mealPlanUtilityService = helperUtility;
        }

        public async Task<List<PersonalMealPlanRecipeModel>> GetMealPlanModels(string category, int count)
        {
            List<int> recipeIds = new();

            recipeIds = _recipesService.GetRecipesIdsByCategory(category);
            recipeIds = await _mealPlanUtilityService.SortRecipeIdsBySettingsAsync(recipeIds, category);

            if (count>1)
                recipeIds = GetFinalRecipeIds(category, count);
            else
                recipeIds = _recipesService.GetRendomRecipesIds(recipeIds, count);

                var personalRecipes = await CreatePersonalMealPlanRecipeModelByIdsAsync(recipeIds);

            return personalRecipes;

        }

        public string GetRecipeIdsByPreference()
        {
            var filter = MealPlanUtilityService.GetTrueBoolNamesFromSetting(_sessionService.GetMealPlanSettingsFromSession());
            var preference= filter.FirstOrDefault(x => x.StartsWith("preferably")) ?? string.Empty;
            return preference;
           
        }

        private List<int> GetFinalRecipeIds(string category, int count)
        {
            var preference = GetRecipeIdsByPreference();
            List<int> currentUsedIds = new();
            List<int> recipeIds = _sessionService.GetRecipesIdsFromSession(preference);
            var recipeIdsByIngredient = _sessionService.GetRecipesIdsFromSession("IngredientsAtHome_" + category);
            var recipeIdsFromSession = _sessionService.GetRecipesIdsFromSession(category);

            if (recipeIds.Any())
            {
                currentUsedIds = _recipesService.GetRendomRecipesIds(recipeIds, count > 2 ? count-2 : count -1);
                _sessionService.ExceptRecipeIdsFromSession(currentUsedIds, preference);
            }
            if(recipeIdsByIngredient.Any())
            {
                currentUsedIds.AddRange(_recipesService.GetRendomRecipesIds(recipeIdsByIngredient, count - currentUsedIds.Count));
                _sessionService.ExceptRecipeIdsFromSession(currentUsedIds, "IngredientsAtHome_" + category);
            }
            if(!recipeIdsByIngredient.Any()||!recipeIds.Any())
            {
                currentUsedIds.AddRange(_recipesService.GetRendomRecipesIds(recipeIdsFromSession, count-currentUsedIds.Count));
                _sessionService.ExceptRecipeIdsFromSession(currentUsedIds, category);
                
            }

            return currentUsedIds;

        }

        public async Task<List<PersonalMealPlanRecipeModel>> GetPersonalMealPlanModelListByIds(List<int> recipesIds, int dayCount)
        {

            var recipesIdsFromDb = _context.Recipes.Where(x => x.Category == "Hauptspeise" && recipesIds.Contains(x.Id))
                                                .Select(x => x.Id)
                                                .ToList();

            var randomRecipesIds = _recipesService.GetRendomRecipesIds(recipesIdsFromDb, dayCount);
            recipesIds.RemoveAll(x => randomRecipesIds.Contains(x));
            _sessionService.SaveRecipesIdToSession(recipesIds, "RecipesFromDbIds");

            var model = await CreatePersonalMealPlanRecipeModelByIdsAsync(randomRecipesIds);


            return model;
        }

        public async Task<List<PersonalMealPlanRecipeModel>> CreatePersonalMealPlanRecipeModelByIdsAsync(List<int> recipesIds)
        {
            List<PersonalMealPlanRecipeModel> modelList = new();
            int index = 1;
            foreach (var recipeId in recipesIds)
            {
                var model = await CreatePersonalMealPlanRecipeModelByIdAsync(recipeId, index);
                index++;
                modelList.Add(model);
            }
            return modelList;
        }

        public async Task<PersonalMealPlanRecipeModel> CreatePersonalMealPlanRecipeModelByIdAsync(int recipeId, int index)
        {
            var recipehandlers = await _recipesHandlerService.GetRecipesHandlerByRecipesIdAsync(recipeId);

            PersonalMealPlanRecipeModel model = new(_ingredientService)
            {
                Index = index,
                Id = recipeId,
                Recipes = _context.Recipes.FirstOrDefault(x => x.Id == recipeId),
                Ingredients = _recipesService.GetOrdetIngredientHandler(recipehandlers)
            };

            model.ScaleIngredients(_sessionService.GetMealPlanSettingsFromSession().PersonCount);

            return model;
        }

        public async Task< Dictionary<int, List<PersonalMealPlanRecipeModel>>> MapToMealPlanDictionaryAsync(Dictionary<int,List<int>> indexAndIds)
        {
            var model = new Dictionary<int, List<PersonalMealPlanRecipeModel>>();

            foreach (var key in indexAndIds.Keys)
            {
                var recipeIds = indexAndIds[key];
                foreach (var id in recipeIds)
                {
                    PersonalMealPlanRecipeModel personalMealPlanRecipeModel = new(_ingredientService)
                    {
                        Index = key,
                        Id = id,
                        Recipes = await _recipesService.GetRecipesFromDbByIdAsync(id),
                        Ingredients = await _ingredientService.GetIngredientsByRecipesIdFromDbAsync(id)
                    };
                    personalMealPlanRecipeModel.ScaleIngredients(_sessionService.GetMealPlanSettingsFromSession().PersonCount);
                    model.TryAdd(key, new List<PersonalMealPlanRecipeModel>());
                    model[key].Add(personalMealPlanRecipeModel);
                }
               
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
