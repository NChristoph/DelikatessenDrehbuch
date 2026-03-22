namespace DelikatessenDrehbuch.Models
{
    public class RecipeJoinSmartStep
    {
        public int Id { get; set; }

        public int RecipeId { get; set; }
        public RecipeBaseData Recipe { get; set; } = null!;

        public int SmartRecipeStepId { get; set; }
        public SmartRecipeStep SmartRecipeStep { get; set; } = null!;

        public int StepIndex { get; set; }
    }
}
