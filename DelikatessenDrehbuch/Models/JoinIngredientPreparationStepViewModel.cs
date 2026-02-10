using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;

namespace DelikatessenDrehbuch.Models
{
    public class JoinIngredientPreparationStepViewModel
    {
        public List<RecipePreperationSteps> PreparationSteps { get; set; } = new();
        public List<IngredientsAndNutrients> Ingredients { get; set; } = new();
        public List<JoinIngredientPreperationStep> ExistingJoins { get; set; } = new();
    }
}
