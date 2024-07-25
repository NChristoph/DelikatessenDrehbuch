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
        public HomeController(ILogger<HomeController> logger, ApplicationDbContext dbContext)
        {
            _logger = logger;
            _context = dbContext;
        }


        private List<QueryHandler> GetQueryHandlerByQuery(string query)
        {
            var sortedQueryList = _context.QueryHandler.Where(x => x.Query.Query.ToLower() == query.ToLower())
                                                     .Include(x => x.Recipe)
                                                     .Include(x => x.Query)
                                                     .ToList();



            return sortedQueryList;
        }


        [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any, NoStore = false)]
        public IActionResult Index(string query)
        {
            bool loggedIn = User.Identity.IsAuthenticated;

            if (loggedIn)
                HelpfulMethods.CreateUserPreferencesQuery(User.Identity.Name, query, _context);

            var filter = _context.Querys.Select(x => x.Query.ToLower()).ToList();
            var queryHandler = new List<QueryHandler>();

            if (string.IsNullOrEmpty(query))
            {
                var random = new Random();
                queryHandler = _context.QueryHandler.Include(x => x.Recipe)
                                                    .Include(x => x.Query).ToList();

                queryHandler = queryHandler.OrderBy(x => random.Next()).Take(15).ToList();


            }
            else
            {
                if (filter.Contains(query.ToLower()))
                    queryHandler = GetQueryHandlerByQuery(query);
                else
                    queryHandler = _context.QueryHandler.Where(x => x.Recipe.Name.Contains(query.ToLower()))
                                                        .Include(x => x.Recipe)
                                                        .Include(x => x.Query).ToList();

            }

            var recipesAndQuerys = CreateDictonary(queryHandler);
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