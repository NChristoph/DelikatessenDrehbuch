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
        public async Task<ActionResult> IndexAsync(bool vegan, bool vegetarisch,
                                  bool cookingTimeOne, bool cookingTimeTwo,
                                  int personCount, int dayCount,
                                  string ingredientIds)
        {
            if (dayCount > 7)
            {
                return Ok("Maximal 7 Tage erlaubt");
            }

            ViewData["PersonCount"] = personCount;
            if (!string.IsNullOrEmpty(ingredientIds))
            {
                var ingredientIdsArray = _utilityService.SplitToArrayBySeperator(ingredientIds, ',');
            }

            IQueryable<QueryHandler> queryHandlers = _context.QueryHandler;

            // Ernährungs-Filter
            if (vegan && !vegetarisch)
            {
                queryHandlers = queryHandlers.Where(r => r.Query.Query.ToLower() == "vegan");
            }
            else if (!vegan && vegetarisch)
            {
                queryHandlers = queryHandlers.Where(r => r.Query.Query.ToLower() == "vegetarisch");
            }
            else if (vegan && vegetarisch)
            {
                // Wenn beides angehakt ist: vegan ODER vegetarisch zulassen
                queryHandlers = queryHandlers.Where(r => r.Query.Query.ToLower() == "vegan"
                                                  || r.Query.Query.ToLower() == "vegetarisch");
            }

            if (cookingTimeOne && !cookingTimeTwo)
            {
                queryHandlers = queryHandlers.Where(r => r.Recipe.PreparationTime <= 40);
            }
            else if (!cookingTimeOne && cookingTimeTwo)
            {
                queryHandlers = queryHandlers.Where(r => r.Recipe.PreparationTime > 40);
            }
            else if (cookingTimeOne && cookingTimeTwo)
            {
                queryHandlers = queryHandlers.Where(r => r.Recipe.PreparationTime < 1);
            }
            queryHandlers = queryHandlers.Where(x => x.Recipe.Category == "Hauptspeise");

            var recipesIds = queryHandlers.Select(x => x.Recipe.Id).ToHashSet();


            _sessionService.ClearSession();
            var model = await _mealPlanService.GetPersonalMealPlanModelListByIds(recipesIds.ToList(), dayCount);
            _sessionService.SavePersonalMealPlanToSession(model);
            model = model.Where(x => x.Recipes.Category == "Hauptspeise").ToList();

            return View("~/Views/MyRecipes/CreateNewMealPlan.cshtml", model);
        }



        public IActionResult GetNameAndDescription(string name, string description)
        {
            string[] model = new[] { name, description };

            return PartialView("~/Views/MyRecipes/_nameAndDescriptionPartialMealPlaner.cshtml", model);
        }

        public IActionResult CreatedMealPlan(string indexAndIds,int personCount)
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
                var model = recipesFromSession.Where(x=>x.Id==int.Parse(recipesIds)).SelectMany(x => x.Ingredients)
                                              .ToList();
                return PartialView("~/Views/MyRecipes/_createMealPlanIngredientPartialView.cshtml", model);

            }





        }

        public async Task<IActionResult> LoadRecipesPartialViewAsync(int recipeId, string index)
        {
            ViewData["Index"] = int.Parse(index);

            var recipes = _sessionService.GetPersonalMealPlanFromSession();
            recipes.RemoveAll(x => x.Id == recipeId);

            var recipesIds = _sessionService.GetRecipesIdsFromSession();
            recipesIds.RemoveAll(x => x == recipeId);

            var randomRecipeId = _utilityService.GetRandomIntFromList(recipesIds);
            var newMealModel = await _mealPlanService.CreatePersonalMealPlanRecipeModelByIdAsync(randomRecipeId);

            recipes.Add(newMealModel);
            _sessionService.SavePersonalMealPlanToSession(recipes);

            recipesIds.RemoveAll(x => x == randomRecipeId);
            _sessionService.SaveRecipesIdToSession(recipesIds);




            return PartialView("~/Views/MyRecipes/_createMealPlanRecipesPartialView.cshtml", newMealModel.Recipes);
        }

        //TODO das session und so weiter auslagern in extra metode und navh kategory filtern geht noch nicht

        public async Task<IActionResult> LoadAppetizerOrDessertPartialViewAsync(string category, string index)
        {
            ViewData["Index"] = int.Parse(index);
            var recipes = _sessionService.GetPersonalMealPlanFromSession();
            var recipesIds = _sessionService.GetRecipesIdsFromSession();
            var randomRecipeId = _utilityService.GetRandomIntFromList(recipesIds);
            var newMealModel = await _mealPlanService.CreatePersonalMealPlanRecipeModelByIdAsync(randomRecipeId);
            recipes.Add(newMealModel);
            _sessionService.SavePersonalMealPlanToSession(recipes);

            recipesIds.RemoveAll(x => x == newMealModel.Recipes.Id);
            _sessionService.SaveRecipesIdToSession(recipesIds);



            return PartialView("~/Views/MyRecipes/_createMealPlanRecipesPartialView.cshtml", newMealModel.Recipes);
        }


    }
}
