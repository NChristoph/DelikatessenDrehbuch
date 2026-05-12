namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    /// <summary>
    /// Request DTO für das neue AI-System (konkrete Steps mit Übersetzungen)
    /// </summary>
    public class GenerateConcreteStepsRequest
    {
        public string UserHash { get; set; } = "";
        public string RecipeTitle { get; set; } = "";
        public string? Category { get; set; }

        /// <summary>
        /// Liste von (IngredientId, IngredientName) Tuples
        /// </summary>
        public List<(int id, string name)>? Ingredients { get; set; }

        /// <summary>
        /// Optional: User-provided instructions text (copy-paste mode)
        /// </summary>
        public string? InstructionsText { get; set; }
    }
}
