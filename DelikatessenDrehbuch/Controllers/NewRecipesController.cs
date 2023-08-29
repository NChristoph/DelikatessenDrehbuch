using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Data.Migrations;
using DelikatessenDrehbuch.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.WebEncoders.Testing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace DelikatessenDrehbuch.Controllers
{
    public class NewRecipesController : Controller
    {
        private readonly ApplicationDbContext _dbContext;
        public NewRecipesController(ApplicationDbContext context)
        {
            _dbContext = context;
        }
        public IActionResult Index(int id)
        {
            if (id == 0)
                return View(new FullRecipes());
            else
            {

                var recipeToEdit = _dbContext.Recipes.SingleOrDefault(x => x.Id == id);

                var fullRecipe = new FullRecipes()
                {
                    Recipes = recipeToEdit,
                    IngredientHandler = GetIngredientHandlers(recipeToEdit)
                };

                return View(fullRecipe);
            }


        }

        public List<IngredientHandlerModel> GetIngredientHandlers(Recipes recipeToEdit)
        {
            List<IngredientHandlerModel> handlers = new List<IngredientHandlerModel>();
            var recipeHandlerFromDb = _dbContext.RecipesHandlers.Where(x => x.Recipe.Id == recipeToEdit.Id)
                                                                .Include(x => x.IngredientHandler)
                                                                .Include(x => x.IngredientHandler.Measure)
                                                                .Include(x => x.IngredientHandler.Quantity)
                                                                .Include(x => x.IngredientHandler.Ingredient).ToList();

            foreach (var handler in recipeHandlerFromDb)
                handlers.Add(handler.IngredientHandler);

            return handlers;
        }

        public IActionResult IngredientPartialView()
        {
            return PartialView("_IngredientPartialView");
        }


        public IActionResult BlankSentence()
        {
            return PartialView("_IngredientPartialView", new IngredientHandlerModel());
        }


        [HttpPost]
        public IActionResult AddOrEditRecipes(FullRecipes newRecipes)
        {
            var recipeFromDb = _dbContext.Recipes.SingleOrDefault(x => x.Id == newRecipes.Recipes.Id);

            if (recipeFromDb == null)
            {
                var recipes = new Recipes()
                {
                    Id = 0,
                    OwnerEmail = User.Identity.Name,
                    Name = newRecipes.Recipes.Name,
                    Preparation = newRecipes.Recipes.Preparation,

                };



                _dbContext.Add(recipes);
                _dbContext.SaveChanges();
                CreateRecipesHandler(newRecipes.Recipes, newRecipes.IngredientHandler);

            }
            else
            {
                recipeFromDb.Name = newRecipes.Recipes.Name;
                recipeFromDb.ImagePath = newRecipes.Recipes.ImagePath;
                recipeFromDb.Preparation = newRecipes.Recipes.Preparation;

                _dbContext.SaveChanges();
                EditeRecipe(newRecipes, recipeFromDb);
            }

            return Ok();
        }

        private void DeleteIngredient(Recipes recipesFromDb, List<IngredientHandlerModel> modal)
        {
            var recipeHandlerFromDb = _dbContext.RecipesHandlers.Where(x => x.Recipe.Id == recipesFromDb.Id)
                                                                .Include(x => x.IngredientHandler)
                                                                .Include(x => x.IngredientHandler.Ingredient).ToList();
            List<string> namesOfIngredients = new();

            foreach (var ingredient in modal)
                namesOfIngredients.Add(ingredient.Ingredient.Name);


            foreach (var handler in recipeHandlerFromDb)
                if (!namesOfIngredients.Contains(handler.IngredientHandler.Ingredient.Name))
                    _dbContext.RecipesHandlers.Remove(handler);

            _dbContext.SaveChanges();

        }

        private void EditeRecipe(FullRecipes fullRecipes, Recipes recipesFromDb)
        {
            DeleteIngredient(recipesFromDb, fullRecipes.IngredientHandler);

            var ingredient = GetIngredientHandlers(recipesFromDb);
            var newIngredient = fullRecipes.IngredientHandler;

            if (ingredient == null)
                CreateRecipesHandler(recipesFromDb, fullRecipes.IngredientHandler);


            foreach (var ingredientFromDb in ingredient)
            {
                foreach (var ingredientHandler in fullRecipes.IngredientHandler)
                {
                    if (!string.IsNullOrEmpty(ingredientHandler.Ingredient.Name))
                    {
                        var recipeHandlerFromDb = _dbContext.RecipesHandlers.SingleOrDefault(x => x.Recipe.Id == recipesFromDb.Id
                                                                                             && x.IngredientHandler.Ingredient.Name.ToLower() == ingredientHandler.Ingredient.Name.ToLower());
                        if (recipeHandlerFromDb != null)
                        {
                            recipeHandlerFromDb.IngredientHandler.Measure = GetOrCreateMeasure(ingredientHandler.Measure);
                            recipeHandlerFromDb.IngredientHandler.Quantity = GetOrCreateQuantity(ingredientHandler.Quantity);

                            _dbContext.SaveChanges();
                        }
                        else
                        {
                            var recipesHandler = new RecipesHandler()
                            {
                                Id = 0,
                                Recipe = recipesFromDb,
                                IngredientHandler = GetOrCreateIngredientHandler(ingredientHandler),

                            };

                            _dbContext.RecipesHandlers.Add(recipesHandler);
                            _dbContext.SaveChanges();
                        }
                    }
                   
                }

            }
        }

        private void CreateRecipesHandler(Recipes recipes, List<IngredientHandlerModel> ingredientHandlers)
        {
            var recipeFromDb = _dbContext.Recipes.SingleOrDefault(x => x.Preparation == recipes.Preparation
                                                                    && x.Name == recipes.Name);
            foreach (var ingredientHandler in ingredientHandlers)
            {
                var recipesHandler = new RecipesHandler()
                {
                    Id = 0,
                    Recipe = recipeFromDb,
                    IngredientHandler = GetOrCreateIngredientHandler(ingredientHandler),

                };

                _dbContext.RecipesHandlers.Add(recipesHandler);
                _dbContext.SaveChanges();
            }

        }


        private IngredientHandlerModel GetOrCreateIngredientHandler(IngredientHandlerModel handler)
        {
            var ingredientHandlerFromDb = _dbContext.IngredientHandlers.SingleOrDefault(x => x.Ingredient.Name.ToLower() == handler.Ingredient.Name.ToLower()
                                                                                        && x.Measure.UnitOfMeasurement.ToLower() == handler.Measure.UnitOfMeasurement.ToLower()
                                                                                        && x.Quantity.Quantitys == handler.Quantity.Quantitys
                                                                                        );

            if (ingredientHandlerFromDb != null)
                return ingredientHandlerFromDb;
            else
            {
                ingredientHandlerFromDb = new IngredientHandlerModel()
                {
                    Id = 0,
                    Ingredient = GetOrCreateIngredient(handler.Ingredient),
                    Quantity = GetOrCreateQuantity(handler.Quantity),
                    Measure = GetOrCreateMeasure(handler.Measure),
                };

                return ingredientHandlerFromDb;
            }

        }

        #region IngredientHandlerContent
        private Ingredient GetOrCreateIngredient(Ingredient ingredient)
        {
            var ingredientFromDb = _dbContext.Ingredients.SingleOrDefault(x => x.Name.ToLower() == ingredient.Name.ToLower());

            if (ingredientFromDb != null)
                return ingredientFromDb;
            else
            {
                ingredientFromDb = new Ingredient()
                {
                    Id = 0,
                    Name = ingredient.Name,

                };

                _dbContext.Add(ingredientFromDb);
                _dbContext.SaveChanges();
            }

            return ingredientFromDb;
        }
        private Quantity GetOrCreateQuantity(Quantity quantity)
        {
            var quantityFromDb = _dbContext.Quantities.FirstOrDefault(x => x.Quantitys == quantity.Quantitys);

            if (quantityFromDb != null)
                return quantityFromDb;
            else
            {
                _dbContext.Quantities.Add(quantity);
                _dbContext.SaveChanges();
                return quantity;
            }
        }
        private Measure GetOrCreateMeasure(Measure measure)
        {
            var mesureFromDb = _dbContext.Metrics.FirstOrDefault(X => X.UnitOfMeasurement.ToLower() == measure.UnitOfMeasurement.ToLower());

            if (mesureFromDb != null)
                return mesureFromDb;
            else
            {
                _dbContext.Metrics.Add(measure);
                _dbContext.SaveChanges();
                return measure;
            }
        }
        #endregion
    }
}
