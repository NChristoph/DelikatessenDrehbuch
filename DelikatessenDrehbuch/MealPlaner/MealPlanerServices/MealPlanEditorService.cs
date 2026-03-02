using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Services.Interfaces;

namespace DelikatessenDrehbuch.MealPlaner.MealPlanerServices.Interfaces
{
    public class MealPlanEditorService:IMealPlanEditorService
    {
        public readonly IUtilityService _utilityService;
        public readonly IMealPlanSortByFilters _mealPlanSortByFilters;
        private readonly IRecipesService _recipesService;

        public MealPlanEditorService(IUtilityService utilityService,IMealPlanSortByFilters mealPlanSortByFilters, IRecipesService recipesService)
        {
            _utilityService = utilityService;
            _mealPlanSortByFilters = mealPlanSortByFilters;
            _recipesService = recipesService;
        }

        public async Task<Recipes> GetOrChangeRecipe(string category, PersonalMealPlanSettings settings,List<int> usedIds)
        {
            var filterBool = _utilityService.GetTrueBoolNamesFromModel(settings);
            var allRecipesIds = _mealPlanSortByFilters.SortRecipeIdsByFilters(category.ToLower(), filterBool,0);

            allRecipesIds = allRecipesIds.Except(usedIds).ToList();


            var random=_utilityService.GetRandomIntFromList(allRecipesIds);

            return await _recipesService.GetRecipesFromDbByIdAsync(random);
        }
    }
}
