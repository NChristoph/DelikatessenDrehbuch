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
using Newtonsoft.Json;
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
using JsonSerializer = System.Text.Json.JsonSerializer;

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

            var dict = model.ToDictionary(
               m => m.Index,
               m => new List<int> { m.Recipes.Id }
           );
            await _mealPlanService.CreateSavedMealPlanAsync(User.Identity.Name,settings,dict);
          

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

            ModelForMealPlanSettingView model = new()
            {
                Ingredients = _context.Ingredients.ToList(),
                PersonalMealPlanSettings = perso,
                HaveCatchContent = perso != null
            };
            return View("~/Views/MealPlaner/MealPlanerUserSettings.cshtml", model);
        }

        public class MealPlanHelper
        {
            public int Index { get; set; }      // Der Tag (1, 2, 3...)
            public int RecipeId { get; set; }   // Die ID des gewählten Rezepts
           
        }
        [HttpGet]
        public async Task<IActionResult> CreateMealPlanAsync(string data,int personCount)
        {
           
            ViewData["PersonCount"] = personCount;
            var indexIds = JsonConvert.DeserializeObject<List<MealPlanHelper>>(data);
            List<MealPlanerModel> model = new();

            foreach (var item in indexIds)
            {
                MealPlanerModel plan = new()
                {
                    Index = item.Index,
                    Recipes = await _recipesService.GetRecipesFromDbByIdAsync(item.RecipeId)
                };
                
                model.Add(plan);
            }
            return View("~/Views/MealPlaner/CreatedMealPlan.cshtml",model);
        }

        public async Task<IActionResult> LoadMealPlanRecipesPartialView(int id, int personCount)
        {
            ViewData["PersonCount"] = personCount;
            var model = await _recipesService.GetRecipesFromDbByIdAsync(id);

            return PartialView("~/Views/MealPlaner/_mealPlanerRecipesPartialView.cshtml", model);
        }

      


        public SavedMealPlans GetSavedMealPlanFromDb()
        {
            var savedMealPlan = _context.SavedMealPlan.Where(x => x.UserMail == User.Identity.Name).First();
            savedMealPlan.MealPlanDictionary = JsonSerializer.Deserialize<Dictionary<int, List<int>>>(savedMealPlan.MealPlanJson, new JsonSerializerOptions());


            return savedMealPlan;
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

        public async Task<IActionResult> LoadLastMealPlanAsync()
        {
            var mealPlan=GetSavedMealPlanFromDb();
            ViewData["PersonCount"] = mealPlan.PersonCount;
            var dict = mealPlan.MealPlanDictionary;
            List<MealPlanerModel> model = new();

            foreach(var key in dict.Keys)
            {
                var listInt = dict[key];
                foreach (var item in listInt)
                {
                    MealPlanerModel plan = new()
                    {
                        Index = key,
                        Recipes = await _recipesService.GetRecipesFromDbByIdAsync(item)
                    };
                    plan.Recipes.ImagePath = FrontendFunctions.GetSmallImagePath(plan.Recipes.ImagePath);
                    model.Add(plan);
                }
            }
           
            return View("~/Views/MealPlaner/CreatedMealPlan.cshtml", model);
            
        }

        public async Task<IActionResult> LoadMealPlanForEditAsync()
        {
            var mealPlan = GetSavedMealPlanFromDb();
            ViewData["PersonCount"] = mealPlan.PersonCount;
            var dict = mealPlan.MealPlanDictionary;
            List<MealPlanerModel> model = new();
            foreach (var key in dict.Keys)
            {
                var listInt = dict[key];
                foreach (var item in listInt)
                {
                    MealPlanerModel plan = new()
                    {
                        Index = key,
                        Recipes = await _recipesService.GetRecipesFromDbByIdAsync(item)
                        
                    };
                    plan.Recipes.ImagePath=FrontendFunctions.GetSmallImagePath(plan.Recipes.ImagePath);
                    model.Add(plan);
                }
            }
            return View("~/Views/MealPlaner/CreateNewMealPlan.cshtml", model);
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
            model.ImagePath=FrontendFunctions.GetSmallImagePath(model.ImagePath);

            return Ok(model);
        }

        public async Task<IActionResult> SaveMealPlanInDb([FromBody]Dictionary<int,List<int>> indexAndIds)
        {
            var mealPlan = GetSavedMealPlanFromDb();
            string jsonString = JsonSerializer.Serialize(indexAndIds);
            mealPlan.MealPlanJson= jsonString;
            _context.SaveChanges();
            return Ok(new { success = true, message = "Gespeichert" });
        }

    }

    


}
