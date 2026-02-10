using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class PreparationStepIngredientLink
    {
        public RecipePreperationSteps? PreperationStep { get; set; }
        public IngredientsAndNutrients? Ingredient { get; set; }
    }
}
