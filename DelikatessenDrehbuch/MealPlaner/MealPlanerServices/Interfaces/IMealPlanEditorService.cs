using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.MealPlaner.MealPlanerServices.Interfaces
{
    public interface IMealPlanEditorService
    {
        public Task<Recipes> GetOrChangeRecipe(string category, PersonalMealPlanSettings settings, List<int> usedIds);
    }
}
