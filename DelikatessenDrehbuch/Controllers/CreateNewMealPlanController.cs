using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Linq;

namespace DelikatessenDrehbuch.Controllers
{
    public class CreateNewMealPlanController : Controller
    {

        private readonly ApplicationDbContext _context;
        private readonly HelpfulMethods _helpfulMethods;
        private List<Recipes> Melplan { get; set; } = new List<Recipes>();

        public CreateNewMealPlanController(ApplicationDbContext applicationDbContext,HelpfulMethods helpfulMethods)
        {

            _context = applicationDbContext;
            _helpfulMethods = helpfulMethods;
        }
        [Authorize]
        public ActionResult Index()
        {

            var querylist = _context.MealplanFilter.Select(x => x.Filter).ToList();

            return View(querylist);
        }


        //TODO noch filter einbauen gibt aktuerll alle rezepte aus
        public ActionResult CreateNewMealPlan(List<string> queryList)
        {
            var mealplan = GetMealplanList(queryList);

            return View("~/Views/MyRecipes/CreateNewMealPlan.cshtml", mealplan);
        }

        private List<string> CleanQueryList(List<string> queryList)
        {
            List<string> cleanedList = queryList.Where(q => !string.IsNullOrWhiteSpace(q))
                                                .Select(q => q.Trim().ToLower())
                                                .ToList();

            return cleanedList;
        }

        private MealModel GetMealplanList(List<string> queryList)
        {
            MealModel mealPlan = new();
            var random = new Random();
            var cleanedList = CleanQueryList(queryList);
            var recipesFromDbIds = _context.QueryHandler.Where(x => cleanedList.Contains(x.Query.Query.ToLower()) && x.Recipe.Category == "Hauptspeise")
                                                         .Select(x => x.Recipe.Id).ToList();

            var recipeIds = recipesFromDbIds.OrderBy(x => random.Next()).Take(7).ToList();

            mealPlan.Recipes=_context.Recipes.Where(x=>recipeIds.Contains(x.Id)).ToList();

            mealPlan.Ingredients = _helpfulMethods.GetIngredientsByRecipesIdsList(_context, recipeIds);


            return mealPlan;
        }

        public IActionResult LoadIngredientPartialView(string recipesIds)
        {
            var idsToList = recipesIds.Split(';', StringSplitOptions.RemoveEmptyEntries)
                                      .Select(id => int.Parse(id))
                                      .ToList();

            var ingredientHandlerFromDb= _helpfulMethods.GetIngredientsByRecipesIdsList(_context, idsToList);

            return PartialView("~/Views/MyRecipes/_createMealPlanIngredientPartialView.cshtml", ingredientHandlerFromDb);
        }

        //TODO: Rezept QAndern einfügen
        public IActionResult LoadRecipesPartialView(string recipesIds)
        {
            return PartialView("~/Views/MyRecipes/_createMealPlanRecipesPartialView.cshtml");
        }


    }
}
