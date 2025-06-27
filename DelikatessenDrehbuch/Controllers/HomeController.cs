using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;
using System.Linq;

namespace DelikatessenDrehbuch.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IMemoryCache _cache;
        private readonly ApplicationDbContext _context;
        private readonly HelpfulMethods _helpfulMethods;

        private readonly List<string> _importantKeyWordsList;
        private readonly List<string> _importantKeyWordsListToLower;

     
        public HomeController(ILogger<HomeController> logger, ApplicationDbContext dbContext, HelpfulMethods helpfulMethods, IMemoryCache cache)
        {
            _logger = logger;
            _context = dbContext;
            _helpfulMethods = helpfulMethods;
            _cache = cache;
            _importantKeyWordsList = _helpfulMethods.GetQueryListFromDb(_context);
            _importantKeyWordsListToLower = _importantKeyWordsList.Select(x => x.ToLower().Trim()).ToList();
        }


        private List<int> GetRandomRecipesIds(List<int> ids)
        {
            var random = new Random();
           
            return ids.OrderBy(x => random.Next()).Take(25).ToList();

        }

        public IActionResult GetRecipesPartialView(List<int> Ids = null)
        {

            List<Recipes> model = new();
            var recipeIdsFromDb = _context.Recipes
                .Where(x => Ids == null || !Ids.Contains(x.Id))
                .Select(x => x.Id)
                .ToList();

            var randomRecipeIds = GetRandomRecipesIds(recipeIdsFromDb);

           
            model=_context.Recipes.Where(x=>randomRecipeIds.Contains(x.Id)).ToList();
            
            return PartialView("_recipesPartialView", model);


        }



        public IActionResult Index()
        {

            return View();
        }

        private List<string> GetListFromQueryString(string query)
        {
            return query.ToLower().Split(" ").ToList();
        }
        private async Task<List<Recipes>> GetRecipeListByQuerys(List<string> querys)
        {
            return await _context.QueryHandler.Where(x => querys.Contains(x.Query.Query.ToLower())).Select(x => x.Recipe).ToListAsync();
        }

        private async Task<List<Recipes>> GetRecipesByName(string query)
        {
            return await _context.Recipes.Where(x => x.Name.Trim().ToLower() == query ||
                                                x.Name.Contains(query)).ToListAsync();
        }
        public async Task<IActionResult> SearchRecipes(string query)
        {
           List<Recipes> model = new();
            var filterList = GetListFromQueryString(query);

            var filtredRecipesByQuereys = await GetRecipeListByQuerys(filterList);
            var filtredRecipesByName = await GetRecipesByName(query.Trim().ToLower());


            model = filtredRecipesByName.Union(filtredRecipesByQuereys).ToList();


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