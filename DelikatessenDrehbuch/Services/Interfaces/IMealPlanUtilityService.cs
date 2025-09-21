namespace DelikatessenDrehbuch.Services.Interfaces
{
    public interface IMealPlanUtilityService
    {
        Task<List<int>> SortRecipeIdsBySettingsAsync(List<int> recipesIds,string category);
    }
}
