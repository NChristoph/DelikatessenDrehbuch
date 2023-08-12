using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using test.Models;
using static System.Net.Mime.MediaTypeNames;

namespace test.Controllers
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
            Story story = new Story
            {
                Sentences = new List<Sentence>
        {
            new Sentence { Id = 1, SentenceText = "AAA"
            },

            new Sentence { Id = 2, SentenceText = "BBB"
           },

            new Sentence {Id = 3, SentenceText = "CCC"}
        }
            };
            return View(story);
        }

       

        public IActionResult BlankSentence()
        {
            return PartialView("_SentenceEditor", new Sentence());
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