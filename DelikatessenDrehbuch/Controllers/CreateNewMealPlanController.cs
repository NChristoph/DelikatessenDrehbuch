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
using Stripe;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Configuration;
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
        private readonly IQueryService _queryService;
        private readonly INutrientService _nutrientService;
        private readonly IRecipesHandlerService _recipesHandlerService;


        public CreateNewMealPlanController(IRecipesService recipesService, IMealPlanService mealPlanService,
                                           IIngredientService ingredientService, IUtilityService utilityService,
                                           ApplicationDbContext context,
                                           ISessionService sessionService, IQueryService queryService,
                                           INutrientService nutrientService, IRecipesHandlerService recipesHandlerService)
        {

            _recipesService = recipesService;
            _mealPlanService = mealPlanService;
            _ingredientService = ingredientService;
            _utilityService = utilityService;
            _context = context;
            _sessionService = sessionService;
            _queryService = queryService;
            _nutrientService = nutrientService;
            _recipesHandlerService = recipesHandlerService;
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

            _sessionService.SavePersonalMealPlanDictionaryToSession(JsonSerializer.Serialize(
                model.GroupBy(x => x.Index)
                     .ToDictionary(g => g.Key, g => g.Select(r => r.Id).ToList())
                ));



            return View("~/Views/MealPlaner/CreateNewMealPlan.cshtml", model);
        }

        public IActionResult MealPlanSetting()
        {
            var haveCatchetContent = _sessionService.GetPersonalMealPlanDictionaryFromSession().Any();

            ModelForMealPlanSettingView model = new()
            {
                Ingredients = _context.Ingredients.ToList(),
                PersonalMealPlanSettings = _sessionService.GetMealPlanSettingsFromSession() ?? new PersonalMealPlanSettings(),
                HaveCatchContent = haveCatchetContent
            };
            return View("~/Views/MealPlaner/MealPlanerUserSettings.cshtml", model);
        }

        public async Task<IActionResult> MealPlanFromSession()
        {
            var indexAndIds = _sessionService.GetPersonalMealPlanDictionaryFromSession();
            var modelFromCache = await _mealPlanService.MapToMealPlanDictionaryAsync(indexAndIds);
            var model = modelFromCache.SelectMany(x => x.Value).ToList();
            ViewData["PersonCount"] = _sessionService.GetMealPlanSettingsFromSession().PersonCount;
            return View("~/Views/MealPlaner/CreateNewMealPlan.cshtml", model);

        }

        public async Task<IActionResult> LoadLastMealPlanAsync()
        {
            ViewData["PersonCount"] = _sessionService.GetMealPlanSettingsFromSession().PersonCount;
            var indexAndIds = _sessionService.GetPersonalMealPlanDictionaryFromSession();
            var model = await _mealPlanService.MapToMealPlanDictionaryAsync(indexAndIds);

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



        public async Task<IActionResult> CreatedMealPlanAsync(string indexAndIds, int personCount)
        {
            ViewData["PersonCount"] = personCount;
            var indexAndId = _sessionService.GetPersonalMealPlanDictionaryFromSession();
            var model = await _mealPlanService.MapToMealPlanDictionaryAsync(indexAndId);

            return View("~/Views/MealPlaner/CreatedMealPlan.cshtml", model);
        }

        [HttpPost]
        public void SaveDayInSession(string indexAndIds)
        {

            _sessionService.SavePersonalMealPlanDictionaryToSession(indexAndIds);
        }




        public async Task<IActionResult> GetIngredientPerDayPartialView(string index)
        {

            ViewData["PersonCount"] = _sessionService.GetMealPlanSettingsFromSession().PersonCount;
            var indexAndIds = _sessionService.GetPersonalMealPlanDictionaryFromSession();
            var mealPlanDic = await _mealPlanService.MapToMealPlanDictionaryAsync(indexAndIds);
            var mealplans = mealPlanDic[int.Parse(index)];
            var model = mealplans.SelectMany(x => x.Ingredients).ToList();
        

            return PartialView("~/Views/MealPlaner/_createMealPlanIngredientPartialView.cshtml", model);
        }
        public async Task<IActionResult> LoadIngredientPartialViewAsync()
        {
            ViewData["PersonCount"] = _sessionService.GetMealPlanSettingsFromSession().PersonCount;

            var indexAndIds = _sessionService.GetPersonalMealPlanDictionaryFromSession();
            var mealPlanDic = await _mealPlanService.MapToMealPlanDictionaryAsync(indexAndIds);
            var recipesFromSession = mealPlanDic.SelectMany(x => x.Value).ToList();
            var model=recipesFromSession.SelectMany(x => x.Ingredients).ToList();

            return PartialView("~/Views/MealPlaner/_createMealPlanIngredientPartialView.cshtml", model);

        }


        public async Task<IActionResult> LoadMealPlanOverviewPartialViewAsync(string IndexAndIds)
        {
            var model = new Dictionary<int, List<Recipes>>();

            var decoded = Uri.UnescapeDataString(IndexAndIds);
            var dictionary = JsonSerializer.Deserialize<Dictionary<int, List<int>>>(decoded);

            var allRecipeIds = dictionary.Values.SelectMany(list => list).Distinct().ToList();

            var order = new[] { "Vorspeise", "Hauptspeise", "Dessert" };

            foreach (var kvp in dictionary)
            {
                var recipesList = await _recipesService.GetRecipesListByIdsAsync(kvp.Value);
                model[kvp.Key] = recipesList
                    .OrderBy(r => Array.IndexOf(order, r.Category.Trim()))
                    .ToList();
            }


            return PartialView("~/Views/MealPlaner/_mealPlanerOverView.cshtml", model);
        }

        public string? GetSessionNameByRecipeId(int recipeId)
        {
            foreach (var key in HttpContext.Session.Keys)
            {

                if (key != "MealPlanSettings" && key != "MealPlanList")
                {
                    var data = _sessionService.GetRecipesIdsFromSession(key);
                    if (data != null && data.Contains(recipeId))
                    {
                        return key;
                    }
                }

            }
            return "";
        }

        public async Task<IActionResult> LoadRecipesPartialViewAsync(int recipeId = 0, string category = "", string index = "")
        {
            if(recipeId!=0)
            {
                EditeSession(recipeId, int.Parse(index));
            }
           
            var recipeIdsFromSession = new List<int>();
            var parsedIndex = int.Parse(index);
            string? sessionName = "";
            Recipes model = new();
            PersonalMealPlanRecipeModel personal = new(_ingredientService);
            recipeIdsFromSession = _sessionService.GetRecipesIdsFromSession(category);

           
            ViewData["Index"] = parsedIndex;


            if (recipeId == 0 && !recipeIdsFromSession.Any())
            {
                var personalList = await _mealPlanService.GetMealPlanModels(category, 1);
                personalList.First().Index = parsedIndex;
                personal = personalList.First();
                model = personal.Recipes;
                sessionName = category;
            }
            else
            {
                if (recipeId != 0 &&!recipeIdsFromSession.Any())
                    sessionName = GetSessionNameByRecipeId(recipeId);

                if (!string.IsNullOrEmpty(sessionName))
                    recipeIdsFromSession = _sessionService.GetRecipesIdsFromSession(sessionName);
                else
                    sessionName = category;

                personal = await _mealPlanService.CreatePersonalMealPlanRecipeModelByIdAsync(
                                                                    _recipesService.GetRendomRecipesIds(recipeIdsFromSession, 1).First()
                                                                    , parsedIndex);

                model = personal.Recipes;
            }


            EditeSession(model.Id, int.Parse(index));
            _sessionService.UpdateRecipeIdInSession(remove: personal.Id, category: sessionName);

            return PartialView("~/Views/MealPlaner/_createMealPlanRecipesPartialView.cshtml", model);

        }

        public void EditeSession(int recipeId, int dayIndex)
        {
            var sessionDic = _sessionService.GetPersonalMealPlanDictionaryFromSession();
            var recipes = _recipesService.GetRecipesFromDbByIdAsync(recipeId).Result;

            if (sessionDic[dayIndex].Contains(recipeId))
                sessionDic[dayIndex].Remove(recipeId);
            else
            {
                if (recipes.Category.Trim() == "Vorspeise")
                    sessionDic[dayIndex].Insert(0,recipeId);
                else if (recipes.Category.Trim() == "Hauptspeise")
                {
                    if (sessionDic[dayIndex].Count > 1)
                        sessionDic[dayIndex].Insert(1, recipeId);
                    else
                        sessionDic[dayIndex].Add(recipeId);
                }
                else
                sessionDic[dayIndex].Add(recipeId);
               

            }


            _sessionService.SavePersonalMealPlanDictionaryToSession(JsonSerializer.Serialize(sessionDic));

        }




    }
}
