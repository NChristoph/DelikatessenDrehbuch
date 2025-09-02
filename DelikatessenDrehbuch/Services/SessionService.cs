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
        
        public void SavePersonalMealPlanToSession(List<PersonalMealPlanRecipeModel> model)
        {
            var json = JsonSerializer.Serialize(model, new JsonSerializerOptions());
            _httpContext.HttpContext.Session.SetString("MealPlanList", json);
        }

        public void SaveRecipesIdToSession(List<int> machingRecipes)
        {
            _httpContext.HttpContext.Session.SetString("RecipesFromDbIds", JsonSerializer.Serialize(machingRecipes, new JsonSerializerOptions()));
        }

        public void RemoveFullRecipesFromSessions(int recipesIds)
        {
            throw new NotImplementedException();
        }
    }
}
