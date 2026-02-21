using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public interface ISearchRecipeService
    {
        public Task<List<Recipes>> GetRecipesByQuery(string query);
    }
}
