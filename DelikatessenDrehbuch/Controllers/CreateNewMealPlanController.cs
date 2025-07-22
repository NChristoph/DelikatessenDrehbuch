using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Services.Interfaces;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Linq;
using System.Text.Json;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace DelikatessenDrehbuch.Controllers
{
    [Authorize]
    public class CreateNewMealPlanController : Controller
    {

        
        private readonly IRecipesService _recipesService;
        private readonly IMealPlanService _mealPlanService;
        private readonly IIngredientService _ingredientService;
    

        public CreateNewMealPlanController(IRecipesService recipesService, IMealPlanService mealPlanService,IIngredientService ingredientService)
        {

            _recipesService = recipesService;
            _mealPlanService = mealPlanService;
            _ingredientService = ingredientService;
        }
        [Authorize]
        public ActionResult Index()
        {

            var querylist = _mealPlanService.GetMealPlanFilter();

            return View(querylist);
        }

        public IActionResult GetNameAndDescription(string name, string description)
        {
            string[] model = new[] { name, description };

            return PartialView("~/Views/MyRecipes/_nameAndDescriptionPartialMealPlaner.cshtml", model);
        }

        public IActionResult CreatedMealPlan(string indexAndIds)
        {
            Dictionary<int, List<Recipes>> model = _mealPlanService.MapToMealPlanDictionary(indexAndIds);

            return View("~/Views/MyRecipes/CreatedMealPlan.cshtml", model);
        }

        public ActionResult CreateNewMealPlan(List<string> queryList, int dayCount)
        {
            if (dayCount > 7)
            {
                return Ok("Maximal 7 Tage erlaubt");
            }

            var mealplan = _mealPlanService.GenerateMealPlan(queryList, dayCount);

            return View("~/Views/MyRecipes/CreateNewMealPlan.cshtml", mealplan);
        }

        public IActionResult LoadIngredientPartialView(string recipesIds)
        {
            var idsToList = _mealPlanService.GetIntListByString(recipesIds);
            var model = _ingredientService.GetIngredientsByRecipesIdsList(idsToList);

            return PartialView("~/Views/MyRecipes/_createMealPlanIngredientPartialView.cshtml",model);
        }


        public IActionResult LoadRecipesPartialView(string recipesIds, int recipeId, string index)
        {
            ViewData["Index"] = int.Parse(index);

            var recipesToChange = _mealPlanService.GetIntListByString(recipesIds);
            var matchingRecipes = _mealPlanService.GetAlternativeRecipes(recipesToChange, recipeId);

            var model=_recipesService.GetOneRendomRecipeFromIdList(matchingRecipes);
            

            return PartialView("~/Views/MyRecipes/_createMealPlanRecipesPartialView.cshtml", model);
        }

        public IActionResult LoadAppetizerOrDessertPartialView(string category, string index)
        {
            ViewData["Index"] = int.Parse(index);

            var matchingRecipes =_recipesService.GetRecipesIdsByCategory(category);
            var model = _recipesService.GetOneRendomRecipeFromIdList(matchingRecipes);

            return PartialView("~/Views/MyRecipes/_createMealPlanRecipesPartialView.cshtml", model);
        }


    }
}
