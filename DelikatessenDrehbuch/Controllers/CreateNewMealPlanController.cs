using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
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


        public CreateNewMealPlanController(IRecipesService recipesService, IMealPlanService mealPlanService,
                                           IIngredientService ingredientService, IUtilityService utilityService,
                                           ApplicationDbContext context, IHttpContextAccessor httpContext,
                                           ISessionService sessionService)
        {

            _recipesService = recipesService;
            _mealPlanService = mealPlanService;
            _ingredientService = ingredientService;
            _utilityService = utilityService;
            _httpContext = httpContext;
            _context = context;
            _sessionService = sessionService;
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

            return View("~/Views/MyRecipes/CreateNewMealPlan.cshtml", model);
        }



        public IActionResult GetNameAndDescription(string name, string description)
        {
            string[] model = new[] { name, description };

            return PartialView("~/Views/MyRecipes/_nameAndDescriptionPartialMealPlaner.cshtml", model);
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

        public async Task<IActionResult> LoadRecipesPartialViewAsync(int recipeId, string index)
        {
            ViewData["Index"] = int.Parse(index);

            var model = await _mealPlanService.GetMealPlanModels("Hauptspeise", 1);
            var recipe = model.First().Recipes;

            EditeSession(model.First(), recipeId);

            return PartialView("~/Views/MyRecipes/_createMealPlanRecipesPartialView.cshtml", recipe);
        }

        private void EditeSession(PersonalMealPlanRecipeModel model,int idToRemove)
        {
            var session = _sessionService.GetPersonalMealPlanFromSession();
            session.RemoveAll(x => x.Id == idToRemove);

            session.Add(model);
            _sessionService.SavePersonalMealPlanToSession(session);
        }

        public async Task<IActionResult> LoadAppetizerOrDessertPartialViewAsync(string category, string index)
        {
            ViewData["Index"] = int.Parse(index);
            var model = await _mealPlanService.GetMealPlanModels(category, 1);
            var recipe=model.First().Recipes;
            EditeSession(model.First(), 0);
            return PartialView("~/Views/MyRecipes/_createMealPlanRecipesPartialView.cshtml",recipe);
        }


    }
}
