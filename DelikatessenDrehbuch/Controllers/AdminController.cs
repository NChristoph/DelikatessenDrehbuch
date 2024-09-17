using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Polly;

namespace DelikatessenDrehbuch.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly HelpfulMethods _helpfulMethods;

        public AdminController(ApplicationDbContext context, IMemoryCache cache, HelpfulMethods helpfulMethods)
        {
            _context = context;
            _cache = cache;
            _helpfulMethods = helpfulMethods;
        }
        public IActionResult Index()
        {
            AdminControllerModel model = new();
            model.SupportMessage = _context.SupportMessage.ToList();
            model.Recipes = _context.Recipes.ToList();
            model.UserCount = _context.Users.Count();
            model.PremiumUser = _context.Users.Where(user => _context.UserRoles
                                              .Any(ur => ur.UserId == user.Id && _context.Roles
                                              .Any(r => r.Id == ur.RoleId && r.Name == "PremiumUser")))
                                              .Count();

            return View(model);
        }

        public IActionResult EditRecipesPartialView(int id)
        {
            var fullRecipes = _helpfulMethods.GetFullRecipeById(_context, id);
            return View("EditRecipes", fullRecipes);
        }
        public IActionResult AddIngredientRow(int id)
        {
            DropdownModel dropdownModel = new DropdownModel();
            dropdownModel.IngredientHandler = new IngredientHandlerModel();
            dropdownModel.Measure = _context.Metrics.ToList();
            dropdownModel.Index = id;


            return PartialView("_IngredientPartialView", dropdownModel);
        }

        //TODO:Bei keiner auswahl in dropdown einen defoult
        //wert übergeben und Läschen von rezept Funktioniert  nicht ansehen
        public IActionResult EditeRecipes(FullRecipes fullRecipes)
        {
            var recipesFromDb = _context.Recipes.SingleOrDefault(x => x.Id == fullRecipes.Recipes.Id);
            var recipeHandlersFromDb = _context.RecipesHandlers.Where(x => x.Recipe.Id == recipesFromDb.Id)
                                                                 .ToList();

            if (recipesFromDb == null)
                return BadRequest("Kein Rezept gefunden");
            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    EditRecipe(recipesFromDb, fullRecipes);
                    DeleteReciphandlerFromDb(recipeHandlersFromDb);
                    CreateRecipeHandler(recipesFromDb, fullRecipes.IngredientHandler);

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw new Exception("Fehler beim Verarbeiten der Daten", ex);
                }
            }



            return RedirectToAction("Index");
        }

        private void EditRecipe(Recipes recipesFromDb, FullRecipes fullRecipes)
        {

            recipesFromDb.Name = fullRecipes.Recipes.Name;
            recipesFromDb.Category = fullRecipes.Recipes.Category;
            recipesFromDb.Description = fullRecipes.Recipes.Description;
            recipesFromDb.Preparation = fullRecipes.Recipes.Preparation;
            recipesFromDb.PreparationTime = fullRecipes.Recipes.PreparationTime;
            recipesFromDb.Calories = fullRecipes.Recipes.Calories;
            if (fullRecipes.Recipes.FormFile != null)
                recipesFromDb.FormFile = fullRecipes.Recipes.FormFile;


            _context.SaveChanges();
        }

   
        private void DeleteReciphandlerFromDb(List<RecipesHandler> recipeHandlersFromDb)
        {
            _context.RecipesHandlers.RemoveRange(recipeHandlersFromDb);
            _context.SaveChanges();
        }
        private void CreateRecipeHandler(Recipes recipesFromDb, List<IngredientHandlerModel> ingredienthandler)
        {
            List<RecipesHandler> newReciphandler = new();
            foreach (var handler in ingredienthandler)
            {
                RecipesHandler newHandler = new RecipesHandler()
                {
                    Id = 0,
                    Recipe = recipesFromDb,
                    IngredientHandler = GetOrCreateIngredientHandler(handler)

                };
                newReciphandler.Add(newHandler);
            }
            _context.AddRange(newReciphandler);
            _context.SaveChanges(true);
        }

        private IngredientHandlerModel GetOrCreateIngredientHandler(IngredientHandlerModel ingredientHandler)
        {
            var handler = _context.IngredientHandlers.SingleOrDefault(x => x.Ingredient.Name.ToLower() == ingredientHandler.Ingredient.Name.ToLower()
                                                                  && x.Measure.UnitOfMeasurement == ingredientHandler.Measure.UnitOfMeasurement
                                                                  && x.Quantity.Quantitys == ingredientHandler.Quantity.Quantitys);

            if (handler != null)
                return handler;
            else
            {
                handler = new IngredientHandlerModel()
                {
                    Id = 0,
                    Ingredient = GetOrCreateIngredient(ingredientHandler.Ingredient),
                    Quantity = GetOrCreateQuantity(ingredientHandler.Quantity),
                    Measure = GetMeasure(ingredientHandler.Measure)
                };
                return handler;
            }
        }

        #region IngredientHandlerContent
        private Ingredient GetOrCreateIngredient(Ingredient ingredient)
        {
            var ingredientFromDb = _context.Ingredients.SingleOrDefault(x => x.Name.ToLower() == ingredient.Name.ToLower());

            if (ingredientFromDb != null)
                return ingredientFromDb;
            else
            {
                ingredientFromDb = new Ingredient()
                {
                    Id = 0,
                    Name = ingredient.Name,

                };

                _context.Add(ingredientFromDb);
                _context.SaveChanges();
            }

            return ingredientFromDb;
        }
        private Quantity GetOrCreateQuantity(Quantity quantity)
        {
            var quantityFromDb = _context.Quantities.FirstOrDefault(x => x.Quantitys == quantity.Quantitys);

            if (quantityFromDb != null)
                return quantityFromDb;
            else
            {
                _context.Quantities.Add(quantity);
                _context.SaveChanges();
                return quantity;
            }
        }

        private Measure GetMeasure(Measure measure)
        {
            var measureFromDb = _context.Metrics.FirstOrDefault(x => x.UnitOfMeasurement.ToLower() == measure.UnitOfMeasurement.ToLower().Trim());

            if (measureFromDb != null)
                return measureFromDb;
            else
            {
                measureFromDb = new Measure()
                {
                    Id = 0,
                    UnitOfMeasurement = measure.UnitOfMeasurement
                };
                _context.Metrics.Add(measureFromDb);
                _context.SaveChanges();

                return measure;
            }
        }

        #endregion

        // [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any, NoStore = false)]
        public IActionResult AdditOrDeliteRecipePartialView(string query)
        {
            // string cacheKey = $"{User.Identity.Name}_handlers_{query?.ToLower()}";
            List<Recipes> recipes = new List<Recipes>();
            int recipeId;
            var isNuber = int.TryParse(query, out recipeId);
            if (isNuber)
                recipes = _context.Recipes.Where(x => x.Id == recipeId).ToList();
            else
                recipes = _context.Recipes.Where(x => x.Name.ToLower().Trim().Contains(query.ToLower().Trim())).ToList();


            // var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(10)); // Setzt das Caching-Timeout auf 10 Minuten (anpassbar)

            //_cache.Set(cacheKey, recipes, cacheEntryOptions);



            return PartialView("_AdditOrDeliteRecipePartialView", recipes);
        }

        [HttpPost]
        public IActionResult DeleteRecipes(int id)
        {

            var recipesFromDb = _context.Recipes.SingleOrDefault(x => x.Id == id);
            var recessionFromDb = _context.Recessions.Where(x => x.Recipes.Id == id).ToList();
            var queryHandlerFromDb = _context.QueryHandler.Where(x => x.Recipe.Id == id).ToList();
            var userPreverenceRecipeFromDb = _context.UserPreferencesRecipes.SingleOrDefault(x => x.Recipes.Id == id);
            
            if (recipesFromDb == null)
                return BadRequest();

            if (userPreverenceRecipeFromDb != null)
                _context.UserPreferencesRecipes.Remove(userPreverenceRecipeFromDb);

            if (recessionFromDb != null)             
                    _context.RemoveRange(recessionFromDb);

            if (queryHandlerFromDb != null)
                    _context.QueryHandler.RemoveRange(queryHandlerFromDb);


            _context.Remove(recipesFromDb);
            _context.SaveChanges();

            return RedirectToAction("Index");

        }

        public IActionResult DeleteSupportTicket(int id)
        {
            if (id == 0)
                return BadRequest("No Id Found");
            var supportMessageFromDb = _context.SupportMessage.SingleOrDefault(x => x.Id == id);

            if (supportMessageFromDb == null)
                return BadRequest("No Message Fund");

            _context.Remove(supportMessageFromDb);
            _context.SaveChanges();


            return RedirectToAction("Index");
        }


    }
}
