using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using Microsoft.EntityFrameworkCore;
using SQLitePCL;

namespace DelikatessenDrehbuch.StaticScripts
{
    public static class HelpfulMethods
    {
        public static FullRecipes GetFullRecipeById(ApplicationDbContext dbContext, int recipeId)
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

        public static void CreateUserPreferencesRecipe(int id, string email, ApplicationDbContext context)
        {
            if (string.IsNullOrEmpty(email))
                return;

            var recipeFromDb = context.Recipes.SingleOrDefault(x => x.Id == id);

            if (recipeFromDb == null)
                return;


            var userPreferenceRecipeFromDb = context.UserPreferencesRecipes.SingleOrDefault(x => x.Recipes == recipeFromDb
                                                             && x.UserEmail == email);

            if (userPreferenceRecipeFromDb != null)
                return;

            var UserPreferenceRecipe = new UserPreferencesRecipe()
            {
                Id = 0,
                Recipes = recipeFromDb,
                UserEmail = email,
            };


            context.UserPreferencesRecipes.Add(UserPreferenceRecipe);
            context.SaveChanges();

        }


        public static void CreateUserPreferencesQuery( string email, string query, ApplicationDbContext context)
        {
            var userPreferencesQueryFromDb = context.UserPreferencesQuerys.Where(x => x.UserEmail == email).ToList();
            UserPreferencesQuery userPreferencesQuery = null;

            if (string.IsNullOrEmpty(query))
                return;

            if (!userPreferencesQueryFromDb.Any())
            {
                userPreferencesQuery = GetUserPreferenceQuery(email, query, context);
                context.UserPreferencesQuerys.Add(userPreferencesQuery);
            }
            else
            {
                userPreferencesQuery = userPreferencesQueryFromDb.SingleOrDefault(x=>x.Query.ToLower()== query.ToLower());
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

        private static UserPreferencesQuery GetUserPreferenceQuery(string email, string query, ApplicationDbContext context)
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


    }



}
