using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    public class RecipeDecisionService : IRecipeDecisionService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private static List<DishPattern> _patterns;
        private static readonly object _lock = new();

        public RecipeDecisionService(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public async Task<RecipeDecisionResult> GetStepSuggestionsAsync(string title, List<int> ingredientIds)
        {
            var patterns = LoadPatterns();
            if (patterns == null || !patterns.Any())
                return new RecipeDecisionResult { Matched = false };

            var ingredientNames = await _context.IngredientsAndNutrients
                .Where(x => ingredientIds.Contains(x.Id))
                .Select(x => x.Name_DE)
                .ToListAsync();

            var bestMatch = FindBestPattern(title, ingredientNames, patterns);
            if (bestMatch == null)
                return new RecipeDecisionResult { Matched = false };

            var allSteps = await _context.RecipePreperationSteps.ToListAsync();
            var stepBindings = await _context.JoinIngredientPreperationStep
                .Include(x => x.Preperation)
                .Include(x => x.Ingredient)
                .ToListAsync();

            var suggestedSteps = MatchStepsToPattern(bestMatch.Pattern, allSteps, stepBindings, ingredientIds);

            return new RecipeDecisionResult
            {
                Matched = true,
                PatternId = bestMatch.Pattern.Id,
                DishType = bestMatch.Pattern.DishType,
                Category = bestMatch.Pattern.Category,
                Confidence = bestMatch.Score,
                SuggestedSteps = suggestedSteps
            };
        }

        private List<DishPattern> LoadPatterns()
        {
            if (_patterns != null) return _patterns;

            lock (_lock)
            {
                if (_patterns != null) return _patterns;

                var path = Path.Combine(_env.ContentRootPath, "Areas", "WorldMiniApp", "Data", "dish_patterns.json");
                if (!File.Exists(path)) return new List<DishPattern>();

                var json = File.ReadAllText(path);
                var doc = JsonSerializer.Deserialize<DishPatternFile>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                _patterns = doc?.Patterns ?? new List<DishPattern>();
                return _patterns;
            }
        }

        private PatternMatch FindBestPattern(string title, List<string> ingredientNames, List<DishPattern> patterns)
        {
            var normalizedTitle = (title ?? "").ToLowerInvariant().Trim();
            PatternMatch best = null;

            foreach (var pattern in patterns)
            {
                double score = 0;

                // Name-Match (gewichtet: 40%)
                double nameScore = 0;
                foreach (var namePattern in pattern.NamePatterns)
                {
                    if (normalizedTitle.Contains(namePattern.ToLowerInvariant()))
                    {
                        nameScore = 1.0;
                        break;
                    }
                }

                // Zutaten-Match (gewichtet: 60%)
                double ingredientScore = 0;
                if (pattern.SignatureIngredients.Any() && ingredientNames.Any())
                {
                    int matchCount = 0;
                    foreach (var sig in pattern.SignatureIngredients)
                    {
                        if (ingredientNames.Any(n => n != null && n.IndexOf(sig, StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            matchCount++;
                        }
                    }

                    ingredientScore = (double)matchCount / pattern.SignatureIngredients.Count;

                    if (matchCount < pattern.MinIngredientMatch && nameScore < 1.0)
                        continue;
                }

                score = (nameScore * 0.4) + (ingredientScore * 0.6);

                // Mindestens Name ODER genug Zutaten müssen matchen
                if (nameScore < 0.5 && ingredientScore < 0.3)
                    continue;

                if (best == null || score > best.Score)
                {
                    best = new PatternMatch { Pattern = pattern, Score = score };
                }
            }

            return best;
        }

        private List<SuggestedStep> MatchStepsToPattern(
            DishPattern pattern,
            List<RecipePreperationSteps> allSteps,
            List<JoinIngredientPreperationStep> stepBindings,
            List<int> ingredientIds)
        {
            var result = new List<SuggestedStep>();
            int stepIndex = 1;

            foreach (var template in pattern.PhaseTemplate)
            {
                var bestStep = FindBestStepForTemplate(
                    template, pattern, allSteps, stepBindings, ingredientIds,
                    result.Select(s => s.StepId).ToHashSet());

                if (bestStep != null)
                {
                    result.Add(new SuggestedStep
                    {
                        StepId = bestStep.Id,
                        Phase = bestStep.Phase,
                        StepIndex = stepIndex++,
                        Label = bestStep.Step_DE,
                        Score = 1.0
                    });
                }
            }

            return result;
        }

        private RecipePreperationSteps FindBestStepForTemplate(
            PhaseTemplate template,
            DishPattern pattern,
            List<RecipePreperationSteps> allSteps,
            List<JoinIngredientPreperationStep> stepBindings,
            List<int> ingredientIds,
            HashSet<int> alreadyUsedStepIds)
        {
            // Filtere Steps nach Phase
            var candidates = allSteps
                .Where(s => s.Phase == template.Phase && !alreadyUsedStepIds.Contains(s.Id))
                .ToList();

            // Equipment-Filter (wenn angegeben)
            if (template.Equipment > 0)
            {
                var equipFiltered = candidates.Where(s => s.Equipment == template.Equipment).ToList();
                if (equipFiltered.Any())
                    candidates = equipFiltered;
            }

            // Score pro Candidate berechnen
            var scored = new List<(RecipePreperationSteps Step, double Score)>();

            // Keywords für diese Phase aus dem Pattern
            var phaseKey = template.Phase.ToString();
            var keywords = pattern.StepKeywords.ContainsKey(phaseKey)
                ? pattern.StepKeywords[phaseKey]
                : new List<string>();

            foreach (var candidate in candidates)
            {
                double score = 0;
                var stepText = (candidate.Step_DE ?? "").ToLowerInvariant();

                // Keyword-Match
                foreach (var kw in keywords)
                {
                    if (stepText.Contains(kw.ToLowerInvariant()))
                    {
                        score += 2.0;
                    }
                }

                // Template-Beschreibung ähnlich?
                var templateWords = template.Description.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var tw in templateWords)
                {
                    if (tw.Length > 3 && stepText.Contains(tw))
                    {
                        score += 1.0;
                    }
                }

                // Zutat-Binding Bonus
                var boundIngredients = stepBindings
                    .Where(b => b.Preperation?.Id == candidate.Id)
                    .Select(b => b.Ingredient?.Id ?? 0)
                    .Where(id => id > 0)
                    .ToList();

                var ingredientMatches = boundIngredients.Count(bid => ingredientIds.Contains(bid));
                score += ingredientMatches * 3.0;

                if (score > 0)
                {
                    scored.Add((candidate, score));
                }
            }

            return scored
                .OrderByDescending(x => x.Score)
                .Select(x => x.Step)
                .FirstOrDefault();
        }

        // JSON Model Classes
        private class DishPatternFile
        {
            public string Version { get; set; }
            public List<DishPattern> Patterns { get; set; } = new();
        }

        private class PatternMatch
        {
            public DishPattern Pattern { get; set; }
            public double Score { get; set; }
        }
    }

    public class DishPattern
    {
        public string Id { get; set; }
        public List<string> NamePatterns { get; set; } = new();
        public List<string> RequiredIngredients { get; set; } = new();
        public List<string> SignatureIngredients { get; set; } = new();
        public int MinIngredientMatch { get; set; }
        public string Category { get; set; }
        public string DishType { get; set; }
        public List<PhaseTemplate> PhaseTemplate { get; set; } = new();
        public Dictionary<string, List<string>> StepKeywords { get; set; } = new();
    }

    public class PhaseTemplate
    {
        public int Phase { get; set; }
        public string Description { get; set; }
        public int Equipment { get; set; }
    }
}
