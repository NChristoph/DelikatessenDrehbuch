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
    }
}
