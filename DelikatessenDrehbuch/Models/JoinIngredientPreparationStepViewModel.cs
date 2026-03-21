using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;

namespace DelikatessenDrehbuch.Models
{
    public class JoinIngredientPreparationStepViewModel
    {
        public List<RecipePreparationSteps> PreparationSteps { get; set; } = new();
        public List<IngredientsAndNutrients> Ingredients { get; set; } = new();
        public List<JoinIngredientPreparationStep> ExistingJoins { get; set; } = new();
    }
}
