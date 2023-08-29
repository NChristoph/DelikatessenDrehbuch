using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Internal;

namespace DelikatessenDrehbuch.Controllers
{
    [Authorize]
    public class MyRecipesController : Controller
    {
        private readonly ApplicationDbContext _dbcontext;

        public MyRecipesController(ApplicationDbContext context)
        {
            _dbcontext = context;
        }

        public IActionResult Index()
        {
            var myRecipesFromDb=_dbcontext.Recipes.Where(x=>x.OwnerEmail==User.Identity.Name).ToList();

            return View(myRecipesFromDb);
        }

        public IActionResult DeleteRecipes(int id)
        {
            //TODO:Loeschen eines Rezeptes
          
            return  RedirectToAction("Index");
        }
    }
}
