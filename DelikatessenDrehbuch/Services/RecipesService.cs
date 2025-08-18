using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Services.Interfaces;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.EntityFrameworkCore;
using NuGet.Packaging.Signing;
using Polly;
using System;

namespace DelikatessenDrehbuch.Services
{
    public class RecipesService : IRecipesService
    {
        private readonly ApplicationDbContext _context;
        private readonly IBlobAzureService _blobAzureService;
        private readonly IRecipesHandlerService _recipesHandlerService;
        private readonly IRecessionsService _recessionsService;
        private readonly IMeasureService _measureService;
        private readonly IQueryService _queryService;
        private readonly ILikeService _likeService;

        private readonly HelpfulMethods _helpfulMethods;


        public RecipesService(ApplicationDbContext context, IBlobAzureService blobAzureService,
                             IRecipesHandlerService recipesHandlerService, IRecessionsService recessionsService,
                             IMeasureService measureService, IQueryService queryService,
                             ILikeService likeService, HelpfulMethods helpfulMethods)
        {
            _context = context;
            _blobAzureService = blobAzureService;
            _recipesHandlerService = recipesHandlerService;
            _recessionsService = recessionsService;
            _measureService = measureService;
            _queryService = queryService;
            _likeService = likeService;
            _helpfulMethods = helpfulMethods;


        }



        public Recipes GetOneRendomRecipeFromIdList(List<int> ids)
        {
            Random rand = new Random();
            var randomId = ids.OrderBy(x => rand.Next()).Take(1).FirstOrDefault();
            return _context.Recipes.Single(x => x.Id == randomId);
        }

        public List<int> GetRecipesIdsByCategory(string category)
        {
            return _context.Recipes.Where(x => x.Category.ToLower().Trim() == category.ToLower().Trim())
                                                  .Select(x => x.Id).ToList();
        }

        public int GetRecipesCount()
        {
            return _context.Recipes.Count();
        }

        public async Task<Recipes> SaveRecipesInDbAsync(Recipes recipes)
        {
            var newRecipes = new Recipes()
            {
                Id = 0,
                OwnerEmail = "Delikatessen.drehbuch@outlook.com",
                Name = recipes.Name,
                Preparation = recipes.Preparation,
                Category = recipes.Category,
                PreparationTime = recipes.PreparationTime,
                Description = recipes.Description,
                RecipePersonCount = recipes.RecipePersonCount,
                LikeCount = 0,
                ImagePath = recipes.FormFile != null ? _blobAzureService.GetImagePathFromAzure(recipes.FormFile) : "",
                Calories = recipes.Calories,


            };

            var Id = await GetRecipeIdByNameAndPreperation(newRecipes.Name, newRecipes.Preparation);
            if (Id == 0)
            {
                _context.Recipes.Add(newRecipes);
                await _context.SaveChangesAsync();
            }


            return newRecipes;


        }

        public async Task<int> GetRecipeIdByNameAndPreperation(string recipeName, string preperation)
        {
            var recipesId = await _context.Recipes.Where(x => x.Preparation == preperation
                                                                  && x.Name == recipeName)
                                                  .Select(x => x.Id)
                                                  .FirstOrDefaultAsync();

            return recipesId;

        }

        public async Task<Recipes> GetRecipesFromDbByIdAsync(int id)
        {

            var recipe = await _context.Recipes.SingleOrDefaultAsync(x => x.Id == id);

            return recipe ?? throw new KeyNotFoundException($"Recipe width ID {id} not found.");
        }





        public async Task EditRecipesAsync(int recipeToChangeId, Recipes newRecipesData)
        {
            var recipeToChange = _context.Recipes.SingleOrDefault(x => x.Id == recipeToChangeId);

            if (recipeToChange == null)
                throw new KeyNotFoundException($"Recipe with ID {recipeToChange} not found.");

            recipeToChange.Name = newRecipesData.Name;
            recipeToChange.Category = newRecipesData.Category;
            recipeToChange.Description = newRecipesData.Description;
            recipeToChange.Preparation = newRecipesData.Preparation;
            recipeToChange.PreparationTime = newRecipesData.PreparationTime;
            recipeToChange.Calories = newRecipesData.Calories;
            if (newRecipesData.FormFile != null)
                recipeToChange.FormFile = newRecipesData.FormFile;



            await _context.SaveChangesAsync();
        }

        public async Task DeleteRecipesByIdAsync(int id)
        {

            var recipesFromDb = await GetRecipesFromDbByIdAsync(id);
            var recessionFromDb = _context.Recessions.Where(x => x.Recipes.Id == id);
            var queryHandlerFromDb = _context.QueryHandler.Where(x => x.Recipe.Id == id);
            var userPreverenceRecipeFromDb = _context.UserPreferencesRecipes.SingleOrDefault(x => x.Recipes.Id == id);



            if (userPreverenceRecipeFromDb != null)
                _context.UserPreferencesRecipes.Remove(userPreverenceRecipeFromDb);

            if (recessionFromDb != null)
                _context.RemoveRange(recessionFromDb);

            if (queryHandlerFromDb != null)
                _context.QueryHandler.RemoveRange(queryHandlerFromDb);


            _context.Remove(recipesFromDb);
            await _context.SaveChangesAsync();
        }

        public List<int> GetRendomRecipesIdsByCountFromIdListAsync(List<int> recipesIds, int count)
        {
            var random = new Random();
            return recipesIds.OrderBy(x => random.Next()).Take(count).ToList();
        }

        public async Task<FullRecipeData> GetFullRecipeDataByRecipesIdAsync(int id)
        {
            var recipeFromDb= await GetRecipesFromDbByIdAsync(id);
            var recipeHandler = await _recipesHandlerService.GetRecipesHandlerByRecipesIdAsync(id);
            FullRecipeData fullRecipeData = new()
            {
                Recipes = recipeFromDb,
                IngredientHandler = GetordetIngredientHandler(recipeHandler),
                Likes = await _likeService.GetLikesByRecipeIdAsync(id),
                Recession = await _recessionsService.GetRecessionsByRecipeIdFromDbAsync(id),
                Measure = await _measureService.GetMeasureFromDbAsync(),
               
            };
            return fullRecipeData;
           
        }

        private List<IngredientHandlerModel> GetordetIngredientHandler(List<RecipesHandler> recipesHandler)
        {
            var ingredienthandler = recipesHandler.Select(x => x.IngredientHandler);

            var recipes = recipesHandler.Select(x => x.Recipe).First();

            List<IngredientHandlerModel> scaledList = new();

            if (recipes.RecipePersonCount <= 1)
                return ingredienthandler.ToList();
            else
            {
                var presonCount = recipes.RecipePersonCount;

                foreach (var ingredientQuantity in ingredienthandler)
                {
                    decimal baseValue = (decimal)ingredientQuantity.Quantity?.Quantitys;
                    int personCountRaw = presonCount ?? 1;
                    int divisor = personCountRaw == 0 ? 1 : personCountRaw;


                    decimal raw = baseValue / divisor;


                    int decimals = (raw % 1m == 0m) ? 0 : 2;

                    if (ingredientQuantity.Quantity.Quantitys > 1 || ingredientQuantity.Measure.UnitOfMeasurement == "Stk.")
                    {
                        IngredientHandlerModel ing = new()
                        {
                            Ingredient = ingredientQuantity.Ingredient,
                            Measure = ingredientQuantity.Measure,
                            Quantity = new Quantity { Id = 0, Quantitys = Math.Round((double)raw, decimals, MidpointRounding.AwayFromZero) }
                        };

                        scaledList.Add(ing);
                    }
                    else
                        scaledList.Add(ingredientQuantity);

                }

                var ordertList = scaledList.OrderByDescending(x => x.Quantity.Quantitys).ToList();
                return ordertList;
            }

        }
    }
}
