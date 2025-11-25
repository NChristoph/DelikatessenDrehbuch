using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public interface IIngredientScaleService
    {
        public (IEnumerable<IGrouping<string, IngredientHandlerModel>>, bool, bool)
            GetScaledIngredienthandlerAsync(int recipeId,int targetPersonCount);

        public IEnumerable<IGrouping<string, IngredientHandlerModel>> 
            CombineIngredienthanderModel(List<IngredientHandlerModel> listToSort);
    }
}
