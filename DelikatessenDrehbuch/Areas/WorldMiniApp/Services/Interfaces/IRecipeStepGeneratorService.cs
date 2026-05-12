using static DelikatessenDrehbuch.Areas.WorldMiniApp.Services.RecipeStepGeneratorService;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces
{
    public interface IRecipeStepGeneratorService
    {
        /// <summary>
        /// Alte Methode: Generiert Steps basierend auf Master Step Templates
        /// </summary>
        Task<GeneratedStepsResult> GenerateStepsAsync(
            string recipeTitle,
            string category,
            List<string> ingredientNames,
            string language,
            string? instructionsText = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// NEUE Methode: Generiert konkrete Steps (nur in Deutsch, User kann bearbeiten)
        /// </summary>
        Task<ConcreteStepsResult> GenerateConcreteStepsAsync(
            string recipeTitle,
            string category,
            List<(int id, string name)> ingredients,
            string? instructionsText = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Übersetzt finale Steps in alle 10 Sprachen (beim Speichern)
        /// </summary>
        Task<TranslationResult> TranslateStepsAsync(
            List<(int stepOrder, string germanText)> steps,
            CancellationToken cancellationToken = default);
    }

    public class GeneratedStepsResult
    {
        public List<GeneratedStep> Steps { get; set; } = new();
        public string Reasoning { get; set; } = "";
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class GeneratedStep
    {
        public string MasterStepId { get; set; } = "";
        public int StepOrder { get; set; }
        public Dictionary<string, string> Variables { get; set; } = new();
        public string PreviewText { get; set; } = "";
    }
}
