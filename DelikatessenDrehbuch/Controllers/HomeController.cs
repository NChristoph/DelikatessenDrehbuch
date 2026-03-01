using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace DelikatessenDrehbuch.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _dbContext;
        public HomeController(ILogger<HomeController> logger,ApplicationDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult SearchRecipes(string keyWord)
        {
            var recipesFromDb = _dbContext.Recipes
                .Where(x => x.Name.ToLower().Contains(keyWord.ToLower()))
                .ToList();

            return PartialView("_FindRecipesPartialView", recipesFromDb);
        }

        public IActionResult GetIngredients(string search)
        {
            var ingredients = _dbContext.Ingredients
                .Where(x => string.IsNullOrEmpty(search) || x.Name.ToLower().Contains(search.ToLower()))
                .OrderBy(x => x.Name)
                .Select(x => new { id = x.Id, name = x.Name })
                .Take(15)
                .ToList();

            return Json(ingredients);
        }

        public IActionResult FindDishByIngredients(List<int> ingredientIds)
        {
            if (ingredientIds == null || !ingredientIds.Any())
                return PartialView("_ScoredRecipesPartialView", new List<RecipeWithScore>());

            var recipeHandlers = _dbContext.RecipesHandlers
                .Include(x => x.Recipe)
                .Include(x => x.IngredientHandler)
                .Include(x => x.IngredientHandler.Ingredient)
                .ToList();

            var scoredRecipes = recipeHandlers
                .GroupBy(x => x.Recipe.Id)
                .Select(group =>
                {
                    var recipe = group.First().Recipe;
                    var recipeIngredientIds = group
                        .Where(x => x.IngredientHandler?.Ingredient != null)
                        .Select(x => x.IngredientHandler.Ingredient.Id)
                        .ToList();

                    int matching = recipeIngredientIds.Count(id => ingredientIds.Contains(id));
                    int total = recipeIngredientIds.Count;

                    return new RecipeWithScore
                    {
                        Recipe = recipe,
                        MatchingIngredients = matching,
                        TotalIngredients = total
                    };
                })
                .Where(x => x.MatchingIngredients > 0)
                .OrderByDescending(x => x.ScorePercent)
                .ThenByDescending(x => x.MatchingIngredients)
                .ToList();

            return PartialView("_ScoredRecipesPartialView", scoredRecipes);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}