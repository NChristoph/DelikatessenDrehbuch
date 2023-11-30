using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Data.Migrations;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.WebEncoders.Testing;
using NuGet.Packaging;
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
                var fullRecipes = HelpfulMethods.GetFullRecipeById(_dbContext, id);
                return View(fullRecipes);
            }

        }

        public IActionResult EditRecipesPartialView(int id)
        {

            var metrics = _dbContext.Metrics.ToList();
            var test = _dbContext.IngredientHandlers.Include(x => x.Ingredient).Include(x => x.Measure).Include(x => x.Quantity).SingleOrDefault(x => x.Id == id);

            var dropDownModel = new DropdownModel();
            dropDownModel.IngredientHandler = test;
            dropDownModel.Measure = metrics;
            return PartialView("_IngredientPartialView", dropDownModel);
        }

        public IActionResult BlankSentence()
        {
            var measureFromDb = _dbContext.Metrics.ToList();
            DropdownModel dropdown = new DropdownModel();
            dropdown.Measure = measureFromDb;
            return PartialView("_IngredientPartialView", dropdown);
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
                    Category= newRecipes.Recipes.Category,  
                    LikeCount = 0
                };



                _dbContext.Add(recipes);
                _dbContext.SaveChanges();
                CreateRecipesHandler(newRecipes);

            }
            else
            {
                recipeFromDb.Name = newRecipes.Recipes.Name;
                recipeFromDb.Category= newRecipes.Recipes.Category;
                //TODO: ImagePath noch bearbeitbar machen Wen Image Server bereit
                //  recipeFromDb.ImagePath = newRecipes.Recipes.ImagePath;
                recipeFromDb.Preparation = newRecipes.Recipes.Preparation;

                _dbContext.SaveChanges();
                EditRecipes(newRecipes, recipeFromDb);
            }

            return Ok();
        }


        private void EditRecipes(FullRecipes newRecipes, Recipes recipesFromDb)
        {

            var editIngredients = newRecipes.IngredientHandler;
            var recipeHandlerFromDb = _dbContext.RecipesHandlers.Where(x => x.Recipe == recipesFromDb)
                                                                .Include(x => x.IngredientHandler)
                                                                .Include(x => x.IngredientHandler.Measure)
                                                                .Include(x => x.IngredientHandler.Quantity)
                                                                .Include(x => x.IngredientHandler.Ingredient).ToList();


            foreach (var handler in recipeHandlerFromDb)
            {
                _dbContext.Remove(handler);
                _dbContext.SaveChanges();
            }

            CreateRecipesHandler(newRecipes, recipesFromDb);

        }

        private void CreateRecipesHandler(FullRecipes newRecipes,Recipes recipesFromDb=null)
        {
            //Entferne die Doppelten
            newRecipes.IngredientHandler = newRecipes.IngredientHandler.DistinctBy(x=>x.Ingredient.Name.ToLower()).ToList();

            Recipes? myRecipes=new();
  
            if(recipesFromDb!=null)
                myRecipes=recipesFromDb;
            else
                myRecipes=_dbContext.Recipes.FirstOrDefault(x=>x.Name==newRecipes.Recipes.Name
                                                           &&x.Preparation==newRecipes.Recipes.Preparation
                                                           &&x.OwnerEmail==newRecipes.Recipes.OwnerEmail
                                                           );
            
            
                foreach (var ingredientHandler in newRecipes.IngredientHandler)
                {
                    var recipesHandler = new RecipesHandler()
                    {
                        Id = 0,
                        Recipe = myRecipes,
                        IngredientHandler = GetOrCreateIngredientHandler(ingredientHandler),

                    };

                    _dbContext.RecipesHandlers.Add(recipesHandler);
                    _dbContext.SaveChanges();
                }
            
           

        }

        private IngredientHandlerModel GetOrCreateIngredientHandler(IngredientHandlerModel handler)
        {
            var ingredientHandlerFromDb = _dbContext.IngredientHandlers.SingleOrDefault(x => x.Ingredient.Name.ToLower() == handler.Ingredient.Name.ToLower()
                                                                                        && x.Measure.UnitOfMeasurement.ToLower().Trim() == handler.Measure.UnitOfMeasurement.ToLower().Trim()
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
                    Measure = GetMeasure(handler.Measure),
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

        private Measure GetMeasure(Measure measure)
        {
            var measureFromDb=_dbContext.Metrics.FirstOrDefault(x=>x.UnitOfMeasurement.ToLower() == measure.UnitOfMeasurement.ToLower().Trim());
            return measureFromDb;
        }
       
        #endregion
    }
}
