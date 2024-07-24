using Azure;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

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
            CreateUserPreferencesRecipe(id);
            return View(HelpfulMethods.GetFullRecipeById(_context,id));
        }

        private void CreateUserPreferencesRecipe(int id)
       // private void CreateUserPreferencesQuery(int id)
        {
            var userName = User.Identity.Name;
            if (userName==null)
                return;
            
            var recipeFromDb=_context.Recipes.SingleOrDefault(x => x.Id == id);

            if (recipeFromDb == null)
                return;


            var userPreferenceRecipeFromDb = _context.UserPreferencesRecipes.SingleOrDefault(x => x.Recipes == recipeFromDb
                                                             && x.UserEmail == userName);

            if(userPreferenceRecipeFromDb != null)
                return;

            var UserPreferenceRecipe = new UserPreferencesRecipe()
            {
                Id = 0,
                Recipes = recipeFromDb,
                UserEmail = userName,
            };


            _context.UserPreferencesRecipes.Add(UserPreferenceRecipe);
            _context.SaveChanges();

        }

        public IActionResult AddOrRemoveLike(int id)
        {
            var currentUserName = User.Identity.Name;
            var recipe = _context.Recipes.SingleOrDefault(x => x.Id == id);
            var like = _context.Likes.SingleOrDefault(x => x.UserMail == currentUserName && x.Recipe == recipe);

            if (recipe == null)
             return BadRequest();



            if (like == null)
                AddLike(currentUserName, id, recipe);
            else
                RemoveLike(like,recipe);
            
            return RedirectToAction("Index",new { id });
        }

        private void AddLike(string currentUserName,int id,Recipes recipes)
        {
            var like = new Like
            {
                Id = 0,
                UserMail = currentUserName,
                Recipe = recipes,
               

            };

            recipes.LikeCount++;

            _context.Likes.Add(like);
            _context.SaveChanges();
        }

        private void RemoveLike(Like like,Recipes recipe)
        {
            recipe.LikeCount--;
            _context.Likes.Remove(like);
            _context.SaveChanges();
        }

        public IActionResult SaveRecessionInDB(int id, string assessment)
        {

            if (id == null)
                return BadRequest();

            Recession newRecession = new();
            newRecession.Id = 0;
            newRecession.CreationDate = DateTime.Now;
            newRecession.UserEmail = User.Identity.Name;
            newRecession.Assessment = assessment;
            newRecession.Recipes = _context.Recipes.SingleOrDefault(_ => _.Id == id);

            _context.Recessions.Add(newRecession);
            _context.SaveChanges();
            return RedirectToAction("Index",new { id = id });
        }
    }
}
