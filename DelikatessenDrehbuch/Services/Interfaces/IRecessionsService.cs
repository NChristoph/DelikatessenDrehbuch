using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public interface IRecessionsService
    {
        Task<List<Recession>> GetRecessionsByRecipeIdFromDbAsync(int id);
        Task SaveNewRecessionInDbAsync(int recipesId, string assessment, string userName);
    }
}
