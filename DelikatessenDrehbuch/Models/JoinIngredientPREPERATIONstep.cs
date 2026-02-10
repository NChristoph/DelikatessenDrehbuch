namespace DelikatessenDrehbuch.Models
{
    public class JoinIngredientPREPERATIONstep
    {
        public int Id { get; set; }
        public int PreperationStepId { get; set; }
        public RecipePreperationSteps? PreperationStep { get; set; }
        public int IngredientId { get; set; }
        public IngredientsAndNutrients? Ingredient { get; set; }
    }
}
