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
        private readonly IIngredientScaleService _ingredientService;
        private readonly ISearchRecipeService _search;

        private readonly List<string> _importantKeyWordsList;
        private readonly List<string> _importantKeyWordsListToLower;


        public HomeController(ILogger<HomeController> logger, ApplicationDbContext dbContext,
                              HelpfulMethods helpfulMethods, IMemoryCache cache,
                              IRecipesService recipesService, IIngredientScaleService ingredientService,
                              ISearchRecipeService search)
        {
            _logger = logger;
            _context = dbContext;
            _helpfulMethods = helpfulMethods;
            _cache = cache;
            _recipesService = recipesService;
            _importantKeyWordsList = _helpfulMethods.GetQueryListFromDb(_context);
            _importantKeyWordsListToLower = _importantKeyWordsList.Select(x => x.ToLower().Trim()).ToList();
            _ingredientService = ingredientService;
            _search = search;
        }

        public IActionResult GetRecipesPartialView(List<int> Ids = null)
        {

            List<Recipes> model = new();
            var recipeIdsFromDb = _context.Recipes
                .Where(x => Ids == null || !Ids.Contains(x.Id))
                .Select(x => x.Id)
                .ToList();

            var randomRecipeIds = _recipesService.GetRendomRecipesIds(recipeIdsFromDb, 25);


            model = _context.Recipes.Where(x => randomRecipeIds.Contains(x.Id)).ToList();

            return PartialView("_recipesPartialView", model);


        }




        public IActionResult Index()
        {

            return View();

        }


        public async Task<IActionResult> SearchRecipes(string query)
        {
            var model = await _search.GetRecipesByQuery(query);
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