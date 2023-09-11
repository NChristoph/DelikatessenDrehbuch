using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Mvc;

namespace DelikatessenDrehbuch.Controllers
{
    public class SelectedRecipeController : Controller
    {
        private readonly ApplicationDbContext _context;
        public SelectedRecipeController(ApplicationDbContext dbContext)
        {
            _context = dbContext;
        }
        public IActionResult Index(int id)
        {
            return View(HelpfulMethods.GetFullRecipeById(_context,id));
        }
    }
}
