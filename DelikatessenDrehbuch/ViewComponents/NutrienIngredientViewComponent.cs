using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Services.Interfaces;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DelikatessenDrehbuch.ViewComponents
{
    public class NutrienIngredientViewComponent : ViewComponent
    {


        private readonly INutrientService _nutrientService;
        private readonly IIngredientService _ingredientService;
        private readonly IRecipesService _recipeService;

        public NutrienIngredientViewComponent(INutrientService nutrientService, IIngredientService ingredientService, IRecipesService recipesService)
        {

            _nutrientService = nutrientService;
            _ingredientService = ingredientService;
            _recipeService = recipesService;
        }

        // Diese Methode wird aufgerufen, wenn du die Komponente nutzt
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
              
                var sum = caloris * ((ingredientHandler.Measure.Metriks_DE == "Stk." ? ingredientHandler.Ingredient.AverageWeight * ingredientHandler.Quantity.Quantitys : ingredientHandler.Ingredient.AverageWeight)
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
                            Calories = (float?)Math.Round((double?)sum ?? 1d, 1),

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


            return View(nutrienhandlers);
        }

    }
}
