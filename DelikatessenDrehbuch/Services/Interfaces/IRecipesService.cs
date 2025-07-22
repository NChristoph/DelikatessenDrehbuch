using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public interface IRecipesService
    {
        Recipes GetOneRendomRecipeFromIdList(List<int> ids);
        List<int> GetRecipesIdsByCategory(string category);
    }
}
