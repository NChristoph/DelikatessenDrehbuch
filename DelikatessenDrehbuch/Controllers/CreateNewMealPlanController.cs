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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace DelikatessenDrehbuch.Controllers
{
    
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
        
        public async Task<ActionResult> IndexAsync(PersonalMealPlanSettings settings)
        {

            _sessionService.ClearSession();


            if (settings.DayCount > 7)
            {
                return Ok("Maximal 7 Tage erlaubt");
            }

            _sessionService.SaveMealPlanSettingsToSession(settings);

            ViewData["PersonCount"] = settings.PersonCount;
            var model = await _mealPlanService.GetMealPlanModels("Hauptspeise", settings.DayCount);
            
            _sessionService.SavePersonalMealPlanToSession(model);


            return View("~/Views/MealPlaner/CreateNewMealPlan.cshtml", model);
        }

        public IActionResult MealPlanSetting()
        {
            var haveCatchetContent = _sessionService.GetPersonalMealPlanFromSession().Any();

            ModelForMealPlanSettingView model = new()
            {
                Ingredients = _context.Ingredients.ToList(),
                PersonalMealPlanSettings = _sessionService.GetMealPlanSettingsFromSession() ?? new PersonalMealPlanSettings(),
                HaveCatchContent = haveCatchetContent
            };
            return View("~/Views/MealPlaner/MealPlanerUserSettings.cshtml", model);
        }
       
        public IActionResult MealPlanFromSession()
        {
            var modelFromCache = _sessionService.GetPersonalMealPlanFromSession();
            ViewData["PersonCount"] = _sessionService.GetMealPlanSettingsFromSession().PersonCount;
            return View("~/Views/MealPlaner/CreateNewMealPlan.cshtml", modelFromCache);

        }

        public IActionResult LoadLastMealPlan()
        {
            ViewData["PersonCount"] = _sessionService.GetMealPlanSettingsFromSession().PersonCount;
            var catOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Vorspeise"] = 0,
                ["Hauptspeise"] = 1,
                ["Dessert"] = 2
            };

            var model = _sessionService.GetPersonalMealPlanFromSession()
                .GroupBy(x => x.Index)
                .OrderBy(g => g.Key)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(r => catOrder.TryGetValue(r.Recipes?.Category.Trim() ?? "", out var i) ? i : int.MaxValue)
                          .ThenBy(r => r.Recipes?.Category.Trim()) // falls Kategorie unbekannt, stabil nach Name
                          .ToList()
                );

            return View("~/Views/MealPlaner/CreatedMealPlan.cshtml", model);
            
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

            return PartialView("~/Views/MealPlaner/_mealPlanerRecipesPartialView.cshtml", model);
        }



        public IActionResult CreatedMealPlan(string indexAndIds, int personCount)
        {
            ViewData["PersonCount"] = personCount;
            Dictionary<int, List<PersonalMealPlanRecipeModel>> model = _mealPlanService.MapToMealPlanDictionary(indexAndIds);

            return View("~/Views/MealPlaner/CreatedMealPlan.cshtml", model);
        }


        public IActionResult LoadIngredientPartialView(string recipesIds = "", int personCount = 0)
        {
            ViewData["PersonCount"] = personCount;
            var ids = _utilityService.ConvertStringListToIntList(recipesIds.Split(";").ToList());
            var recipesFromSession = _sessionService.GetPersonalMealPlanFromSession();


            if (!int.TryParse(recipesIds, out var id))
            {
                recipesFromSession = recipesFromSession.Where(x => ids.Contains(x.Id)).ToList();
                var model = recipesFromSession.SelectMany(x => x.Ingredients)
                                              .ToList();
                return PartialView("~/Views/MealPlaner/_createMealPlanIngredientPartialView.cshtml", model);

            }
            else
            {

                var model = recipesFromSession.Where(x => x.Id == int.Parse(recipesIds)).SelectMany(x => x.Ingredients)
                                              .ToList();
                return PartialView("~/Views/MealPlaner/_createMealPlanIngredientPartialView.cshtml", model);

            }

        }

        public async Task<IActionResult> LoadRecipesPartialViewAsync(int recipeId = 0, string category = "", string index = "")
        {
            ViewData["Index"] = int.Parse(index);
            var recipeIdsFromSession = _sessionService.GetRecipesIdsFromSession(category);

            if (!recipeIdsFromSession.Any())
            {
                var model = await _mealPlanService.GetMealPlanModels(category, 1);
                model.First().Index = int.Parse(index);
                var recipe = model.First().Recipes;
                EditeSession(model.First(), recipeId, int.Parse(index));

                return PartialView("~/Views/MealPlaner/_createMealPlanRecipesPartialView.cshtml", recipe);
            }
            if (recipeId != 0)
                _sessionService.UpdateRecipesIdsInSession(remove: recipeId, category: category);

            var ran = _recipesService.GetRendomRecipesIds(recipeIdsFromSession, 1);

            var model2 = await _mealPlanService.CreatePersonalMealPlanRecipeModelByIdAsync(ran.First(),0);
            model2.Index = int.Parse(index);
            var recipe2 = model2.Recipes;
            _sessionService.UpdateRecipesIdsInSession(remove: recipe2.Id, category: category);
            EditeSession(model2, recipeId, int.Parse(index));

            return PartialView("~/Views/MealPlaner/_createMealPlanRecipesPartialView.cshtml", recipe2);



        }

        private void EditeSession(PersonalMealPlanRecipeModel model, int idToRemove,int dayIndex)
        {
            var session = _sessionService.GetPersonalMealPlanFromSession();
            session.RemoveAll(x => x.Id == idToRemove);

            if (model != null)
            {
                model.Index = dayIndex;
                session.Add(model);
            }

            _sessionService.SavePersonalMealPlanToSession(session);
        }

       


    }
}
