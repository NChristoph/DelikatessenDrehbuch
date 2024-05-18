using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Data.Migrations;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.WebEncoders.Testing;
using NuGet.Packaging;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace DelikatessenDrehbuch.Controllers
{
    [Authorize]


    public class NewRecipesController : Controller
    {
        //Azure storage Adresse und Container Name
        private readonly string _connectionString = "DefaultEndpointsProtocol=https;EndpointSuffix=core.windows.net;AccountName=blobdelikatessendrehbuch;AccountKey=NNJKin4e0NxZwD8XpLgZC+21vgcvkMd5tcp1gXiM4+zSAYV2DGDBx7unmFglQrs9YQH/RdtJIMME+AStw4Espg==;BlobEndpoint=https://blobdelikatessendrehbuch.blob.core.windows.net/;FileEndpoint=https://blobdelikatessendrehbuch.file.core.windows.net/;QueueEndpoint=https://blobdelikatessendrehbuch.queue.core.windows.net/;TableEndpoint=https://blobdelikatessendrehbuch.table.core.windows.net/";
        private readonly string _containerName = "picdelikatessendrehbuch";
        private readonly string _azureAcoutName = "blobdelikatessendrehbuch";



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


        public void UploadMsToAzureBlop(IFormFile file)
        {

            string blobName = $"{file.FileName}";


            // Get a reference to a container named "sample-container" and then create it
            BlobContainerClient container = new BlobContainerClient(_connectionString, _containerName);
            //container.Create();

            // Get a reference to a blob named "sample-file" in a container named "sample-container"
            BlobClient blob = container.GetBlobClient(blobName);

            bool blobExist =  blob.Exists();

            if (blobExist)
                return;

            if (file != null)
            {
                

                using (var ms = new MemoryStream())
                {
                    file.CopyTo(ms);
                    var byteArry = ms.ToArray();
                    // Upload local file

                    blob.Upload(new BinaryData(byteArry));
                }
            }
           
        }

        private string GetImagePathFromAzure(IFormFile formFile)
        {
            string blobName = $"{formFile.FileName}";

            return $"https://{_azureAcoutName}.blob.core.windows.net/{_containerName}/{blobName}";
        }

        private void AddOrEditRecipeType(Recipetype recipetype)
        {

        }

        [HttpPost]
        public IActionResult AddOrEditRecipes(FullRecipes newRecipes)
        {
            if (newRecipes.Recipes.FormFile != null)
                UploadMsToAzureBlop(newRecipes.Recipes.FormFile);

            newRecipes.Recipes.OwnerEmail = User.Identity.Name;

            

            var recipeFromDb = _dbContext.Recipes.SingleOrDefault(x => x.Id == newRecipes.Recipes.Id);

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


                _dbContext.Add(recipes);
                _dbContext.SaveChanges();
                CreateRecipesHandler(newRecipes);

            }
            else
            {
                recipeFromDb.Name = newRecipes.Recipes.Name;
                recipeFromDb.Category = newRecipes.Recipes.Category;
                recipeFromDb.ImagePath = newRecipes.Recipes.FormFile == null ? recipeFromDb.ImagePath:GetImagePathFromAzure(newRecipes.Recipes.FormFile) ;
                recipeFromDb.Preparation = newRecipes.Recipes.Preparation;
              

                _dbContext.SaveChanges();
                EditRecipes(newRecipes, recipeFromDb);
            }

            return Json(new { redirect = Url.Action("Index", "Home") });
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

        private void CreateRecipesHandler(FullRecipes newRecipes, Recipes recipesFromDb = null)
        {
            //Entferne die Doppelten
            newRecipes.IngredientHandler = newRecipes.IngredientHandler.DistinctBy(x => x.Ingredient.Name.ToLower()).ToList();

            Recipes? myRecipes = new();

            if (recipesFromDb != null)
                myRecipes = recipesFromDb;
            else
                myRecipes = _dbContext.Recipes.FirstOrDefault(x => x.Name == newRecipes.Recipes.Name
                                                           && x.Preparation == newRecipes.Recipes.Preparation
                                                           && x.OwnerEmail == newRecipes.Recipes.OwnerEmail
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
            var measureFromDb = _dbContext.Metrics.FirstOrDefault(x => x.UnitOfMeasurement.ToLower() == measure.UnitOfMeasurement.ToLower().Trim());
            return measureFromDb;
        }

        #endregion
    }
}
