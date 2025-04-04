using Azure.Storage.Blobs;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.MyExceptions;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;





public class TestResult
{
    public bool Result { get; set; }
    public string Value { get; set; }
}

namespace DelikatessenDrehbuch.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        private readonly AddRecipeException _myExceptions;
        private readonly HelpfulMethods _helpfulMethods;


        private readonly string _connectionString = "DefaultEndpointsProtocol=https;EndpointSuffix=core.windows.net;AccountName=blobdelikatessendrehbuch;AccountKey=NNJKin4e0NxZwD8XpLgZC+21vgcvkMd5tcp1gXiM4+zSAYV2DGDBx7unmFglQrs9YQH/RdtJIMME+AStw4Espg==;BlobEndpoint=https://blobdelikatessendrehbuch.blob.core.windows.net/;FileEndpoint=https://blobdelikatessendrehbuch.file.core.windows.net/;QueueEndpoint=https://blobdelikatessendrehbuch.queue.core.windows.net/;TableEndpoint=https://blobdelikatessendrehbuch.table.core.windows.net/";
        private readonly string _containerName = "picdelikatessendrehbuch";
        private readonly string _containerNameSmall = "blobsmalldelikatessendrehbuch";
        private readonly string _containerNameMedium = "blobmediumdelikatessendrehbuch";
        private readonly string _azureAcoutName = "blobdelikatessendrehbuch";

        public AdminController(ApplicationDbContext context, HelpfulMethods helpfulMethods, AddRecipeException myExceptions)
        {
            _context = context;
            _helpfulMethods = helpfulMethods;
            _myExceptions = myExceptions;



        }
        public IActionResult Index()
        {
            AdminControllerModel model = new()
            {
                SupportMessage = _context.SupportMessage.ToList(),
                Recipes = _context.Recipes.ToList(),
                UserCount = _context.Users.Count(),
                PremiumUser = _context.Users.Where(user => _context.UserRoles
                                                  .Any(ur => ur.UserId == user.Id && _context.Roles
                                                  .Any(r => r.Id == ur.RoleId && r.Name == "PremiumUser")))
                                              .Count()
            };

            return View(model);
        }









        public IActionResult AddNewRecipes()
        {


            return View(new NewRecipesMobileUpload());
        }

        #region BlobAzure_SaveImage
        public async Task UploadMsToAzureBlop(IFormFile file)
        {

            string blobName = $"{file.FileName}";


            BlobServiceClient blobServiceClient = new(_connectionString);
            BlobContainerClient container = blobServiceClient.GetBlobContainerClient(_containerName);
            BlobContainerClient smallContainer = blobServiceClient.GetBlobContainerClient(_containerNameSmall);
            BlobContainerClient mediumContainer = blobServiceClient.GetBlobContainerClient(_containerNameMedium);



            await Task.WhenAll(

                   ResizeAndUploadImage(container, blobName, 1024, 1024, "", file),
                   ResizeAndUploadImage(smallContainer, blobName, 313, 313, "_small", file),
                   ResizeAndUploadImage(mediumContainer, blobName, 600, 600, "_medium", file)
            );

        }

        private string GetImagePathFromAzure(IFormFile formFile)
        {
            string blobName = $"{formFile.FileName}";

            return $"https://{_azureAcoutName}.blob.core.windows.net/{_containerName}/{blobName}";
        }

        private static MemoryStream ResizeImageToWebP(SixLabors.ImageSharp.Image imageStream, int width, int height)
        {

            imageStream.Mutate(x => x.Resize(width, height));

            MemoryStream memoryStream = new()
            {
                Position = 0
            };

            imageStream.Save(memoryStream, new WebpEncoder { Quality = 80 });

            return new MemoryStream(memoryStream.ToArray());
        }
        private static async Task ResizeAndUploadImage(BlobContainerClient targetContainer, string fileName, int width, int height, string suffix, IFormFile file)
        {

            BlobClient resizedBlob = targetContainer.GetBlobClient(GetResizedFileName(fileName, suffix));

            using var ms = new MemoryStream();

            file.CopyTo(ms);
            ms.Position = 0;
            var byteArray = ms.ToArray();


            try
            {
                using var image = SixLabors.ImageSharp.Image.Load(byteArray);
                using var resizedStream = ResizeImageToWebP(image, width, height);


                BlobClient blob = targetContainer.GetBlobClient(fileName);
                await resizedBlob.UploadAsync(resizedStream, overwrite: true);


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

        }

        private static string GetResizedFileName(string originalFileName, string suffix)
        {
            return Path.GetFileNameWithoutExtension(originalFileName) + suffix + ".webp";
        }
        #endregion

        #region SaveNewRecipe_In_DB




        [ValidateAntiForgeryToken]
        public IActionResult SaveNewRecipes(NewRecipesMobileUpload newRecipes = null, [FromForm] EditRecipesModel recipes = null)
        {

            using (var transAction = _context.Database.BeginTransaction())
            {
                try
                {
                    if (!string.IsNullOrEmpty(newRecipes.Name))
                        CreateRecipesFromStringAsync(newRecipes);
                    else
                    {
                        EditeRecipes(recipes);
                    }

                    transAction.Commit();
                }
                catch (Exception ex)
                {
                    transAction.Rollback();
                    throw new Exception($"{_myExceptions.ErrorMessage} {ex}");
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
        private async Task CreateRecipesFromStringAsync(NewRecipesMobileUpload newRecipes)
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
                var melplanArry = newRecipes.MealPlan.Split(";");
                mealPlan = new()
                {
                    Name = melplanArry[0],
                    MyMealModel = _context.MyMealModel.Single(x => x.Id == Int32.Parse(melplanArry[1]))
                };
            }

            var ingredients = GetArrayFromString(newRecipes.Ingredients);

            foreach (var ingredient in ingredients)
            {
                if (!string.IsNullOrEmpty(ingredient))
                {
                    string[] ing = ingredient.Split("#");
                    IngredientHandlerModel ingredientHandler = new();
                    ingredientHandler.Id = 0;
                    ingredientHandler.Ingredient.Name = ing[2].Trim();
                    ingredientHandler.Measure.UnitOfMeasurement = ing[1].Trim();
                    ingredientHandler.Quantity.Quantitys = double.Parse(ing[0].Trim());

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
                await AddRecipesAsync(currentRecipe, mealPlan);

            }


        }
        public async Task AddRecipesAsync(FullRecipes newRecipes, MealPlan mealPlan = null)
        {
            MealPlanHandler mealPlanHandler = null;


            if (newRecipes.Recipes.FormFile != null)
                await UploadMsToAzureBlop(newRecipes.Recipes.FormFile);



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
                _myExceptions.ErrorMessage = "Fehler bei den Menüplan erstellung";

                var exist = _context.MealPlan.FirstOrDefault(x => x.Name.ToLower() == mealPlan.Name.ToLower());
                if (exist == null)
                    _context.MealPlan.Add(mealPlan);

                _context.SaveChanges();
                try
                {


                    mealPlanHandler = new()
                    {
                        Id = 0,
                        MealPlan = _context.MealPlan.SingleOrDefault(x => x.Name.ToLower() == mealPlan.Name.ToLower()),
                        Recipes = _context.Recipes.SingleOrDefault(x => x.Name.ToLower() == recipes.Name.ToLower() && x.Preparation.ToLower() == recipes.Preparation.ToLower())
                    };

                    _context.MealPlanHandler.Add(mealPlanHandler);
                    _context.SaveChanges();


                }

                catch (Exception ex)
                {

                    throw new Exception("Fehler bei den Menüplans", ex);

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
            DeleteQuerys(recipesFromDb.Id);

            recipesFromDb.Name = fullRecipes.Recipes.Name;
            recipesFromDb.Category = fullRecipes.Recipes.Category;
            recipesFromDb.Description = fullRecipes.Recipes.Description;
            recipesFromDb.Preparation = fullRecipes.Recipes.Preparation;
            recipesFromDb.PreparationTime = fullRecipes.Recipes.PreparationTime;
            recipesFromDb.Calories = fullRecipes.Recipes.Calories;
            if (fullRecipes.Recipes.FormFile != null)
                recipesFromDb.FormFile = fullRecipes.Recipes.FormFile;

            CreateQuaryHandler(recipesFromDb, fullRecipes.Querys.Split(",").Select(l => l.Trim()).ToList());

            _context.SaveChanges();
        }

        public void DeleteQuerys(int id)
        {
            var queryhandlerFromDb = _context.QueryHandler.Where(x => x.Recipe.Id == id).ToList();
            _context.RemoveRange(queryhandlerFromDb);
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


                RecipesHandler newHandler = new()
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
        private Quantity GetOrCreateQuantity(double quantity)
        {
            var quantityFromDb = _context.Quantities.SingleOrDefault(x => x.Quantitys == quantity);

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

            var queryList = _context.QueryHandler.Where(x => x.Recipe.Id == id).Select(x => x.Query.Query).ToList();
            EditRecipesModel editRecipesModel = new()
            {
                Recipes = recipeFromDb,
                IngredientHandler = ingredientHandlersFromDb,
                Measure = _context.Metrics.ToList(),
                Querys = string.Join(",", queryList),


            };

            ViewData.TemplateInfo.HtmlFieldPrefix = string.Empty;
            return View("EditRecipes", editRecipesModel);
        }
        public IActionResult AddIngredientRow(int id)
        {
            DropdownModel dropdownModel = new()
            {
                IngredientHandler = new(),
                Measure = _context.Metrics.ToList(),
                Index = id
            };


            return PartialView("_IngredientPartialViewEditRecipes", dropdownModel);
        }
        public IActionResult EditeRecipes(EditRecipesModel fullRecipes)
        {
            _myExceptions.ErrorMessage = "Bearbeiten des rezeptes Fehlgeschlagen";
            var recipesFromDb = _context.Recipes.FirstOrDefault(x => x.Id == fullRecipes.Recipes.Id);
            var recipeHandlersFromDb = _context.RecipesHandlers.Where(x => x.Recipe.Id == recipesFromDb.Id)
                                                                 .ToList();

            if (recipesFromDb == null)
                return BadRequest("Kein Rezept gefunden");


            EditRecipe(recipesFromDb, fullRecipes);
            DeleteReciphandlerFromDb(recipeHandlersFromDb);
            CreateRecipeHandler(recipesFromDb, fullRecipes.IngredientHandler);




            return RedirectToAction("Index");
        }





        public IActionResult AddNutrients()
        {
            return View();
        }

        public IActionResult AddNutrienHandlers(string Nutrients, string Ingredient)
        {
            var IngredientFromDb=_context.Ingredients.SingleOrDefault(x=>x.Name.ToLower()==Ingredient.ToLower().Trim());
            if (IngredientFromDb == null)
                return BadRequest("Zutat nicht in der Db");

            var nutrientsArray= Nutrients.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).ToArray();
            List<NutrienHandler> nutrienHandlers = new();

            for (int i = 0; i < nutrientsArray.Length; i++)
            {
                var nutrient = nutrientsArray[i].Split("#").ToArray();

                NutrienHandler nutrienHandler = new()
                {
                    Ingredient = IngredientFromDb,
                    Nutrients = GetOrCreateNutrients(nutrient[0]),
                    Quantity = GetOrCreateQuantity(double.Parse(nutrient[1])),
                    Metrics = GetorCreateMeasure(nutrient[2])
                };

                nutrienHandlers.Add(nutrienHandler);
               
            }
            var nutrientFromDb = _context.NutrienHandler.SingleOrDefault(x => x.Nutrients.Nutrient.ToLower() == Nutrients.ToLower());

            if(nutrientFromDb==null)
            {
                _context.NutrienHandler.AddRange(nutrienHandlers);
                _context.SaveChanges();
            }
            else
            {
                BadRequest("Zutat schon Erledigt");
            }
           

          return RedirectToAction("Index");
        }

        private Nutrients GetOrCreateNutrients(string nutrient)
        {
            var NutrientsFromDb=_context.Nutrients.SingleOrDefault(x=>x.Nutrient.ToLower().Trim()==nutrient.ToLower().Trim());

            if (NutrientsFromDb == null)
                throw new Exception($"Nutrient mit den Namen: {nutrient} existierst nicht.");

            return NutrientsFromDb;
        }


        // [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any, NoStore = false)]
        public IActionResult AdditOrDeliteRecipePartialView(string query)
        {
            // string cacheKey = $"{User.Identity.Name}_handlers_{query?.ToLower()}";
            List<Recipes> recipes = new();
            var isNuber = int.TryParse(query, out int recipeId);
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

