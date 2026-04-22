using DelikatessenDrehbuch.Models;

namespace DelikatessenDrehbuch.Services.Interfaces
{
    public interface IIngredientResolverService
    {
        Task<IngredientResolutionCandidate?> ResolveByNameAsync(
            string rawName,
            string? language = null,
            IEnumerable<string>? allowedCategoryKeys = null,
            CancellationToken cancellationToken = default);

        Task<List<IngredientResolutionCandidate>> GetCandidatesForGoalAsync(
            string goal,
            string? language = null,
            IEnumerable<string>? allowedCategoryKeys = null,
            IEnumerable<int>? excludeIngredientIds = null,
            int take = 8,
            CancellationToken cancellationToken = default);

        // Used for "pantry pools" where we want the AI to freely pick practical items (oils, acids, spices, herbs)
        // even when they would score poorly in goal scoring heuristics.
        Task<List<IngredientResolutionCandidate>> GetCandidatesForCategoriesAsync(
            string? language = null,
            IEnumerable<string>? allowedCategoryKeys = null,
            IEnumerable<int>? excludeIngredientIds = null,
            int take = 200,
            CancellationToken cancellationToken = default);

        Task<List<IngredientResolutionCandidate>> GetCandidatesByIdsAsync(
            IEnumerable<int> ingredientIds,
            string? language = null,
            CancellationToken cancellationToken = default);
    }
}
