using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Services;
using DelikatessenDrehbuch.Services.Interfaces;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using NuGet.Packaging.Signing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace DelikatessenDrehbuch.Controllers
{
    [Authorize]
    public class CreateNewMealPlanController : Controller
    {


        private readonly IRecipesService _recipesService;
        private readonly IMealPlanService _mealPlanService;
        private readonly IIngredientService _ingredientService;
        private readonly IUtilityService _utilityService;
        private readonly ApplicationDbContext _context;
        private readonly ISessionService _sessionService;
        private readonly IHttpContextAccessor _httpContext;
        private readonly IQueryService _queryService;
        private readonly INutrientService _nutrientService;


        public CreateNewMealPlanController(IRecipesService recipesService, IMealPlanService mealPlanService,
                                           IIngredientService ingredientService, IUtilityService utilityService,
                                           ApplicationDbContext context, IHttpContextAccessor httpContext,
                                           ISessionService sessionService, IQueryService queryService, INutrientService nutrientService)
        {

            _recipesService = recipesService;
            _mealPlanService = mealPlanService;
            _ingredientService = ingredientService;
            _utilityService = utilityService;
            _httpContext = httpContext;
            _context = context;
            _sessionService = sessionService;
            _queryService = queryService;
            _nutrientService = nutrientService;
        }
        [Authorize]
        public async Task<ActionResult> IndexAsync(PersonalMealPlanSettings settings)
        {
            if (settings.DayCount > 7)
            {
                return Ok("Maximal 7 Tage erlaubt");
            }

            _sessionService.ClearSession();
            
            _sessionService.SaveMealPlanSettingsToSession(settings);


            ViewData["PersonCount"] = settings.PersonCount;
            var model = await _mealPlanService.GetMealPlanModels("Hauptspeise", settings.DayCount);
            _sessionService.SavePersonalMealPlanToSession(model);
            _sessionService.SaveRecipesIdToSession(model.Select(x => x.Id).ToList());

            return View("~/Views/MyRecipes/CreateNewMealPlan.cshtml", model);
        }

        public async Task<IActionResult> GetRecipe(int recipeId)
        {

            ViewData["index"] = _sessionService.GetMealPlanSettingsFromSession().PersonCount;
            var querys = await _queryService.GetQuerysFromDbByRecipeIdAsync(recipeId);
            
            

            SelectedRecipesModel model = new()
            {
                FullRecipeData = await _recipesService.GetFullRecipeDataByRecipesIdAsync(recipeId),
                Querys = querys,
                NutrienHandlers = await _nutrientService.GetNutrienHandlersByIngredientNamesAsync(
                                       await _ingredientService.GetIngredientsNamesByRecipesId(recipeId)),

            };

            return PartialView("~/Views/MealPlaner/_mealPlanerRecipesPartialView.cshtml",model);
        }



        public IActionResult CreatedMealPlan(string indexAndIds, int personCount)
        {
            ViewData["PersonCount"] = personCount;
            Dictionary<int, List<PersonalMealPlanRecipeModel>> model = _mealPlanService.MapToMealPlanDictionary(indexAndIds);

            return View("~/Views/MyRecipes/CreatedMealPlan.cshtml", model);
        }


        public IActionResult LoadIngredientPartialView(string recipesIds = "", int personCount = 0)
        {
            ViewData["PersonCount"] = personCount;
            var recipesFromSession = _sessionService.GetPersonalMealPlanFromSession();
            if (!int.TryParse(recipesIds, out var id))
            {
                var model = recipesFromSession.SelectMany(x => x.Ingredients)
                                              .ToList();
                return PartialView("~/Views/MyRecipes/_createMealPlanIngredientPartialView.cshtml", model);

            }
            else
            {
                var model = recipesFromSession.Where(x => x.Id == int.Parse(recipesIds)).SelectMany(x => x.Ingredients)
                                              .ToList();
                return PartialView("~/Views/MyRecipes/_createMealPlanIngredientPartialView.cshtml", model);

            }

        }

        public async Task<IActionResult> LoadRecipesPartialViewAsync(int recipeId=0,string category="", string index= "")
        {
            ViewData["Index"] = int.Parse(index);

            var model = await _mealPlanService.GetMealPlanModels(category, 1);
            var recipe = model.First().Recipes;

            EditeSession(model.First(), recipeId);

            _sessionService.UpdateRecipesIdsInSession(remove: recipeId, add: recipe.Id);

            return PartialView("~/Views/MyRecipes/_createMealPlanRecipesPartialView.cshtml", recipe);
        }

        private void EditeSession(PersonalMealPlanRecipeModel model,int idToRemove)
        {
            var session = _sessionService.GetPersonalMealPlanFromSession();
            session.RemoveAll(x => x.Id == idToRemove);

            session.Add(model);
            _sessionService.SavePersonalMealPlanToSession(session);
        }

        public async Task<IActionResult> LoadAppetizerOrDessertPartialViewAsync(string category, string index,int id=0)
        {
            ViewData["Index"] = int.Parse(index);
            var model = await _mealPlanService.GetMealPlanModels(category, 1);
            var recipe=model.First().Recipes;
            EditeSession(model.First(), 0);
            if(id!=0)
            {
                _sessionService.UpdateRecipesIdsInSession(remove: id);
            }
          
            return PartialView("~/Views/MyRecipes/_createMealPlanRecipesPartialView.cshtml",recipe);
        }


    }
}
