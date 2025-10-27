using DelikatessenDrehbuch.Services.Interfaces;

namespace DelikatessenDrehbuch.Models
{
    public class PersonalMealPlanRecipeModel
    {
        public int Id { get; set; }
        public int Index { get; set; }
        public Recipes Recipes { get; set; }
        public virtual List<IngredientHandlerModel> Ingredients { get; set; } = new();

        private readonly IIngredientService _ingredientService;

        public PersonalMealPlanRecipeModel(IIngredientService ingredientService)
        {
            _ingredientService = ingredientService;
        }

        public void ScaleIngredients(int newPersonCount)
        {
            Ingredients = _ingredientService.GetScaledIngredienthandler(Ingredients, (int)Recipes.RecipePersonCount, newPersonCount);
        }
    }
}
