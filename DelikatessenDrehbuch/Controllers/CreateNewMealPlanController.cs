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
    [Authorize]
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
            int personCount = settings.PersonCount;

            if (settings.DayCount > 7)
            {
                return Ok("Maximal 7 Tage erlaubt");
            }

            _sessionService.SaveMealPlanSettingsToSession(settings);

            ViewData["PersonCount"] = personCount;
            var model = await _mealPlanService.GetMealPlanModels("Hauptspeise", settings.DayCount);

            var dict = model
                .Select((value, index) => new { index, value })
                .ToDictionary(
                    x => x.index+1,                     // Key = Position
                    x => new List<int> { x.value.Id } // Value = Liste mit einer ID
                );

            SaveMealPlanToDb(JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }), personCount);

         

            return View("~/Views/MealPlaner/CreateNewMealPlan.cshtml", model);
        }

        public IActionResult MealPlanSetting()
        {
            var catche = _context.SavedMealPlan.Where(x => x.UserMail == User.Identity.Name).First();
            ModelForMealPlanSettingView model = new()
            {
                Ingredients = _context.Ingredients.ToList(),
                PersonalMealPlanSettings = _sessionService.GetMealPlanSettingsFromSession() ?? new PersonalMealPlanSettings(),
                HaveCatchContent = catche != null
            };
            return View("~/Views/MealPlaner/MealPlanerUserSettings.cshtml", model);
        }

        public async Task<IActionResult> MealPlanFromSession()
        {
            var mealPlan = GetSavedMealPlanFromDb();

            var indexAndIds = mealPlan.MealPlanDictionary;
            var modelFromCache = await _mealPlanService.MapToMealPlanDictionaryAsync(indexAndIds, mealPlan.PersonCount);
            var model = modelFromCache.SelectMany(x => x.Value).ToList();
            ViewData["PersonCount"] = mealPlan.PersonCount;
            return View("~/Views/MealPlaner/CreateNewMealPlan.cshtml", model);

        }

        public async Task<IActionResult> LoadLastMealPlanAsync()
        {

            var mealPlan = GetSavedMealPlanFromDb();
            ViewData["personCount"] = mealPlan.PersonCount;
            var model = await _mealPlanService.MapToMealPlanDictionaryAsync(mealPlan.MealPlanDictionary, mealPlan.PersonCount);

            return View("~/Views/MealPlaner/CreatedMealPlan.cshtml", model);

        }



        public async Task<IActionResult> GetRecipe(int recipeId)
        {
            var mealPlan = GetSavedMealPlanFromDb();
            ViewData["index"] = mealPlan.PersonCount;
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

            var mealPlan = GetSavedMealPlanFromDb();
            var model = await _mealPlanService.MapToMealPlanDictionaryAsync(mealPlan.MealPlanDictionary, mealPlan.PersonCount);

            SaveMealPlanToDb(indexAndIds, personCount);

            return View("~/Views/MealPlaner/CreatedMealPlan.cshtml", model);
        }

        private void SaveMealPlanToDb(string indexAndIds, int personCount)
        {
            var savedMealPlan = _context.SavedMealPlan.Where(x => x.UserMail == User.Identity.Name).First();
            if (savedMealPlan != null)
            {
                savedMealPlan.MealPlanJson = indexAndIds;
               
                savedMealPlan.CreationTime= DateTime.Now;
                _context.SavedMealPlan.Update(savedMealPlan);
                _context.SaveChanges();

            }
            else
            {
                savedMealPlan = new SavedMealPlans
                {
                    UserMail = User.Identity.Name,
                    PersonCount = personCount,
                    MealPlanJson = indexAndIds
                };
                _context.SavedMealPlan.Add(savedMealPlan);
                _context.SaveChanges();
            }


        }

        [HttpPost]
        public void SaveDayInSession(string indexAndIds)
        {
            SaveMealPlanToDb(indexAndIds, _sessionService.GetMealPlanSettingsFromSession().PersonCount);
           
        }

        public SavedMealPlans GetSavedMealPlanFromDb()
        {
            var savedMealPlan = _context.SavedMealPlan.Where(x => x.UserMail == User.Identity.Name).First();
            savedMealPlan.MealPlanDictionary = JsonSerializer.Deserialize<Dictionary<int, List<int>>>(savedMealPlan.MealPlanJson, new JsonSerializerOptions());

            return savedMealPlan;
        }



        public async Task<IActionResult> GetIngredientPerDayPartialView(string index)
        {
            var mealPlan = GetSavedMealPlanFromDb();
            ViewData["PersonCount"] = mealPlan.PersonCount;

            var mealPlanDic = await _mealPlanService.MapToMealPlanDictionaryAsync(mealPlan.MealPlanDictionary, mealPlan.PersonCount);
            var mealplans = mealPlanDic[int.Parse(index)];
            var model = mealplans.SelectMany(x => x.Ingredients).ToList();


            return PartialView("~/Views/MealPlaner/_createMealPlanIngredientPartialView.cshtml", model);
        }
        public async Task<IActionResult> LoadIngredientPartialViewAsync()
        {
            var mealPlan = GetSavedMealPlanFromDb();
            ViewData["PersonCount"] = mealPlan.PersonCount;

            var indexAndIds = mealPlan.MealPlanDictionary;
            var mealPlanDic = await _mealPlanService.MapToMealPlanDictionaryAsync(indexAndIds, _sessionService.GetMealPlanSettingsFromSession().PersonCount);
            var recipesFromSession = mealPlanDic.SelectMany(x => x.Value).ToList();
            var model = recipesFromSession.SelectMany(x => x.Ingredients).ToList();

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
            if (recipeId != 0)
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
                if (recipeId != 0 && !recipeIdsFromSession.Any())
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
            var sessionDic =GetSavedMealPlanFromDb().MealPlanDictionary;
            var recipes = _recipesService.GetRecipesFromDbByIdAsync(recipeId).Result;

            if (sessionDic[dayIndex].Contains(recipeId))
                sessionDic[dayIndex].Remove(recipeId);
            else
            {
                if (recipes.Category.Trim() == "Vorspeise")
                    sessionDic[dayIndex].Insert(0, recipeId);
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


            SaveMealPlanToDb(JsonSerializer.Serialize(sessionDic, new JsonSerializerOptions { WriteIndented = true }), _sessionService.GetMealPlanSettingsFromSession().PersonCount);
        }




    }
}
