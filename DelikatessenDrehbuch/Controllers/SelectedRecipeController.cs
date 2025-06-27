using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Immutable;

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

        public async Task<List<Recipes>> GetRecipesSuggetions(Task<List<Recipes>> recipes)
        {
            var random= new Random();
            var recipeList = await recipes;
            return  recipeList.GroupBy(x => x.Name).OrderBy(g => random.Next())
                          .Take(7)
                          .Select(g => g.First())
                          .ToList();
        }
        public async Task<IActionResult> Index(int id, string name)
        {
            var userIsLoggedIn = User.Identity.IsAuthenticated;
            if (userIsLoggedIn)
                _helpfulMethods.CreateUserPreferencesRecipe(id, User.Identity.Name, _context);

            SelectedRecipesModel model = new()
            {
                FullRecipes = _helpfulMethods.GetFullRecipeById(_context, id)
            };
         
            var IngredientNames= model.FullRecipes.IngredientHandler.Select(x=>x.Ingredient.Name.ToLower()).ToList();
            model.NutrienHandlers = await _context.NutrienHandler.Where(x => IngredientNames.Contains(x.Ingredient.Name.ToLower()))
                                                           .Include(x=>x.Ingredient)
                                                           .Include(x=>x.Quantity)
                                                           .Include(x=>x.Nutrients).ToListAsync();

            var querys = _context.QueryHandler.Where(x=>x.Recipe==model.FullRecipes.Recipes).Select(x=>x.Query.Query.ToLower().Trim()).ToList();
            model.RecipeSuggestions= await GetRecipesSuggetions(_context.QueryHandler.Where(x=>x.Query.Query.ToLower().Trim()==querys.First()).Select(x=>x.Recipe).ToListAsync());
          
            return View(model);
        }



        public async Task <IActionResult> AddOrRemoveLike(int id)
        {
            var currentUserName = User.Identity.Name;
            var recipe = _helpfulMethods.GetRecipeFromDbById(_context, id);
            var like = _context.Likes.SingleOrDefault(x => x.UserMail == currentUserName && x.Recipe == recipe);

            if (recipe == null)
                return BadRequest();

            if (like == null)
               await AddLikeAsync(currentUserName, id, recipe);
            else
               await RemoveLikeAsync(like, recipe);

            return RedirectToAction("Index", new { id });
        }

        private async Task AddLikeAsync(string currentUserName, int id, Recipes recipe)
        {
            var like = new Like
            {
                UserMail = currentUserName,
                Recipe = recipe
            };

            _context.Likes.Add(like);

            
            recipe.LikeCount = await _context.Likes.CountAsync(x => x.Recipe.Id == id) + 1;

            await _context.SaveChangesAsync();
        }


        private async Task RemoveLikeAsync(Like like, Recipes recipe)
        {
           
            _context.Likes.Remove(like);
            recipe.LikeCount = await _context.Likes.CountAsync(x => x.Recipe.Id == recipe.Id) - 1;
            _context.SaveChanges();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveRecessionInDB(int id, string assessment)
        {

            if (id == 0)
                return NotFound("Rezept nicht gefunden");

            Recession newRecession = new()
            {
                Id = 0,
                CreationDate = DateTime.Now,
                UserEmail = User.Identity.Name,
                Assessment = assessment,
                Recipes = _helpfulMethods.GetRecipeFromDbById(_context, id)
            };

           await _context.Recessions.AddAsync(newRecession);
           await _context.SaveChangesAsync();
            return RedirectToAction("Index", new { id });
        }
    }
}
