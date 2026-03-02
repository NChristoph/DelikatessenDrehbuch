using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Services;
using DelikatessenDrehbuch.Services.Interfaces;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Mvc;

namespace DelikatessenDrehbuch.ViewComponents
{
    public class NutrientSumViewComponent:ViewComponent
    {
        private readonly INutrientService _nutrientService;
        private readonly IIngredientService _ingredientService;
        private readonly IRecipesService _recipeService;

        public NutrientSumViewComponent(INutrientService nutrientService, IIngredientService ingredientService,IRecipesService recipeService)
        {

            _nutrientService = nutrientService;
            _ingredientService = ingredientService;
            _recipeService = recipeService;
        }

        public async Task<IViewComponentResult> InvokeAsync(int recipeId)
        {
            var nutrienhandlers = new List<NutrienHandler>();

            var ingredientIds = StaticData.RecipeAndIngredients[recipeId];

            var recipes = await _recipeService.GetRecipesFromDbByIdAsync(recipeId);
            var nutrients = await _nutrientService.GetNutrienHandlersByIngredientIdsAsync(ingredientIds);
            var ingredientHadlers = _ingredientService.GetIngredientHandlerByRecipesId(recipeId);


            foreach (var nutrient in nutrients)
            {
                var ingredientHandler = ingredientHadlers.FirstOrDefault(ih => ih.Ingredient.Id == nutrient.Ingredient.Id);
                var caloris = ingredientHandler.Ingredient.Calories / 100.0;
                var sum = caloris * ((ingredientHandler.Measure.Metriks_DE == "Stk."  ? ingredientHandler.Ingredient.AverageWeight * ingredientHandler.Quantity.Quantitys: ingredientHandler.Ingredient.AverageWeight)
                                      ?? ingredientHandler.Quantity.Quantitys / recipes.RecipePersonCount);

                if (ingredientHandler != null)
                {
                    NutrienHandler nut = new()
                    {
                        Id = nutrient.Id,
                        Ingredient = new Ingredient()
                        {
                            Id = nutrient.Ingredient.Id,
                            Name = nutrient.Ingredient.Name,
                            AverageWeight = nutrient.Ingredient.AverageWeight,
                            Calories = (float?)Math.Round((double?)sum??1, 1),

                        },
                        Nutrients = nutrient.Nutrients,
                        Quantity = new Quantity()
                        {
                            Id = nutrient.Quantity.Id,
                            Quantitys = Math.Round((nutrient.Quantity.Quantitys / 100.0) * ingredientHandler.Quantity.Quantitys, 3)
                        },
                        Metrics = nutrient.Metrics
                    };
                    nutrienhandlers.Add(nut);
                }
            }

            var groupedNutrients = nutrienhandlers
                .GroupBy(nh => nh.Nutrients.Id)
                .Select(g => new NutrienHandler
                {
                    Nutrients = g.First().Nutrients,
                    Quantity = new Quantity
                    {
                        Quantitys = g.Sum(nh => nh.Quantity.Quantitys)
                    },
                    Ingredient=nutrienhandlers.First().Ingredient,
                    Metrics = g.First().Metrics
                })
                .ToList();

            var calories = nutrienhandlers.GroupBy(x => x.Ingredient.Name).Select(x=>x.First());
            var sumCalories = calories.Select(x=>x.Ingredient.Calories).Sum();
            ViewBag.SumCalories = Math.Round((decimal)sumCalories,0);

            return View(groupedNutrients);
        }

    }
}

