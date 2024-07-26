using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using Microsoft.EntityFrameworkCore;
using SQLitePCL;

namespace DelikatessenDrehbuch.StaticScripts
{
    public class HelpfulMethods
    {
        public FullRecipes GetFullRecipeById(ApplicationDbContext dbContext, int recipeId)
        {
            var recipeFromDb = dbContext.Recipes.SingleOrDefault(x => x.Id == recipeId);
            var recipHandlerFromDb = dbContext.RecipesHandlers.Where(x => x.Recipe == recipeFromDb)
                                                             .Include(x => x.IngredientHandler.Ingredient)
                                                             .Include(x => x.IngredientHandler.Measure)
                                                             .Include(x => x.IngredientHandler.Quantity)
                                                             .ToList();
            FullRecipes fullRecipes = new FullRecipes();
            fullRecipes.Recipes = recipeFromDb;
            fullRecipes.IngredientHandler = recipHandlerFromDb.Select(x => x.IngredientHandler).ToList();
            fullRecipes.Likes = dbContext.Likes.Where(x => x.Recipe == recipeFromDb).ToList();
            fullRecipes.Recession = dbContext.Recessions.Where(x => x.Recipes == recipeFromDb).ToList();
            fullRecipes.Measure = dbContext.Metrics.ToList();
            fullRecipes.QueryHandler = dbContext.QueryHandler.Where(x => x.Recipe == recipeFromDb)
                                                             .Select(x => x.Query.Query).ToList();
            fullRecipes.Querys = dbContext.Querys.ToList();
            return fullRecipes;
        }

        public async void CreateUserPreferencesRecipe(int id, string email, ApplicationDbContext context)
        {
            if (string.IsNullOrEmpty(email))
                throw new KeyNotFoundException($"Email {email} not found.");


            var recipeFromDb = await GetRecipeFromDbById(context,id);

            if (recipeFromDb == null)
                throw new KeyNotFoundException($"Recipe with ID {id} not found.");



            var userPreferenceRecipeFromDb = await context.UserPreferencesRecipes.SingleOrDefaultAsync(x => x.Recipes == recipeFromDb
                                                             && x.UserEmail == email);

            if (userPreferenceRecipeFromDb != null)
                return;

            var UserPreferenceRecipe = new UserPreferencesRecipe()
            {
                Id = 0,
                Recipes = recipeFromDb,
                UserEmail = email,
            };


           await context.UserPreferencesRecipes.AddAsync(UserPreferenceRecipe);
           await context.SaveChangesAsync();

        }


        public void CreateUserPreferencesQuery(string email, string query, ApplicationDbContext context)
        {
            var userPreferencesQueryFromDb = context.UserPreferencesQuerys.Where(x => x.UserEmail == email).ToList();
            UserPreferencesQuery userPreferencesQuery = null;

            if (string.IsNullOrEmpty(query))
                throw new KeyNotFoundException($" Query was empty");


            if (!userPreferencesQueryFromDb.Any())
            {
                userPreferencesQuery = GetUserPreferenceQuery(email, query, context);
                context.UserPreferencesQuerys.Add(userPreferencesQuery);
            }
            else
            {
                userPreferencesQuery = userPreferencesQueryFromDb.SingleOrDefault(x => x.Query.ToLower() == query.ToLower());
                if (userPreferencesQuery != null)
                    userPreferencesQuery.Count++;
                else
                {
                    userPreferencesQuery = GetUserPreferenceQuery(email, query, context);
                    context.UserPreferencesQuerys.Add(userPreferencesQuery);
                }


            }


            context.SaveChanges();


        }

        private UserPreferencesQuery GetUserPreferenceQuery(string email, string query, ApplicationDbContext context)
        {
            var userPreferencesQuery = new UserPreferencesQuery()
            {
                Id = 0,
                UserEmail = email,
                Query = query,
                Count = 0,

            };

            return userPreferencesQuery;
        }

        public async Task<DropdownModel> GetDropdownModel(ApplicationDbContext context,int id=0)
        {
            var metricsFromDb = context.Metrics.ToList();
            var ingredientHandlerFromDb = await context.IngredientHandlers.Include(x => x.Ingredient).Include(x => x.Measure).Include(x => x.Quantity).SingleOrDefaultAsync(x => x.Id == id);

            DropdownModel dropdownModel = new DropdownModel();
            dropdownModel.Measure = metricsFromDb;
            if (ingredientHandlerFromDb != null)
                dropdownModel.IngredientHandler = ingredientHandlerFromDb;
            else
                dropdownModel.IngredientHandler = new();
            return dropdownModel;
        }

        public async Task<Recipes> GetRecipeFromDbById(ApplicationDbContext context,int id)
        {
            
            var recipe = await context.Recipes.SingleOrDefaultAsync(x => x.Id == id);

            if(recipe == null)
                throw new KeyNotFoundException($"Recipe with ID {id} not found.");

            return recipe;

        }
    }



}
