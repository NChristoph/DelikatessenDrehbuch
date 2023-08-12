using FoodForFun.Data;
using FoodForFun.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace FoodForFun.Controllers
{
    public class NewRecipesController : Controller
    {
        private readonly ApplicationDbContext _dbContext;
        public NewRecipesController(ApplicationDbContext context)
        {
            _dbContext = context;
        }
        public IActionResult Index()
        {
            return View(new FullRecipes());
        }

        public IActionResult IngredientPartialView()
        {
            return PartialView("_IngredientPartialView");
        }
        public IActionResult BlankSentence()
        {
            return PartialView("_IngredientPartialView", new Ingredient());
        }

        //Todoo:Auf einzelnne konten umrüsten
        //Nach speichern zurück zur startseite

        public IActionResult AddNewRecipes(FullRecipes newRecipes)
        {
            var recipes = new Recipes()
            {
                Id = 0,
                Name = newRecipes.Recipes.Name,
                Preparation = newRecipes.Recipes.Preparation,

            };



            _dbContext.Add(recipes);
            _dbContext.SaveChanges();
            CreateRecipesHandler(newRecipes);

            return Ok();
        }

        private void CreateRecipesHandler(FullRecipes newRecipes)
        {
            var recipesFromDb = _dbContext.Recipes.SingleOrDefault(x => x.Preparation == newRecipes.Recipes.Preparation);
            foreach (var ingrdient in newRecipes.Ingredient)
            {
                var recipesHandler = new RecipesHandler()
                {
                    Id = 0,
                    Recipe = recipesFromDb,
                    Ingredient = GetOrCreateIngredient(ingrdient),

                };

                _dbContext.RecipesHandlers.Add(recipesHandler);
                _dbContext.SaveChanges();
            }

        }
        //Todo=dataenbank speicherung noch machen in den methoden
        
        

        private Ingredient GetOrCreateIngredient(Ingredient ingredient)
        {
            var ingredientFromDb = _dbContext.Ingredients.SingleOrDefault(x => x.Name.ToLower() == ingredient.Name.ToLower());

            if (ingredientFromDb != null)
            {
                if(ingredient.Quantity.Quantitys == ingredientFromDb.Quantity.Quantitys)
                    return ingredientFromDb;
            }
            else
            {
                ingredientFromDb = new Ingredient()
                {
                    Id = 0,
                    Name = ingredient.Name,
                    Quantity = ingredient.Quantity,
                };

                _dbContext.Add(ingredientFromDb);
                _dbContext.SaveChanges();
            }

           

            return ingredientFromDb;
        }

       
    }
}
