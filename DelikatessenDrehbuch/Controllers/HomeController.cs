using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;

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

        private bool Vegan { get; set; }
        public HomeController(ILogger<HomeController> logger, ApplicationDbContext dbContext, HelpfulMethods helpfulMethods, IMemoryCache cache)
        {
            _logger = logger;
            _context = dbContext;
            _helpfulMethods = helpfulMethods;
            _cache = cache;
            _importantKeyWordsList = _helpfulMethods.GetQueryListFromDb(_context);
            _importantKeyWordsListToLower = _importantKeyWordsList.Select(x => x.ToLower().Trim()).ToList();
        }







        private List<Recipes> GetRandomRecipes()
        {
            var random = new Random();
            var handler = _context.Recipes.ToList();
            return handler.OrderBy(x => random.Next()).ToList();


        }




        public IActionResult GetRecipesPartialView(string Ids = null)
        {

            ShowRecipesModel model = new();
            var allRecipes = _context.Recipes.ToList();
            List<int> idList = new List<int>();


            var list = allRecipes.Where(x => !idList.Contains(x.Id)).OrderBy(x => Guid.NewGuid()).Take(25).ToList();
            model.RecipesList = list;
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
            ShowRecipesModel model = new();
            var filterList = GetListFromQueryString(query);

            var filtredRecipesByQuereys = await GetRecipeListByQuerys(filterList);
            var filtredRecipesByName = await GetRecipesByName(query.Trim().ToLower());


            model.RecipesList = filtredRecipesByName.Union(filtredRecipesByQuereys).ToList();


            return PartialView("_recipesPartialView", model);
        }
        public IActionResult Index2(string query)
        {

            bool? vegan = Request.Query.ContainsKey("vegan");
            if (vegan.HasValue)
            {
                if (vegan.Value == true)
                    Vegan = true;
                else
                {
                    vegan = false;
                    Vegan = false;
                }
            }



            HttpContext.Session.SetString("veganFilter", vegan.Value.ToString());
            var veganFilter = HttpContext.Session.GetString("veganFilter");
            ViewBag.IsVegan = veganFilter != null && bool.Parse(veganFilter);



            var isLoggedIn = User.Identity.IsAuthenticated;
            List<Recipes> handlers;
            string cacheKey = $"{User.Identity.Name}_handlers_{query?.ToLower()}";


            handlers = new();
            if (string.IsNullOrEmpty(query))
            {
                if (_cache.TryGetValue(cacheKey, out handlers))
                {
                    var dictonaryFromCache = handlers;
                    return View(dictonaryFromCache);
                }

                if (isLoggedIn)
                {
                    //TODO:UserPreferenz noch einbinden
                    handlers = GetRandomRecipes();
                }
                else
                {

                    handlers = GetRandomRecipes();


                }
            }
            else
            {

                if (isLoggedIn)
                {
                    _helpfulMethods.CreateUserPreferencesQuery(User.Identity.Name, query, _context);
                }



                //    handlers = GetQueryHandlersByNameAndQuery(query.ToLower());


                if (!handlers.Any())
                {
                    handlers = GetRandomRecipes();
                }



            }
            var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(10)); // Setzt das Caching-Timeout auf 10 Minuten (anpassbar)

            _cache.Set(cacheKey, handlers, cacheEntryOptions);



            return View(handlers);

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