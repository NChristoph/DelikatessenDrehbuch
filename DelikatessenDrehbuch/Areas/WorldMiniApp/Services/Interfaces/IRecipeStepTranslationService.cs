using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces
{
    public interface IRecipeStepTranslationService
    {
        /// <summary>
        /// Übersetzt alle Steps eines Rezepts in die 4 Hauptsprachen (Background)
        /// </summary>
        Task TranslateRecipeStepsAsync(int recipeId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queue Translation Job für Background Processing
        /// </summary>
        Task QueueTranslationAsync(int recipeId);

        /// <summary>
        /// On-Demand Übersetzung für seltene Sprachen
        /// </summary>
        Task<List<string>> TranslateStepsOnDemandAsync(int recipeId, string targetLanguage, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lade Steps in gewünschter Sprache
        /// </summary>
        Task<List<string>> GetStepsForLanguageAsync(int recipeId, string language, CancellationToken cancellationToken = default);
    }
}
