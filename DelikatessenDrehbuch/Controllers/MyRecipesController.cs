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
          
            return View();
        }

        public IActionResult DeleteRecipes(int id)
        {
            
            var recipesFromDb=_dbcontext.Recipes.SingleOrDefault(x=>x.Id==id);

            if (recipesFromDb != null)
            {
                _dbcontext.Remove(recipesFromDb);
                _dbcontext.SaveChanges();

                return RedirectToAction("Index");
            }
            else
                return BadRequest();
          
        }

        public IActionResult LoadMyRecipes()
        {
            if(User.IsInRole("Admin"))
            {
                var recipesFromDb = _dbcontext.Recipes.ToList();
                return PartialView("_MyRecipesPartialView", recipesFromDb);
            }

            var myRecipesFromDb = _dbcontext.Recipes.Where(x => x.OwnerEmail == User.Identity.Name).ToList();
            return PartialView("_MyRecipesPartialView", myRecipesFromDb);
        }

        public IActionResult LoadRecipesILike()
        {
            var likesFromDb = _dbcontext.Likes.Where(x => x.UserMail == User.Identity.Name).Include(x=>x.Recipe).ToList();
            var recipesFromLikes=likesFromDb.Select(x=>x.Recipe).ToList();
            return PartialView("_MyRecipesPartialView", recipesFromLikes);
        }

        public IActionResult LoadSupportTicketsPartialView()
        {
            var supportMessagesFromDb=_dbcontext.SupportMessage.ToList();
            return PartialView("_AdminSupportMessage", supportMessagesFromDb);
        }
    }
}
