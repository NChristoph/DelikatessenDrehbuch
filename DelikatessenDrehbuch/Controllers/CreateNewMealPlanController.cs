using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.MealPlaner.MealPlanerServices.Interfaces;
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
        private readonly IMealPlanEditorService _mealPlanEditorService;
        private readonly IIngredientScaleService _ingredientScaleService;


        public CreateNewMealPlanController(IRecipesService recipesService, IMealPlanService mealPlanService,
                                           IIngredientService ingredientService, IUtilityService utilityService,
                                           ApplicationDbContext context,
                                           ISessionService sessionService, IQueryService queryService,
                                           INutrientService nutrientService, IMealPlanEditorService mealPlanEditorService,
                                           IIngredientScaleService ingredientScaleService)
        {

            _recipesService = recipesService;
            _mealPlanService = mealPlanService;
            _ingredientService = ingredientService;
            _utilityService = utilityService;
            _context = context;
            _sessionService = sessionService;
            _queryService = queryService;
            _nutrientService = nutrientService;
            _mealPlanEditorService = mealPlanEditorService;
            _ingredientScaleService = ingredientScaleService;
        }


        public async Task<ActionResult> IndexAsync(PersonalMealPlanSettings settings)
        {

            _sessionService.ClearSession();
            int personCount = settings.PersonCount;

            if (settings.DayCount > 7)
                return Ok("Maximal 7 Tage erlaubt");

            ViewData["PersonCount"] = personCount;



            var model = await _mealPlanService.CreateMealPlanModels("Hauptspeise", settings.DayCount, User.Identity.Name, settings);

            //var dict = model
            //    .Select((value, index) => new { index, value })
            //    .ToDictionary(
            //        x => x.index + 1,                     // Key = Position
            //       // Value = Liste mit einer ID
            //    );

            //SaveMealPlanToDb(JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }), personCount,
            //                 JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true })
            //);


            return View("~/Views/MealPlaner/CreateNewMealPlan.cshtml", model);
        }

        private PersonalMealPlanSettings GetPersonalMealPlanSettingsFromDb()
        {
            var catche = _context.SavedMealPlan.FirstOrDefault(x => x.UserMail == User.Identity.Name);
            PersonalMealPlanSettings? perso = new();
            if (!string.IsNullOrEmpty(catche.UserSettingJson))
            {
                perso = JsonSerializer.Deserialize<PersonalMealPlanSettings?>(catche.UserSettingJson);
            }

            return perso;
        }

        public IActionResult MealPlanSetting()
        {

            var perso = GetPersonalMealPlanSettingsFromDb();
//TODO:das mit have catchconrtent könnte probleme machen
            ModelForMealPlanSettingView model = new()
            {
                Ingredients = _context.Ingredients.ToList(),
                PersonalMealPlanSettings = perso,
                HaveCatchContent = perso != null
            };
            return View("~/Views/MealPlaner/MealPlanerUserSettings.cshtml", model);
        }

        //TODO: Hier noch anpassen damit die Einstellungen aus der Session geladen werden

        //public async Task<IActionResult> EditeLastMealPlan()
        //{
        //  //  ViewData["PersonCount"] = mealPlan.PersonCount;
        //  //  return View("~/Views/MealPlaner/CreateNewMealPlan.cshtml", model);

        //}
        //TODO: Hier noch anpassen damit die Einstellungen aus der Session geladen werden

        //public async Task<IActionResult> LoadLastMealPlanAsync()
        //{


        //  //  ViewData["personCount"] = mealPlan.PersonCount;

        //   // return View("~/Views/MealPlaner/CreatedMealPlan.cshtml", model);

        //}



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

            var model = await _mealPlanService.CreateMealPlanModelsByIdsAsync(indexAndIds, personCount);
            SaveMealPlanToDb(indexAndIds, personCount);

            return View("~/Views/MealPlaner/CreatedMealPlan.cshtml", model);
        }

        public async Task<IActionResult> LoadMealPlanRecipesPartialView(int id, int personCount)
        {
            ViewData["PersonCount"] = personCount;
            var model = await _recipesService.GetRecipesFromDbByIdAsync(id);

            return PartialView("~/Views/MealPlaner/_mealPlanerRecipesPartialView.cshtml", model);
        }

        public IActionResult LoadMealPlanerIngredients(int id, int personCount)
        {
            var model = _ingredientScaleService.GetScaledIngredienthandlerAsync(id, personCount);
            return PartialView("~/Views/MealPlaner/_mealPlanerIngredientsPartial.cshtml", model);
        }

        private void SaveMealPlanToDb(string indexAndIds = "", int personCount = 0, string settingsJson = "")
        {
            var userMail = User?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(userMail))
                throw new InvalidOperationException("Kein Benutzerkontext (User.Identity.Name ist null).");

            // Bestehenden Plan laden (ohne Exception bei 'kein Treffer')
            var savedMealPlan = _context.SavedMealPlan
                                        .FirstOrDefault(x => x.UserMail == userMail);

            if (savedMealPlan is null)
            {
                // Neu anlegen
                savedMealPlan = new SavedMealPlans
                {
                    UserMail = userMail,
                    PersonCount = personCount,
                    MealPlanJson = indexAndIds,
                    Token = _utilityService.GenerateRandomToken(20),
                    CreationTime = DateTime.UtcNow,
                    UserSettingJson = settingsJson

                };

                _context.SavedMealPlan.Add(savedMealPlan);
                _context.SaveChanges();
                return;
            }

            // Update
            savedMealPlan.MealPlanJson = indexAndIds;
            savedMealPlan.PersonCount = personCount;
            savedMealPlan.UserSettingJson = savedMealPlan.UserSettingJson = !string.IsNullOrEmpty(settingsJson)
                                                                            ? settingsJson
                                                                            : savedMealPlan.UserSettingJson;

            // Token nur erzeugen, wenn noch keiner existiert
            savedMealPlan.Token = string.IsNullOrEmpty(savedMealPlan.Token)
                ? _utilityService.GenerateRandomToken(20)
                : savedMealPlan.Token;

            // Beim Update kein CreationTime überschreiben
            savedMealPlan.CreationTime = DateTime.UtcNow;

            // Kein .Update(...) nötig, Entity ist getrackt
            _context.SaveChanges();
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

        public IActionResult GetIngredientPerDayPartialView(string index)
        {
            var mealPlan = GetSavedMealPlanFromDb();
            ViewData["PersonCount"] = mealPlan.PersonCount;
            int id = int.Parse(index);
            //TODO: Hier noch anpassen das alle Zutaten geladen werden und nicht nur von einem Rezept
            var model = new List<IngredientHandlerModel>();// _mealPlanService.GetScalIngredientsById(id, mealPlan.PersonCount);


            return PartialView("~/Views/MealPlaner/_createMealPlanIngredientPartialView.cshtml", model);
        }
        public async Task<IActionResult> LoadIngredientPartialViewAsync()
        {
            var mealPlan = GetSavedMealPlanFromDb();
            ViewData["PersonCount"] = mealPlan.PersonCount;

            var indexAndIds = mealPlan.MealPlanDictionary;
            //var mealPlanDic = await _mealPlanService.MapToMealPlanDictionaryAsync(indexAndIds, mealPlan.PersonCount);
            //var recipesFromSession = mealPlanDic.SelectMany(x => x.Value).ToList();
            var model = new List<IngredientHandlerModel>();//recipesFromSession.SelectMany(x => x.Ingredients).ToList();

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
                UpdateRecipeDictInDB(recipeId, int.Parse(index));
            }

            var settings = JsonSerializer.Deserialize<PersonalMealPlanSettings>(GetSavedMealPlanFromDb().UserSettingJson);

            var recipeIdsFromSession = new List<int>();
            var parsedIndex = int.Parse(index);
            string? sessionName = "";
            Recipes model = new();
            CreateNewMealPlanModel personal = new();
            recipeIdsFromSession = _sessionService.GetRecipesIdsFromSession(category);
            var userMail = User.Identity.Name;


            ViewData["Index"] = parsedIndex;


            if (recipeId == 0 || !recipeIdsFromSession.Any())
            {
                var personalList = await _mealPlanService.CreateMealPlanModels(category, 1, userMail, settings);
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

                var recipes = _utilityService.GetRandomIntFromList(recipeIdsFromSession);

                personal = await _mealPlanService.CreatePersonalMealPlanRecipeModelByIdAsync(
                                                                    recipes
                                                                    , parsedIndex);

                model = personal.Recipes;
            }


            UpdateRecipeDictInDB(model.Id, int.Parse(index));
            _sessionService.UpdateRecipeIdInSession(remove: personal.Recipes.Id, category: sessionName);

            return PartialView("~/Views/MealPlaner/_createMealPlanRecipesPartialView.cshtml", model);

        }

        public void UpdateRecipeDictInDB(int recipeId, int dayIndex)
        {
            var sessionDic = GetSavedMealPlanFromDb().MealPlanDictionary;
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


        //public IActionResult GetShoppingList()
        //{
        //    var mealPlan = GetSavedMealPlanFromDb();
        //    ViewData["HaveShoppingList"] = mealPlan.ShoppingList != null;

        //    var dictionary = _mealPlanService.MapToMealPlanDictionaryAsync(mealPlan.MealPlanDictionary, mealPlan.PersonCount).Result;
        //    var ingredients = dictionary.SelectMany(x => x.Value).SelectMany(x => x.Ingredients).ToList();
        //    return PartialView("~/Views/MealPlaner/_shoppingListPartialView.cshtml", ingredients);
        //}

        [AllowAnonymous]
        [HttpGet("/ShoppingList/{token?}", Name = "ShoppingList")]
        [HttpGet("/CreateNewMealPlan/LoadShoppingList")]
        public IActionResult LoadShoppingList(string? token)
        {
            string shoppingList = "";

            if (!string.IsNullOrWhiteSpace(token))
            {
                var list = _context.SavedMealPlan.FirstOrDefault(x => x.Token == token);
                if (list == null) return NotFound();
                shoppingList = list.ShoppingList;
            }
            else if (User?.Identity?.IsAuthenticated == true)
            {
                var mealPlan = GetSavedMealPlanFromDb();
                shoppingList = mealPlan?.ShoppingList ?? "";
            }
            else
            {
                return NotFound();
            }

            return View("~/Views/MealPlaner/ShoppingList.cshtml", shoppingList);
        }


        [Authorize]
        public IActionResult CreateNewShoppingList([FromBody] List<string> listToParse)
        {
            var mealPlan = GetSavedMealPlanFromDb();
            if (mealPlan == null)
                return BadRequest("Kein MealPlan gefunden.");

            var cleanedList = new List<string>();

            foreach (var item in listToParse)
            {
                if (string.IsNullOrWhiteSpace(item))
                    continue;

                // Einzelne Werte trennen: "Name;Menge;Einheit;Kategorie"
                var parts = item.Split(';', StringSplitOptions.RemoveEmptyEntries)
                                 .Select(p => p.Trim())
                                 .ToArray();

                var name = parts.ElementAtOrDefault(0) ?? "";
                var category = parts.Length > 0 ? parts[^1] : "Sonstiges";

                // Wenn Kategorie "Gewürze" → nur Name + Kategorie speichern
                if (category.Equals("Gewürze", StringComparison.OrdinalIgnoreCase))
                {
                    if (parts[2] != "Bund")
                        cleanedList.Add($"{name};{category}");
                    else
                        cleanedList.Add(item);
                }
                else
                {
                    cleanedList.Add(item);
                }
            }

            mealPlan.ShoppingList = string.Join("|", cleanedList);
            _context.SavedMealPlan.Update(mealPlan);
            _context.SaveChanges();

            return Ok(mealPlan.Token);
        }
        [HttpGet]
        public async Task<IActionResult> LoadRecipeInMealPlaner(List<int> usedIds,string category)
        {
            
            var settings=GetPersonalMealPlanSettingsFromDb();
           
            var model = await _mealPlanEditorService.GetOrChangeRecipe(category, settings,usedIds.ToList());

            return Ok(model);
        }

    }

    


}
