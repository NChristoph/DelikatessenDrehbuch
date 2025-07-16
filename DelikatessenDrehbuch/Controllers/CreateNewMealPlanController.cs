using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Linq;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace DelikatessenDrehbuch.Controllers
{
    public class CreateNewMealPlanController : Controller
    {

        private readonly ApplicationDbContext _context;
        private readonly HelpfulMethods _helpfulMethods;
        private int[] RecipesId { get; set; } = new int[6];

        public CreateNewMealPlanController(ApplicationDbContext applicationDbContext, HelpfulMethods helpfulMethods)
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

        public IActionResult GetNameAndDescription(string name,string description)
        {
            string[] model = new[] { name, description };

            return PartialView("~/Views/MyRecipes/_nameAndDescriptionPartialMealPlaner.cshtml",model);
        }

        public IActionResult CreatedMealPlan(string ids)
        {
           
            var idList = ids.Split(";").Select(x => int.Parse(x)).ToList();
            var model = _context.Recipes.Where(x => idList.Contains(x.Id)).ToList();

            return View("~/Views/MyRecipes/CreatedMealPlan.cshtml",model);
        }
       
        public ActionResult CreateNewMealPlan(List<string> queryList, int dayCount)
        {
            if (dayCount > 7)
            {
                return Ok("Maximal 7 Tage erlaubt"); 
            }

            var mealplan = GetMealplanList(queryList,dayCount);

            return View("~/Views/MyRecipes/CreateNewMealPlan.cshtml", mealplan);
        }

        private List<string> CleanQueryList(List<string> queryList)
        {
            List<string> cleanedList = queryList.Where(q => !string.IsNullOrWhiteSpace(q))
                                                .Select(q => q.Trim().ToLower())
                                                .ToList();

            return cleanedList;
        }

        private MealModel GetMealplanList(List<string> queryList, int dayCount)
        {
           
            MealModel mealPlan = new();
            var random = new Random();
            var cleanedList = CleanQueryList(queryList);
            var recipesFromDbIds = _context.QueryHandler.Where(x => cleanedList.Contains(x.Query.Query.ToLower()) && x.Recipe.Category == "Hauptspeise")
                                                         .Select(x => x.Recipe.Id).ToList();

            var recipeIds = recipesFromDbIds.OrderBy(x => random.Next()).Take(dayCount).ToList();

            HttpContext.Session.SetString("RecipesFromDbIds", string.Join(";", recipesFromDbIds));

            mealPlan.Recipes = _context.Recipes.Where(x => recipeIds.Contains(x.Id)).ToList();

            mealPlan.Ingredients = _helpfulMethods.GetIngredientsByRecipesIdsList(_context, recipeIds);


            return mealPlan;
        }

        public IActionResult LoadIngredientPartialView(string recipesIds)
        {
            var idsToList = recipesIds.Split(';', StringSplitOptions.RemoveEmptyEntries)
                                      .Select(id => int.Parse(id))
                                      .ToList();

            var ingredientHandlerFromDb = _helpfulMethods.GetIngredientsByRecipesIdsList(_context, idsToList);

            return PartialView("~/Views/MyRecipes/_createMealPlanIngredientPartialView.cshtml", ingredientHandlerFromDb);
        }

        //TODO: Ordentlicher machen

        private List<int> GetMatchingRecipesIds(List<int> recipesIds, int recipeId)
        {
            var idsString = HttpContext.Session.GetString("RecipesFromDbIds")?.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse);
           
            var matchingRecipes = _context.Recipes.Where(x =>
                                  idsString.Contains(x.Id) &&
                                  x.Id != recipeId &&
                                  !recipesIds.Contains(x.Id))
                                  .Select(x => x.Id).ToList();

            return matchingRecipes;
        }

       

        public IActionResult LoadRecipesPartialView(string recipesIds, int recipeId,string index)
        {
            var random = new Random();
            ViewData["Index"] = int.Parse(index);
           var recipesToChange = recipesIds.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();

            var matchingRecipes = GetMatchingRecipesIds(recipesToChange, recipeId);

            var Id = matchingRecipes
                .OrderBy(x => random.Next())
                .Take(1).FirstOrDefault();

            var model= _context.Recipes.Single(x=>x.Id == Id);

            return PartialView("~/Views/MyRecipes/_createMealPlanRecipesPartialView.cshtml", model);
        }

        public IActionResult LoadAppetizerOrDessertPartialView(string category,string index)
        {
            var random = new Random();
            ViewData["Index"] = int.Parse(index);
            var matchingRecipes = _context.Recipes.Where(x=>x.Category.ToLower().Trim() == category.ToLower().Trim())
                                                  .Select(x=>x.Id).ToList();

            var randomId=matchingRecipes.OrderBy(x => random.Next()).Take(1).FirstOrDefault();

            var model =_context.Recipes.Single(x=>x.Id==randomId);



            return PartialView("~/Views/MyRecipes/_createMealPlanRecipesPartialView.cshtml",model);
        }


    }
}
