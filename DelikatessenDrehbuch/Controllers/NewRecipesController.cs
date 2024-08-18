using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using DelikatessenDrehbuch.Data;

using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.WebEncoders.Testing;
using Microsoft.Extensions.Logging;

using NuGet.Packaging;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;

namespace DelikatessenDrehbuch.Controllers
{
    [Authorize]
    public class NewRecipesController : Controller
    {
        //Azure storage Adresse und Container Name
        private readonly string _connectionString = "DefaultEndpointsProtocol=https;EndpointSuffix=core.windows.net;AccountName=blobdelikatessendrehbuch;AccountKey=NNJKin4e0NxZwD8XpLgZC+21vgcvkMd5tcp1gXiM4+zSAYV2DGDBx7unmFglQrs9YQH/RdtJIMME+AStw4Espg==;BlobEndpoint=https://blobdelikatessendrehbuch.blob.core.windows.net/;FileEndpoint=https://blobdelikatessendrehbuch.file.core.windows.net/;QueueEndpoint=https://blobdelikatessendrehbuch.queue.core.windows.net/;TableEndpoint=https://blobdelikatessendrehbuch.table.core.windows.net/";
        private readonly string _containerName = "picdelikatessendrehbuch";
        private readonly string _azureAcoutName = "blobdelikatessendrehbuch";


        private readonly ILogger<NewRecipesController> _logger;
        private readonly ApplicationDbContext _dbContext;
        private readonly HelpfulMethods _helpfulMethods;
        public NewRecipesController(ILogger<NewRecipesController> logger, ApplicationDbContext context, HelpfulMethods helpfulMethods)
        {
            _logger = logger;
            _dbContext = context;
            _helpfulMethods = helpfulMethods;

        }


        public IActionResult CreateFolder()
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string folderPath = Path.Combine(desktopPath, "Recipes");
            string recipesNeuPath = Path.Combine(folderPath, "Rezepte Neu");

            string txtPath = Path.Combine(folderPath, "RezeptNamen.txt");

            string[] lines = System.IO.File.ReadAllLines(txtPath, Encoding.UTF8);

            foreach (var line in lines)
            {
                string path = Path.Combine(recipesNeuPath, line);
                Directory.CreateDirectory(path);
                System.IO.File.Create(Path.Combine(path, "recipes.txt"));
            }

            return RedirectToAction("Index");
        }

        // public IActionResult CreateRechipes()
        //{

        //    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        //    string folderPath = Path.Combine(desktopPath, "Recipes");
        //    string imageFolderPath = Path.Combine(folderPath, "Images");
        //    string filePath = Path.Combine(imageFolderPath, "recipes.txt");

        //    var imageFiles = Directory.GetFiles(imageFolderPath, "*", SearchOption.TopDirectoryOnly)
        //                        .Where(x => x.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)).ToList();

        //    List<IFormFile> formFiles = new List<IFormFile>();
        //    var mimeTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        //{
        //    { ".jpg", "image/jpeg" },   // Einheitlich als image/jpeg
        //    { ".jpeg", "image/jpeg" },
        //    { ".png", "image/png" }
        //};
        //    foreach (var imageFile in imageFiles)
        //    {
        //        var fileInfo = new FileInfo(imageFile);
        //        if (fileInfo.Name.Contains("_"))
        //        {
        //            fileInfo.Name.Replace("_", " ");
        //        }

        //        string extension = fileInfo.Extension.ToLower();
        //        string contentType = mimeTypes.ContainsKey(extension) ? mimeTypes[extension] : "application/octet-stream";

        //        using (var fileStream = new FileStream(imageFile, FileMode.Open, FileAccess.Read))
        //        {
        //            var memoryStream = new MemoryStream();
        //            fileStream.CopyTo(memoryStream);
        //            memoryStream.Position = 0;

        //            var formFile = new FormFile(memoryStream, 0, memoryStream.Length, "Recipes.FormFile", fileInfo.Name)
        //            {
        //                Headers = new HeaderDictionary(),
        //                ContentType = contentType
        //            };
        //            formFile.Headers.Add("Content-Disposition", $"form-data; name=\"Recipes.FormFile\"; filename=\"{fileInfo.Name}\"");
        //            formFiles.Add(formFile);
        //        }
        //    }


        //    List<FullRecipes> recipes = new List<FullRecipes>();
        //    FullRecipes currentRecipe = null;
        //    string[] lines = System.IO.File.ReadAllLines(filePath, Encoding.UTF8);

        //    foreach (string line in lines)
        //    {
        //        if (line == "Rezept")
        //        {
        //            currentRecipe = new FullRecipes();
        //            currentRecipe.Recipes.OwnerEmail = "delikatessen.drehbuch@outlook.com";
        //            recipes.Add(currentRecipe);

        //        }
        //        if (line.StartsWith("Id:"))
        //        {

        //            currentRecipe.Recipes.Id = int.Parse(line.Split(':')[1].Trim());
        //        }
        //        else if (line.StartsWith("Category:"))
        //        {
        //            currentRecipe.Recipes.Category = line.Split(':')[1].Trim();
        //        }
        //        else if (line.StartsWith("Name:"))
        //        {
        //            currentRecipe.Recipes.Name = line.Split(':')[1].Trim();
        //        }
        //        else if (line.StartsWith("Preparation:"))
        //        {
        //            currentRecipe.Recipes.Preparation = line.Split(':')[1].Trim();
        //        }

        //        else if (line.StartsWith("Description:"))
        //        {
        //            currentRecipe.Recipes.Description = line.Split(':')[1].Trim();
        //        }

        //        else if (line.StartsWith("PreparationTime:"))
        //        {
        //            var test = line.Split(":")[1].Trim();
        //            currentRecipe.Recipes.PreparationTime = int.Parse(line.Split(':')[1].Trim());
        //        }
        //        if (line.StartsWith("Zutaten"))
        //        {
        //            string[] ing = line.Split(":");
        //            IngredientHandlerModel ingredientHandler = new IngredientHandlerModel();
        //            ingredientHandler.Id = 0;
        //            ingredientHandler.Ingredient.Name = ing[1].Trim();
        //            ingredientHandler.Measure.UnitOfMeasurement = ing[2].Trim();
        //            ingredientHandler.Quantity.Quantitys = float.Parse(ing[3].Trim());

        //            currentRecipe.IngredientHandler.Add(ingredientHandler);
        //        }
        //        if (line.StartsWith("Query:"))
        //        {
        //            currentRecipe.QueryHandler.Add(line.Split(':')[1].Trim());
        //        }
        //        currentRecipe.Recipes.FormFile = formFiles.FirstOrDefault(x => x.FileName.Equals($"{currentRecipe.Recipes.Name}.jpg"));


        //    }

        //    foreach (var fullrecipes in recipes)
        //    {
        //        var ifExist = _dbContext.Recipes.SingleOrDefault(x => x.Name == fullrecipes.Recipes.Name && x.Preparation == fullrecipes.Recipes.Preparation);
        //        if (ifExist == null)
        //        {
        //            AddRecipes(fullrecipes);
        //        }

        //    }

        //    return RedirectToAction("Index");
        //}


        public IActionResult CreateRecipes()
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string folderPath = Path.Combine(desktopPath, "Recipes/Rezepte Neu");

            var direktoryInfo = Directory.GetDirectories(folderPath);
           
            FullRecipes currentRecipe = null;
            foreach (var directory in direktoryInfo)
            {
                var subDirectory = Path.Combine(folderPath, directory);
                var lines = System.IO.File.ReadAllLines(Path.Combine(subDirectory, "rexipes.txt"), Encoding.UTF8);
                var image = Directory.GetFiles(subDirectory, "*.webp").FirstOrDefault();
                string newPath = "";
                if (image != null)
                {
                    var x=Path.GetFileNameWithoutExtension(image);
                    x = x + ".jpg";
                     newPath = Path.Combine(subDirectory,x );
                   System.IO.File.Move(image, newPath);
                }
                var jpgPath = Directory.GetFiles(subDirectory, "*.jpg").FirstOrDefault();
                var fileInfo = new FileInfo(jpgPath);
                IFormFile file= null;
                using (var fileStream = new FileStream(jpgPath, FileMode.Open, FileAccess.Read))
                {
                    var memoryStream = new MemoryStream();
                    fileStream.CopyTo(memoryStream);
                    memoryStream.Position = 0;

                    var formFile = new FormFile(memoryStream, 0, memoryStream.Length, "Recipes.FormFile", fileInfo.Name)
                    {
                        Headers = new HeaderDictionary(),
                       
                    };
                    formFile.Headers.Add("Content-Disposition", $"form-data; name=\"Recipes.FormFile\"; filename=\"{fileInfo.Name}\"");
                    file= formFile;
                }

                foreach (string line in lines)
                {
                    if (line == "Rezept")
                    {
                        currentRecipe = new FullRecipes();
                        currentRecipe.Recipes.OwnerEmail = "delikatessen.drehbuch@outlook.com";
                       

                    }
                    if (line.StartsWith("Id:"))
                    {

                        currentRecipe.Recipes.Id = int.Parse(line.Split(':')[1].Trim());
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
                    if (line.StartsWith("Zutaten"))
                    {
                        string[] ing = line.Split(":");
                        IngredientHandlerModel ingredientHandler = new IngredientHandlerModel();
                        ingredientHandler.Id = 0;
                        ingredientHandler.Ingredient.Name = ing[1].Trim();
                        ingredientHandler.Measure.UnitOfMeasurement = ing[2].Trim();
                        ingredientHandler.Quantity.Quantitys = float.Parse(ing[3].Trim());

                        currentRecipe.IngredientHandler.Add(ingredientHandler);
                    }
                    if (line.StartsWith("Query:"))
                    {
                        currentRecipe.QueryHandler.Add(line.Split(':')[1].Trim());
                    }
                    currentRecipe.Recipes.FormFile = file;

                    

                }

                var ifExist = _dbContext.Recipes.SingleOrDefault(x => x.Name == currentRecipe.Recipes.Name && x.Preparation == currentRecipe.Recipes.Preparation);
                if (ifExist == null)
                {
                    AddRecipes(currentRecipe);
                }
            }
            return RedirectToAction("Index");
        }

        private Recipes GetRecipeFromDb(Recipes recipes)
        {
            var recipeFromDb = _dbContext.Recipes.FirstOrDefault(x => x.Name == recipes.Name
                                                           && x.Preparation == recipes.Preparation
                                                           && x.OwnerEmail == recipes.OwnerEmail
                                                           );


            if (recipeFromDb == null)
                recipeFromDb = new Recipes();


            return recipeFromDb;
        }
        public IActionResult Index(int id)
        {
            if (id == 0)
            {
                var fullRecipes = new FullRecipes();
                fullRecipes.Querys = _dbContext.Querys.ToList();
                return View(fullRecipes);
            }
            else
            {
                var fullRecipes = _helpfulMethods.GetFullRecipeById(_dbContext, id);
                return View(fullRecipes);
            }

        }

        public IActionResult EditRecipesPartialViewAsync(int id)
        {
            return PartialView("_IngredientPartialView", _helpfulMethods.GetDropdownModel(_dbContext, id));
        }

        public IActionResult BlankSentenceAsync()
        {
            return PartialView("_IngredientPartialView", _helpfulMethods.GetDropdownModel(_dbContext));
        }

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
                LikeCount = 0,
                ImagePath = newRecipes.Recipes.FormFile != null ? GetImagePathFromAzure(newRecipes.Recipes.FormFile) : "",


            };


            _dbContext.Recipes.Add(recipes);
            _dbContext.SaveChanges();

            CreateRecipesHandler(newRecipes);
            CreateQuaryHandler(recipes, newRecipes.QueryHandler);

        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("NewRecipes/AddOrEditRecipes")]
        public IActionResult AddOrEditRecipes([FromForm] FullRecipes newRecipes)
        {
            var recipeFromDb = _dbContext.Recipes.SingleOrDefault(x => x.Id == newRecipes.Recipes.Id);

            if (newRecipes.Recipes.FormFile != null)
                UploadMsToAzureBlop(newRecipes.Recipes.FormFile);


            if (recipeFromDb == null)
            {
                var recipes = new Recipes()
                {
                    Id = 0,
                    OwnerEmail = User.Identity.Name,
                    Name = newRecipes.Recipes.Name,
                    Preparation = newRecipes.Recipes.Preparation,
                    Category = newRecipes.Recipes.Category,
                    LikeCount = 0,
                    ImagePath = newRecipes.Recipes.FormFile != null ? GetImagePathFromAzure(newRecipes.Recipes.FormFile) : "",


                };

                try
                {
                    _dbContext.Recipes.Add(recipes);
                    _dbContext.SaveChanges();

                    CreateRecipesHandler(newRecipes, recipes);
                    CreateQuaryHandler(recipes, newRecipes.QueryHandler);

                }
                catch (Exception ex)
                {
                    _logger.LogError($"A Error by adsing new Recipes {ex.ToString()}");
                    return StatusCode(500, "Internal server error");
                }


            }
            else
                EditRecipes(recipeFromDb, newRecipes);



            newRecipes.Recipes.OwnerEmail = User.Identity.Name;




            return Json(new { redirect = Url.Action("Index", "Home") });
        }
        private void EditRecipes(Recipes recipeFromDb, FullRecipes editedRecipes)
        {
            recipeFromDb.Name = editedRecipes.Recipes.Name;
            recipeFromDb.Category = editedRecipes.Recipes.Category;
            recipeFromDb.ImagePath = editedRecipes.Recipes.FormFile == null ? recipeFromDb.ImagePath : GetImagePathFromAzure(editedRecipes.Recipes.FormFile);
            recipeFromDb.Preparation = editedRecipes.Recipes.Preparation;

            RemoveOldRecipeHandler(editedRecipes, recipeFromDb);
            CreateRecipesHandler(editedRecipes, recipeFromDb);
            CreateQuaryHandler(recipeFromDb, editedRecipes.QueryHandler);
        }
        private void RemoveOldRecipeHandler(FullRecipes newRecipes, Recipes recipesFromDb)
        {


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



        }
        private void RemoveOldQueryHandler(Recipes recipesFromDb)
        {
            var queryHandlerFromDb = _dbContext.QueryHandler.Where(x => x.Recipe == recipesFromDb).ToList();
            foreach (var queryHandler in queryHandlerFromDb)
            {
                _dbContext.Remove(queryHandler);

            }
        }
        private void CreateQuaryHandler(Recipes recipes, List<string> queryHandlers)
        {
            //var recipesFromDb = new Recipes();
            //if (recipes.Id == 0)
            //    recipesFromDb = GetRecipeFromDb(recipes);
            //else
            //{
            //    recipesFromDb = recipes;
            //    RemoveOldQueryHandler(recipesFromDb);
            //}

            var querysFromDb = _dbContext.Querys.ToList();

            foreach (var queryHandler in queryHandlers)
            {
                if (!querysFromDb.Select(x => x.Query.ToLower()).Contains(queryHandler.ToLower()))
                {
                    var query = new Querys()
                    {
                        Id = 0,
                        Query = queryHandler,
                    };

                    _dbContext.Querys.Add(query);
                }
            }
            _dbContext.SaveChanges();

            var querysFromDbNew = _dbContext.Querys.ToList();

            foreach (var query in queryHandlers)
            {
                if (querysFromDbNew.Select(x => x.Query.ToLower()).Contains(query.ToLower()))
                {
                    var quaryHandler = new QueryHandler()
                    {
                        Id = 0,
                        Recipe = recipes,
                        Query = _dbContext.Querys.SingleOrDefault(x => x.Query.ToLower() == query.ToLower()),
                    };

                    _dbContext.QueryHandler.Add(quaryHandler);
                }

            }

            _dbContext.SaveChanges();

        }

        private void CreateRecipesHandler(FullRecipes newRecipes, Recipes recipesFromDb = null)
        {
            //Entferne die Doppelten
            newRecipes.IngredientHandler = newRecipes.IngredientHandler.DistinctBy(x => x.Ingredient.Name.ToLower()).ToList();

            Recipes? myRecipes = new();

            if (recipesFromDb != null)
                myRecipes = recipesFromDb;
            else
                myRecipes = GetRecipeFromDb(newRecipes.Recipes);


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
            var measureFromDb = _dbContext.Metrics.FirstOrDefault(x => x.UnitOfMeasurement.ToLower() == measure.UnitOfMeasurement.ToLower().Trim());

            if (measureFromDb != null)
                return measureFromDb;
            else
            {
                measureFromDb = new Measure()
                {
                    Id = 0,
                    UnitOfMeasurement = measure.UnitOfMeasurement
                };
                _dbContext.Metrics.Add(measureFromDb);
                _dbContext.SaveChanges();

                return measure;
            }
        }

        #endregion
    }
}
