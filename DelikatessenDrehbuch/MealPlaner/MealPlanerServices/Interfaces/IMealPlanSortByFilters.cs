
namespace DelikatessenDrehbuch.MealPlaner.MealPlanerServices.Interfaces
{
    public interface IMealPlanSortByFilters
    {
        List<int> SortRecipeIdsByFilters(string category,List<string> filterBools,int dayCount);
                                               
    }
}
