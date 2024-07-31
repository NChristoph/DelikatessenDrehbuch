using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using System.Diagnostics;
using System.Drawing.Text;
using System.Reflection;

namespace DelikatessenDrehbuch.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly HelpfulMethods _helpfulMethods;
        public HomeController(ILogger<HomeController> logger, ApplicationDbContext dbContext, HelpfulMethods helpfulMethods)
        {
            _logger = logger;
            _context = dbContext;
            _helpfulMethods = helpfulMethods;
        }


      

        private List<QueryHandler> GetQueryHandlerByUserPreferences()
        {
            var userPreferencesQueryListFromDb = _helpfulMethods.GetUserPreferencesQueryListByEmail(_context, User.Identity.Name)
                                                                .OrderByDescending(x => x.Count).Take(3).ToList();
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
                var random = new Random();
                queryHandlerFromDb = _context.QueryHandler.Include(x => x.Recipe)
                                                    .Include(x => x.Query).ToList();

                queryHandlerFromDb = queryHandlerFromDb.OrderBy(x => random.Next()).Take(50).ToList();
            }

            return queryHandlerFromDb;

        }

        private string[] SplitQuery(string query)
        {
            return query.Split(' ');
        }

        private List<string> GetQueryListFromDb()
        {
            return _context.Querys.Select(x => x.Query.ToLower()).ToList();
        }

        private List<string> GetFilterlistByQuery(string query)
        {
            string[] querySplit = SplitQuery(query);
            var listFilterQuerys = new List<string>();
            var querysFromDb = GetQueryListFromDb();

            for (int i = 0; i < querySplit.Length; i++)
                if (querysFromDb.Contains(querySplit[i].ToLower()))
                    listFilterQuerys.Add(querySplit[i].ToLower());

            return listFilterQuerys;
        }

        private string GetRecipesNameByQuery(string query)
        {
            string[] querySplit = SplitQuery(query);
            string recipName = "";
            var querysFromDb = GetQueryListFromDb();

            for (int i = 0; i < querySplit.Length; i++)
                if (!querysFromDb.Contains(querySplit[i].ToLower()))
                    recipName += querySplit[i].ToLower() + " ";

            return recipName;
        }


        [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any, NoStore = false)]
        public IActionResult Index(string query)
        {
            List<QueryHandler> handlers = new List<QueryHandler>();

            if (string.IsNullOrEmpty(query))
            {
                handlers = GetQueryHandlerByUserPreferences();
              
            }
            else
            {
                var isLoggedIn = User.Identity.IsAuthenticated;
                if(isLoggedIn)
                {
                    _helpfulMethods.CreateUserPreferencesQuery(User.Identity.Name, query, _context);
                }
                var querysFromQuery = GetFilterlistByQuery(query);
                var recipeName = GetRecipesNameByQuery(query);

                if (querysFromQuery.Any())
                {
                    handlers = _context.QueryHandler.Where(x => querysFromQuery.Contains(x.Query.Query.ToLower())).Include(x => x.Recipe).Include(x => x.Query).ToList();
                    if (!string.IsNullOrEmpty(recipeName))
                    {
                        var sortedQuerys = handlers.Where(x => x.Recipe.Name.ToLower().Trim().Contains(recipeName.ToLower().Trim())).ToList();
                        if (!sortedQuerys.Any())
                            sortedQuerys = _context.QueryHandler.Where(x => x.Recipe.Name.ToLower().Trim().Contains(recipeName.ToLower().Trim())).ToList();
                        handlers = sortedQuerys;
                    }

                }
                else
                {
                    handlers = _context.QueryHandler.Where(x => x.Recipe.Name.ToLower().Contains(recipeName.ToLower().Trim()))
                                                                  .Include(x => x.Recipe)
                                                                  .Include(x => x.Query).ToList();
                }


               

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

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}