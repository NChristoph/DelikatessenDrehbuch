using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DelikatessenDrehbuch.Services
{
    public sealed class IngredientResolverService : IIngredientResolverService
    {
        private static readonly Regex MultiWhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
        private static readonly Regex NonWordRegex = new(@"[^a-z0-9\s]", RegexOptions.Compiled);

        private readonly ApplicationDbContext _context;
        private readonly IFoodCategoryService _foodCategoryService;

        public IngredientResolverService(ApplicationDbContext context, IFoodCategoryService foodCategoryService)
        {
            _context = context;
            _foodCategoryService = foodCategoryService;
        }

        public async Task<IngredientResolutionCandidate?> ResolveByNameAsync(
            string rawName,
            string? language = null,
            IEnumerable<string>? allowedCategoryKeys = null,
            CancellationToken cancellationToken = default)
        {
            var normalizedName = NormalizeIngredientText(rawName);
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return null;
            }

            var candidates = await QueryCandidateIngredientsAsync(allowedCategoryKeys, cancellationToken);
            var resolvedLanguage = NormalizeLanguage(language);

            var match = candidates
                .Select(item => new
                {
                    Ingredient = item,
                    Score = GetNameMatchScore(item, normalizedName)
                })
                .Where(x => x.Score >= 0)
                .OrderBy(x => x.Score)
                .ThenByDescending(x => x.Ingredient.Protein_a_100g)
                .ThenBy(x => x.Ingredient.Name_DE)
                .FirstOrDefault();

            return match == null
                ? null
                : MapCandidate(
                    match.Ingredient,
                    resolvedLanguage,
                    match.Score switch
                    {
                        0 => "exact",
                        1 => "prefix",
                        _ => "contains"
                    },
                    ComputeGoalScore(match.Ingredient, "neutral"));
        }

        public async Task<List<IngredientResolutionCandidate>> GetCandidatesForGoalAsync(
            string goal,
            string? language = null,
            IEnumerable<string>? allowedCategoryKeys = null,
            IEnumerable<int>? excludeIngredientIds = null,
            int take = 8,
            CancellationToken cancellationToken = default)
        {
            var normalizedGoal = NormalizeGoal(goal);
            var resolvedLanguage = NormalizeLanguage(language);
            var excludedIds = (excludeIngredientIds ?? Enumerable.Empty<int>()).ToHashSet();

            var candidates = await QueryCandidateIngredientsAsync(allowedCategoryKeys, cancellationToken);

            return candidates
                .Where(x => !excludedIds.Contains(x.Id))
                .Where(x => IsSuitableForGoal(x, normalizedGoal))
                .Select(x => new
                {
                    Ingredient = x,
                    Score = ComputeGoalScore(x, normalizedGoal)
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Ingredient.Protein_a_100g)
                .ThenBy(x => x.Ingredient.Name_DE)
                .Take(Math.Max(1, take))
                .Select(x => MapCandidate(x.Ingredient, resolvedLanguage, normalizedGoal, x.Score))
                .ToList();
        }

        public async Task<List<IngredientResolutionCandidate>> GetCandidatesForCategoriesAsync(
            string? language = null,
            IEnumerable<string>? allowedCategoryKeys = null,
            IEnumerable<int>? excludeIngredientIds = null,
            int take = 200,
            CancellationToken cancellationToken = default)
        {
            var resolvedLanguage = NormalizeLanguage(language);
            var excludedIds = (excludeIngredientIds ?? Enumerable.Empty<int>())
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            // IMPORTANT: Don't load the full Ingredients table into memory.
            // This method is used for "pantry pools" (spices, oils, herbs, broths, etc.) and can easily be huge.
            var normalizedKeys = (allowedCategoryKeys ?? Enumerable.Empty<string>())
                .Select(NormalizeCategoryKey)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var query = _context.IngredientsAndNutrients
                .AsNoTracking()
                .Include(x => x.Group)
                .Include(x => x.FoodCategory)
                .AsQueryable();

            if (normalizedKeys.Count > 0)
            {
                query = query.Where(x => x.FoodCategory != null && normalizedKeys.Contains(x.FoodCategory.CategoryKey));
            }

            if (excludedIds.Count > 0)
            {
                query = query.Where(x => !excludedIds.Contains(x.Id));
            }

            var slice = await query
                .OrderBy(x => x.Name_DE)
                .Take(Math.Max(1, take))
                .ToListAsync(cancellationToken);

            return slice
                .Select(x => MapCandidate(x, resolvedLanguage, "pantry_pool", score: 1m))
                .ToList();
        }

        public async Task<List<IngredientResolutionCandidate>> GetCandidatesByIdsAsync(
            IEnumerable<int> ingredientIds,
            string? language = null,
            CancellationToken cancellationToken = default)
        {
            var resolvedLanguage = NormalizeLanguage(language);
            var ids = (ingredientIds ?? Enumerable.Empty<int>())
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            if (ids.Count == 0)
            {
                return new List<IngredientResolutionCandidate>();
            }

            var items = await _context.IngredientsAndNutrients
                .AsNoTracking()
                .Include(x => x.Group)
                .Include(x => x.FoodCategory)
                .Where(x => ids.Contains(x.Id))
                .ToListAsync(cancellationToken);

            return items
                .Select(x => MapCandidate(x, resolvedLanguage, "by_id", score: 1m))
                .ToList();
        }

        private async Task<List<IngredientsAndNutrients>> QueryCandidateIngredientsAsync(
            IEnumerable<string>? allowedCategoryKeys,
            CancellationToken cancellationToken)
        {
            var normalizedKeys = (allowedCategoryKeys ?? Enumerable.Empty<string>())
                .Select(NormalizeCategoryKey)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var query = _context.IngredientsAndNutrients
                .AsNoTracking()
                .Include(x => x.Group)
                .Include(x => x.FoodCategory)
                .AsQueryable();

            if (normalizedKeys.Count > 0)
            {
                query = query.Where(x => x.FoodCategory != null && normalizedKeys.Contains(x.FoodCategory.CategoryKey));
            }

            return await query.ToListAsync(cancellationToken);
        }

        private IngredientResolutionCandidate MapCandidate(
            IngredientsAndNutrients ingredient,
            string language,
            string matchType,
            decimal score)
        {
            var category = ingredient.FoodCategory;
            return new IngredientResolutionCandidate
            {
                IngredientId = ingredient.Id,
                Name = GetLocalizedIngredientName(ingredient, language),
                CategoryKey = category?.CategoryKey ?? string.Empty,
                CategoryLabel = _foodCategoryService.GetLocalizedName(category, language),
                ProteinPer100g = ingredient.Protein_a_100g,
                CarbsPer100g = ingredient.Carbohydrates_a_100g,
                FatPer100g = ingredient.Fat_a_100g,
                FiberPer100g = ingredient.Fiber_a_100g,
                CaloriesPer100g = ingredient.Calories_a_100g,
                Score = score,
                MatchType = matchType
            };
        }

        private static int GetNameMatchScore(IngredientsAndNutrients ingredient, string normalizedQuery)
        {
            var names = EnumerateIngredientNames(ingredient)
                .Select(NormalizeIngredientText)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (names.Count == 0)
            {
                return -1;
            }

            if (names.Any(name => string.Equals(name, normalizedQuery, StringComparison.OrdinalIgnoreCase)))
            {
                return 0;
            }

            if (names.Any(name => name.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase)))
            {
                return 1;
            }

            if (names.Any(name => name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)))
            {
                return 2;
            }

            return -1;
        }

        private static decimal ComputeGoalScore(IngredientsAndNutrients ingredient, string goal)
        {
            return goal switch
            {
                "highprotein" => ingredient.Protein_a_100g * 4m
                    + ingredient.Fiber_a_100g * 0.8m
                    - ingredient.Carbohydrates_a_100g * 0.35m
                    - ingredient.Fat_a_100g * 0.1m,

                "lowcarb" => 100m
                    - ingredient.Carbohydrates_a_100g * 2.5m
                    + ingredient.Protein_a_100g * 1.2m
                    + ingredient.Fiber_a_100g * 0.6m,

                "vegan" => ingredient.Protein_a_100g * 2m
                    + ingredient.Fiber_a_100g * 1m
                    - ingredient.Fat_a_100g * 0.1m,

                "mealprep" => ingredient.Protein_a_100g * 1.5m
                    + ingredient.Fiber_a_100g * 1m
                    + (ingredient.is_soft ? -2m : 0m)
                    + (ingredient.is_liquid ? -1m : 0m),

                _ => ingredient.Protein_a_100g
                    + ingredient.Fiber_a_100g * 0.5m
                    - ingredient.Carbohydrates_a_100g * 0.15m
            };
        }

        private static bool IsSuitableForGoal(IngredientsAndNutrients ingredient, string goal)
        {
            var categoryKey = ingredient.FoodCategory?.CategoryKey?.Trim().ToLowerInvariant() ?? string.Empty;

            return goal switch
            {
                "lowcarb" => IsLowCarbReplacementCandidate(ingredient, categoryKey),
                "highprotein" => IsHighProteinCandidate(ingredient, categoryKey),
                "vegan" => !ingredient.is_powder && !ingredient.is_fat && !IsFlavorOnlyCategory(categoryKey),
                "mealprep" => !ingredient.is_powder && !IsFlavorOnlyCategory(categoryKey),
                _ => true
            };
        }

        private static bool IsHighProteinCandidate(IngredientsAndNutrients ingredient, string categoryKey)
        {
            // For "highprotein" we want a broad, useful candidate pool. The AI/prompt will ensure
            // the actual recipe change is meaningful (no tiny 2g "protein upgrades").
            // Do not exclude powders here; some apps legitimately use e.g. chickpea flour as a component.
            if (IsFlavorOnlyCategory(categoryKey))
            {
                return false;
            }

            // Keep out typical low-protein items while not being overly strict.
            // (Strict "protein calorie share" filters tend to shrink the list too much.)
            if (ingredient.Protein_a_100g < 10m)
            {
                return false;
            }

            return true;
        }

        private static bool IsLowCarbReplacementCandidate(IngredientsAndNutrients ingredient, string categoryKey)
        {
            if (ingredient.is_powder || ingredient.is_liquid || ingredient.is_fat)
            {
                return false;
            }

            if (IsFlavorOnlyCategory(categoryKey))
            {
                return false;
            }

            var hasUsefulTexture = ingredient.is_hard || ingredient.is_soft;
            var canBeCookedAsSide = ingredient.is_roastable || ingredient.is_boilable || ingredient.is_steamable || ingredient.is_fryable || ingredient.is_searable || ingredient.is_grillable;
            if (!hasUsefulTexture && !canBeCookedAsSide)
            {
                return false;
            }

            return ingredient.Carbohydrates_a_100g <= 15m;
        }

        private static bool IsFlavorOnlyCategory(string categoryKey)
        {
            return categoryKey is "spices" or "herbs" or "sauces" or "sweeteners" or "broths" or "oils" or "fats";
        }

        private static IEnumerable<string> EnumerateIngredientNames(IngredientsAndNutrients ingredient)
        {
            yield return ingredient.Name_DE ?? string.Empty;
            yield return ingredient.Name_EN ?? string.Empty;
            yield return ingredient.Name_PRT ?? string.Empty;
            yield return ingredient.Name_ESP ?? string.Empty;
            yield return ingredient.Name_ID ?? string.Empty;
            yield return ingredient.Name_NL ?? string.Empty;
            yield return ingredient.Name_SE ?? string.Empty;
            yield return ingredient.Name_DK ?? string.Empty;
            yield return ingredient.Name_NO ?? string.Empty;
            yield return ingredient.Name_MS ?? string.Empty;
        }

        private static string GetLocalizedIngredientName(IngredientsAndNutrients ingredient, string language)
        {
            return language switch
            {
                "en" => ingredient.Name_EN ?? ingredient.Name_DE ?? string.Empty,
                "pt" => ingredient.Name_PRT ?? ingredient.Name_DE ?? string.Empty,
                "es" => ingredient.Name_ESP ?? ingredient.Name_DE ?? string.Empty,
                "id" => ingredient.Name_ID ?? ingredient.Name_DE ?? string.Empty,
                "nl" => ingredient.Name_NL ?? ingredient.Name_DE ?? string.Empty,
                "se" => ingredient.Name_SE ?? ingredient.Name_DE ?? string.Empty,
                "dk" => ingredient.Name_DK ?? ingredient.Name_DE ?? string.Empty,
                "no" => ingredient.Name_NO ?? ingredient.Name_DE ?? string.Empty,
                "ms" => ingredient.Name_MS ?? ingredient.Name_DE ?? string.Empty,
                _ => ingredient.Name_DE ?? string.Empty
            };
        }

        private static string NormalizeGoal(string? goal)
        {
            return (goal ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "highprotein" or "protein" or "mehrprotein" => "highprotein",
                "lowcarb" => "lowcarb",
                "vegan" => "vegan",
                "mealprep" => "mealprep",
                _ => "neutral"
            };
        }

        private static string NormalizeLanguage(string? language)
        {
            return (language ?? "de").Trim().ToLowerInvariant();
        }

        private static string NormalizeCategoryKey(string? categoryKey)
        {
            return (categoryKey ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string NormalizeIngredientText(string? value)
        {
            var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            normalized = normalized
                .Replace("ä", "ae", StringComparison.Ordinal)
                .Replace("ö", "oe", StringComparison.Ordinal)
                .Replace("ü", "ue", StringComparison.Ordinal)
                .Replace("ß", "ss", StringComparison.Ordinal);

            normalized = RemoveDiacritics(normalized);
            normalized = NonWordRegex.Replace(normalized, " ");
            normalized = MultiWhitespaceRegex.Replace(normalized, " ").Trim();
            return normalized;
        }

        private static string RemoveDiacritics(string input)
        {
            var normalized = input.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);

            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(c);
                }
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
