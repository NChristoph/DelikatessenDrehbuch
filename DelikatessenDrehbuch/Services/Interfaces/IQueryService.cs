using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public interface IQueryService
    {
        Task<List<string>> GetQuerysFromDbByRecipeIdAsync(int id);
        Task DeleteQuerysByRecipesId(int id);
        Task CreateQuaryHandlerAsync(int recipesId, List<string> queryHandlers);
        Task <List<int>> GetRecipeIdsByQuerry(string query);

    }
}
