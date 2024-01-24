using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using Microsoft.AspNetCore.Mvc;

namespace DelikatessenDrehbuch.Controllers
{
    public class RecessionController : Controller
    {
        private readonly ApplicationDbContext _context;
        public RecessionController(ApplicationDbContext applicationDbContext)
        {
            _context = applicationDbContext;
        }


        public IActionResult Index(int id)
        {
            var recipeFromDb = _context.Recipes.SingleOrDefault(x => x.Id == id);

            if (recipeFromDb == null)
                return BadRequest();

            Recession recession = new Recession();
          
            recession.Recipes = recipeFromDb;
            recession.UserEmail = User.Identity.Name;
            recession.Assessment = "";
           
            return View(recession);
        }

        public IActionResult SaveRecessionInDB(Recession recession)
        {
            recession.UserEmail = User.Identity.Name;
            return Ok();
        }
    }
}

