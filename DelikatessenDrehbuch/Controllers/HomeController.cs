using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace DelikatessenDrehbuch.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;
        public HomeController(ILogger<HomeController> logger,ApplicationDbContext dbContext)
        {
            _logger = logger;
            _context = dbContext;
        }

      
        public IActionResult Index()
        {
           

            return View(new List<Recipes>());
          
        }

        public IActionResult Privacy()
        {
            return View();
        }

        //public IActionResult SearchRecipes(string keyWord)
        //{
        //    var recipesFromDb=_ontext.Recipes.Where(x=>x.Name.ToLower() == keyWord.ToLower()).ToList();  
            
        //    return PartialView("_FindRecipesPartialView",recipesFromDb);
        //}

        //public IActionResult CaruselParial()
        //{
        //    var randomRecipesFromDb=_dbContext.Recipes.ToList();

        //    return PartialView("_CaruselPartialView",randomRecipesFromDb);
        //}

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}