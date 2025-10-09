
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public class MealPlanUtilityService : IMealPlanUtilityService
    {
        private readonly ISessionService _sessionService;
        private readonly ApplicationDbContext _context;
        private readonly string[] _staticFilter = { "vegan", "vegetarisch" };
        private readonly string _cookingTimeFilter = "cookingtimeone";


        private List<int> RecipeIds { get; set; } = new();
        public MealPlanUtilityService(ISessionService sessionService, ApplicationDbContext context)
        {
            _sessionService = sessionService;
            _context = context;
        }

        public async Task<List<int>> SortRecipeIdsBySettingsAsync(List<int> recipesIds, string category)
        {
            var settings = _sessionService.GetMealPlanSettingsFromSession();
            var filterBool = GetTrueBoolNamesFromSetting(settings);

            RecipeIds = await GetFilteredRecipeIdsByQueriesAsync(recipesIds, filterBool);
            RecipeIds = await GetFilteredRecipeIdsByCookingTime(RecipeIds, filterBool);
            RecipeIds = await GetFiltredRecipeIdsByIngredient(RecipeIds, settings, category);

            if (category == "Hauptspeise")
                await FilterByPreference(RecipeIds, filterBool, category);

            return RecipeIds;
        }


        #region QueriesFilter
        private async Task<List<int>> GetFilteredRecipeIdsByQueriesAsync(List<int> recipesIds, List<string> filterBool)
        {
            var onlyFilter = _staticFilter.Intersect(filterBool).ToList();
            var excludedFilter = filterBool.Where(x => x.StartsWith("no"))
                                           .Select(x => x.Substring(2))
                                           .ToList();

            var includedRecipesIds = await FilterRecipeIdsByQueriesAsync(recipesIds, onlyFilter);
            var excludedRecipesIds = await FilterRecipeIdsByQueriesAsync(recipesIds, excludedFilter);


            if (includedRecipesIds.Any())
                return includedRecipesIds.Except(excludedRecipesIds ?? Enumerable.Empty<int>()).ToList();
            else
                return recipesIds.Except(excludedRecipesIds).ToList();
        }

        private async Task<List<int>> FilterRecipeIdsByQueriesAsync(List<int> recipesIds, List<string> filterBool)
        {
            var includedQueryHandler = await _context.QueryHandler.Where(x => filterBool.Contains(x.Query.Query.ToLower())
                                                                          && recipesIds.Contains(x.Recipe.Id))
                                                                  .AsNoTracking()
                                                                  .Select(x => x.Recipe.Id)
                                                                  .ToListAsync();

            return includedQueryHandler.ToHashSet().ToList();

        }

        #endregion

        #region CookingTimeFilter
        private async Task<List<int>> GetFilteredRecipeIdsByCookingTime(List<int> recipeIdsByQueryie, List<string> filterBool)
        {

            if (!filterBool.Contains(_cookingTimeFilter))
                return recipeIdsByQueryie;
            else
                return await _context.Recipes.Where(x => x.PreparationTime <= 45
                                                     && recipeIdsByQueryie.Contains(x.Id))
                                             .AsNoTracking()
                                             .Select(x => x.Id)
                                             .ToListAsync();
        }

        #endregion

        #region IngredientFilter

        public async Task<List<int>> GetFiltredRecipeIdsByIngredient(List<int> recipesIds, PersonalMealPlanSettings settings, string category)
        {
            var ingredientIdontLike = settings.IngredientIds;
            var ingredientIHaveAtHome = settings.IngredientsAtHome;

            var excludeIngredientsRecipeIds = await FilterRecipeIdsByIngredientIdontLikeAsync(ingredientIdontLike, recipesIds);

            var includeIngredientsRecipeIds = await FilterByIncludeIngredientAsync(
                                                    ingredientIHaveAtHome.Except(ingredientIdontLike).ToList(),
                                                    recipesIds
                                                    );


            var edidRecipeIds = recipesIds.Except(excludeIngredientsRecipeIds).ToList();

            var name = nameof(settings.IngredientsAtHome);
            SaveRecipesIdsToSession(edidRecipeIds.Except(includeIngredientsRecipeIds).ToList(), category);
            SaveRecipesIdsToSession(includeIngredientsRecipeIds.Except(excludeIngredientsRecipeIds).ToList(), name.ToLower() + "_" + category);


            return edidRecipeIds;

        }
        private async Task<List<int>> FilterRecipeIdsByIngredientIdontLikeAsync(List<int> ingredientIds, List<int> recipeIds)
        {
            if (!ingredientIds.Any())
                return new List<int>();

            var recipeHandler = await _context.RecipesHandlers.Where(x => recipeIds.Contains(x.Recipe.Id)
                                                                    && ingredientIds.Contains(x.IngredientHandler.Ingredient.Id))
                                                             .Distinct()
                                                             .Select(x => x.Recipe.Id)
                                                             .ToListAsync();
            return recipeHandler;


        }
        private async Task<List<int>> FilterByIncludeIngredientAsync(List<int> ingredientIds, List<int> recipeIds)
        {
            if (!ingredientIds.Any())
                return new List<int>();

            // Matches je Rezept zählen (distinct Ingredients, falls ein Rezept dieselbe Zutat mehrfach hat)
            var matches = await _context.RecipesHandlers
                .Where(x => recipeIds.Contains(x.Recipe.Id)
                         && ingredientIds.Contains(x.IngredientHandler.Ingredient.Id))
                .GroupBy(x => x.Recipe.Id)
                .Select(g => new
                {
                    RecipeId = g.Key,
                    MatchCount = g.Select(r => r.IngredientHandler.Ingredient.Id).Distinct().Count()
                })
                .ToListAsync();

            if (matches.Count == 0)
                return new List<int>(); // keine Übereinstimmung gefunden

            // höchste Übereinstimmung ermitteln
            var maxCount = matches.Max(m => m.MatchCount);

            // nur Rezepte mit maximaler Übereinstimmung zurückgeben
            return matches
                .Where(m => m.MatchCount == maxCount)
                .Select(m => m.RecipeId)
                .ToList();

        }

        #endregion

        #region PrefereceFilter
        public async Task FilterByPreference(List<int> recipeIds, List<string> filterBool, string category)
        {
            string preference = filterBool.FirstOrDefault(x => x.StartsWith("preferably") || x.StartsWith("balanced")) ?? string.Empty;

            var vegetarianIds = await FilterRecipeIdsByQueriesAsync(recipeIds, new List<string> { "Vegetarisch" });
            var veganIds = await FilterRecipeIdsByQueriesAsync(recipeIds, new List<string> { "Vegan" });
            var meatIds = recipeIds.Except(vegetarianIds).Except(veganIds).ToList();

            switch (preference)
            {
                case "balanceddiet":
                    break;
                case "preferablymeat":
                    if (meatIds.Any())
                    {
                        SaveRecipesIdsToSession(meatIds, "preferablymeat");
                        _sessionService.ExceptRecipeIdsFromSession(meatIds, category);
                    }
                    break;
                case "preferablyvegetarian":
                    if (vegetarianIds.Any())
                    {
                        SaveRecipesIdsToSession(vegetarianIds, "preferablyvegetarian");
                        _sessionService.ExceptRecipeIdsFromSession(vegetarianIds, category);
                    }
                    break;
                default:
                    break;
            }
        }
        #endregion


        private void SaveRecipesIdsToSession(List<int> recipesIds, string sessionName)
        {
            _sessionService.SaveRecipesIdToSession(recipesIds, sessionName);
        }
        public static List<string> GetTrueBoolNamesFromSetting(PersonalMealPlanSettings settings)
        {
           
            var bools = typeof(PersonalMealPlanSettings)
                       .GetProperties()
                       .Where(p => p.PropertyType == typeof(bool));

            return bools.Where(b => (bool)b.GetValue(settings) == true).Select(x => x.Name.ToLower()).ToList();
        }
    }
}
