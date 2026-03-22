namespace DelikatessenDrehbuch.Models
{
    public class SaveNewRecipeModel
    {
        public string Querys { get; set; }
        public Recipes Recipes { get; set; }
        public List<IngredientMeasureQuantity> IngredientMeasureQuantity { get; set; }
        public List<RecipeJoinPreparationSteps> RecipeJoinPreparationSteps { get; set; }
        public List<SmartStepReferenceInput> SmartStepReferences { get; set; }
    }
}
