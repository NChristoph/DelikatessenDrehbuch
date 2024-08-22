using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;
using System.Drawing.Text;
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
        string[] importentKeyWords = { "vegan", "vegetarisch" };
        public HomeController(ILogger<HomeController> logger, ApplicationDbContext dbContext, HelpfulMethods helpfulMethods, IMemoryCache cache)
        {
            _logger = logger;
            _context = dbContext;
            _helpfulMethods = helpfulMethods;
            _cache = cache;
        }




        private List<QueryHandler> GetQueryHandlerByUserPreferences()
        {
            var userPreferencesQueryListFromDb = _helpfulMethods.GetUserPreferencesQueryListByEmail(_context, User.Identity.Name)
                                                                .OrderByDescending(x => x.Count).Take(5).ToList();

            List<QueryHandler> queryHandlerFromDb = new List<QueryHandler>();

            if (userPreferencesQueryListFromDb.Any())
            {
                var queryListFromDb = GetQueryListFromDb();
                var filterList = userPreferencesQueryListFromDb.Where(x => queryListFromDb.Contains(x.Query.ToLower())).Select(x => x.Query).ToList();

                queryHandlerFromDb = _context.QueryHandler.Where(x => filterList.Contains(x.Query.Query))
                                                          .Include(x => x.Recipe)
                                                          .Include(x => x.Query).ToList();


            }
            else
            {
                return GetRandomRecipes();
            }

            return queryHandlerFromDb;

        }

        private List<string> RemoveQuerys(List<string> splitedQuerys)
        {
            List<string> filtredSplitedQuerys = new();

            for (int i = 0; i < splitedQuerys.Count; i++)
            {
                if (!importentKeyWords.Contains(splitedQuerys[i].ToLower()))
                    filtredSplitedQuerys.Add(splitedQuerys[i]);
            }


            return filtredSplitedQuerys;
        }

        private List<string> GetQueryListFromDb()
        {
            return _context.Querys.Select(x => x.Query.ToLower()).ToList();
        }

        private List<QueryHandler> FindeRecipesByQuerys(List<string> splitedQuery)
        {
            List<QueryHandler> querySearchResults = new();
            querySearchResults = _context.QueryHandler.Where(x => splitedQuery.Contains(x.Query.Query.ToLower())).Include(x => x.Recipe).Include(x => x.Query).ToList();
            return querySearchResults;
        }

        private List<QueryHandler> FindRecipesByName(string query)
        {

            List<QueryHandler> nameSearchResults = new();
            var recipesFromDb = _context.Recipes.Where(x => x.Name.Contains(query.ToLower())).ToList();
            nameSearchResults = _context.QueryHandler.Where(x => recipesFromDb.Contains(x.Recipe))
                                                     .Include(x => x.Query)
                                                     .GroupBy(x => x.Recipe.Id)
                                                     .Select(x => x.First())
                                                     .ToList();
            return nameSearchResults;
        }

        private List<string> GetQuerysplitedListToLower(string query)
        {
            var splitedQuery = query.Split(' ').ToList();
            List<string> splitedQueryList = new();

            splitedQueryList = splitedQuery.ConvertAll(x => x.ToLower());

            return splitedQueryList;
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

        private List<QueryHandler> GetFinalFiltredList(List<QueryHandler> querySearchResults, List<string> splitedQuerys)
        {
            List<QueryHandler> finalList = new();
            finalList = querySearchResults.Where(x => splitedQuerys.Any(w => x.Recipe.Name.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();

            return finalList;
        }


        [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any, NoStore = false)]
        public IActionResult Index(string query)
        {

            var isLoggedIn = User.Identity.IsAuthenticated;
            List<QueryHandler> handlers;
            string cacheKey = $"{User.Identity.Name}_handlers_{query?.ToLower()}";

            if (!_cache.TryGetValue(cacheKey, out handlers))
            {
                handlers = new();
                if (string.IsNullOrEmpty(query))
                {

                    if (isLoggedIn)
                    {
                        handlers = GetQueryHandlerByUserPreferences();
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

                    List<string> splitedQuery = GetQuerysplitedListToLower(query);

                    List<QueryHandler> querySearchResults = FindeRecipesByQuerys(splitedQuery);



                    if (splitedQuery.Contains("vegan"))
                    {
                        splitedQuery = RemoveQuerys(splitedQuery);
                        handlers = GetFinalFiltredList(querySearchResults, splitedQuery);
                    }
                    if (splitedQuery.Contains("vegetarisch"))
                    {
                        splitedQuery = RemoveQuerys(splitedQuery);
                        handlers.AddRange(GetFinalFiltredList(querySearchResults, splitedQuery));
                    }
                    if (!querySearchResults.Any())
                    {
                        List<QueryHandler> nameSearchResults = FindRecipesByName(query);
                        handlers = nameSearchResults.ToList();
                    }



                }
                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(10)); // Setzt das Caching-Timeout auf 10 Minuten (anpassbar)

                _cache.Set(cacheKey, handlers, cacheEntryOptions);
            }
           

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