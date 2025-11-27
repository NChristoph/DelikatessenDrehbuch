using DelikatessenDrehbuch.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
namespace DelikatessenDrehbuch.ViewComponents
{
    public class IngredientViewComponent:ViewComponent
    {
        private readonly IIngredientScaleService _ingredientScaleService;
        public IngredientViewComponent(IIngredientScaleService ingredientScaleService)
        {
            _ingredientScaleService = ingredientScaleService;
        }
        public async Task<IViewComponentResult> InvokeAsync(int recipeId,int personCount)
        {
            var model = _ingredientScaleService.GetScaledIngredienthandlerAsync(recipeId, personCount);
            return View(model);
        }
    }
}
