using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces
{
    public interface ISaveNewRecipeService
    {
        public Task SaveNewAsync(SaveNewRecipeModel recipesModel,bool worldUserImage);
    }
}
