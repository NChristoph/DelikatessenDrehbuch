namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public sealed class RecipeAiBaseRecipeStep
    {
        public int Id { get; set; }

        public int RecipeAiBaseRecipeId { get; set; }
        public RecipeAiBaseRecipe RecipeAiBaseRecipe { get; set; } = null!;

        public int StepIndex { get; set; }
        public string Text { get; set; } = string.Empty;
    }
}

