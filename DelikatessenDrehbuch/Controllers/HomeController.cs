using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using System.Diagnostics;
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

      
        private List<Recipes> GetRecipeByQuery(string query)
        {
            var sortedQueryList=_context.QueryHandler.Where(x=>x.Query.Query.ToLower()==query.ToLower())
                                                     .Include(x=>x.Recipe)
                                                     .ToList();

            sortedQueryList = sortedQueryList.DistinctBy(x => x.Recipe).ToList();

            var recipes=sortedQueryList.Select(x=>x.Recipe).ToList();

            return recipes;
        }


        public IActionResult Index(string query)
        {
            var filter =_context.Querys.Select(x=>x.Query.ToLower()).ToList();
            List<Recipes> recipes = new List<Recipes>();

            if (string.IsNullOrEmpty(query))
            {
                var random = new Random();
                recipes = _context.Recipes.ToList();
                recipes.OrderBy(x => random.Next()).Take(15).ToList();

            }
            else
            {
                if (filter.Contains(query.ToLower()))
                    recipes=GetRecipeByQuery(query);
                else
                    recipes = _context.Recipes.Where(x => x.Name.Contains(query.ToLower())).ToList();
                    
            }


            return View(recipes);

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