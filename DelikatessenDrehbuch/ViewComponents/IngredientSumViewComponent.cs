using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DelikatessenDrehbuch.ViewComponents
{
    public class IngredientSumViewComponent:ViewComponent
    {
        private readonly IIngredientScaleService _ingredientScaleService;

        public IngredientSumViewComponent(IIngredientScaleService ingredientScaleService)
        {
            _ingredientScaleService= ingredientScaleService;
        }

        public async Task<IViewComponentResult> InvokeAsync(List<int> reciepeIds, int personCount)
        {
            var allHandlers = new List<IngredientHandlerModel>();

            bool totalSalt = false;
            bool totalPepper = false;

            foreach (var reciepeId in reciepeIds)
            {
                // Der Service liefert ein Tupel: (group, salt, pepper)
                var result = _ingredientScaleService.GetScaledIngredienthandler(reciepeId, personCount);

                // result ist ein Tupel: (IEnumerable<IGrouping<string, IngredientHandlerModel>>, bool, bool)
                var group = result.Item1;
                var salt = result.Item2;
                var pepper = result.Item3;

                if (salt) totalSalt = true;
                if (pepper) totalPepper = true;

                var flatList = group.SelectMany(g => g).ToList();

                allHandlers.AddRange(flatList);
            }

            var summedIngredients = allHandlers
                .GroupBy(x => new { x.Ingredient.Id, x.Measure.UnitOfMeasurement })
                .Select(g => new IngredientHandlerModel
                {
                    Ingredient = g.First().Ingredient,
                    Measure = g.First().Measure,
                    Quantity = new Quantity
                    {
                        Quantitys = g.Sum(x => x.Quantity.Quantitys)
                    }
                })
                .ToList();

            var finalGrouped = summedIngredients
                .GroupBy(x => x.Ingredient.Group.Name ?? "Sonstiges")
                .OrderBy(g => g.Key)
                .AsEnumerable();

            var model = (finalGrouped, totalSalt, totalPepper);

            return View(model);
        }
    }
}
