using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using Microsoft.EntityFrameworkCore;

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
            FullRecipes fullRecipes = new()
            {
                Recipes = recipeFromDb,
                IngredientHandler = recipHandlerFromDb.Select(x => x.IngredientHandler).ToList(),
                Likes = dbContext.Likes.Where(x => x.Recipe == recipeFromDb).ToList(),
                Recession = dbContext.Recessions.Where(x => x.Recipes == recipeFromDb).ToList(),
                Measure = dbContext.Metrics.ToList(),
                QueryHandler = dbContext.QueryHandler.Where(x => x.Recipe == recipeFromDb)
                                                             .Select(x => x.Query.Query).ToList(),
                Querys = dbContext.Querys.ToList()
            };
            return fullRecipes;
        }

        public void CreateUserPreferencesRecipe(int id, string email, ApplicationDbContext context)
        {
            if (string.IsNullOrEmpty(email))
                throw new KeyNotFoundException($"Email {email} not found.");


            var recipeFromDb = GetRecipeFromDbById(context, id) ?? throw new KeyNotFoundException($"Recipe with ID {id} not found.");

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

        public DropdownModel GetDropdownModel(ApplicationDbContext context, int id = 0)
        {
            var metricsFromDb = context.Metrics.ToList();
            var ingredientHandlerFromDb = context.IngredientHandlers.Include(x => x.Ingredient).Include(x => x.Measure).Include(x => x.Quantity).SingleOrDefault(x => x.Id == id);

            DropdownModel dropdownModel = new()
            {
                Measure = metricsFromDb
            };
            if (ingredientHandlerFromDb != null)
                dropdownModel.IngredientHandler = ingredientHandlerFromDb;
            else
                dropdownModel.IngredientHandler = new();
            return dropdownModel;
        }

        public Recipes GetRecipeFromDbById(ApplicationDbContext context, int id)
        {

            var recipe = context.Recipes.SingleOrDefault(x => x.Id == id);

            return recipe ?? throw new KeyNotFoundException($"Recipe with ID {id} not found.");
        }

        public List<UserPreferencesQuery> GetUserPreferencesQueryListByEmail(ApplicationDbContext context, string email)
        {
            return context.UserPreferencesQuerys.Where(x => x.UserEmail == email).ToList();
        }

        public List<string> GetQueryListFromDb(ApplicationDbContext context)
        {
            return context.Querys.Select(x => x.Query).ToList();
        }

        public static string CaseInsensitive(string encodetString)
        {
            var name = encodetString.ToLowerInvariant()
                                    .Replace("ä", "ae")
                                    .Replace("ö", "oe")
                                    .Replace("ü", "ue")
                                    .Replace("ß", "ss")
                                    .Replace(" ", "-")
                                    .Replace("&", "und")
                                    .Replace("?", "")
                                    .Replace("!", "")
                                    .Replace(",", "")
                                    .Replace(".", "")
                                    .Replace(":", "")
                                    .Replace(";", "");

            return name;
        }
    }



}
