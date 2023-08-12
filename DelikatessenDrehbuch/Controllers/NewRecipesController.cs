using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Runtime.CompilerServices;

namespace DelikatessenDrehbuch.Controllers
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
            //var myRecipes= _dbContext.Recipes.Where(x=>x.)
            return View(new FullRecipes());
        }

        public IActionResult IngredientPartialView()
        {
            return PartialView("_IngredientPartialView");
        }


        public IActionResult BlankSentence()
        {
            return PartialView("_IngredientPartialView", new IngredientHandler());
        }



        public IActionResult AddNewRecipes(FullRecipes newRecipes)
        {
            var identity = User.Identity;
            var recipes = new Recipes()
            {
                Id = 0,
                OwnerEmail = User.Identity.Name,
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
            foreach (var ingrdientHandler in newRecipes.IngredientHandler)
            {
                var recipesHandler = new RecipesHandler()
                {
                    Id = 0,
                    Recipe = recipesFromDb,
                    IngredientHandler = GetOrCreateIngredientHandler(ingrdientHandler),


                };

                _dbContext.RecipesHandlers.Add(recipesHandler);
                _dbContext.SaveChanges();
            }

        }




        private IngredientHandler GetOrCreateIngredientHandler(IngredientHandler handler)
        {
            var ingredientHandlerFromDb = _dbContext.IngredientHandlers.SingleOrDefault(x => x.Ingredient.Name.ToLower() == handler.Ingredient.Name.ToLower()
                                                                                        && x.Measure.UnitOfMeasurement.ToLower() == handler.Measure.UnitOfMeasurement.ToLower()
                                                                                        && x.Quantity.Quantitys == handler.Quantity.Quantitys
                                                                                        );

            if (ingredientHandlerFromDb != null)
                return ingredientHandlerFromDb;
            else
            {
                ingredientHandlerFromDb = new IngredientHandler()
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
