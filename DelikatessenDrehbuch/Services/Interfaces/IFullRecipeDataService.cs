using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public interface IFullRecipeDataService
    {
        public Task<FullRecipeData> GetFullRecipeDataAsync(int id);
    }
}
