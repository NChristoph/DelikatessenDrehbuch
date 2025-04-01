using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DelikatessenDrehbuch.Controllers
{
    public class SelectedRecipeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly HelpfulMethods _helpfulMethods;
        public SelectedRecipeController(ApplicationDbContext dbContext, HelpfulMethods helpfulMethods)
        {
            _context = dbContext;
            _helpfulMethods = helpfulMethods;
        }

        public List<Recipes> GetRecipesSuggetions(List<Recipes> recipes)
        {
            return recipes.GroupBy(x => x.Name).OrderBy(g => Guid.NewGuid())
                          .Take(7)
                          .Select(g => g.First())
                          .ToList();
        }
        public IActionResult Index(int id, string name)
        {
            var userIsLoggedIn = User.Identity.IsAuthenticated;
            if (userIsLoggedIn)
                _helpfulMethods.CreateUserPreferencesRecipe(id, User.Identity.Name, _context);

            SelectedRecipesModel model = new()
            {
                FullRecipes = _helpfulMethods.GetFullRecipeById(_context, id)
            };
         
            var IngredientNames=model.FullRecipes.IngredientHandler.Select(x=>x.Ingredient.Name.ToLower()).ToList();
            model.NutrienHandlers = _context.NutrienHandler.Where(x => IngredientNames.Contains(x.Ingredient.Name.ToLower()))
                                                           .Include(x=>x.Ingredient)
                                                           .Include(x=>x.Quantity)
                                                           .Include(x=>x.Nutrients).ToList();

            model.RecipeSuggestions = GetRecipesSuggetions(_context.QueryHandler.Where(x => model.FullRecipes.QueryHandler.Contains(x.Query.Query)).Select(x => x.Recipe).ToList());


            return View(model);
        }



        public IActionResult AddOrRemoveLike(int id)
        {
            var currentUserName = User.Identity.Name;
            var recipe = _helpfulMethods.GetRecipeFromDbById(_context, id);
            var like = _context.Likes.SingleOrDefault(x => x.UserMail == currentUserName && x.Recipe == recipe);

            if (recipe == null)
                return BadRequest();

            if (like == null)
                AddLike(currentUserName, id, recipe);
            else
                RemoveLike(like, recipe);

            return RedirectToAction("Index", new { id });
        }

        private void AddLike(string currentUserName, int id, Recipes recipes)
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

        private void RemoveLike(Like like, Recipes recipe)
        {
            recipe.LikeCount--;
            _context.Likes.Remove(like);
            _context.SaveChanges();
        }

        public IActionResult SaveRecessionInDB(int id, string assessment)
        {

            if (id == null)
                return BadRequest();

            Recession newRecession = new()
            {
                Id = 0,
                CreationDate = DateTime.Now,
                UserEmail = User.Identity.Name,
                Assessment = assessment,
                Recipes = _helpfulMethods.GetRecipeFromDbById(_context, id)
            };

            _context.Recessions.Add(newRecession);
            _context.SaveChanges();
            return RedirectToAction("Index", new { id });
        }
    }
}
