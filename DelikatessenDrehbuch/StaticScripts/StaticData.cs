using DelikatessenDrehbuch.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;

namespace DelikatessenDrehbuch.StaticScripts
{
    public static class StaticData
    {
        // 🟢 Statische Listen für Ernährungsarten
        public static List<int> VeganRecipeIds { get; private set; } = new();
        public static List<int> VegetarianRecipeIds { get; private set; } = new();
        public static List<int> MeatRecipeIds { get; private set; } = new();
        public static List<int> FishRecipeIds { get; private set; } = new();
        public static List<int> PorkRecipeIds { get; private set; } = new();
        public static List<int> CookingTimeOne { get; private set; } = new();
        public static List<int> MainMeals { get; private set; } = new();
        public static List<int> Appetizers { get; private set; } = new();
        public static List<int> Desserts { get; private set; } = new();
        public static Dictionary<int, List<int>> RecipeAndIngredients { get; private set; } = new();
        public static Dictionary<int, List<int>> RecipeAndIngredHandlers { get; private set; } = new();
        public static Dictionary<int, List<int>> RecipeAndNutrients { get; private set; } = new();


        private static bool _isLoaded = false;
        private static readonly object _lock = new();

        private static async Task<List<int>> GetRecipeIdsByQuerysAsync(string category, ApplicationDbContext context)
        {
            var normalizedCategory = category.ToLower();

            var recipeIds = await context.QueryHandler
                                         .AsNoTracking()
                                         .Where(x => x.Query.Query.ToLower() == normalizedCategory)
                                         .Select(x => x.Recipe.Id)
                                         .ToListAsync();


            return recipeIds.Distinct().ToList();
        }

        private static async Task<List<int>> GetRecipeIdsByCategoryAsync(string category, ApplicationDbContext context)
        {


            var recipeIds = await context.Recipes
                                         .AsNoTracking()
                                         .Where(x => x.Category == category)
                                         .Select(x => x.Id)
                                         .ToListAsync();


            return recipeIds.Distinct().ToList();
        }

        private static async Task<List<int>> GetRecipeIdsByCookingTimeAsync(int preperationTime, ApplicationDbContext context)
        {


            var recipeIds = await context.Recipes
                                         .AsNoTracking()
                                         .Where(x => x.PreparationTime <= preperationTime)
                                         .Select(x => x.Id)
                                         .ToListAsync();


            return recipeIds.Distinct().ToList();
        }

        private static async Task<Dictionary<int, List<int>>> GetRecipeAndIngredients(ApplicationDbContext context)
        {
            var list = await context.RecipesHandlers
                                    .AsNoTracking()
                                    .Select(x => new
                                    {
                                        RecipeId = x.Recipe.Id,
                                        IngredientId = x.IngredientHandler.Ingredient.Id
                                    })
                                    .ToListAsync();

            var dic = list.GroupBy(x => x.RecipeId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.IngredientId).Distinct().ToList()
                );
                

            return dic;
        }

        private static async Task<Dictionary<int, List<int>>> GetRecipeAndIngredientHandlers(ApplicationDbContext context)
        {
            {
                var list = await context.RecipesHandlers
                                        .AsNoTracking()
                                        .Select(x => new
                                        {
                                            RecipeId = x.Recipe.Id,
                                            IngredientHandlersId = x.IngredientHandler.Id
                                        })
                                        .ToListAsync();

                var dic = list.GroupBy(x => x.RecipeId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(x => x.IngredientHandlersId).Distinct().ToList()
                    );


                return dic;
            }
        }
        

        public static async Task LoadInternal(ApplicationDbContext context)
        {
            if (_isLoaded)
                return;

            lock (_lock)
            {
                if (_isLoaded) return;
                _isLoaded = true;
            }

          

            // 🔹 Ernährungsfilter nacheinander laden (keine Parallel-Queries)
            VeganRecipeIds = await GetRecipeIdsByQuerysAsync("vegan", context);
            VegetarianRecipeIds = await GetRecipeIdsByQuerysAsync("vegetarisch", context);
            FishRecipeIds = await GetRecipeIdsByQuerysAsync("fisch", context);
            PorkRecipeIds = await GetRecipeIdsByQuerysAsync("schweinefleisch", context);
            CookingTimeOne = await GetRecipeIdsByCookingTimeAsync(45, context);

            MainMeals = await GetRecipeIdsByCategoryAsync("Hauptspeise", context);
            Appetizers = await GetRecipeIdsByCategoryAsync("Vorspeise", context);
            Desserts = await GetRecipeIdsByCategoryAsync("Dessert", context);

         

            RecipeAndIngredients = await GetRecipeAndIngredients(context);
            RecipeAndIngredHandlers = await GetRecipeAndIngredientHandlers(context);

            // 🔹 Fleisch = alles, was nicht in den anderen Listen ist
            MeatRecipeIds = RecipeAndIngredients.Keys
                .Except(VeganRecipeIds)
                .Except(VegetarianRecipeIds)
                .Except(FishRecipeIds)
                .Distinct()
                .ToList();
        }


        public static IReadOnlyList<int> GetRecipesByType(string type)
        {
            return type.ToLower() switch
            {
                "vegan" => VeganRecipeIds,
                "vegetarian" => VegetarianRecipeIds,
                "meat" => MeatRecipeIds,
                "fish" => FishRecipeIds,
                _ => new List<int>()
            };
        }

        public static IReadOnlyList<int> GetRecipesByCategory(string category)
        {
            return category.ToLower() switch
            {
                "hauptspeise" => MainMeals,
                "vorspeise" => Appetizers,
                "dessert" => Desserts,
                _ => new List<int>()
            };
        }

      
    }
}
