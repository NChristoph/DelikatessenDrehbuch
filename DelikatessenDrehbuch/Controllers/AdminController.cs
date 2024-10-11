using Azure.Storage.Blobs;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Polly;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace DelikatessenDrehbuch.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly HelpfulMethods _helpfulMethods;
        private List<IngredientHandlerModel?> IngredientHandlers { get; set; }

        private readonly string _connectionString = "DefaultEndpointsProtocol=https;EndpointSuffix=core.windows.net;AccountName=blobdelikatessendrehbuch;AccountKey=NNJKin4e0NxZwD8XpLgZC+21vgcvkMd5tcp1gXiM4+zSAYV2DGDBx7unmFglQrs9YQH/RdtJIMME+AStw4Espg==;BlobEndpoint=https://blobdelikatessendrehbuch.blob.core.windows.net/;FileEndpoint=https://blobdelikatessendrehbuch.file.core.windows.net/;QueueEndpoint=https://blobdelikatessendrehbuch.queue.core.windows.net/;TableEndpoint=https://blobdelikatessendrehbuch.table.core.windows.net/";
        private readonly string _containerName = "picdelikatessendrehbuch";
        private readonly string _azureAcoutName = "blobdelikatessendrehbuch";

        public AdminController(ApplicationDbContext context, IMemoryCache cache, HelpfulMethods helpfulMethods)
        {
            _context = context;
            _cache = cache;
            _helpfulMethods = helpfulMethods;
            IngredientHandlers = new();
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
        public IActionResult SaveNewRecipes(NewRecipesMobileUpload newRecipes)
        {
            CreateRecipesFromString(newRecipes.RecipeData, newRecipes.RecipesImage);
            return RedirectToAction("Index");
        }

        private void CreateRecipesFromString(string recipesData, IFormFile recipesImage)
        {
            var lines = recipesData.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
                                   .Select(line => line.Trim())
                                   .ToArray();

            FullRecipes currentRecipe = null;
            foreach (string line in lines)
            {
                try
                {
                    if (line == "Rezept")
                    {
                        currentRecipe = new FullRecipes();
                        currentRecipe.Recipes.OwnerEmail = "delikatessen.drehbuch@outlook.com";


                    }
                    if (line.StartsWith("Id:"))
                    {

                        currentRecipe.Recipes.Id = 0;
                    }
                    else if (line.StartsWith("Category:"))
                    {
                        currentRecipe.Recipes.Category = line.Split(':')[1].Trim();
                    }
                    else if (line.StartsWith("Name:"))
                    {
                        currentRecipe.Recipes.Name = line.Split(':')[1].Trim();
                    }
                    else if (line.StartsWith("Preparation:"))
                    {
                        currentRecipe.Recipes.Preparation = line.Split(':')[1].Trim();
                    }

                    else if (line.StartsWith("Description:"))
                    {
                        currentRecipe.Recipes.Description = line.Split(':')[1].Trim();
                    }

                    else if (line.StartsWith("PreparationTime:"))
                    {
                        var test = line.Split(":")[1].Trim();
                        currentRecipe.Recipes.PreparationTime = int.Parse(line.Split(':')[1].Trim());
                    }
                    else if (line.StartsWith("Kalorien:"))
                    {

                        currentRecipe.Recipes.Calories = line.Split(':')[1].Trim();
                    }
                    if (line.StartsWith("Zutaten"))
                    {
                        try
                        {
                            string[] ing = line.Split(":");
                            IngredientHandlerModel ingredientHandler = new IngredientHandlerModel();
                            ingredientHandler.Id = 0;
                            ingredientHandler.Ingredient.Name = ing[1].Trim();
                            ingredientHandler.Measure.UnitOfMeasurement = ing[2].Trim();
                            ingredientHandler.Quantity.Quantitys = float.Parse(ing[3].Trim());

                            currentRecipe.IngredientHandler.Add(ingredientHandler);
                        }
                        catch
                        {
                            BadRequest($"Zutaten des Rezeptes: {currentRecipe.Recipes.Name} kannten nicht gespeichert werden , {line}");
                        }

                    }
                    if (line.StartsWith("Query:"))
                    {
                        currentRecipe.QueryHandler.Add(line.Split(':')[1].Trim());
                    }
                    currentRecipe.Recipes.FormFile = recipesImage;


                }
                catch (Exception ex)
                {
                    BadRequest($"Fehler in {line}");
                }

            }

            var ifExist = _context.Recipes.SingleOrDefault(x => x.Name == currentRecipe.Recipes.Name && x.Preparation == currentRecipe.Recipes.Preparation);
            if (ifExist == null)
            {
                AddRecipes(currentRecipe);
            }



        }
        public void AddRecipes(FullRecipes newRecipes)
        {


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

        private void CreateRecipeHandler(Recipes recipes, List<IngredientHandlerModel> ingredienthandler)
        {
            var recipesFromDb = GetRecipeFromDb(recipes);
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
            var handler = _context.IngredientHandlers.FirstOrDefault(x => x.Ingredient.Name.ToLower() == ingredientHandler.Ingredient.Name.ToLower()
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

        public IActionResult AddWeekRecipes(int recipesCount)
        {
            MealPlanModel model = new MealPlanModel();
            model.MealPlanHandlers = new MealPlanHandler[recipesCount];
            return View(model);
        }

        public IActionResult AddNewMealPlan(MealPlanModel mealPlanModel)
        {
            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    AddNewMealPlanToDb(mealPlanModel.MealPlan.Name);
                    CreateMealPlanHandler(mealPlanModel.MealPlanHandlers, mealPlanModel.MealPlan.Name);


                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw new Exception(ex.Message, ex);
                }

            }


            //TODO Weekly Recipes erstellen
            return RedirectToAction("Index");
        }

        private void AddNewMealPlanToDb(string name)
        {
            var weeklyRecipes = new MealPlan()
            {
                Id = 0,
                Name = name,
            };
            _context.MealPlans.Add(weeklyRecipes);
            _context.SaveChanges();
        }
        private void CreateMealPlanHandler(MealPlanHandler[] mealPlanHandlers, string mealPlanName)
        {
            foreach (var mealPlanHandler in mealPlanHandlers)
            {
                MealPlanHandler handler = new()
                {
                    Id = 0,
                    Recipes = mealPlanHandler.Recipes.Id != 0 ? _helpfulMethods.GetRecipeFromDbById(_context, mealPlanHandler.Recipes.Id) : null,
                    MealPlan = _context.MealPlans.SingleOrDefault(x => x.Name.ToLower() == mealPlanName.ToLower()),
                };
                _context.MealPlanHandlers.Add(handler);
                _context.SaveChanges();
            }
        }

        public IActionResult CreateShopingList(List<int> recipesIds)
        {

            IngredientHandlers = _context.RecipesHandlers.Where(rh => recipesIds
                                                           .Contains(rh.Recipe.Id))
                                                           .Include(rh => rh.IngredientHandler)
                                                           .Include(rh => rh.IngredientHandler.Ingredient)
                                                           .Include(rh => rh.IngredientHandler.Measure)
                                                           .Include(rh => rh.IngredientHandler.Quantity)
                                                           .Select(x => x.IngredientHandler)
                                                           .ToList();

            var groupedIngredients = IngredientHandlers.GroupBy(ih => new { ih.Ingredient.Id, ih.Measure.UnitOfMeasurement })
                                                       .Select(g => new IngredientHandlerModel
                                                       {
                                                           Ingredient = g.First().Ingredient,
                                                           Measure = g.First().Measure,
                                                           Quantity = new Quantity { Quantitys = g.Sum(ih => ih.Quantity.Quantitys) }
                                                       })
                                                       .ToList();
            return PartialView("_shopingListPartialView", groupedIngredients);
        }
        public IActionResult SelectWeeklyRecipes()
        {
            var recipesFromDb = _context.Recipes.ToList();

            return PartialView("_SelectRecipeForWeeklyRecipePartialView", recipesFromDb);
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
