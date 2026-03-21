using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class JoinIngredientPreparationStep
    {
        public int Id { get; set; }
        public RecipePreparationSteps Preparation { get; set; }
        public IngredientsAndNutrients Ingredient { get; set; }
    }
}
