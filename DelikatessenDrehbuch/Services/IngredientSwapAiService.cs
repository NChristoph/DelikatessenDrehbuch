using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
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
            string? preferredProvider = null
        );
    }

    public class IngredientSwapAiService : IIngredientSwapAiService
    {
        private const string OpenAiEndpoint = "https://api.openai.com/v1/responses";
        private const string DefaultOpenAiModel = "gpt-4o-mini";
        private const int AiRequestTimeoutSeconds = 45;
        private const string IngredientListCacheKey = "swap_ingredients_list_v2";
        private static readonly TimeSpan IngredientListCacheDuration = TimeSpan.FromHours(24);

        private readonly HttpClient _httpClient;
        private readonly string _openaiApiKey;
        private readonly string _openAiModel;
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<IngredientSwapAiService> _logger;

        private sealed record OriginalUsageInfo(decimal DisplayQuantity, string DisplayUnit, decimal QuantityInGrams);

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
            string? preferredProvider = null)
        {
            var stopwatch = Stopwatch.StartNew();
            var provider = "openai";
            var normalizedLanguage = NormalizeLanguage(language);
            var normalizedGoal = string.IsNullOrWhiteSpace(goal) ? "default" : goal.Trim().ToLowerInvariant();

            var cacheKey = $"swap_{provider}_{original.Id}_{recipe.Id}_{normalizedGoal}_{normalizedLanguage}";
            if (_cache.TryGetValue<List<IngredientSwapOption>>(cacheKey, out var cachedSuggestions) && cachedSuggestions != null)
            {
                stopwatch.Stop();
                return (cachedSuggestions, provider, (int)stopwatch.ElapsedMilliseconds);
            }

            var suggestions = await GetSwapSuggestionsFromOpenAiAsync(original, recipe, goal, normalizedLanguage);

            _cache.Set(cacheKey, suggestions, TimeSpan.FromMinutes(30));

            stopwatch.Stop();
            return (suggestions, provider, (int)stopwatch.ElapsedMilliseconds);
        }

        private async Task<List<IngredientSwapOption>> GetSwapSuggestionsFromOpenAiAsync(
            IngredientsAndNutrients original,
            RecipeBaseData recipe,
            string? goal,
            string language)
        {
            EnsureApiKeyConfigured(_openaiApiKey, "OpenAI");

            var prompt = await BuildOptimizedSwapPromptAsync(original, recipe, goal, language);
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

            return await ParseAndValidateSuggestionsAsync(outputText, original, language);
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
            string language)
        {
            var relevantGroupIds = GetRelevantGroupsForSwap(original.GroupId, goal);
            var availableIngredients = await GetCachedIngredientListAsync(language, relevantGroupIds);
            var originalUsage = GetOriginalUsage(recipe, original);
            var responseLanguage = GetResponseLanguageName(language);

            var goalInstruction = goal?.Trim().ToLowerInvariant() switch
            {
                "low_carb" => "LOW CARB",
                "high_protein" => "HIGH PROTEIN",
                "vegan" => "VEGAN",
                "low_calorie" => "LOW CALORIE",
                _ => "HEALTHIER OR MORE INTERESTING"
            };

            return $@"Swap ingredient ""{GetIngredientName(original, language)}"" in recipe ""{recipe.Title}"".
Goal: {goalInstruction}
Original nutrition per 100g: {original.Calories_a_100g} kcal, protein {original.Protein_a_100g} g, carbs {original.Carbohydrates_a_100g} g, fat {original.Fat_a_100g} g.
Amount used in this recipe: {FormatDecimal(originalUsage.DisplayQuantity)} {originalUsage.DisplayUnit} (about {FormatDecimal(originalUsage.QuantityInGrams)} g).

Rules:
- Suggest at most 5 real alternatives from the available ingredient list.
- Keep the recipe style plausible.
- Prefer similar culinary use, not random ingredients.
- Suggested quantity must realistically replace the full amount used in this recipe, not a generic 100 g default.
- Use grams as unit.
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
                return new List<int?> { 1, 4, 7, 8, 9 };
            }

            if (originalGroupId == 3)
            {
                return new List<int?> { 2, 3, 9 };
            }

            return new List<int?> { originalGroupId, 9 };
        }

        private async Task<List<(int Id, string Name, string Nutrition)>> GetCachedIngredientListAsync(
            string language,
            List<int?> relevantGroupIds)
        {
            var cacheKey = $"{IngredientListCacheKey}_{language}_{string.Join("_", relevantGroupIds.Select(x => x?.ToString() ?? "null"))}";
            if (_cache.TryGetValue<List<(int Id, string Name, string Nutrition)>>(cacheKey, out var ingredientList) && ingredientList != null)
            {
                return ingredientList;
            }

            var loaded = await _context.IngredientsAndNutrients
                .Where(i => relevantGroupIds.Contains(i.GroupId))
                .OrderBy(i => i.Id)
                .Take(80)
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
                    Fat = i.Fat_a_100g
                })
                .ToListAsync();

            ingredientList = loaded
                .Select(i => (
                    i.Id,
                    GetIngredientName(i, language),
                    $"{i.Calories}kcal,P{i.Protein}g,C{i.Carbs}g,F{i.Fat}g"))
                .ToList();

            _cache.Set(cacheKey, ingredientList, IngredientListCacheDuration);
            return ingredientList;
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
                    Unit = string.IsNullOrWhiteSpace(suggestion.Unit) ? "g" : suggestion.Unit.Trim(),
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
                .Take(5)
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

            if (normalizedUnit is "l" or "liter")
            {
                return quantity * 1000m;
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
                        maxItems = 5,
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

        private sealed class IngredientListItem
        {
            public int Id { get; set; }
            public string Name_DE { get; set; } = string.Empty;
            public string Name_EN { get; set; } = string.Empty;
            public string Name_ESP { get; set; } = string.Empty;
            public string Name_PRT { get; set; } = string.Empty;
            public string Name_ID { get; set; } = string.Empty;
            public string Name_NL { get; set; } = string.Empty;
            public string Name_SE { get; set; } = string.Empty;
            public string Name_DK { get; set; } = string.Empty;
            public string Name_NO { get; set; } = string.Empty;
            public string Name_MS { get; set; } = string.Empty;
            public decimal Calories { get; set; }
            public decimal Protein { get; set; }
            public decimal Carbs { get; set; }
            public decimal Fat { get; set; }
        }
    }
}
