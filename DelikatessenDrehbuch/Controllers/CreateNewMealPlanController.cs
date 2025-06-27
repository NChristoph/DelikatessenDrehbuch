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

        private List<Recipes> Melplan { get; set; } = new List<Recipes>();

        public CreateNewMealPlanController(ApplicationDbContext applicationDbContext)
        {

            _context = applicationDbContext;

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

        private List<Recipes> GetMealplanList(List<string> queryList)
        {
            List<Recipes> mealPlan = new();
            var random = new Random();
            var cleanedList = CleanQueryList(queryList);
            var recipesFromDbIds = _context.QueryHandler.Where(x => cleanedList.Contains(x.Query.Query.ToLower()) && x.Recipe.Category == "Hauptspeise")
                                                         .Select(x => x.Recipe.Id).ToList();

            var recipeIds = recipesFromDbIds.OrderBy(x => random.Next()).Take(7).ToList();

            mealPlan=_context.Recipes.Where(x=>recipeIds.Contains(x.Id)).ToList();


            return mealPlan;
        }


    }
}
