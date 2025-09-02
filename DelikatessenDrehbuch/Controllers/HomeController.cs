using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Services.Interfaces;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace DelikatessenDrehbuch.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IMemoryCache _cache;
        private readonly ApplicationDbContext _context;
        private readonly HelpfulMethods _helpfulMethods;

        private readonly IRecipesService _recipesService;
        private readonly IIngredientService _ingredientService;
        private readonly IUtilityService _utilityService;

        private readonly List<string> _importantKeyWordsList;
        private readonly List<string> _importantKeyWordsListToLower;


        public HomeController(ILogger<HomeController> logger, ApplicationDbContext dbContext,
                              HelpfulMethods helpfulMethods, IMemoryCache cache,
                              IRecipesService recipesService, IIngredientService ingredientService,
                              IUtilityService utilityService)
        {
            _logger = logger;
            _context = dbContext;
            _helpfulMethods = helpfulMethods;
            _cache = cache;
            _recipesService = recipesService;
            _importantKeyWordsList = _helpfulMethods.GetQueryListFromDb(_context);
            _importantKeyWordsListToLower = _importantKeyWordsList.Select(x => x.ToLower().Trim()).ToList();
            _ingredientService = ingredientService;
            _utilityService = utilityService;
        }

        public IActionResult GetRecipesPartialView(List<int> Ids = null)
        {

            List<Recipes> model = new();
            var recipeIdsFromDb = _context.Recipes
                .Where(x => Ids == null || !Ids.Contains(x.Id))
                .Select(x => x.Id)
                .ToList();

            var randomRecipeIds = _recipesService.GetRendomRecipesIdsByCountFromIdList(recipeIdsFromDb, 25);


            model = _context.Recipes.Where(x => randomRecipeIds.Contains(x.Id)).ToList();

            return PartialView("_recipesPartialView", model);


        }

        //ToDo:Das mit den durchschnittlichen gewicht geht noch nicht

        private void CreatePerson()
        {
            var allRecipes = _context.Recipes.ToList();
            foreach (var recipe in allRecipes)
            {
                var ingredient = _context.RecipesHandlers.Where(x => x.Recipe.Id == recipe.Id)
                                                         .Include(w => w.IngredientHandler.Quantity)
                                                         .Include(x=>x.IngredientHandler.Ingredient)
                                                         .Include(x=>x.IngredientHandler.Measure)
                                                         .Select(x => x.IngredientHandler).ToList();

                var number = 0.0;
                foreach (var i in ingredient)
                {
                    if (i.Measure.UnitOfMeasurement == "Stk.")
                        if (i.Ingredient.AverageWeight != null)
                            number += i.Ingredient.AverageWeight.Value;
                }
                var totalIngredientWeight = ingredient.Where(x => x.Quantity != null)
                                            .Sum(x => x.Quantity.Quantitys);

                var totalWeight = number + totalIngredientWeight;


                if (recipe.Category == "Dessert")
                {
                    const double Dessert = 175;
                    recipe.RecipePersonCount = (int)Math.Round(totalWeight / Dessert, MidpointRounding.AwayFromZero);
                    _context.SaveChanges();

                }
                if (recipe.Category == "Vorspeise")
                {
                    const double Vorspeise = 250;
                    recipe.RecipePersonCount = (int)Math.Round(totalWeight / Vorspeise, MidpointRounding.AwayFromZero);
                    _context.SaveChanges();

                }
                if (recipe.Category == "Frühstück")
                {
                    const double Vorspeise = 300;
                    recipe.RecipePersonCount = (int)Math.Round(totalWeight / Vorspeise, MidpointRounding.AwayFromZero);
                    _context.SaveChanges();

                }
                else
                {
                    const double Hauptspeise = 450;
                    var recipesCount = (int)Math.Round(totalWeight / Hauptspeise, MidpointRounding.AwayFromZero);
                    recipe.RecipePersonCount = recipesCount == 0 ? 1 : recipesCount;
                    _context.SaveChanges();

                }


            }

        }
        public IActionResult Index()
        {
            
            return View();
            
        }

        private async Task<List<Recipes>> GetRecipesByName(string query)
        {

            var recipesFromDbByName = _context.Recipes.Where(x => EF.Functions.Like(x.Name, $"%{query}%"))
                                              .AsNoTracking()
                                              .AsEnumerable()
                                              .Where(x => Regex.IsMatch(x.Name, @"\b" + Regex.Escape(query) + @"\b", RegexOptions.IgnoreCase)
                                                       || Regex.Match(x.Name, query, RegexOptions.IgnoreCase).Success)
                                              .ToList();

            var splittQueryArray = _utilityService.SplitToArrayBySeperator(query, ' ');


            var recipesFromDbByQuery = await _context.QueryHandler.Where(x => splittQueryArray.Any(t => t == x.Query.Query.ToLower()))
                                              .AsNoTracking()
                                              .Select(x => x.Recipe).ToListAsync();


            var recipesFromDbByIngredient = await _context.RecipesHandlers.Where(x => splittQueryArray.Any(t => t == x.IngredientHandler.Ingredient.Name.ToLower()))
                                              .AsNoTracking()
                                              .Select(x => x.Recipe).ToListAsync();


            var combined = recipesFromDbByName.Concat(recipesFromDbByQuery).Concat(recipesFromDbByIngredient)
                                        .GroupBy(r => r.Id)
                                        .Select(g => g.First())
                                        .ToList();

            return combined;
        }
        public async Task<IActionResult> SearchRecipes(string query)
        {
            List<Recipes> model = new();
            var filterList = _utilityService.GetListFromQueryString(query);


            model = await GetRecipesByName(query.Trim().ToLower());
            return PartialView("_recipesPartialView", model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Impressum()
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