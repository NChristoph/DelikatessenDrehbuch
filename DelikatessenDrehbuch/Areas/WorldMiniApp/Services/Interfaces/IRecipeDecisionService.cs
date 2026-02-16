namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces
{
    public interface IRecipeDecisionService
    {
        /// <summary>
        /// Analysiert Rezepttitel und Zutatenliste und gibt passende Step-Vorschläge zurück.
        /// </summary>
        Task<RecipeDecisionResult> GetStepSuggestionsAsync(string title, List<int> ingredientIds);
    }

    public class RecipeDecisionResult
    {
        public bool Matched { get; set; }
        public string PatternId { get; set; }
        public string DishType { get; set; }
        public string Category { get; set; }
        public double Confidence { get; set; }
        public List<SuggestedStep> SuggestedSteps { get; set; } = new();
    }

    public class SuggestedStep
    {
        public int StepId { get; set; }
        public int Phase { get; set; }
        public int StepIndex { get; set; }
        public string Label { get; set; }
        public double Score { get; set; }
    }
}
