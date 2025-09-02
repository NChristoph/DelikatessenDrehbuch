using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public interface ISessionService
    {
        void ClearSession();
        void SavePersonalMealPlanToSession(List<PersonalMealPlanRecipeModel> model);
        List<PersonalMealPlanRecipeModel> GetPersonalMealPlanFromSession();
        List<int> GetRecipesIdsFromSession();
        void SaveRecipesIdToSession(List<int> machingRecipes);
        void RemoveLoadedRecipesIdsFromSessions(List<int> loadedRecipesIds);
        void RemoveFullRecipesFromSessions(int recipesIds);
        Task<int> AddRandomFullRecipesToSessionsAsync();
    }
}
