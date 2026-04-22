using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces
{
    public interface ISaveNewRecipeService
    {
        public Task<RecipeBaseData> SaveNewAsync(SaveNewRecipeModel recipesModel,bool worldUserImage);
    }
}
