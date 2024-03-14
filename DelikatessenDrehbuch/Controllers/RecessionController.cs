using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DelikatessenDrehbuch.Controllers
{
    [Authorize]
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
          
            return View(recession);
        }

        public IActionResult SaveRecessionInDB(Recession recession)
        {
            
            if(recession==null)
                return BadRequest();

            Recession newRecession = new();
            newRecession.Id = 0;
            newRecession.CreationDate = DateTime.Now;
            newRecession.UserEmail = User.Identity.Name;
            newRecession.Assessment =recession.Assessment;
            newRecession.Recipes = _context.Recipes.SingleOrDefault(_ => _.Id == recession.Recipes.Id);

            _context.Recessions.Add(newRecession);
            _context.SaveChanges();
            return Ok();
        }
    }
}

