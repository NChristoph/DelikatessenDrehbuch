using DelikatessenDrehbuch.Models;
using System.Text.Json;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public class SessionService : ISessionService
    {
        private readonly IHttpContextAccessor _httpContext;
        private readonly IRecipesService _recipesService;
        public SessionService(IHttpContextAccessor httpContext, IRecipesService recipesService)
        {
            _httpContext = httpContext;
            _recipesService = recipesService;
        }

        public Task<int> AddRandomFullRecipesToSessionsAsync()
        {
            throw new NotImplementedException();
        }

        public List<PersonalMealPlanRecipeModel> GetPersonalMealPlanFromSession()
        {
            var json = _httpContext.HttpContext?.Session.GetString("MealPlanList");
     
            var items = string.IsNullOrEmpty(json)
                ? new List<PersonalMealPlanRecipeModel>()
                : JsonSerializer.Deserialize<List<PersonalMealPlanRecipeModel>>(json, new JsonSerializerOptions());
            return items;
        }

        public void ClearSession()
        {
            _httpContext.HttpContext?.Session.Clear();
        }

        public List<int> GetRecipesIdsFromSession()
        {
            var json = _httpContext.HttpContext.Session.GetString("RecipesFromDbIds");
            var items = string.IsNullOrEmpty(json)
                ? new List<int>()
                : JsonSerializer.Deserialize<List<int>>(json, new JsonSerializerOptions());
            return items;
        }

      


        public void RemoveLoadedRecipesIdsFromSessions(List<int> loadedRecipesIds)
        {
            var idsFromSession = GetRecipesIdsFromSession();
            idsFromSession.RemoveAll(x=>loadedRecipesIds.Contains(x));
        }

        public void SaveMealPlanSettingsToSession(PersonalMealPlanSettings model)
        {
            var json = JsonSerializer.Serialize(model, new JsonSerializerOptions());
            _httpContext.HttpContext.Session.SetString("MealPlanSettings", json);
        }

        public void SavePersonalMealPlanToSession(List<PersonalMealPlanRecipeModel> model)
        {
            var json = JsonSerializer.Serialize(model, new JsonSerializerOptions());
            _httpContext.HttpContext.Session.SetString("MealPlanList", json);
        }

        public void SaveRecipesIdToSession(List<int> machingRecipes,string name)
        {
            _httpContext.HttpContext.Session.SetString(name, JsonSerializer.Serialize(machingRecipes, new JsonSerializerOptions()));
        }

        public void UpdateRecipesIdsInSession(int remove = 0,int add=0)
        {
            var idsFromSession = GetRecipesIdsFromSession();

            if (remove!=0){        
                idsFromSession.Remove(remove);
            }
            if(add!=0) {
                idsFromSession.Add(add);               
            }

            SaveRecipesIdToSession(idsFromSession,"RecipesFromDbIds");
        }

        public void RemoveFullRecipesFromSessions(int recipesIds)
        {
            throw new NotImplementedException();
        }

        public PersonalMealPlanSettings GetMealPlanSettingsFromSession()
        {
            var json = _httpContext.HttpContext?.Session.GetString("MealPlanSettings");

            var items = string.IsNullOrEmpty(json)
                ? new PersonalMealPlanSettings()
                : JsonSerializer.Deserialize<PersonalMealPlanSettings>(json, new JsonSerializerOptions());
            return items;
        }

      
    }
}
