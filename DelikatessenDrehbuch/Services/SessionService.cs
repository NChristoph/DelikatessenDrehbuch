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


        public void SavePersonalMealPlanToSession(List<PersonalMealPlanRecipeModel> model)
        {
            var json = JsonSerializer.Serialize(model, new JsonSerializerOptions());
            _httpContext.HttpContext.Session.SetString("MealPlanList", json);
        }
        public List<PersonalMealPlanRecipeModel> GetPersonalMealPlanFromSession()
        {
            var json = _httpContext.HttpContext?.Session.GetString("MealPlanList");
     
            var items = string.IsNullOrEmpty(json)
                ? new List<PersonalMealPlanRecipeModel>()
                : JsonSerializer.Deserialize<List<PersonalMealPlanRecipeModel>>(json, new JsonSerializerOptions());
            return items;
        }

        public void SaveRecipesIdToSession(List<int> machingRecipes, string name)
        {
            _httpContext.HttpContext.Session.SetString(name, JsonSerializer.Serialize(machingRecipes, new JsonSerializerOptions()));
        }
        public List<int> GetRecipesIdsFromSession(string category)
        {
            var json = _httpContext.HttpContext.Session.GetString(category);
            var items = string.IsNullOrEmpty(json)
                ? new List<int>()
                : JsonSerializer.Deserialize<List<int>>(json, new JsonSerializerOptions());
            return items;
        }
        public void UpdateRecipeIdInSession(int remove = 0, int add = 0, string category = "")
        {
            var idsFromSession = GetRecipesIdsFromSession(category);

            if (remove != 0)
            {
                idsFromSession.Remove(remove);
            }
            if (add != 0)
            {
                idsFromSession.Add(add);
            }

            SaveRecipesIdToSession(idsFromSession, category);
        }

        public void ExceptRecipeIdsFromSession(List<int> exceptIds, string category)
        {
            var idsFromSession = GetRecipesIdsFromSession(category);
            idsFromSession = idsFromSession.Except(exceptIds).ToList();
            SaveRecipesIdToSession(idsFromSession, category);
        }

        public void SaveMealPlanSettingsToSession(PersonalMealPlanSettings model)
        {
            var json = JsonSerializer.Serialize(model, new JsonSerializerOptions());
            _httpContext.HttpContext.Session.SetString("MealPlanSettings", json);
        }
        public PersonalMealPlanSettings GetMealPlanSettingsFromSession()
        {
            var json = _httpContext.HttpContext?.Session.GetString("MealPlanSettings");

            var items = string.IsNullOrEmpty(json)
                ? new PersonalMealPlanSettings()
                : JsonSerializer.Deserialize<PersonalMealPlanSettings>(json, new JsonSerializerOptions());
            return items;
        }

        public void ClearSession()
        {
            _httpContext.HttpContext?.Session.Clear();
        }










    }
}
