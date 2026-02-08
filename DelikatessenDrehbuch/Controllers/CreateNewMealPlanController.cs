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

        private readonly ApplicationDbContext _context;

        private readonly IRecipesService _recipesService;
        private readonly IMealPlanService _mealPlanService;
        private readonly ISessionService _sessionService;
        private readonly IMealPlanEditorService _mealPlanEditorService;
        private readonly ISearchRecipeService _searchRecipeService;



        public CreateNewMealPlanController(IRecipesService recipesService, IMealPlanService mealPlanService,
                                           ApplicationDbContext context, ISearchRecipeService searchRecipeService,
                                           ISessionService sessionService,
                                           IMealPlanEditorService mealPlanEditorService)
        {

            _recipesService = recipesService;
            _mealPlanService = mealPlanService;
            _context = context;
            _sessionService = sessionService;
            _mealPlanEditorService = mealPlanEditorService;
            _searchRecipeService = searchRecipeService;

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
            await _mealPlanService.CreateSavedMealPlanAsync(User.Identity.Name, settings, dict);



            return View("~/Views/MealPlaner/CreateNewMealPlan.cshtml", model);
        }



        private PersonalMealPlanSettings GetPersonalMealPlanSettingsFromDb()
        {
            var catche = _context.SavedMealPlan.FirstOrDefault(x => x.UserMail == User.Identity.Name);
            PersonalMealPlanSettings? perso = new();
            if (catche != null)
            {
                if (!string.IsNullOrEmpty(catche.UserSettingJson))
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
                HaveCatchContent = perso.PersonCount == 0 ? false : true
            };
            return View("~/Views/MealPlaner/MealPlanerUserSettings.cshtml", model);
        }

        public class MealPlanHelper
        {
            public int Index { get; set; }      // Der Tag (1, 2, 3...)
            public int RecipeId { get; set; }   // Die ID des gewählten Rezepts

        }
        [HttpGet]
        public async Task<IActionResult> CreateMealPlanAsync(string data, int personCount)
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
            return View("~/Views/MealPlaner/CreatedMealPlan.cshtml", model);
        }

        public async Task<IActionResult> LoadMealPlanRecipesPartialView(int id, int personCount)
        {
            ViewData["PersonCount"] = personCount;
            var model = await _recipesService.GetRecipesFromDbByIdAsync(id);

            return PartialView("~/Views/MealPlaner/_mealPlanerRecipesPartialView.cshtml", model);
        }

        public async Task<IActionResult> GetRecipesByQueryAsync(string query, string dayIndex, string category)
        {
            var ids = GetFiltretRecipesIds(category);

            ViewBag.DayIndex = dayIndex;
            var recipes = await _searchRecipeService.GetRecipesByQuery(query);
            var model = recipes.Where(r => ids.Contains(r.Id)).ToList();



            return PartialView("~/Views/MealPlaner/_SearchRecipe.cshtml", model);
        }

        public IActionResult LoadSearchImput(string category, string dayIndex)
        {
            ViewBag.DayIndex = dayIndex;
            ViewBag.Category = category;
            return PartialView("~/Views/MealPlaner/_SearchImput.cshtml");
        }

        private List<int> GetFiltretRecipesIds(string category)
        {
            var includeIds = new List<int>();
            var excludeIds = new List<int>();
            var ids = StaticData.GetRecipesByCategory(category);
            var settings = GetPersonalMealPlanSettingsFromDb();

            if (settings.Vegan)
                includeIds.AddRange(StaticData.VeganRecipeIds);
            else if (settings.Vegetarisch)
                includeIds.AddRange(StaticData.VegetarianRecipeIds);
            else if (settings.NoSchweinefleisch)
                excludeIds.AddRange(StaticData.PorkRecipeIds);
            else if (settings.NoFisch)
                excludeIds.AddRange(StaticData.FishRecipeIds);
            else if (settings.NoVegan)
                excludeIds.AddRange(StaticData.VeganRecipeIds);
            else if (settings.NoVegetarisch)
                excludeIds.AddRange(StaticData.VegetarianRecipeIds);

            if (includeIds.Any())
                ids = ids.Intersect(includeIds).ToList();
            else
                ids = ids.Except(excludeIds).ToList();

            return ids.ToList();
        }

        public async Task<IActionResult> LoadRecipesSearch(string category, string dayIndex)
        {
            ViewBag.DayIndex = dayIndex;
            ViewBag.Category = category;
            var ids = GetFiltretRecipesIds(category);
            var recipesIds = _recipesService.GetRendomRecipesIds(ids.ToList(), 10);

            var model = await _recipesService.GetRecipesListByIdsAsync(recipesIds);

            return PartialView("~/Views/MealPlaner/_SearchRecipe.cshtml", model);
        }


        public SavedMealPlans GetSavedMealPlanFromDb()
        {
            var savedMealPlan = _context.SavedMealPlan.Where(x => x.UserMail == User.Identity.Name).First();
            savedMealPlan.MealPlanDictionary = JsonSerializer.Deserialize<Dictionary<int, List<int>>>(savedMealPlan.MealPlanJson, new JsonSerializerOptions());


            return savedMealPlan;
        }



        public async Task<IActionResult> LoadLastMealPlanAsync()
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
                    plan.Recipes.ImagePath = FrontendFunctions.GetSmallImagePath(plan.Recipes.ImagePath);
                    model.Add(plan);
                }
            }
            return View("~/Views/MealPlaner/CreateNewMealPlan.cshtml", model);
        }



        [HttpGet]
        public async Task<IActionResult> LoadRecipeInMealPlaner(List<int> usedIds, string category)
        {

            var settings = GetPersonalMealPlanSettingsFromDb();

            var model = await _mealPlanEditorService.GetOrChangeRecipe(category, settings, usedIds.ToList());
            model.ImagePath = FrontendFunctions.GetSmallImagePath(model.ImagePath);

            return Ok(model);
        }

        public async Task<IActionResult> SaveMealPlanInDb([FromBody] Dictionary<int, List<int>> indexAndIds)
        {
            var mealPlan = GetSavedMealPlanFromDb();
            string jsonString = JsonSerializer.Serialize(indexAndIds);
            mealPlan.MealPlanJson = jsonString;
            _context.SaveChanges();
            return Ok(new { success = true, message = "Gespeichert" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteSavedMealPlan()
        {
            var savedMealPlan = _context.SavedMealPlan.FirstOrDefault(x => x.UserMail == User.Identity.Name);
            if (savedMealPlan != null)
            {
                _context.SavedMealPlan.Remove(savedMealPlan);
                _context.SaveChanges();
            }

            return RedirectToAction("MealPlanSetting");
        }

    }




}
