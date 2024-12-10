using Azure.Storage.Blobs;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.MyExceptions;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        private readonly AddRecipeException _myExceptions;
        private readonly HelpfulMethods _helpfulMethods;


        private readonly string _connectionString = "DefaultEndpointsProtocol=https;EndpointSuffix=core.windows.net;AccountName=blobdelikatessendrehbuch;AccountKey=NNJKin4e0NxZwD8XpLgZC+21vgcvkMd5tcp1gXiM4+zSAYV2DGDBx7unmFglQrs9YQH/RdtJIMME+AStw4Espg==;BlobEndpoint=https://blobdelikatessendrehbuch.blob.core.windows.net/;FileEndpoint=https://blobdelikatessendrehbuch.file.core.windows.net/;QueueEndpoint=https://blobdelikatessendrehbuch.queue.core.windows.net/;TableEndpoint=https://blobdelikatessendrehbuch.table.core.windows.net/";
        private readonly string _containerName = "picdelikatessendrehbuch";
        private readonly string _azureAcoutName = "blobdelikatessendrehbuch";

        public AdminController(ApplicationDbContext context, IMemoryCache cache, HelpfulMethods helpfulMethods, AddRecipeException myExceptions)
        {
            _context = context;
            _cache = cache;
            _helpfulMethods = helpfulMethods;
            _myExceptions = myExceptions;



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

        public IActionResult AddNewRecipes()
        {
            return View(new NewRecipesMobileUpload());
        }

        #region BlobAzure_SaveImage
        public void UploadMsToAzureBlop(IFormFile file)
        {

            string blobName = $"{file.FileName}";


            // Get a reference to a container named "sample-container" and then create it
            BlobContainerClient container = new BlobContainerClient(_connectionString, _containerName);
            //container.Create();

            // Get a reference to a blob named "sample-file" in a container named "sample-container"
            BlobClient blob = container.GetBlobClient(blobName);

            bool blobExist = blob.Exists();

            if (blobExist)
                return;

            if (file != null)
            {


                using (var ms = new MemoryStream())
                {

                    file.CopyTo(ms);
                    ms.Position = 0;
                    var byteArry = ms.ToArray();

                    blob.Upload(new BinaryData(byteArry));
                }
            }

        }

        private string GetImagePathFromAzure(IFormFile formFile)
        {
            string blobName = $"{formFile.FileName}";

            return $"https://{_azureAcoutName}.blob.core.windows.net/{_containerName}/{blobName}";
        }
        #endregion

        #region SaveNewRecipe_In_DB


        [ValidateAntiForgeryToken]
        public IActionResult SaveNewRecipes(NewRecipesMobileUpload newRecipes = null, [FromForm] EditRecipesModel recipes = null)
        {
            var x = ModelState;
            if (ModelState.IsValid)
            {

                return BadRequest(ModelState);
            }

            using (var transAction = _context.Database.BeginTransaction())
            {
                try
                {
                    if (!string.IsNullOrEmpty(newRecipes.Name))
                        CreateRecipesFromString(newRecipes);
                    else
                    {
                        EditeRecipes(recipes);
                    }

                    transAction.Commit();
                }
                catch (Exception ex)
                {
                    transAction.Rollback();
                    throw new Exception($"{_myExceptions.ErrorMessage}");
                }

            }



            return RedirectToAction("Index");
        }

        private string[] GetArrayFromString(string convertToArray)
        {
            var lines = convertToArray.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
                                 .Select(line => line.Trim())
                                 .ToArray();

            return lines;
        }
        private void CreateRecipesFromString(NewRecipesMobileUpload newRecipes)
        {

            FullRecipes currentRecipe = new();
            MealPlan mealPlan = null;

            currentRecipe.Recipes.Name = newRecipes.Name;
            currentRecipe.Recipes.Calories = newRecipes.Calories;
            currentRecipe.Recipes.Category = newRecipes.Category;
            currentRecipe.Recipes.Description = newRecipes.Description;
            currentRecipe.Recipes.Preparation = newRecipes.Preperation;
            currentRecipe.Recipes.PreparationTime = Int32.Parse(newRecipes.PreperationTime);
            currentRecipe.Recipes.OwnerEmail = "Delikatessen.drehbuch@outlook.com";
            if (!string.IsNullOrEmpty(newRecipes.MealPlan))
            {
                mealPlan = new();
                mealPlan.Name = newRecipes.MealPlan;
            }

            var ingredients = GetArrayFromString(newRecipes.Ingredients);

            foreach (var ingredient in ingredients)
            {
                if (!string.IsNullOrEmpty(ingredient))
                {
                    string[] ing = ingredient.Split("#");
                    IngredientHandlerModel ingredientHandler = new IngredientHandlerModel();
                    ingredientHandler.Id = 0;
                    ingredientHandler.Ingredient.Name = ing[2].Trim();
                    ingredientHandler.Measure.UnitOfMeasurement = ing[1].Trim();
                    ingredientHandler.Quantity.Quantitys = float.Parse(ing[0].Trim());

                    currentRecipe.IngredientHandler.Add(ingredientHandler);
                }

            }

            var querys = GetArrayFromString(newRecipes.Querys);
            foreach (var query in querys)
            {
                currentRecipe.QueryHandler.Add(query.Trim());
            }

            if (currentRecipe != null)
                currentRecipe.Recipes.FormFile = newRecipes.RecipesImage;



            var ifExist = _context.Recipes.FirstOrDefault(x => x.Name == currentRecipe.Recipes.Name && x.Preparation == currentRecipe.Recipes.Preparation);
            if (ifExist == null)
            {
                AddRecipes(currentRecipe, mealPlan);

            }


        }
        public void AddRecipes(FullRecipes newRecipes, MealPlan mealPlan = null)
        {
            MealPlanHandler mealPlanHandler = null;


            if (newRecipes.Recipes.FormFile != null)
                UploadMsToAzureBlop(newRecipes.Recipes.FormFile);



            var recipes = new Recipes()
            {
                Id = 0,
                OwnerEmail = newRecipes.Recipes.OwnerEmail,
                Name = newRecipes.Recipes.Name,
                Preparation = newRecipes.Recipes.Preparation,
                Category = newRecipes.Recipes.Category,
                PreparationTime = newRecipes.Recipes.PreparationTime,
                Description = newRecipes.Recipes.Description,
                LikeCount = 0,
                ImagePath = newRecipes.Recipes.FormFile != null ? GetImagePathFromAzure(newRecipes.Recipes.FormFile) : "",
                Calories = newRecipes.Recipes.Calories

            };


            _context.Recipes.Add(recipes);
            _context.SaveChanges();

            CreateRecipeHandler(newRecipes.Recipes, newRecipes.IngredientHandler);
            CreateQuaryHandler(recipes, newRecipes.QueryHandler);

            if (mealPlan != null)
            {

                var exist = _context.MealPlan.FirstOrDefault(x => x.Name.ToLower() == mealPlan.Name.ToLower());
                if (exist == null)
                    _context.MealPlan.Add(mealPlan);

                _context.SaveChanges();
                using (var transaction = _context.Database.BeginTransaction())
                {
                    try
                    {


                        mealPlanHandler = new();
                        mealPlanHandler.Id = 0;
                        mealPlanHandler.MealPlan = _context.MealPlan.SingleOrDefault(x => x.Name.ToLower() == mealPlan.Name.ToLower());
                        mealPlanHandler.Recipes = _context.Recipes.SingleOrDefault(x => x.Name.ToLower() == recipes.Name.ToLower() && x.Preparation.ToLower() == recipes.Preparation.ToLower());

                        _context.MealPlanHandler.Add(mealPlanHandler);
                        _context.SaveChanges();

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception("Fehler bei den Menüplans", ex);

                    }
                }



            }

        }

        #endregion

        #region Create_QuaryHandler
        private void CreateQuaryHandler(Recipes recipes, List<string> queryHandlers)
        {

            var querysFromDb = _context.Querys.ToList();

            foreach (var queryHandler in queryHandlers)
            {
                if (!querysFromDb.Select(x => x.Query.ToLower()).Contains(queryHandler.ToLower()))
                {
                    var query = new Querys()
                    {
                        Id = 0,
                        Query = queryHandler,
                    };

                    _context.Querys.Add(query);
                }
            }
            _context.SaveChanges();

            var querysFromDbNew = _context.Querys.ToList();

            foreach (var query in queryHandlers)
            {
                if (querysFromDbNew.Select(x => x.Query.ToLower()).Contains(query.ToLower()))
                {
                    var quaryHandler = new QueryHandler()
                    {
                        Id = 0,
                        Recipe = recipes,
                        Query = _context.Querys.SingleOrDefault(x => x.Query.ToLower() == query.ToLower()),
                    };

                    _context.QueryHandler.Add(quaryHandler);
                }

            }

            _context.SaveChanges();

        }
        #endregion

        #region EditeRecipe
        private void EditRecipe(Recipes recipesFromDb, EditRecipesModel fullRecipes)
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

        private void CreateRecipeHandler(Recipes recipes, List<IngredientHandlerModel> ingredienthandler)
        {
            var recipesFromDb = GetRecipeFromDb(recipes);
            List<RecipesHandler> newReciphandler = new();
            foreach (var handler in ingredienthandler)
            {
                if (string.IsNullOrEmpty(handler.Measure.UnitOfMeasurement))
                {
                    handler.Measure.UnitOfMeasurement = "Stk.";
                }


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
            _myExceptions.ErrorMessage = $"IngredientHandler mit Zutat: {ingredientHandler.Ingredient.Name} " +
                                       $"Mänge: {ingredientHandler.Quantity.Quantitys} " +
                                       $"Maßeinheit: {ingredientHandler.Measure.UnitOfMeasurement} " +
                                       $"Hat ein Fehler ausgegeben möglicherweise doppelter Eintrag in der Db";

           

            var handler = _context.IngredientHandlers
                          .SingleOrDefault(x => x.Ingredient.Name.ToLower().Trim() == ingredientHandler.Ingredient.Name.ToLower().Trim()
                                                  &&
                                                     (
                                                         x.Measure.UnitOfMeasurement.Trim().ToLower() == ingredientHandler.Measure.UnitOfMeasurement.Trim().ToLower()
                                                      || x.Measure.UnitOfMeasurement.Trim().ToLower() == ingredientHandler.Measure.UnitOfMeasurement.Trim().ToLower() + "."
                                                     )
                                                  && x.Quantity.Quantitys == ingredientHandler.Quantity.Quantitys)
                          ;

            if (handler != null)
                return handler;

            handler = new IngredientHandlerModel()
            {
                Id = 0,
                Ingredient = GetOrCreateIngredient(ingredientHandler.Ingredient.Name),
                Quantity = GetOrCreateQuantity(ingredientHandler.Quantity.Quantitys),
                Measure = GetorCreateMeasure(ingredientHandler.Measure.UnitOfMeasurement)
            };




            return handler;


        }

        #region IngredientHandlerContent
        private Ingredient GetOrCreateIngredient(string ingredient)
        {
            var ingredientFromDb = _context.Ingredients.Single(x => x.Name.ToLower().Trim() == ingredient.ToLower().Trim());

            if (ingredientFromDb != null)
                return ingredientFromDb;
            else
            {
                ingredientFromDb = new Ingredient()
                {
                    Id = 0,
                    Name = ingredient.Trim(),

                };

                _context.Add(ingredientFromDb);
                _context.SaveChanges();
            }

            return ingredientFromDb;
        }
        private Quantity GetOrCreateQuantity(float quantity)
        {
            var quantityFromDb = _context.Quantities.Single(x => x.Quantitys == quantity);

            if (quantityFromDb != null)
                return quantityFromDb;
            else
            {
                quantityFromDb = new()
                {
                    Id = 0,
                    Quantitys = quantity
                };

                _context.Quantities.Add(quantityFromDb);
                _context.SaveChanges();
                return quantityFromDb;
            }
        }

        private Measure GetorCreateMeasure(string measure)
        {

            var measureFromDb = _context.Metrics.Single(x => x.UnitOfMeasurement.ToLower().Trim() == measure.ToLower().Trim()
                                                                || x.UnitOfMeasurement.ToLower().Trim() == measure.Trim().ToLower() + ".");

            if (measureFromDb != null)
                return measureFromDb;
            else
            {
                measureFromDb = new Measure()
                {
                    Id = 0,
                    UnitOfMeasurement = measure
                };
                _context.Metrics.Add(measureFromDb);
                _context.SaveChanges();

                return measureFromDb;
            }
        }

        #endregion

        #endregion
        private Recipes GetRecipeFromDb(Recipes recipes)
        {
            var recipeFromDb = _context.Recipes.FirstOrDefault(x => x.Name == recipes.Name
                                                           && x.Preparation == recipes.Preparation
                                                           && x.OwnerEmail == recipes.OwnerEmail
                                                           );

            return recipeFromDb;
        }


        public IActionResult EditRecipesPartialView(int id)
        {
            var recipeFromDb = _helpfulMethods.GetRecipeFromDbById(_context, id);
            var ingredientHandlersFromDb = _context.RecipesHandlers.Where(x => x.Recipe == recipeFromDb)
                                                             .Include(x => x.IngredientHandler.Ingredient)
                                                             .Include(x => x.IngredientHandler.Measure)
                                                             .Include(x => x.IngredientHandler.Quantity)
                                                             .Select(x => x.IngredientHandler)
                                                             .ToList();
            EditRecipesModel editRecipesModel = new()
            {
                Recipes = recipeFromDb,
                IngredientHandler = ingredientHandlersFromDb,
                Measure = _context.Metrics.ToList(),


            };

            ViewData.TemplateInfo.HtmlFieldPrefix = string.Empty;
            return View("EditRecipes", editRecipesModel);
        }
        public IActionResult AddIngredientRow(int id)
        {
            DropdownModel dropdownModel = new DropdownModel();
            dropdownModel.IngredientHandler = new IngredientHandlerModel();
            dropdownModel.Measure = _context.Metrics.ToList();
            dropdownModel.Index = id;


            return PartialView("_IngredientPartialViewEditRecipes", dropdownModel);
        }
        public IActionResult EditeRecipes(EditRecipesModel fullRecipes)
        {
            var recipesFromDb = _context.Recipes.FirstOrDefault(x => x.Id == fullRecipes.Recipes.Id);
            var recipeHandlersFromDb = _context.RecipesHandlers.Where(x => x.Recipe.Id == recipesFromDb.Id)
                                                                 .ToList();

            if (recipesFromDb == null)
                return BadRequest("Kein Rezept gefunden");


            EditRecipe(recipesFromDb, fullRecipes);
            DeleteReciphandlerFromDb(recipeHandlersFromDb);
            CreateRecipeHandler(recipesFromDb, fullRecipes.IngredientHandler);

            _myExceptions.ErrorMessage = "Bearbeiten des rezeptes Fehlgeschlagen";


            return RedirectToAction("Index");
        }





        private IngredientNutrientHandler GetOrCreateIngredientNutrienHandler(NutrientsModel nutrients)
        {
            var ingredientNutrienHandlerFromDb = _context.IngredientNutrientHandler.FirstOrDefault(x => x.Ingredient.Name.ToLower().Trim() == nutrients.IngredientNutrientHandler.Ingredient.Name.ToLower().Trim());

            if (ingredientNutrienHandlerFromDb != null)
                return ingredientNutrienHandlerFromDb;
            else
            {
                ingredientNutrienHandlerFromDb = new IngredientNutrientHandler()
                {
                    Id = 0,
                    Ingredient = GetOrCreateIngredient(nutrients.IngredientNutrientHandler.Ingredient.Name),
                    Effect = nutrients.IngredientNutrientHandler.Effect,

                };
                _context.IngredientNutrientHandler.Add(ingredientNutrienHandlerFromDb);
                _context.SaveChanges();

            }
            return _context.IngredientNutrientHandler.FirstOrDefault(x => x.Ingredient.Name.ToLower().Trim() == nutrients.IngredientNutrientHandler.Ingredient.Name.ToLower().Trim());

        }
        public Nutrient GetOrCreateNutrients(string name, NutrientsModel nutrients)
        {
            var nutrientFromDb = _context.Nutrient.FirstOrDefault(x => x.Name.Trim().ToLower() == name.ToLower().Trim());

            if (nutrientFromDb != null)
                return nutrientFromDb;
            else
            {
                nutrientFromDb = new Nutrient()
                {
                    Id = 0,
                    Name = name,

                };
                _context.Nutrient.Add(nutrientFromDb);
                _context.SaveChanges();
            }
            return _context.Nutrient.FirstOrDefault(x => x.Name.Trim().ToLower() == name.ToLower().Trim());

        }

        public IActionResult AddNutriensAndEffects()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "/Nutrients_and_Effects.txt";
            var lines = System.IO.File.ReadAllLines(path);
            NutrientsModel nutrients = new();
            string ingredient = "";

            foreach (var line in lines)
            {

                using (var transaction = _context.Database.BeginTransaction())
                {
                    try
                    {

                        if (line.StartsWith("Neu#"))
                        {
                            nutrients = new();

                        }
                        else if (line.StartsWith("Zutat#"))
                        {

                            var word = line.Split('#');
                            ingredient = word[1];
                            nutrients.IngredientNutrientHandler.Ingredient = GetOrCreateIngredient(word[1]);
                        }
                        else if (line.StartsWith("Nutzen#"))
                        {
                            var word = line.Split("#");
                            nutrients.IngredientNutrientHandler.Ingredient.Name = ingredient;
                            nutrients.IngredientNutrientHandler.Effect = word[1];



                        }
                        else if (line.StartsWith("End#"))
                        {



                        }
                        else
                        {
                            var ingredients = line.Split('#');
                            NutrientsHandler handler = new();

                            var floatToParse = ingredients[1].Replace(".", ",");
                            handler.Quantity = GetOrCreateQuantity(float.Parse(floatToParse.Trim()));
                            handler.Measure = GetorCreateMeasure(ingredients[2]);
                            handler.Nutrient = GetOrCreateNutrients(ingredients[0], nutrients);
                            handler.IngredientNutrientHandler = GetOrCreateIngredientNutrienHandler(nutrients);

                            var handlerExist = _context.NutrientsHandler.FirstOrDefault(x => x.IngredientNutrientHandler.Ingredient.Name.ToLower().Trim()
                                                                        == ingredient.ToLower().Trim() && x.Nutrient.Name.ToLower() == ingredients[0].ToLower());

                            if (handlerExist == null)
                            {
                                _context.NutrientsHandler.Add(handler);
                                _context.SaveChanges();
                                transaction.Commit();
                            }



                        }


                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception($"Fehler in Line {line}", ex);
                    }
                }




            }

            return RedirectToAction("Index");
        }






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

