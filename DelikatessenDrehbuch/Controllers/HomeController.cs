using DelikatessenDrehbuch.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace DelikatessenDrehbuch.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult SearchRecipes(string keyWord)
        {
            //TODO:Hier die Suche machen und partial view zurück geben
            //Partial View muss noch erstellt werden
            return Ok("Treffer");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}