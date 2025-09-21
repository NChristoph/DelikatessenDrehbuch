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
        List<int> GetRecipesIdsFromSession(string category);
        void SaveRecipesIdToSession(List<int> machingRecipes,string category);
        void RemoveLoadedRecipesIdsFromSessions(List<int> loadedRecipesIds,string category);
       

        void UpdateRecipesIdsInSession(int remove = 0, int add = 0,string category="");


    }
}
