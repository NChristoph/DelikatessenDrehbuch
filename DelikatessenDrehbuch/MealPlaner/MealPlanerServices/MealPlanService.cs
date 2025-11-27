using Azure.Storage.Blobs.Models;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.MealPlaner.MealPlanerServices.Interfaces;
using DelikatessenDrehbuch.MealPlaner;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Services.Interfaces;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.EntityFrameworkCore;
using NuGet.Packaging;
using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.Tasks;

namespace DelikatessenDrehbuch.MealPlaner.MealPlanerServices
{

    public class MealPlanService : IMealPlanService
    {
        private readonly ApplicationDbContext _context;
        private readonly IUtilityService _utilityService;
        private readonly IHttpContextAccessor _httpContext;
        private readonly IRecipesService _recipesService;
        private readonly IRecipesHandlerService _recipesHandlerService;
        private readonly IIngredientService _ingredientService;
        private readonly ISessionService _sessionService;
        private readonly IMealPlanSortByFilters _mealPlanSortByFilters;
        private readonly IIngredientScaleService _ingredientScaleService;

        private List<int> RecipeIds { get; set; } = new();

        public MealPlanService(ApplicationDbContext context, IUtilityService utilityService, IHttpContextAccessor httpContext,
                               IRecipesService recipesService, IIngredientScaleService ingredientScaleService,
                               ISessionService sessionService, IRecipesHandlerService recipesHandlerService,
                                IMealPlanSortByFilters mealPlanSortByFilters)
        {
            _context = context;
            _httpContext = httpContext;
            _recipesService = recipesService;
            _ingredientScaleService = ingredientScaleService;
            _sessionService = sessionService;
            _recipesHandlerService = recipesHandlerService;
            _utilityService = utilityService;
            _mealPlanSortByFilters = mealPlanSortByFilters;
        }

        public async Task<List<MealPlanerModel>> CreateMealPlanModels(string category, int count, string email, PersonalMealPlanSettings settings)
        {

            var filterBool = _utilityService.GetTrueBoolNamesFromModel(settings);

            RecipeIds = _mealPlanSortByFilters.SortRecipeIdsByFilters(category, filterBool, count);

            return await CreatePersonalMealPlanRecipeModelByIdsAsync(RecipeIds);
        }

      


        public async Task<List<MealPlanerModel>> CreatePersonalMealPlanRecipeModelByIdsAsync(List<int> recipesIds)
        {
            List<MealPlanerModel> modelList = new();
            int index = 1;
            foreach (var recipeId in recipesIds)
            {
                var model = await CreatePersonalMealPlanRecipeModelByIdAsync(recipeId, index);
                index++;
                modelList.Add(model);
            }
            return modelList;
        }






        public async Task<MealPlanerModel> CreatePersonalMealPlanRecipeModelByIdAsync(int recipeId, int index)
        {
            var recipehandlers = await _recipesHandlerService.GetRecipesHandlerByRecipesIdAsync(recipeId);

            MealPlanerModel model = new()
            {
                Index = index,
                Recipes = _context.Recipes.FirstOrDefault(x => x.Id == recipeId),

            };

            model.Recipes.ImagePath=FrontendFunctions.GetSmallImagePath(model.Recipes.ImagePath);


            return model;
        }









        //TODO:Mach das ordentlicher und in der view ein dropdown zum auswählen des plans
        public async Task CreateMealPlanAsync(int recipesId, string mealPlanName)
        {
            var melplanArry = mealPlanName.Split(";");
            MealPlan mealPlan = new()
            {
                Name = melplanArry[0],
                MyMealModel = _context.MyMealModel.Single(x => x.Id == int.Parse(melplanArry[1]))
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
