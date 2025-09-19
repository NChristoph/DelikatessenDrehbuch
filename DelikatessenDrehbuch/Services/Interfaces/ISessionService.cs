using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public interface ISessionService
    {
        void ClearSession();
        void SavePersonalMealPlanToSession(List<PersonalMealPlanRecipeModel> model);
        void SaveMealPlanSettingsToSession(PersonalMealPlanSettings model);
        PersonalMealPlanSettings GetMealPlanSettingsFromSession();
        List<PersonalMealPlanRecipeModel> GetPersonalMealPlanFromSession();
        List<int> GetRecipesIdsFromSession();
        void SaveRecipesIdToSession(List<int> machingRecipes,string name);
        void RemoveLoadedRecipesIdsFromSessions(List<int> loadedRecipesIds);
        void RemoveFullRecipesFromSessions(int recipesIds);
        Task<int> AddRandomFullRecipesToSessionsAsync();
        void UpdateRecipesIdsInSession(int remove = 0, int add = 0);


    }
}
