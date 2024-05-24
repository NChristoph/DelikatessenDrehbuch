using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using System.Diagnostics;
using System.Reflection;

namespace DelikatessenDrehbuch.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;
        public HomeController(ILogger<HomeController> logger, ApplicationDbContext dbContext)
        {
            _logger = logger;
            _context = dbContext;
        }

        private FilterClass CreateFilterClass(RecipeType recipeType)
        {
            FilterClass filterClass = new FilterClass()
            {
                Vegan = recipeType.Vegan,
                Vegetarian = recipeType.Vegetarian,
                LowCarb = recipeType.LowCarb,
                BBQ = recipeType.BBQ,
                Pastry = recipeType.Pastry,
                Bread = recipeType.Bread,
                Cake = recipeType.Cake,
                Biscuits = recipeType.Biscuits,
                Cocktails = recipeType.Cocktails,
                Pie = recipeType.Pie,
                Diet = recipeType.Diet,
                
            };

            filterClass.ListToCompare = new()
            {
                filterClass.Vegan,
                filterClass.Vegetarian,
                filterClass.LowCarb,
                filterClass.BBQ,
                filterClass.Pastry,
                filterClass.Bread,
                filterClass.Cake,
                filterClass.Biscuits,
                filterClass.Cocktails,
                filterClass.Pie,
                filterClass.Diet,

            };

            return filterClass;
        }

        private List<Recipes> FilterRecipes(List<RecipeType> recipeTypes, RecipeType recipeType)
        {
            var filter = CreateFilterClass(recipeType);
            List<Recipes> filtered = new List<Recipes>();   
           
            foreach (var recipe in recipeTypes)
            {
                var dataFromDb = CreateFilterClass(recipe);
                bool equal = filter.ListToCompare.SequenceEqual(dataFromDb.ListToCompare);
                if (equal)
                    filtered.Add(recipe.Recipes);

            }

            return filtered;
        }


        public IActionResult Index(RecipeType recipeType)
        {
            List<Recipes> recipes = new List<Recipes>();

            if (recipeType.Recipes == null)
            {
                var random = new Random();
                recipes = _context.Recipes.ToList();
                recipes.OrderBy(x => random.Next()).Take(15).ToList();

            }
            else
            {
                recipes = FilterRecipes( _context.RecipeType.Where(x => x.Recipes.Name.Contains(recipeType.Recipes.Name.ToLower()))
                                                            .Include(x=>x.Recipes).ToList(),recipeType);
              
            }


            return View(recipes);

        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}