using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class JoinIngredientPreperationStep
    {
        public int Id { get; set; }
        public RecipePreperationSteps Preperation { get; set; }
        public IngredientsAndNutrients Ingredient { get; set; }
    }
}
