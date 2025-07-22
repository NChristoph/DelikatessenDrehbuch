using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public interface IIngredientService
    {
        List<IngredientHandlerModel> GetIngredientsByRecipesIdsList(List<int> ids);
    }
}
