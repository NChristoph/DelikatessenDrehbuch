using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Caching.Memory;
using NuGet.Packaging;
using System.Diagnostics;
using System.Drawing.Text;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

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


        private List<QueryHandler> GetQueryHandlerByUserPreferences()
        {
            var queryHandlers = new List<QueryHandler>();
            var queryHandlersByPreference = _helpfulMethods.GetUserPreferencesQueryListByEmail(_context, User.Identity.Name.ToString());

            if (!queryHandlersByPreference.Any())
            {
                queryHandlers = GetRandomRecipes();
                return queryHandlers;
            }
            else
            {
                var sortet = queryHandlersByPreference.OrderByDescending(x => x.Count).Take(3).ToList();
                var queryList = new List<string>();


                queryList = sortet.Select(x => x.Query.ToString().ToLower()).ToList();
                queryHandlers = _context.QueryHandler.Where(x => queryList.Contains(x.Query.Query.ToLower()))
                                                     .Include(x => x.Recipe)
                                                     .Include(x => x.Query)
                                                     .ToList();

                return queryHandlers;
            }
        }




        private List<QueryHandler> GetRandomRecipes()
        {
            var random = new Random();
            List<QueryHandler> handler = new();

            handler = _context.QueryHandler.Include(x => x.Recipe)
                                                .Include(x => x.Query).ToList();

            handler = handler.OrderBy(x => random.Next()).Take(50).ToList();

            return handler;
        }

        private List<QueryHandler> GetQueryHandlersByNameAndQuery(string searchQuery)
        {
            var query = searchQuery.ToLower().Trim();
            // ToDo:Zutatensuche einbinden Wird für die zutaten suche noch gebraucht
            var keywords = query.Split(' ').Where(x => x.Length > 3).ToList();

            

            var queryHandlerFromDb = _context.QueryHandler
                                     .Include(x => x.Recipe)
                                     .Include(x => x.Query)
                                     .Where(x => x.Recipe.Name.ToLower().Contains(query) ||
                                                 x.Query.Query.ToLower().Contains(query))
                                     .ToList();

           


            if (Vegan)
            {
                queryHandlerFromDb = queryHandlerFromDb
                                    .Where(x => x.Query.Query.ToLower().Contains("vegan") ||
                                                x.Recipe.Name.ToLower().Contains("vegan"))
                                    .ToList();
            }

            return queryHandlerFromDb;
        }



        [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any, NoStore = false)]
        public IActionResult Index(string query)
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
            List<QueryHandler> handlers;
            string cacheKey = $"{User.Identity.Name}_handlers_{query?.ToLower()}";


            handlers = new();
            if (string.IsNullOrEmpty(query))
            {
                if (_cache.TryGetValue(cacheKey, out handlers))
                {
                    var dictonaryFromCache = CreateDictonary(handlers);
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



                handlers = GetQueryHandlersByNameAndQuery(query.ToLower());


                if (!handlers.Any())
                {
                    handlers = GetRandomRecipes();
                }



            }
            var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(10)); // Setzt das Caching-Timeout auf 10 Minuten (anpassbar)

            _cache.Set(cacheKey, handlers, cacheEntryOptions);

            var recipesAndQuerys = CreateDictonary(handlers);
            return View(recipesAndQuerys);

        }

        private Dictionary<Recipes, List<string>> CreateDictonary(List<QueryHandler> queryHandler)
        {
            var recipesAndQuerys = new Dictionary<Recipes, List<string>>();

            foreach (var recipe in queryHandler)
            {
                if (recipesAndQuerys.TryGetValue(recipe.Recipe, out List<string> querys))
                {
                    querys.Add(recipe.Query.Query);
                }
                else
                {
                    recipesAndQuerys[recipe.Recipe] = new List<string> { recipe.Query.Query };
                }
            }

            return recipesAndQuerys;
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