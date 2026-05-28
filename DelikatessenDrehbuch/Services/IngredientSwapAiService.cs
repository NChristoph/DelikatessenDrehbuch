using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DelikatessenDrehbuch.Services
{
    public interface IIngredientSwapAiService
    {
        Task<(List<IngredientSwapOption> suggestions, string provider, int processingTimeMs)> GetSwapSuggestionsAsync(
            IngredientsAndNutrients original,
            RecipeBaseData recipe,
            string? goal,
            string language,
            IReadOnlyList<IngredientSwap>? contextSwaps = null,
            string? preferredProvider = null
        );
    }

    public class IngredientSwapAiService : IIngredientSwapAiService
    {
        private const string OpenAiEndpoint = "https://api.openai.com/v1/responses";
        private const string DefaultOpenAiModel = "gpt-4o-mini";
        private const int AiRequestTimeoutSeconds = 45;
        // NOTE: We intentionally do not cache the ingredient candidate list.
        // The DB is the source of truth, and caching caused stale/incorrect candidates during tuning.
        private const int MaxSwapSuggestions = 8;

        private readonly HttpClient _httpClient;
        private readonly string _openaiApiKey;
        private readonly string _openAiModel;
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<IngredientSwapAiService> _logger;
        private static readonly string[] SeasoningGroupNameHints = new[]
        {
            // Keep ASCII in code; DB values may include umlauts.
            "gewuerz", "gewürz", "spice",
            "kraeuter", "kräuter", "herb"
        };

        private sealed record OriginalUsageInfo(decimal DisplayQuantity, string DisplayUnit, decimal QuantityInGrams);

        private sealed class IngredientListItem
        {
            public int Id { get; set; }
            public string Name_DE { get; set; } = string.Empty;
            public string Name_EN { get; set; } = string.Empty;
            public string Name_PRT { get; set; } = string.Empty;
            public string Name_ESP { get; set; } = string.Empty;
            public string Name_ID { get; set; } = string.Empty;
            public string Name_NL { get; set; } = string.Empty;
            public string Name_SE { get; set; } = string.Empty;
            public string Name_DK { get; set; } = string.Empty;
            public string Name_NO { get; set; } = string.Empty;
            public string Name_MS { get; set; } = string.Empty;
            public int Calories { get; set; }
            public decimal Protein { get; set; }
            public decimal Carbs { get; set; }
            public decimal Fat { get; set; }
            public int? FoodCategoryId { get; set; }
            public int? GroupId { get; set; }
            public bool IsLiquid { get; set; }
            public bool IsFat { get; set; }
            public bool IsPowder { get; set; }
            public bool IsHard { get; set; }
            public bool IsSoft { get; set; }
        }

        public IngredientSwapAiService(
            HttpClient httpClient,
            IConfiguration config,
            ApplicationDbContext context,
            IMemoryCache cache,
            ILogger<IngredientSwapAiService> logger)
        {
            _httpClient = httpClient;
            _openaiApiKey = (config["OpenAI:ApiKey"] ?? config["SecretKeyOpenAi"] ?? string.Empty).Trim();
            _openAiModel = (config["OpenAI:IngredientSwapModel"] ?? config["OpenAI:Model"] ?? DefaultOpenAiModel).Trim();
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        public async Task<(List<IngredientSwapOption> suggestions, string provider, int processingTimeMs)> GetSwapSuggestionsAsync(
            IngredientsAndNutrients original,
            RecipeBaseData recipe,
            string? goal,
            string language,
            IReadOnlyList<IngredientSwap>? contextSwaps = null,
            string? preferredProvider = null)
        {
            var stopwatch = Stopwatch.StartNew();
            var provider = "openai";
            var normalizedLanguage = NormalizeLanguage(language);
            var normalizedGoal = string.IsNullOrWhiteSpace(goal) ? "default" : goal.Trim().ToLowerInvariant();

            // Context matters: if the user has already queued/applied other swaps in the same recipe,
            // suggestions should reflect that. Include a short context key in caching so we don't serve stale suggestions.
            var contextKey = BuildContextKey(contextSwaps);
            var cacheKey = $"swap_{provider}_{original.Id}_{recipe.Id}_{normalizedGoal}_{normalizedLanguage}_{contextKey}";
            if (_cache.TryGetValue<List<IngredientSwapOption>>(cacheKey, out var cachedSuggestions) && cachedSuggestions != null)
            {
                stopwatch.Stop();
                return (cachedSuggestions, provider, (int)stopwatch.ElapsedMilliseconds);
            }

            var suggestions = await GetSwapSuggestionsFromOpenAiAsync(original, recipe, goal, normalizedLanguage, contextSwaps);

            _cache.Set(cacheKey, suggestions, TimeSpan.FromMinutes(30));

            stopwatch.Stop();
            return (suggestions, provider, (int)stopwatch.ElapsedMilliseconds);
        }

        private async Task<List<IngredientSwapOption>> GetSwapSuggestionsFromOpenAiAsync(
            IngredientsAndNutrients original,
            RecipeBaseData recipe,
            string? goal,
            string language,
            IReadOnlyList<IngredientSwap>? contextSwaps)
        {
            EnsureApiKeyConfigured(_openaiApiKey, "OpenAI");

            var prompt = await BuildOptimizedSwapPromptAsync(original, recipe, goal, language, contextSwaps);
            var requestBody = new
            {
                model = string.IsNullOrWhiteSpace(_openAiModel) ? DefaultOpenAiModel : _openAiModel,
                input = new object[]
                {
                    new
                    {
                        role = "system",
                        content = new object[]
                        {
                            new
                            {
                                type = "input_text",
                                text = "You are a culinary ingredient substitution assistant. Return only valid JSON that matches the schema."
                            }
                        }
                    },
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "input_text",
                                text = prompt
                            }
                        }
                    }
                },
                max_output_tokens = 1600,
                text = new
                {
                    format = new
                    {
                        type = "json_schema",
                        name = "ingredient_swap_suggestions",
                        strict = true,
                        schema = BuildOpenAiSchema()
                    }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, OpenAiEndpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _openaiApiKey);

            using var response = await SendWithTimeoutAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(BuildApiErrorMessage("OpenAI", response.StatusCode, responseBody));
            }

            using var document = JsonDocument.Parse(responseBody);
            var outputText = TryExtractOutputText(document.RootElement);
            if (string.IsNullOrWhiteSpace(outputText))
            {
                _logger.LogWarning("OpenAI ingredient swap call returned no output text. Body={Body}", responseBody);
                return new List<IngredientSwapOption>();
            }

            var parsed = await ParseAndValidateSuggestionsAsync(outputText, original, language, recipe);
            var hasCommunityVariants = await _context.RecipeCommunityVariants
                .AsNoTracking()
                .AnyAsync(v => v.OriginalRecipeId == recipe.Id);
            var maxSuggestions = hasCommunityVariants ? 4 : MaxSwapSuggestions;
            return parsed.Take(maxSuggestions).ToList();
        }

        private async Task<HttpResponseMessage> SendWithTimeoutAsync(HttpRequestMessage request)
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(AiRequestTimeoutSeconds));
            try
            {
                return await _httpClient.SendAsync(request, timeoutCts.Token);
            }
            catch (OperationCanceledException ex) when (timeoutCts.IsCancellationRequested)
            {
                throw new InvalidOperationException($"AI request timed out after {AiRequestTimeoutSeconds} seconds.", ex);
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException("Network error while calling the AI provider.", ex);
            }
        }

        private async Task<string> BuildOptimizedSwapPromptAsync(
            IngredientsAndNutrients original,
            RecipeBaseData recipe,
            string? goal,
            string language,
            IReadOnlyList<IngredientSwap>? contextSwaps)
        {
            var hasCommunityVariants = await _context.RecipeCommunityVariants
                .AsNoTracking()
                .AnyAsync(v => v.OriginalRecipeId == recipe.Id);
            var maxSuggestions = hasCommunityVariants ? 4 : MaxSwapSuggestions;

            var originalUsage = GetOriginalUsage(recipe, original);

            // For liquids we intentionally avoid group-filtering (GroupId tagging can be noisy),
            // and let the score pick the best liquid-like items from a larger pool.
            var relevantGroupIds = IsLiquidUnit(originalUsage.DisplayUnit)
                ? new List<int?> { null }
                : GetRelevantGroupsForSwap(original.GroupId, goal);

            // Avoid suggesting ingredients that are already used as swap targets elsewhere in this recipe.
            // (But don't exclude the swap target for THIS ingredient, so users can still see their current pick.)
            var excludeIngredientIds = new HashSet<int> { original.Id };
            var otherContextSwaps = (contextSwaps ?? Array.Empty<IngredientSwap>())
                .Where(s => s != null && s.FromIngredientId > 0 && s.ToIngredientId > 0 && s.FromIngredientId != original.Id)
                .ToList();
            foreach (var s in otherContextSwaps)
            {
                excludeIngredientIds.Add(s.ToIngredientId);
            }

            var availableIngredients = await GetCachedIngredientListAsync(
                language,
                original,
                goal,
                originalUsage.DisplayUnit,
                relevantGroupIds,
                excludeIngredientIds);
            var responseLanguage = GetResponseLanguageName(language);

            var goalInstruction = goal?.Trim().ToLowerInvariant() switch
            {
                "low_carb" => "LOW CARB",
                "high_protein" => "HIGH PROTEIN",
                "vegan" => "VEGAN",
                "low_calorie" => "LOW CALORIE",
                _ => "HEALTHIER OR MORE INTERESTING"
            };

            var unitRule = IsLiquidUnit(originalUsage.DisplayUnit) ? "ml" : "g";

            var contextBlock = string.Empty;
            if (otherContextSwaps.Count > 0)
            {
                // Keep it compact: this is context, not the main task.
                var lines = otherContextSwaps
                    .Take(10)
                    .Select(s => $"- {s.FromName} -> {s.ToName} ({FormatDecimal(s.NewQuantity)} {s.Unit})");
                contextBlock = "\n\nAlready selected swaps (context for this recipe):\n" + string.Join("\n", lines);
            }

            return $@"Swap ingredient ""{GetIngredientName(original, language)}"" in recipe ""{recipe.Title}"".
Goal: {goalInstruction}
Original nutrition per 100g: {original.Calories_a_100g} kcal, protein {original.Protein_a_100g} g, carbs {original.Carbohydrates_a_100g} g, fat {original.Fat_a_100g} g.
Amount used in this recipe: {FormatDecimal(originalUsage.DisplayQuantity)} {originalUsage.DisplayUnit} (about {FormatDecimal(originalUsage.QuantityInGrams)} g).
{contextBlock}

CRITICAL Rules - Follow Strictly:
- Suggest at most {maxSuggestions} real alternatives from the available ingredient list.
- ONLY suggest ingredients that serve the SAME CULINARY ROLE as the original ingredient.
- Match the ingredient's PURPOSE in the recipe:
  * Nuts → other nuts or seeds (NEVER grains, sweeteners, or vegetables)
  * Proteins (meat/fish) → other proteins from same category
  * Grains (rice/pasta) → other grains (NEVER proteins or sweeteners)
  * Sweeteners (sugar/honey) → other sweeteners (NEVER savory ingredients)
  * Vegetables → similar vegetables with similar texture
  * Herbs/Spices → other herbs/spices with similar flavor profile
  * Dairy → other dairy or appropriate plant-based alternatives
- REJECT suggestions that would drastically change the dish type or flavor category.
- Match texture and physical form: crunchy → crunchy, creamy → creamy, liquid → liquid.
- Suggested quantity must realistically replace the FULL amount used ({FormatDecimal(originalUsage.QuantityInGrams)} g), not a generic 100 g default.
- Use unit '{unitRule}' for quantity.
- Do not suggest the original ingredient itself.
- Keep reasons short and concrete.
- Write every natural-language field in {responseLanguage}.
- This includes reason, preparation_change, taste_impact, pros, and cons.

Available ingredients:
{string.Join("\n", availableIngredients.Select(i => $"{i.Id}|{i.Name}|{i.Nutrition}"))}";
        }

        private List<int?> GetRelevantGroupsForSwap(int? originalGroupId, string? goal)
        {
            if (string.Equals(goal, "vegan", StringComparison.OrdinalIgnoreCase))
            {
                return new List<int?> { 2, 3, 5, 8, 9 };
            }

            if (originalGroupId == 1 || originalGroupId == 7)
            {
                // In this project, all meat variants are stored under the same meat group.
                // Keep it simple and don't restrict further here, otherwise the candidate pool can collapse to one animal only.
                return new List<int?> { 1 };
            }

            if (originalGroupId == 3)
            {
                // Seafood-family: keep it seafood.
                return new List<int?> { 3 };
            }

            // Default: keep it to the same group. (We intentionally do NOT add a "generic" group here,
            // because that leaks unrelated vegetables for herbs/spices.)
            return new List<int?> { originalGroupId };
        }

        private async Task<List<(int Id, string Name, string Nutrition)>> GetCachedIngredientListAsync(
            string language,
            IngredientsAndNutrients original,
            string? goal,
            string? originalUnit,
            List<int?> relevantGroupIds,
            ISet<int>? excludeIngredientIds)
        {
            var unitHint = IsLiquidUnit(originalUnit) ? "liquid" : "solid";
            var normalizedGoal = string.IsNullOrWhiteSpace(goal) ? "default" : goal.Trim().ToLowerInvariant();
            // No caching here on purpose.
            List<(int Id, string Name, string Nutrition)> ingredientList;

            // Two-stage selection:
            // 1) DB preselection by "shared true flags" (fallback cascade: all flags match -> 1 may miss -> 2 may miss ...)
            // 2) Score ranking to pick the final compact candidate list sent to the AI.
            var baseQuery = _context.IngredientsAndNutrients.AsQueryable();

            if (excludeIngredientIds != null && excludeIngredientIds.Count > 0)
            {
                baseQuery = baseQuery.Where(i => !excludeIngredientIds.Contains(i.Id));
            }

            if (!(relevantGroupIds.Count == 1 && relevantGroupIds[0] == null))
            {
                baseQuery = baseQuery.Where(i => relevantGroupIds.Contains(i.GroupId));
            }

            // If the ingredient belongs to a seasoning group (herbs/spices), do NOT use FoodCategoryId
            // to narrow down candidates. FoodCategory is often too broad (e.g. "vegetables") and
            // causes nonsense suggestions like cauliflower for coriander.
            var isSeasoningGroup = await IsSeasoningGroupAsync(original.GroupId);

            // For vegan swaps, don't narrow too much; otherwise keep the pool close to the original.
            // IMPORTANT: If FoodCategoryId is set, prefer it strongly (herbs/spices often share a food-category,
            // while GroupId can be broad like "vegetables" and would leak weird options such as cauliflower for coriander).
            if (!string.Equals(goal, "vegan", StringComparison.OrdinalIgnoreCase))
            {
                if (!isSeasoningGroup && original.FoodCategoryId.HasValue)
                {
                    var strict = baseQuery.Where(i => i.FoodCategoryId == original.FoodCategoryId.Value);
                    var strictCount = await strict.Take(25).CountAsync();
                    baseQuery = strictCount >= 8
                        ? strict
                        : strict.Union(baseQuery.Where(i => i.GroupId == original.GroupId));
                }
                else if (original.GroupId.HasValue)
                {
                    baseQuery = baseQuery.Where(i => i.GroupId == original.GroupId.Value);
                }
            }

            var unitLiquid = IsLiquidUnit(originalUnit);
            var requiredFlags = 0;
            if (unitLiquid) requiredFlags++;
            if (original.is_fat) requiredFlags++;
            if (original.is_powder) requiredFlags++;
            if (original.is_hard) requiredFlags++;
            if (original.is_soft) requiredFlags++;

            var pool = new List<IngredientListItem>(capacity: 650);
            var usedIds = new HashSet<int>();

            // If we don't have any "true" flags, just take a reasonable pool and let score + diversification do the work.
            // (This avoids useless extra DB roundtrips.)
            var maxPool = 600;
            var roundTake = 260;

            if (requiredFlags == 0)
            {
                var round = await baseQuery
                    .OrderBy(i => i.Id)
                    .Take(maxPool)
                    .Select(i => new IngredientListItem
                    {
                        Id = i.Id,
                        Name_DE = i.Name_DE,
                        Name_EN = i.Name_EN,
                        Name_ESP = i.Name_ESP,
                        Name_PRT = i.Name_PRT,
                        Name_ID = i.Name_ID,
                        Name_NL = i.Name_NL,
                        Name_SE = i.Name_SE,
                        Name_DK = i.Name_DK,
                        Name_NO = i.Name_NO,
                        Name_MS = i.Name_MS,
                        Calories = i.Calories_a_100g,
                        Protein = i.Protein_a_100g,
                        Carbs = i.Carbohydrates_a_100g,
                        Fat = i.Fat_a_100g,
                        FoodCategoryId = i.FoodCategoryId,
                        GroupId = i.GroupId,
                        IsLiquid = i.is_liquid,
                        IsFat = i.is_fat,
                        IsPowder = i.is_powder,
                        IsHard = i.is_hard,
                        IsSoft = i.is_soft
                    })
                    .ToListAsync();

                foreach (var item in round)
                {
                    if (usedIds.Add(item.Id))
                    {
                        pool.Add(item);
                    }
                }
            }
            else
            {
                // Fallback cascade: require >= threshold shared-true flags.
                // missing=0 => threshold=requiredFlags (strict)
                // missing=1 => threshold=requiredFlags-1 (looser) ...
                var maxMissing = Math.Min(requiredFlags, 3); // don't explode DB work; score will handle the rest
                for (var missing = 0; missing <= maxMissing && pool.Count < maxPool; missing++)
                {
                    var threshold = Math.Max(1, requiredFlags - missing);

                    var round = await baseQuery
                        .Select(i => new
                        {
                            i,
                            MatchCount =
                                (unitLiquid ? (i.is_liquid ? 1 : 0) : 0) +
                                (original.is_fat ? (i.is_fat ? 1 : 0) : 0) +
                                (original.is_powder ? (i.is_powder ? 1 : 0) : 0) +
                                (original.is_hard ? (i.is_hard ? 1 : 0) : 0) +
                                (original.is_soft ? (i.is_soft ? 1 : 0) : 0)
                        })
                        .Where(x => x.MatchCount >= threshold)
                        .OrderByDescending(x => x.MatchCount)
                        .ThenBy(x => x.i.Id)
                        .Take(roundTake)
                        .Select(x => new IngredientListItem
                        {
                            Id = x.i.Id,
                            Name_DE = x.i.Name_DE,
                            Name_EN = x.i.Name_EN,
                            Name_ESP = x.i.Name_ESP,
                            Name_PRT = x.i.Name_PRT,
                            Name_ID = x.i.Name_ID,
                            Name_NL = x.i.Name_NL,
                            Name_SE = x.i.Name_SE,
                            Name_DK = x.i.Name_DK,
                            Name_NO = x.i.Name_NO,
                            Name_MS = x.i.Name_MS,
                            Calories = x.i.Calories_a_100g,
                            Protein = x.i.Protein_a_100g,
                            Carbs = x.i.Carbohydrates_a_100g,
                            Fat = x.i.Fat_a_100g,
                            FoodCategoryId = x.i.FoodCategoryId,
                            GroupId = x.i.GroupId,
                            IsLiquid = x.i.is_liquid,
                            IsFat = x.i.is_fat,
                            IsPowder = x.i.is_powder,
                            IsHard = x.i.is_hard,
                            IsSoft = x.i.is_soft
                        })
                        .ToListAsync();

                    foreach (var item in round)
                    {
                        if (usedIds.Add(item.Id))
                        {
                            pool.Add(item);
                            if (pool.Count >= maxPool)
                            {
                                break;
                            }
                        }
                    }
                }
            }

            _logger.LogInformation(
                "Swap candidates pool size={Count} scoring=true cascade=true requiredFlags={RequiredFlags} unitHint={UnitHint} relevantGroups=[{Groups}]",
                pool.Count,
                requiredFlags,
                unitHint,
                string.Join(",", relevantGroupIds.Select(x => x?.ToString() ?? "null")));

            var scored = pool
                .Where(x => x.Id != original.Id)
                .Select(x => new { Item = x, Score = ComputeMatchScore(original, originalUnit, x) })
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Item.Protein)
                .ThenBy(x => x.Item.Id)
                .Take(90)
                .Where(x => x.Score >= (IsLiquidUnit(originalUnit) ? 10m : -9999m))
                .Select(x => x.Item)
                .ToList();

            var diversified = DiversifyCandidates(scored, maxTotal: 55, maxPerFoodCategory: 10);

            ingredientList = diversified
                .Select(i => (
                    i.Id,
                    GetIngredientName(i, language),
                    $"{i.Calories}kcal,P{i.Protein}g"))
                .ToList();
            return ingredientList;
        }

        private async Task<bool> IsSeasoningGroupAsync(int? groupId)
        {
            if (!groupId.HasValue)
            {
                return false;
            }

            // Cheap cache: group ids are stable and rarely change.
            var cacheKey = "swap_seasoning_group_ids_v1";
            if (!_cache.TryGetValue<HashSet<int>>(cacheKey, out var ids) || ids == null)
            {
                var all = await _context.Group
                    .AsNoTracking()
                    .Select(g => new { g.Id, g.Name })
                    .ToListAsync();

                ids = all
                    .Where(g =>
                        SeasoningGroupNameHints.Any(h =>
                            (!string.IsNullOrWhiteSpace(g.Name) && g.Name.Contains(h, StringComparison.OrdinalIgnoreCase))))
                    .Select(g => g.Id)
                    .ToHashSet();

                _cache.Set(cacheKey, ids, TimeSpan.FromHours(6));
            }

            return ids.Contains(groupId.Value);
        }

        private static string BuildContextKey(IReadOnlyList<IngredientSwap>? contextSwaps)
        {
            if (contextSwaps == null || contextSwaps.Count == 0)
            {
                return "ctx0";
            }

            var normalized = contextSwaps
                .Where(s => s != null && s.FromIngredientId > 0 && s.ToIngredientId > 0)
                .OrderBy(s => s.FromIngredientId)
                .Select(s => $"{s.FromIngredientId}>{s.ToIngredientId}")
                .ToArray();

            if (normalized.Length == 0)
            {
                return "ctx0";
            }

            var raw = string.Join(",", normalized);
            var bytes = Encoding.UTF8.GetBytes(raw);
            var hash = SHA256.HashData(bytes);

            // Short, stable key (avoid long cache keys).
            var shortHex = Convert.ToHexString(hash.AsSpan(0, 6)).ToLowerInvariant();
            return $"ctx{normalized.Length}_{shortHex}";
        }

        private static List<IngredientListItem> DiversifyCandidates(
            List<IngredientListItem> pool,
            int maxTotal,
            int maxPerFoodCategory)
        {
            if (pool == null || pool.Count == 0)
            {
                return new List<IngredientListItem>();
            }

            maxTotal = Math.Max(1, maxTotal);
            maxPerFoodCategory = Math.Max(1, maxPerFoodCategory);

            // Take a few from each food category to avoid "all pork cuts" bias.
            var grouped = pool
                .GroupBy(x => x.FoodCategoryId ?? -1)
                .OrderByDescending(g => g.Count())
                .ToList();

            var result = new List<IngredientListItem>(capacity: Math.Min(maxTotal, pool.Count));
            foreach (var g in grouped)
            {
                foreach (var item in g.Take(maxPerFoodCategory))
                {
                    result.Add(item);
                    if (result.Count >= maxTotal)
                    {
                        return result;
                    }
                }
            }

            // Fill remaining with best of the rest (still ordered by protein desc from the query).
            if (result.Count < maxTotal)
            {
                var used = result.Select(x => x.Id).ToHashSet();
                foreach (var item in pool)
                {
                    if (used.Add(item.Id))
                    {
                        result.Add(item);
                        if (result.Count >= maxTotal)
                        {
                            break;
                        }
                    }
                }
            }

            return result;
        }

        private async Task<List<IngredientSwapOption>> ParseAndValidateSuggestionsAsync(
            string? jsonResponse,
            IngredientsAndNutrients original,
            string language,
            RecipeBaseData? recipe = null)
        {
            if (string.IsNullOrWhiteSpace(jsonResponse))
            {
                return new List<IngredientSwapOption>();
            }

            GeminiSwapResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<GeminiSwapResponse>(jsonResponse, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Failed to parse AI response: {ex.Message}", ex);
            }

            if (parsed?.Suggestions == null || parsed.Suggestions.Count == 0)
            {
                return new List<IngredientSwapOption>();
            }

            var ingredientIds = parsed.Suggestions
                .Select(s => s.IngredientId)
                .Where(id => id > 0 && id != original.Id)
                .Distinct()
                .ToList();

            var ingredientsDict = await _context.IngredientsAndNutrients
                .Where(i => ingredientIds.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id);

            var validated = new List<IngredientSwapOption>();
            var originalUsage = GetOriginalUsage(recipe, original);
            var isLiquidSwap = IsLiquidUnit(originalUsage.DisplayUnit);

            foreach (var suggestion in parsed.Suggestions)
            {
                if (suggestion.IngredientId == original.Id)
                {
                    continue;
                }

                if (!ingredientsDict.TryGetValue(suggestion.IngredientId, out var ingredient))
                {
                    continue;
                }

                if (suggestion.Quantity <= 0)
                {
                    continue;
                }

                validated.Add(new IngredientSwapOption
                {
                    IngredientId = suggestion.IngredientId,
                    Name = GetIngredientName(ingredient, language),
                    Quantity = suggestion.Quantity,
                    Unit = NormalizeSwapUnit(isLiquidSwap, suggestion.Unit),
                    Reason = string.IsNullOrWhiteSpace(suggestion.Reason) ? "Good culinary alternative for this recipe." : suggestion.Reason.Trim(),
                    CompatibilityScore = Math.Clamp(suggestion.CompatibilityScore, 0d, 1d),
                    PreparationChange = string.IsNullOrWhiteSpace(suggestion.PreparationChange) ? null : suggestion.PreparationChange.Trim(),
                    TasteImpact = string.IsNullOrWhiteSpace(suggestion.TasteImpact) ? "Similar overall taste profile." : suggestion.TasteImpact.Trim(),
                    Pros = suggestion.Pros?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().Take(4).ToList() ?? new List<string>(),
                    Cons = suggestion.Cons?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().Take(4).ToList() ?? new List<string>(),
                    Delta = CalculateActualDelta(originalUsage.QuantityInGrams, original, ingredient, suggestion.Quantity)
                });
            }

            return validated
                .OrderByDescending(s => s.CompatibilityScore)
                .ThenByDescending(s => s.Delta.ProteinDelta)
                .Take(MaxSwapSuggestions)
                .ToList();
        }

        private NutritionDelta CalculateActualDelta(
            decimal originalQuantityInGrams,
            IngredientsAndNutrients original,
            IngredientsAndNutrients replacement,
            decimal newQuantity)
        {
            var originalWeight = originalQuantityInGrams > 0 ? originalQuantityInGrams : (original.Weight_per_piece > 0 ? original.Weight_per_piece : 100m);

            var originalTotal = new
            {
                Calories = original.Calories_a_100g * (originalWeight / 100m),
                Protein = original.Protein_a_100g * (originalWeight / 100m),
                Carbs = original.Carbohydrates_a_100g * (originalWeight / 100m),
                Fat = original.Fat_a_100g * (originalWeight / 100m)
            };

            var replacementTotal = new
            {
                Calories = replacement.Calories_a_100g * (newQuantity / 100m),
                Protein = replacement.Protein_a_100g * (newQuantity / 100m),
                Carbs = replacement.Carbohydrates_a_100g * (newQuantity / 100m),
                Fat = replacement.Fat_a_100g * (newQuantity / 100m)
            };

            return new NutritionDelta
            {
                CaloriesDelta = replacementTotal.Calories - originalTotal.Calories,
                ProteinDelta = replacementTotal.Protein - originalTotal.Protein,
                CarbsDelta = replacementTotal.Carbs - originalTotal.Carbs,
                FatDelta = replacementTotal.Fat - originalTotal.Fat
            };
        }

        private static OriginalUsageInfo GetOriginalUsage(RecipeBaseData? recipe, IngredientsAndNutrients original)
        {
            var fallbackGrams = original.Weight_per_piece > 0 ? original.Weight_per_piece : 100m;

            var recipeIngredient = recipe?.Ingredients?
                .FirstOrDefault(ri => ri?.Ingredient?.IngredientsAndNutrients?.Id == original.Id);

            var quantityValue = recipeIngredient?.Ingredient?.Quantity?.Quantitys;
            var measure = recipeIngredient?.Ingredient?.Measure;

            if (quantityValue == null || quantityValue <= 0)
            {
                return new OriginalUsageInfo(fallbackGrams, "g", fallbackGrams);
            }

            var quantity = (decimal)quantityValue.Value;
            var unit = (measure?.UnitOfMeasurement ?? "g").Trim();
            var grams = ConvertToGrams(quantity, unit, measure?.IsPieceUnit() == true, original.Weight_per_piece);

            return new OriginalUsageInfo(quantity, string.IsNullOrWhiteSpace(unit) ? "g" : unit, grams);
        }

        private static decimal ConvertToGrams(decimal quantity, string? unit, bool isPieceUnit, int weightPerPiece)
        {
            var normalizedUnit = (unit ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedUnit is "g" or "gr" or "gram" or "grams" or "ml")
            {
                return quantity;
            }

            if (normalizedUnit is "kg" or "kilogramm" or "kilogram")
            {
                return quantity * 1000m;
            }

            if (normalizedUnit is "l" or "liter" or "litre")
            {
                return quantity * 1000m;
            }

            // Tablespoon conversions (approximate for solids)
            if (normalizedUnit is "el" or "essl" or "esslöffel" or "tbsp" or "tablespoon" or "tablespoons")
            {
                return quantity * 15m; // ~15g per tablespoon for most solids
            }

            // Teaspoon conversions (approximate for solids)
            if (normalizedUnit is "tl" or "teel" or "teelöffel" or "tsp" or "teaspoon" or "teaspoons")
            {
                return quantity * 5m; // ~5g per teaspoon for most solids
            }

            // Cup conversions (approximate)
            if (normalizedUnit is "cup" or "cups" or "tasse" or "tassen")
            {
                return quantity * 200m; // ~200g per cup (varies by ingredient)
            }

            // Ounce conversions
            if (normalizedUnit is "oz" or "ounce" or "ounces" or "unze" or "unzen")
            {
                return quantity * 28.35m; // 1 oz = 28.35g
            }

            // Pound conversions
            if (normalizedUnit is "lb" or "lbs" or "pound" or "pounds" or "pfund")
            {
                return quantity * 453.59m; // 1 lb = 453.59g
            }

            if (isPieceUnit && weightPerPiece > 0)
            {
                return quantity * weightPerPiece;
            }

            return weightPerPiece > 0 ? quantity * weightPerPiece : quantity;
        }

        private static string FormatDecimal(decimal value)
        {
            return value % 1m == 0m
                ? value.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
                : value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string GetResponseLanguageName(string language)
        {
            return language switch
            {
                "en" => "English",
                "es" => "Spanish",
                "pt" => "Portuguese",
                "id" => "Indonesian",
                "nl" => "Dutch",
                "sv" => "Swedish",
                "da" => "Danish",
                "nb" => "Norwegian Bokmal",
                "ms" => "Malay",
                _ => "German"
            };
        }

        private static string NormalizeSwapUnit(bool isLiquidSwap, string? suggestedUnit)
        {
            _ = suggestedUnit; // We keep output units stable for UI consistency.
            return isLiquidSwap ? "ml" : "g";
        }

        private static bool IsLiquidUnit(string? unit)
        {
            var u = (unit ?? string.Empty).Trim().ToLowerInvariant();
            return u is "ml" or "l" or "liter" or "litre";
        }

        private static decimal ComputeMatchScore(IngredientsAndNutrients original, string? originalUnit, IngredientListItem candidate)
        {
            var score = 0m;
            var unitLiquid = IsLiquidUnit(originalUnit);

            // Unit hint is the strongest signal: if the recipe uses ml/l, liquids should float to the top.
            if (unitLiquid)
            {
                // Keep this decisive, otherwise solids can leak into liquid swaps when DB tags are sparse.
                score += candidate.IsLiquid ? 60m : -80m;
            }
            else
            {
                score += candidate.IsLiquid ? -25m : 10m;
            }

            // Match physical flags where present (DB tagging can be imperfect, so keep weights moderate).
            if (original.is_fat && candidate.IsFat) score += 15m;
            if (original.is_powder && candidate.IsPowder) score += 15m;
            if (original.is_hard && candidate.IsHard) score += 6m;
            if (original.is_soft && candidate.IsSoft) score += 6m;

            // Prefer same group/category when available, but don't hard-lock (keeps "pork only" from happening).
            if (original.GroupId.HasValue && candidate.GroupId == original.GroupId) score += 12m;
            if (original.FoodCategoryId.HasValue && candidate.FoodCategoryId == original.FoodCategoryId) score += 8m;

            // CRITICAL: Nutritional profile matching to filter out incompatible ingredients
            // This prevents suggesting grains for nuts, sweeteners for proteins, etc.
            var origProtein = original.Protein_a_100g;
            var origCarbs = original.Carbohydrates_a_100g;
            var origFat = original.Fat_a_100g;
            var candProtein = candidate.Protein;
            var candCarbs = candidate.Carbs;
            var candFat = candidate.Fat;

            // Calculate nutritional profile similarity
            // Heavily penalize ingredients with drastically different macro ratios
            var proteinDiff = Math.Abs(origProtein - candProtein);
            var carbsDiff = Math.Abs(origCarbs - candCarbs);
            var fatDiff = Math.Abs(origFat - candFat);

            // If macros are vastly different, this is probably a bad swap
            // Example: peanuts (50% fat) vs rice (1% fat) → fatDiff = 49 → penalty = -24.5
            score -= (proteinDiff + carbsDiff + fatDiff) / 6m;

            // Extra penalty for extreme mismatches
            // If the dominant macro is completely different, heavily penalize
            var origDominantMacro = Math.Max(origProtein, Math.Max(origCarbs, origFat));
            var candDominantMacro = Math.Max(candProtein, Math.Max(candCarbs, candFat));

            // Check if dominant macros align
            var origIsFatDominant = origFat >= origProtein && origFat >= origCarbs && origFat > 15m;
            var origIsProteinDominant = origProtein >= origFat && origProtein >= origCarbs && origProtein > 10m;
            var origIsCarbDominant = origCarbs >= origFat && origCarbs >= origProtein && origCarbs > 40m;

            var candIsFatDominant = candFat >= candProtein && candFat >= candCarbs && candFat > 15m;
            var candIsProteinDominant = candProtein >= candFat && candProtein >= candCarbs && candProtein > 10m;
            var candIsCarbDominant = candCarbs >= candFat && candCarbs >= candProtein && candCarbs > 40m;

            // Heavily penalize if dominant macro doesn't match
            if (origIsFatDominant && !candIsFatDominant) score -= 40m;
            if (origIsProteinDominant && !candIsProteinDominant) score -= 40m;
            if (origIsCarbDominant && !candIsCarbDominant) score -= 40m;

            // Bonus for matching dominant macro
            if (origIsFatDominant && candIsFatDominant) score += 20m;
            if (origIsProteinDominant && candIsProteinDominant) score += 20m;
            if (origIsCarbDominant && candIsCarbDominant) score += 20m;

            return score;
        }

        private static object BuildOpenAiSchema()
        {
            return new
            {
                type = "object",
                additionalProperties = false,
                properties = new
                {
                    suggestions = new
                    {
                        type = "array",
                        maxItems = MaxSwapSuggestions,
                        items = new
                        {
                            type = "object",
                            additionalProperties = false,
                            properties = new
                            {
                                ingredient_id = new { type = "integer" },
                                ingredient_name = new { type = "string" },
                                quantity = new { type = "number" },
                                unit = new { type = "string" },
                                reason = new { type = "string" },
                                compatibility_score = new { type = "number" },
                                nutrition_delta = new
                                {
                                    type = "object",
                                    additionalProperties = false,
                                    properties = new
                                    {
                                        calories = new { type = "number" },
                                        protein = new { type = "number" },
                                        carbs = new { type = "number" },
                                        fat = new { type = "number" }
                                    },
                                    required = new[] { "calories", "protein", "carbs", "fat" }
                                },
                                preparation_change = new { type = new[] { "string", "null" } },
                                taste_impact = new { type = "string" },
                                pros = new
                                {
                                    type = "array",
                                    items = new { type = "string" }
                                },
                                cons = new
                                {
                                    type = "array",
                                    items = new { type = "string" }
                                }
                            },
                            required = new[]
                            {
                                "ingredient_id",
                                "ingredient_name",
                                "quantity",
                                "unit",
                                "reason",
                                "compatibility_score",
                                "nutrition_delta",
                                "preparation_change",
                                "taste_impact",
                                "pros",
                                "cons"
                            }
                        }
                    }
                },
                required = new[] { "suggestions" }
            };
        }

        private static string NormalizeLanguage(string? language)
        {
            return string.IsNullOrWhiteSpace(language) ? "de" : language.Trim().ToLowerInvariant();
        }

        private static void EnsureApiKeyConfigured(string apiKey, string providerName)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException($"{providerName} API key is missing.");
            }
        }

        private static string BuildApiErrorMessage(string providerName, HttpStatusCode statusCode, string? responseContent)
        {
            var compactBody = (responseContent ?? string.Empty).Trim();
            if (compactBody.Length > 600)
            {
                compactBody = compactBody.Substring(0, 600) + "...";
            }

            return string.IsNullOrWhiteSpace(compactBody)
                ? $"{providerName} API returned {(int)statusCode}."
                : $"{providerName} API returned {(int)statusCode}: {compactBody}";
        }

        private static string? TryExtractOutputText(JsonElement root)
        {
            if (root.TryGetProperty("output_text", out var outputTextElement) && outputTextElement.ValueKind == JsonValueKind.String)
            {
                return ExtractJsonPayloadFromString(outputTextElement.GetString());
            }

            if (root.TryGetProperty("output", out var outputElement) && outputElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in outputElement.EnumerateArray())
                {
                    if (!item.TryGetProperty("content", out var contentElement) || contentElement.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var contentItem in contentElement.EnumerateArray())
                    {
                        if (contentItem.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String)
                        {
                            var extracted = ExtractJsonPayloadFromString(textElement.GetString());
                            if (!string.IsNullOrWhiteSpace(extracted))
                            {
                                return extracted;
                            }
                        }
                    }
                }
            }

            if (root.TryGetProperty("choices", out var choicesElement)
                && choicesElement.ValueKind == JsonValueKind.Array
                && choicesElement.GetArrayLength() > 0)
            {
                var firstChoice = choicesElement[0];
                if (firstChoice.TryGetProperty("message", out var messageElement)
                    && messageElement.TryGetProperty("content", out var contentElement)
                    && contentElement.ValueKind == JsonValueKind.String)
                {
                    return ExtractJsonPayloadFromString(contentElement.GetString());
                }
            }

            return null;
        }

        private static string? ExtractJsonPayloadFromString(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            if ((trimmed.StartsWith("{") && trimmed.EndsWith("}")) || (trimmed.StartsWith("[") && trimmed.EndsWith("]")))
            {
                return trimmed;
            }

            var firstBrace = trimmed.IndexOf('{');
            var lastBrace = trimmed.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                return trimmed.Substring(firstBrace, lastBrace - firstBrace + 1);
            }

            return null;
        }

        private static string GetIngredientName(IngredientsAndNutrients ingredient, string language)
        {
            return language switch
            {
                "en" => ingredient.Name_EN,
                "es" => ingredient.Name_ESP,
                "pt" => ingredient.Name_PRT,
                "id" => ingredient.Name_ID,
                "nl" => ingredient.Name_NL,
                "sv" => ingredient.Name_SE,
                "da" => ingredient.Name_DK,
                "nb" => ingredient.Name_NO,
                "ms" => ingredient.Name_MS,
                _ => ingredient.Name_DE
            };
        }

        private static string GetIngredientName(IngredientListItem ingredient, string language)
        {
            return language switch
            {
                "en" => ingredient.Name_EN,
                "es" => ingredient.Name_ESP,
                "pt" => ingredient.Name_PRT,
                "id" => ingredient.Name_ID,
                "nl" => ingredient.Name_NL,
                "sv" => ingredient.Name_SE,
                "da" => ingredient.Name_DK,
                "nb" => ingredient.Name_NO,
                "ms" => ingredient.Name_MS,
                _ => ingredient.Name_DE
            };
        }

    }
}
