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

        private readonly List<string> importantKeyWordsList;
        public HomeController(ILogger<HomeController> logger, ApplicationDbContext dbContext, HelpfulMethods helpfulMethods, IMemoryCache cache)
        {
            _logger = logger;
            _context = dbContext;
            _helpfulMethods = helpfulMethods;
            _cache = cache;
            importantKeyWordsList = GetQueryListFromDb();
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
                if (!importantKeyWordsList.Contains(splitedQuerys[i].ToLower()))
                    filtredSplitedQuerys.Add(splitedQuerys[i]);
            }


            return filtredSplitedQuerys;
        }

        private List<string> GetQueryListFromDb()
        {
            return _context.Querys.Select(x => x.Query.ToLower()).ToList();
        }

        private List<string> GetQuerysFromSplitedQuerys(List<string> splitedQuerys)
        {
            return importantKeyWordsList.Where(x => splitedQuerys.Contains(x.ToLower())).ToList();
        }
        private List<QueryHandler> GetQueryHandlersByQuerys(List<string> splitedQuerys)
        {
            var queryFilter = GetQuerysFromSplitedQuerys(splitedQuerys);
            var querySearchResults = _context.QueryHandler.Where(x => queryFilter.Contains(x.Query.Query.ToLower()))
                                                    .Include(x => x.Recipe)
                                                    .Include(x => x.Query)
                                                    .ToList();

            return querySearchResults;
        }
        //TodoAuch wen eine zutrift sollte rezeptausgabe funktionieren prüfe den splittetQueryCount ps Du hast bei ein Paar gerichten die querys doppelt

        private List<QueryHandler> GroupeAndSortQueryList(List<QueryHandler> querySearchResults, List<string> splitedQuerys)
        {
            var queryFilter = GetQuerysFromSplitedQuerys(splitedQuerys);
            var groupedQueryhandler = querySearchResults.GroupBy(x => x.Recipe.Id).ToList();
            List<QueryHandler > sortedQueryHandler = new ();
            if (splitedQuerys.Count > 1)
            {
                foreach (var queryHandler in groupedQueryhandler)
                {
                    var selectQueryFromGroup = queryHandler.Select(x => x.Query.Query).ToList();
                    int matchCount = selectQueryFromGroup.Count(query => queryFilter.Contains(query, StringComparer.OrdinalIgnoreCase));
                    if (matchCount > 1)
                    {
                        sortedQueryHandler.Add(queryHandler.First());
                    }
                }
            }
            else
            {
                sortedQueryHandler=groupedQueryhandler.Select(x=>x.First()).ToList();
            }
            
            

            return sortedQueryHandler;
        }

        private List<QueryHandler> GetQueryHandlersByNameAndQuery(List<string> splitedQuery)
        {
            List<QueryHandler> querySearchResults = GetQueryHandlersByQuerys(splitedQuery);

            var sortedQueryhandler= GroupeAndSortQueryList(querySearchResults, splitedQuery);
            var nameFilter = RemoveQuerys(splitedQuery);

            List<QueryHandler> nameSearchResults = new();


            var querys = RemoveQuerys(splitedQuery);
            foreach (var filter in querys)
            {

                nameSearchResults.AddRange(_context.QueryHandler.Where(x => x.Recipe.Name.ToLower().Contains(filter))
                                                                .Include(x => x.Recipe)
                                                                .Include(x => x.Query)
                                                                .ToList());
            }



            List<QueryHandler> finalFiltredList = new();

            if(querySearchResults.Any())
            {
                finalFiltredList = nameSearchResults.Where(x => querySearchResults.Contains(x)).ToList();
            }
            else
            {
                finalFiltredList = nameSearchResults;
            }
           
            if (!finalFiltredList.Any())
            {
                finalFiltredList = nameSearchResults;
                finalFiltredList.AddRange(sortedQueryhandler);
            }
                


            return finalFiltredList;
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



        [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any, NoStore = false)]
        public IActionResult Index(string query)
        {

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

                handlers = GetQueryHandlersByNameAndQuery(splitedQuery);
                //Todo:Vegan Mexikanisch funktionirt wieder nicht aber 3 querys gehen 

                if (!handlers.Any())
                {

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