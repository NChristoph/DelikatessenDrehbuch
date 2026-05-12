namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class GenerateStepsRequest
    {
        public string UserHash { get; set; } = "";
        public string RecipeTitle { get; set; } = "";
        public string? Category { get; set; }
        public List<string>? IngredientNames { get; set; }
        public string? Language { get; set; }
        public string? InstructionsText { get; set; }  // NEW: User-provided instructions text
    }
}
