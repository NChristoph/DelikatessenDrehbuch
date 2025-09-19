
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
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


       
        public async Task<List<int>> SortRecipeIdsBySettingsAsync(List<int> recipesIds)
        {
            var settings = _sessionService.GetMealPlanSettingsFromSession();
            var filterBool = GetTrueBoolNamesFromSetting(settings);

            RecipeIds = await GetFilteredRecipeIdsByQueriesAsync(recipesIds, filterBool);
            RecipeIds = await GetFilteredRecipeIdsByCookingTime(RecipeIds, filterBool);

            RecipeIds = await GetFiltredRecipeIdsByIngredient(RecipeIds, settings);

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

        //TODO:
        //Ingredient at home filter speichere die Ids in einer seperaten Session.
        //Entferne die Ids aus der Hauptsession.
        //Beim erstellen des Essensplans wird eine Kombination der Ids aus der beiden Sessinos geladen.
        //Wobei aus Ingrediets at home X geladen werden.
        //Überprüfe beim Rezeptwechsel aus welcher Session das Rezept stammt und Lade aus dieser Session neu.
        //Der SessionServise gehört auch überarbeitet.
        //Dessert laden über den Button in der MenüPlan Ansicht funktionirt auch nicht.
        public async Task<List<int>> GetFiltredRecipeIdsByIngredient(List<int> recipesIds, PersonalMealPlanSettings settings)
        {
            var ingredientIdontLike = settings.IngredientIds;
            var ingredientIHaveAtHome = settings.IngredientsAtHome;

            var excludeIngredientsRecipeIds= await FilterRecipeIdsByIngredientAsync(ingredientIdontLike, recipesIds);
            var includeIngredientsRecipeIds = await FilterRecipeIdsByIngredientAsync(ingredientIHaveAtHome, recipesIds);

            if(excludeIngredientsRecipeIds.Any())
                return recipesIds.Except(excludeIngredientsRecipeIds).ToList();
            else
                return new List<int>();
        }

        private async Task<List<int>> FilterRecipeIdsByIngredientAsync(List<int> ingredientIds,List<int> recipeIds)
        {
            if(!ingredientIds.Any())
                return ingredientIds;

            var recipeHandler = await _context.RecipesHandlers.Where(x => recipeIds.Contains(x.Recipe.Id)
                                                                     && ingredientIds.Contains(x.IngredientHandler.Ingredient.Id))
                                                              .Distinct()
                                                              .Select(x => x.Recipe.Id)
                                                              .ToListAsync();
            return recipeHandler;
                                                         
        }

        #endregion






        private List<string> GetTrueBoolNamesFromSetting(PersonalMealPlanSettings settings)
        {
            var bools = typeof(PersonalMealPlanSettings)
                       .GetProperties()
                       .Where(p => p.PropertyType == typeof(bool));

            return bools.Where(b => (bool)b.GetValue(settings) == true).Select(x => x.Name.ToLower()).ToList();
        }
    }
}
