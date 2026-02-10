namespace DelikatessenDrehbuch.Models
{
    public class JoinIngredientPreparationStepViewModel
    {
        public List<RecipePreperationSteps> PreparationSteps { get; set; } = new();
        public List<IngredientsAndNutrients> Ingredients { get; set; } = new();
        public List<JoinIngredientPREPERATIONstep> ExistingJoins { get; set; } = new();
    }
}
