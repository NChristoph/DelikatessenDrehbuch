using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using DelikatessenDrehbuch.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using DelikatessenDrehbuch.Services.Interfaces;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    public sealed class RecipeAiTransformService : IRecipeAiTransformService
    {
        private const string OpenAiEndpoint = "https://api.openai.com/v1/responses";
        private const string ModelName = "gpt-5-mini";
        private const int MaxChangeRounds = 2;
        private const string MasterStepDataPath = "wwwroot/data/master_steps.json";
        private const string MasterStepVariableDataPath = "wwwroot/data/master_step_variables.json";

        private static readonly Regex TemplateVariableRegex = new(@"{{\s*([^}]+?)\s*}}", RegexOptions.Compiled);
        private static readonly Regex OptionalTemplateRegex = new(@"\(([^()]*{{\s*[^}]+?\s*}}[^()]*)\)|\[([^\[\]]*{{\s*[^}]+?\s*}}[^\[\]]*)\]", RegexOptions.Compiled);
        private static readonly Regex FallbackTokenRegex = new(@"\{~([^~]+)~([^~]+)~\}", RegexOptions.Compiled);
        private static readonly Regex MultiWhitespaceRegex = new(@"\s{2,}", RegexOptions.Compiled);
        private static readonly Regex SpaceBeforePunctuationRegex = new(@"\s+([,.;:!?])", RegexOptions.Compiled);
        private static readonly Regex EggPoachGermanRegex = new(@"^Gib\s+Eier\s+\((.+?)\)\s+dazu\s+(\d+)\s+Minuten\s+vorsichtig\s+aufschlagen\s+und\s+inschwimmen\s+lassen,\s+bis\s+sie\s+gestockt\.?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex MissingGermanVerbRegex = new(@"\bbis\s+sie\s+gestockt\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex GermanTimeAfterDazuRegex = new(@"\bdazu\s+(\d+)\s+Minuten\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<RecipeAiTransformService> _logger;
        private readonly IIngredientResolverService _ingredientResolverService;
        private readonly Lazy<Dictionary<string, MasterStepTemplateDefinition>> _masterStepsByKey;
        private readonly Lazy<Dictionary<string, MasterStepVariableDefinition>> _masterStepVariablesByName;

        public RecipeAiTransformService(
            HttpClient httpClient,
            IConfiguration configuration,
            IHostEnvironment environment,
            IIngredientResolverService ingredientResolverService,
            ILogger<RecipeAiTransformService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _environment = environment;
            _ingredientResolverService = ingredientResolverService;
            _logger = logger;
            _masterStepsByKey = new Lazy<Dictionary<string, MasterStepTemplateDefinition>>(LoadMasterSteps, LazyThreadSafetyMode.ExecutionAndPublication);
            _masterStepVariablesByName = new Lazy<Dictionary<string, MasterStepVariableDefinition>>(LoadMasterStepVariables, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public async Task<RecipeAiTransformPreview> BuildPreviewAsync(
            RecipeBaseData recipe,
            string variantType,
            string language,
            string? userNote,
            int appliedChangeCount,
            IReadOnlyList<RecipeAiIngredientSuggestionItem>? selectedIngredients = null,
            bool forceFallback = false,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(recipe);

            var normalizedVariantType = NormalizeVariantType(variantType);
            if (string.IsNullOrWhiteSpace(normalizedVariantType))
            {
                throw new InvalidOperationException("AI-Variante ist erforderlich.");
            }

            var normalizedLanguage = NormalizeLanguage(language);
            var safeChangeCount = Math.Clamp(appliedChangeCount, 0, MaxChangeRounds);
            var trimmedUserNote = (userNote ?? string.Empty).Trim();
            if (safeChangeCount >= MaxChangeRounds && !string.IsNullOrWhiteSpace(trimmedUserNote))
            {
                throw new InvalidOperationException("Maximal zwei AI-Änderungen sind erlaubt.");
            }

            var selectedIngredientList = (selectedIngredients ?? Array.Empty<RecipeAiIngredientSuggestionItem>())
                .Where(x => x != null)
                .Take(2)
                .ToList();
            var primarySelectedIngredient = selectedIngredientList.FirstOrDefault();

            var sourceStepPlan = BuildSourceStepPlan(recipe, normalizedLanguage);
            var stepSelectionContext = BuildStepSelectionContext(sourceStepPlan, selectedIngredientList, normalizedVariantType);

            if (forceFallback)
            {
                return BuildFallbackPreview(recipe, normalizedVariantType, normalizedLanguage, trimmedUserNote, safeChangeCount, sourceStepPlan, stepSelectionContext, selectedIngredientList);
            }

            var apiKey = Environment.GetEnvironmentVariable("SecretKeyOpenAi") ?? _configuration["SecretKeyOpenAi"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                if (_environment.IsDevelopment())
                {
                    _logger.LogWarning("SecretKeyOpenAi fehlt. Verwende lokalen Rezept-AI-Fallback für RecipeId={RecipeId} VariantType={VariantType}.", recipe.Id, normalizedVariantType);
                    return BuildFallbackPreview(recipe, normalizedVariantType, normalizedLanguage, trimmedUserNote, safeChangeCount, sourceStepPlan, stepSelectionContext, selectedIngredientList);
                }

                throw new InvalidOperationException("Die Umgebungsvariable oder Konfiguration 'SecretKeyOpenAi' ist nicht gesetzt.");
            }

            var requestBody = BuildOpenAiRequestBody(recipe, normalizedVariantType, normalizedLanguage, trimmedUserNote, safeChangeCount, sourceStepPlan, stepSelectionContext, selectedIngredientList);
            using var request = new HttpRequestMessage(HttpMethod.Post, OpenAiEndpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OpenAI recipe transform failed: {StatusCode} {Body}", response.StatusCode, responseContent);
                throw new InvalidOperationException(BuildApiErrorMessage(response.StatusCode, responseContent));
            }

            using var document = JsonDocument.Parse(responseContent);
            var outputText = TryExtractOutputText(document.RootElement);
            if (string.IsNullOrWhiteSpace(outputText))
            {
                _logger.LogError("OpenAI recipe transform returned no output_text. Response: {Body}", responseContent);
                throw new InvalidOperationException(BuildApiErrorMessage(response.StatusCode, responseContent, "AI hat keine verwertbare Vorschau geliefert."));
            }

            var preview = JsonSerializer.Deserialize<RecipeAiTransformPreview>(outputText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("AI-Vorschau konnte nicht gelesen werden.");

            NormalizePreview(preview, recipe, normalizedVariantType, normalizedLanguage, trimmedUserNote, safeChangeCount, false, sourceStepPlan, stepSelectionContext.AllowedStepKeys, allowOnlySourceKeys: sourceStepPlan.Count > 0);
            return preview;
        }

        public async Task<RecipeAiIngredientSuggestionResponse> BuildIngredientSuggestionsAsync(
            RecipeBaseData recipe,
            string variantType,
            string language,
            string? userNote,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(recipe);

            var normalizedVariantType = NormalizeVariantType(variantType);
            if (string.IsNullOrWhiteSpace(normalizedVariantType))
            {
                throw new InvalidOperationException("AI-Variante ist erforderlich.");
            }

            var normalizedLanguage = NormalizeLanguage(language);
            var trimmedUserNote = (userNote ?? string.Empty).Trim();
            var apiKey = Environment.GetEnvironmentVariable("SecretKeyOpenAi") ?? _configuration["SecretKeyOpenAi"];

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                if (_environment.IsDevelopment())
                {
                    _logger.LogWarning("SecretKeyOpenAi fehlt. Verwende lokalen Zutaten-Fallback für RecipeId={RecipeId} VariantType={VariantType}.", recipe.Id, normalizedVariantType);
                    return await BuildIngredientSuggestionFallbackAsync(recipe, normalizedVariantType, normalizedLanguage, trimmedUserNote, cancellationToken);
                }

                throw new InvalidOperationException("Die Umgebungsvariable oder Konfiguration 'SecretKeyOpenAi' ist nicht gesetzt.");
            }

            var ingredientSelectionContext = AnalyzeIngredientSelectionContext(recipe);
            var defaultCategoryKeys = GetDefaultCategoryKeysForVariant(normalizedVariantType, ingredientSelectionContext);
            var recipeContext = BuildIngredientSelectionRecipeContext(recipe, normalizedLanguage, ingredientSelectionContext);

            var categoryPlan = await SelectIngredientCategoriesAsync(
                recipe,
                normalizedVariantType,
                normalizedLanguage,
                trimmedUserNote,
                defaultCategoryKeys,
                recipeContext,
                apiKey,
                cancellationToken);

            var preferredCategoryKeys = categoryPlan.PreferredCategoryKeys
                .Select(NormalizeCategoryKey)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (preferredCategoryKeys.Count == 0)
            {
                preferredCategoryKeys = defaultCategoryKeys;
            }

            var existingIngredientIds = recipe.Ingredients?
                .Select(x => x.Ingredient?.IngredientsAndNutrients?.Id ?? 0)
                .Where(x => x > 0)
                .Distinct()
                .ToList() ?? new List<int>();

            var candidates = await _ingredientResolverService.GetCandidatesForGoalAsync(
                normalizedVariantType,
                normalizedLanguage,
                preferredCategoryKeys,
                existingIngredientIds,
                take: 18,
                cancellationToken: cancellationToken);

            if (candidates.Count == 0 && !preferredCategoryKeys.SequenceEqual(defaultCategoryKeys, StringComparer.OrdinalIgnoreCase))
            {
                candidates = await _ingredientResolverService.GetCandidatesForGoalAsync(
                    normalizedVariantType,
                    normalizedLanguage,
                    defaultCategoryKeys,
                    existingIngredientIds,
                    take: 18,
                    cancellationToken: cancellationToken);
            }

            if (candidates.Count == 0)
            {
                return await BuildIngredientSuggestionFallbackAsync(recipe, normalizedVariantType, normalizedLanguage, trimmedUserNote, cancellationToken);
            }

            candidates = FilterCandidatesForRecipeContext(candidates, normalizedVariantType, ingredientSelectionContext);
            if (candidates.Count == 0)
            {
                return await BuildIngredientSuggestionFallbackAsync(recipe, normalizedVariantType, normalizedLanguage, trimmedUserNote, cancellationToken);
            }

            var selected = await SelectConcreteIngredientsAsync(
                recipe,
                normalizedVariantType,
                normalizedLanguage,
                trimmedUserNote,
                candidates,
                apiKey,
                cancellationToken);

            return new RecipeAiIngredientSuggestionResponse
            {
                VariantType = normalizedVariantType,
                VariantLabel = GetVariantLabel(normalizedVariantType, normalizedLanguage),
                Strategy = string.IsNullOrWhiteSpace(categoryPlan.Strategy) ? "add" : categoryPlan.Strategy,
                Reasoning = string.IsNullOrWhiteSpace(categoryPlan.Reasoning)
                    ? LocalizedWord(normalizedLanguage, "Drei passende Zutaten aus deiner Datenbank wurden ausgewählt.", "Three suitable ingredients from your database were selected.", "Se seleccionaron tres ingredientes adecuados de tu base de datos.", "Foram selecionados três ingredientes adequados da tua base de dados.")
                    : categoryPlan.Reasoning,
                UsedFallback = false,
                PreferredCategoryKeys = preferredCategoryKeys,
                PreferredCategoryLabels = selected
                    .Select(x => x.CategoryLabel)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                Suggestions = selected.Select(x => new RecipeAiIngredientSuggestionItem
                {
                    IngredientId = x.IngredientId,
                    Name = x.Name,
                    CategoryKey = x.CategoryKey,
                    CategoryLabel = x.CategoryLabel,
                    ProteinPer100g = x.ProteinPer100g,
                    CarbsPer100g = x.CarbsPer100g,
                    FatPer100g = x.FatPer100g,
                    FiberPer100g = x.FiberPer100g,
                    CaloriesPer100g = x.CaloriesPer100g,
                    Score = x.Score,
                    AiReason = x.MatchType
                }).ToList()
            };
        }

        private object BuildOpenAiRequestBody(
            RecipeBaseData recipe,
            string variantType,
            string language,
            string userNote,
            int appliedChangeCount,
            IReadOnlyList<RecipeAiTransformStepPlanItem> sourceStepPlan,
            StepSelectionContext stepSelectionContext,
            IReadOnlyList<RecipeAiIngredientSuggestionItem> selectedIngredients)
        {
            var sourceIngredients = BuildIngredientPreview(recipe, language);
            var variantLabel = GetVariantLabel(variantType, language);
            var languageLabel = GetLanguageLabel(language);
            var allowedStepKeys = stepSelectionContext.AllowedStepKeys.ToArray();
            var selectedIngredientContext = selectedIngredients
                .Select(selectedIngredient => new
                {
                    id = selectedIngredient.IngredientId,
                    name = selectedIngredient.Name,
                    categoryKey = selectedIngredient.CategoryKey,
                    categoryLabel = selectedIngredient.CategoryLabel,
                    proteinPer100g = selectedIngredient.ProteinPer100g,
                    carbsPer100g = selectedIngredient.CarbsPer100g,
                    fatPer100g = selectedIngredient.FatPer100g,
                    fiberPer100g = selectedIngredient.FiberPer100g,
                    caloriesPer100g = selectedIngredient.CaloriesPer100g
                })
                .ToList();
            var serializedSourceStepPlan = sourceStepPlan.Select(x => new
            {
                masterStepKey = x.MasterStepKey,
                phase = x.Phase,
                variables = x.Variables
            }).ToList();

            var stepTemplateContext = sourceStepPlan
                .Select(x => _masterStepsByKey.Value.TryGetValue(x.MasterStepKey, out var def) ? def : null)
                .Where(x => x != null)
                .DistinctBy(x => x!.MasterId)
                .Select(x => BuildStepPromptContext(x!, language))
                .ToList();

            var keepableExistingSteps = stepSelectionContext.KeepExistingSteps
                .Select(x => new
                {
                    masterStepKey = x.MasterStepKey,
                    reason = "Kann wahrscheinlich erhalten bleiben."
                })
                .ToList();

            var replaceableExistingSteps = stepSelectionContext.ReplaceExistingSteps
                .Select(x => new
                {
                    masterStepKey = x.MasterStepKey,
                    reason = selectedIngredients.Count == 0
                        ? "Bei Bedarf austauschen."
                        : $"Dieser Schritt passt eher zur alten Zutat und sollte für {string.Join(", ", selectedIngredients.Select(x => x.Name))} bevorzugt ersetzt werden."
                })
                .ToList();

            var selectedIngredientStepCandidates = stepSelectionContext.CandidateSteps
                .Select(x => BuildStepPromptContext(x, language))
                .ToList();

            return new
            {
                model = ModelName,
                input = new object[]
                {
                    new
                    {
                        role = "system",
                        content = new[]
                        {
                            new
                            {
                                type = "input_text",
                                text =
                                    "Rezept-Transformation für eine Food-App. Antwort: valides JSON im Schema. " +
                                    "stepPlan: NUR masterStepKeys aus der erlaubten Liste, keine Freitexte. Leeres stepPlan wenn keine Keys erlaubt. " +
                                    "DB-Zutat wenn mitgegeben sichtbar einbauen. Variablenwerte bevorzugt aus mitgegebenen Optionen. " +
                                    "Deutsch: {{ingredient}} mit Artikel ('die Eier', 'den Spinat'). " +
                                    "Bei Zutatenwechsel auch stepPlan-Variablen anpassen (ingredient, base, item)."
                            }
                        }
                    },
                    new
                    {
                        role = "user",
                        content = new[]
                        {
                            new
                            {
                                type = "input_text",
                                text = $"Sprache: {languageLabel}\n" +
                                       $"AI-Variante: {variantLabel} ({variantType})\n" +
                                       $"Bisherige Änderungsrunden: {appliedChangeCount} von {MaxChangeRounds}\n" +
                                       $"Rezepttitel: {recipe.Title}\n" +
                                       $"Portionen: {recipe.PersonCount}\n" +
                                       $"Zubereitungszeit: {recipe.PreparationTime} Minuten\n" +
                                       $"Zutaten: {JsonSerializer.Serialize(sourceIngredients)}\n" +
                                       $"Gewählte DB-Zutaten: {(selectedIngredientContext.Count == 0 ? "Keine feste Zutat ausgewählt" : JsonSerializer.Serialize(selectedIngredientContext))}\n" +
                                       $"Erlaubte masterStepKeys: {JsonSerializer.Serialize(allowedStepKeys)}\n" +
                                       $"Aktueller StepPlan: {JsonSerializer.Serialize(serializedSourceStepPlan)}\n" +
                                       $"Step-Templates (zeigt verfügbare Variablen): {JsonSerializer.Serialize(stepTemplateContext)}\n" +
                                       $"User-Hinweis: {(string.IsNullOrWhiteSpace(userNote) ? "Kein zusätzlicher Hinweis" : userNote)}\n" +
                                       "Gib eine umgesetzte Rezeptvorschau zurück. stepPlan darf nur Keys aus der erlaubten Liste enthalten. Variablenwerte sollen konkrete, kurze Strings sein. Wenn eine DB-Zutat ausgewählt wurde, integriere genau diese Zutat sichtbar."
                            }
                        }
                    }
                },
                text = new
                {
                    format = new
                    {
                        type = "json_schema",
                        name = "recipe_ai_transform_preview",
                        strict = true,
                        schema = BuildSchema()
                    }
                }
            };
        }

        private static object BuildSchema()
        {
            return new
            {
                type = "object",
                additionalProperties = false,
                required = new[] { "variantType", "variantLabel", "title", "summary", "usedFallback", "userNoteApplied", "remainingChanges", "ingredients", "stepPlan", "highlights" },
                properties = new
                {
                    variantType = new { type = "string" },
                    variantLabel = new { type = "string" },
                    title = new { type = "string" },
                    summary = new { type = "string" },
                    usedFallback = new { type = "boolean" },
                    userNoteApplied = new { type = "boolean" },
                    remainingChanges = new { type = "integer" },
                    ingredients = new
                    {
                        type = "array",
                        items = new
                        {
                            type = "object",
                            additionalProperties = false,
                            required = new[] { "name", "quantity", "changeHint", "isModified" },
                            properties = new
                            {
                                name = new { type = "string" },
                                quantity = new { type = "string" },
                                changeHint = new { type = new[] { "string", "null" } },
                                isModified = new { type = "boolean" }
                            }
                        }
                    },
                    stepPlan = new
                    {
                        type = "array",
                        items = new
                        {
                            type = "object",
                            additionalProperties = false,
                            required = new[] { "masterStepKey", "variables" },
                            properties = new
                            {
                                masterStepKey = new { type = "string" },
                                variables = new
                                {
                                    type = "array",
                                    items = new
                                    {
                                        type = "object",
                                        additionalProperties = false,
                                        required = new[] { "key", "value" },
                                        properties = new
                                        {
                                            key = new { type = "string" },
                                            value = new { type = "string" }
                                        }
                                    }
                                }
                            }
                        }
                    },
                    highlights = new
                    {
                        type = "array",
                        items = new { type = "string" }
                    }
                }
            };
        }

        private async Task<IngredientCategoryPlanResponse> SelectIngredientCategoriesAsync(
            RecipeBaseData recipe,
            string variantType,
            string language,
            string userNote,
            IReadOnlyList<string> defaultCategoryKeys,
            object recipeContext,
            string apiKey,
            CancellationToken cancellationToken)
        {
            var requestBody = new
            {
                model = ModelName,
                input = new object[]
                {
                    new
                    {
                        role = "system",
                        content = new[]
                        {
                            new
                            {
                                type = "input_text",
                                text =
                                    "Du hilfst bei Rezeptanpassungen. " +
                                    "Wähle nur Lebensmittelkategorien aus einer vorhandenen Liste. " +
                                    "Antworte ausschließlich als valides JSON. " +
                                    "Wähle höchstens 3 Kategorien, die geschmacklich am besten passen. " +
                                    "Wenn das Rezept bereits ein Hauptprotein plus Kartoffeln, Reis oder Pasta enthält, bevorzuge Kategorien für passende Beilagen-Ersatzprodukte statt weiterer Gewürze oder Zusatztoppings."
                            }
                        }
                    },
                    new
                    {
                        role = "user",
                        content = new[]
                        {
                            new
                            {
                                type = "input_text",
                                text =
                                    $"Variante: {GetVariantLabel(variantType, language)} ({variantType})\n" +
                                    $"Rezeptkontext: {JsonSerializer.Serialize(recipeContext)}\n" +
                                    $"Erlaubte Kategorien: {JsonSerializer.Serialize(defaultCategoryKeys)}\n" +
                                    $"User-Hinweis: {(string.IsNullOrWhiteSpace(userNote) ? "Kein Hinweis" : userNote)}\n" +
                                    "Gib die kulinarisch passendsten Kategorien zurück und sage kurz, ob man eher ergänzen oder ersetzen sollte."
                            }
                        }
                    }
                },
                text = new
                {
                    format = new
                    {
                        type = "json_schema",
                        name = "recipe_ai_ingredient_category_plan",
                        strict = true,
                        schema = new
                        {
                            type = "object",
                            additionalProperties = false,
                            required = new[] { "strategy", "reasoning", "preferredCategoryKeys" },
                            properties = new
                            {
                                strategy = new { type = "string" },
                                reasoning = new { type = "string" },
                                preferredCategoryKeys = new
                                {
                                    type = "array",
                                    items = new { type = "string" }
                                }
                            }
                        }
                    }
                }
            };

            var result = await ExecuteStructuredOpenAiCallAsync<IngredientCategoryPlanResponse>(requestBody, apiKey, cancellationToken);
            if (result == null)
            {
                return new IngredientCategoryPlanResponse
                {
                    Strategy = "add",
                    Reasoning = string.Empty,
                    PreferredCategoryKeys = defaultCategoryKeys.ToList()
                };
            }

            result.PreferredCategoryKeys = result.PreferredCategoryKeys?
                .Select(NormalizeCategoryKey)
                .Where(x => defaultCategoryKeys.Contains(x, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToList() ?? new List<string>();

            return result;
        }

        private async Task<List<IngredientResolutionCandidate>> SelectConcreteIngredientsAsync(
            RecipeBaseData recipe,
            string variantType,
            string language,
            string userNote,
            IReadOnlyList<IngredientResolutionCandidate> candidates,
            string apiKey,
            CancellationToken cancellationToken)
        {
            var compactCandidates = candidates.Select(x => new
            {
                id = x.IngredientId,
                name = x.Name,
                category = x.CategoryKey,
                protein = x.ProteinPer100g,
                carbs = x.CarbsPer100g,
                fat = x.FatPer100g,
                fiber = x.FiberPer100g,
                calories = x.CaloriesPer100g,
                score = x.Score
            }).ToList();

            var requestBody = new
            {
                model = ModelName,
                input = new object[]
                {
                    new
                    {
                        role = "system",
                        content = new[]
                        {
                            new
                            {
                                type = "input_text",
                                text =
                                    "Du wählst aus bestehenden Zutaten-Kandidaten die geschmacklich passendsten 3 aus. " +
                                    "Du darfst nur IDs zurückgeben, die in der Kandidatenliste enthalten sind. " +
                                    "Antworte ausschließlich als valides JSON."
                            }
                        }
                    },
                    new
                    {
                        role = "user",
                        content = new[]
                        {
                            new
                            {
                                type = "input_text",
                                text =
                                    $"Variante: {GetVariantLabel(variantType, language)} ({variantType})\n" +
                                    $"Rezepttitel: {recipe.Title}\n" +
                                    $"Aktuelle Zutaten: {JsonSerializer.Serialize(BuildIngredientPreview(recipe, language))}\n" +
                                    $"User-Hinweis: {(string.IsNullOrWhiteSpace(userNote) ? "Kein Hinweis" : userNote)}\n" +
                                    $"Kandidaten: {JsonSerializer.Serialize(compactCandidates)}\n" +
                                    "Wähle die 3 besten Zutaten aus, die geschmacklich passen und die Aufgabe sinnvoll erfüllen. " +
                                    "Wenn eine stärkehaltige Beilage ersetzt werden soll, wähle echte Beilagen-Alternativen und keine Gewürze, Kräuter, Milch oder Saucen."
                            }
                        }
                    }
                },
                text = new
                {
                    format = new
                    {
                        type = "json_schema",
                        name = "recipe_ai_ingredient_pick",
                        strict = true,
                        schema = new
                        {
                            type = "object",
                            additionalProperties = false,
                            required = new[] { "selectedIngredientIds", "reasons" },
                            properties = new
                            {
                                selectedIngredientIds = new
                                {
                                    type = "array",
                                    items = new { type = "integer" }
                                },
                                reasons = new
                                {
                                    type = "array",
                                    items = new { type = "string" }
                                }
                            }
                        }
                    }
                }
            };

            var result = await ExecuteStructuredOpenAiCallAsync<IngredientPickResponse>(requestBody, apiKey, cancellationToken);
            var selectedIds = result?.SelectedIngredientIds?
                .Distinct()
                .Where(id => candidates.Any(x => x.IngredientId == id))
                .Take(3)
                .ToList() ?? new List<int>();

            var selected = candidates
                .Where(x => selectedIds.Contains(x.IngredientId))
                .OrderBy(x => selectedIds.IndexOf(x.IngredientId))
                .ToList();

            foreach (var fallback in candidates)
            {
                if (selected.Count >= 3)
                {
                    break;
                }

                if (selected.All(x => x.IngredientId != fallback.IngredientId))
                {
                    selected.Add(fallback);
                }
            }

            var reasons = result?.Reasons ?? new List<string>();
            for (var index = 0; index < selected.Count; index++)
            {
                selected[index].MatchType = index < reasons.Count && !string.IsNullOrWhiteSpace(reasons[index])
                    ? reasons[index]
                    : LocalizedWord(language, "passt gut zum Rezeptstil", "fits the recipe style well", "encaja bien con el estilo de la receta", "combina bem com o estilo da receita");
            }

            return selected;
        }

        private async Task<RecipeAiIngredientSuggestionResponse> BuildIngredientSuggestionFallbackAsync(
            RecipeBaseData recipe,
            string variantType,
            string language,
            string userNote,
            CancellationToken cancellationToken)
        {
            var existingIngredientIds = recipe.Ingredients?
                .Select(x => x.Ingredient?.IngredientsAndNutrients?.Id ?? 0)
                .Where(x => x > 0)
                .Distinct()
                .ToList() ?? new List<int>();

            var ingredientSelectionContext = AnalyzeIngredientSelectionContext(recipe);
            var categoryKeys = GetDefaultCategoryKeysForVariant(variantType, ingredientSelectionContext);
            var candidates = await _ingredientResolverService.GetCandidatesForGoalAsync(
                variantType,
                language,
                categoryKeys,
                existingIngredientIds,
                take: 3,
                cancellationToken: cancellationToken);

            candidates = FilterCandidatesForRecipeContext(candidates, variantType, ingredientSelectionContext);

            return new RecipeAiIngredientSuggestionResponse
            {
                VariantType = variantType,
                VariantLabel = GetVariantLabel(variantType, language),
                Strategy = "add",
                Reasoning = LocalizedWord(language, "Lokale Auswahl aus deinen DB-Zutaten basierend auf Makros und Kategorien.", "Local selection from your DB ingredients based on macros and categories.", "Selección local de tus ingredientes de la BD basada en macros y categorías.", "Seleção local dos teus ingredientes da BD com base em macros e categorias."),
                UsedFallback = true,
                PreferredCategoryKeys = categoryKeys.ToList(),
                PreferredCategoryLabels = candidates.Select(x => x.CategoryLabel).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Suggestions = candidates.Select(x => new RecipeAiIngredientSuggestionItem
                {
                    IngredientId = x.IngredientId,
                    Name = x.Name,
                    CategoryKey = x.CategoryKey,
                    CategoryLabel = x.CategoryLabel,
                    ProteinPer100g = x.ProteinPer100g,
                    CarbsPer100g = x.CarbsPer100g,
                    FatPer100g = x.FatPer100g,
                    FiberPer100g = x.FiberPer100g,
                    CaloriesPer100g = x.CaloriesPer100g,
                    Score = x.Score,
                    AiReason = LocalizedWord(language, "Makro-stark und passend zur Zielkategorie", "strong macro fit for the target category", "buen perfil de macros para la categoría objetivo", "bom perfil de macros para a categoria alvo")
                }).ToList()
            };
        }

        private async Task<T?> ExecuteStructuredOpenAiCallAsync<T>(object requestBody, string apiKey, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, OpenAiEndpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OpenAI structured ingredient call failed: {StatusCode} {Body}", response.StatusCode, responseContent);
                throw new InvalidOperationException(BuildApiErrorMessage(response.StatusCode, responseContent));
            }

            using var document = JsonDocument.Parse(responseContent);
            var outputText = TryExtractOutputText(document.RootElement);
            if (string.IsNullOrWhiteSpace(outputText))
            {
                _logger.LogError("OpenAI structured ingredient call returned no output_text. Response: {Body}", responseContent);
                throw new InvalidOperationException(BuildApiErrorMessage(response.StatusCode, responseContent, "AI hat keine verwertbaren Zutatenvorschläge geliefert."));
            }

            return JsonSerializer.Deserialize<T>(outputText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        private object BuildIngredientSelectionRecipeContext(RecipeBaseData recipe, string language, IngredientSelectionContext context)
        {
            var ingredientContext = recipe.Ingredients?
                .Where(x => x.Ingredient?.IngredientsAndNutrients != null)
                .Select(x => (object)new
                {
                    id = x.Ingredient!.IngredientsAndNutrients!.Id,
                    name = language switch
                    {
                        "en" => x.Ingredient.IngredientsAndNutrients.Name_EN,
                        "pt" => x.Ingredient.IngredientsAndNutrients.Name_PRT,
                        "es" => x.Ingredient.IngredientsAndNutrients.Name_ESP,
                        _ => x.Ingredient.IngredientsAndNutrients.Name_DE
                    },
                    category = x.Ingredient.IngredientsAndNutrients.FoodCategory?.CategoryKey ?? string.Empty
                })
                .ToList() ?? new List<object>();

            return new
            {
                title = recipe.Title,
                category = recipe.Category,
                portions = recipe.PersonCount,
                prepMinutes = recipe.PreparationTime,
                ingredients = ingredientContext,
                context = new
                {
                    context.HasPrimaryProtein,
                    context.HasCarbSide,
                    context.HasPotatoSide,
                    context.HasRiceOrPastaSide,
                    context.PreferSideReplacement
                }
            };
        }

        private static List<string> GetDefaultCategoryKeysForVariant(string variantType, IngredientSelectionContext? context = null)
        {
            return variantType switch
            {
                "highprotein" => new List<string> { "legumes", "tofu", "tempeh", "yogurt", "eggs", "fish", "poultry", "cheese" },
                "lowcarb" when context?.PreferSideReplacement == true => new List<string> { "vegetables", "leafy_greens", "mushrooms" },
                "lowcarb" => new List<string> { "vegetables", "leafy_greens", "mushrooms", "fish", "poultry", "eggs", "tofu" },
                "vegan" => new List<string> { "legumes", "tofu", "tempeh", "vegetables", "leafy_greens", "mushrooms", "nuts", "seeds" },
                "mealprep" => new List<string> { "legumes", "rice", "vegetables", "poultry", "fish", "tofu", "potato" },
                _ => new List<string> { "vegetables", "legumes", "tofu" }
            };
        }

        private static IngredientSelectionContext AnalyzeIngredientSelectionContext(RecipeBaseData recipe)
        {
            var categoryKeys = recipe.Ingredients?
                .Select(x => x.Ingredient?.IngredientsAndNutrients?.FoodCategory?.CategoryKey)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim().ToLowerInvariant())
                .ToList() ?? new List<string>();

            var names = recipe.Ingredients?
                .Select(x => x.Ingredient?.IngredientsAndNutrients?.Name_DE ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim().ToLowerInvariant())
                .ToList() ?? new List<string>();

            var hasPrimaryProteinCategory = categoryKeys.Any(x => x is "beef" or "pork" or "lamb" or "poultry" or "fish" or "seafood");
            var hasPotatoSide = categoryKeys.Contains("potato") || names.Any(ContainsAnyKeyword("kartoffel", "potato", "erdapfel"));
            var hasRiceOrPastaSide =
                categoryKeys.Any(x => x is "rice" or "pasta" or "grains" or "pseudograins") ||
                names.Any(ContainsAnyKeyword("reis", "rice", "pasta", "nudel", "spaghetti", "penne", "tagliatelle", "quinoa", "couscous"));

            return new IngredientSelectionContext
            {
                HasPrimaryProtein = hasPrimaryProteinCategory,
                HasPotatoSide = hasPotatoSide,
                HasRiceOrPastaSide = hasRiceOrPastaSide,
                HasCarbSide = hasPotatoSide || hasRiceOrPastaSide,
                PreferSideReplacement = hasPrimaryProteinCategory && (hasPotatoSide || hasRiceOrPastaSide)
            };
        }

        private static List<IngredientResolutionCandidate> FilterCandidatesForRecipeContext(
            IReadOnlyList<IngredientResolutionCandidate> candidates,
            string variantType,
            IngredientSelectionContext context)
        {
            if (!string.Equals(variantType, "lowcarb", StringComparison.OrdinalIgnoreCase) || !context.PreferSideReplacement)
            {
                return candidates.Take(12).ToList();
            }

            var filtered = candidates
                .Where(x => x.CategoryKey is "vegetables" or "leafy_greens" or "mushrooms")
                .Where(x => x.CarbsPer100g <= 15m)
                .Where(x => !LooksLikeFlavorOnlyIngredient(x.Name))
                .Take(12)
                .ToList();

            return filtered.Count > 0 ? filtered : candidates.Take(12).ToList();
        }

        private static bool LooksLikeFlavorOnlyIngredient(string? name)
        {
            var normalized = (name ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return true;
            }

            string[] keywords =
            {
                "pulver", "powder", "gewurz", "gewürz", "kraut", "herb", "sauce", "sosse", "soße", "fond", "brühe", "bruhe", "milch", "cream", "sahne", "essig", "oil", "öl"
            };

            return keywords.Any(keyword => normalized.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        private static Func<string, bool> ContainsAnyKeyword(params string[] keywords)
        {
            return value => keywords.Any(keyword => value.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeCategoryKey(string? categoryKey)
        {
            return (categoryKey ?? string.Empty).Trim().ToLowerInvariant();
        }

        private RecipeAiTransformPreview BuildFallbackPreview(
            RecipeBaseData recipe,
            string variantType,
            string language,
            string userNote,
            int appliedChangeCount,
            IReadOnlyList<RecipeAiTransformStepPlanItem> sourceStepPlan,
            StepSelectionContext stepSelectionContext,
            IReadOnlyList<RecipeAiIngredientSuggestionItem> selectedIngredients)
        {
            var ingredients = BuildIngredientPreview(recipe, language);
            var highlights = new List<string>();
            var summaryParts = new List<string>();
            var title = recipe.Title;
            var stepPlan = sourceStepPlan.Select(CloneStepPlanItem).ToList();
            var primarySelectedIngredient = selectedIngredients.FirstOrDefault();

            switch (variantType)
            {
                case "vegan":
                    title = PrefixTitle(language, recipe.Title, "Vegan");
                    ReplaceIngredient(ingredients, language, new[] { "huhn", "hähn", "hühn", "chicken", "pollo", "frango", "rind", "beef", "ternera", "schinken", "ham", "jamón", "presunto", "schwein", "pork", "cerdo", "porco", "speck", "bacon", "wurst", "sausage", "pute", "turkey", "pavo", "peru", "lamm", "lamb", "cordero", "cordeiro", "fleisch", "meat", "carne" }, LocalizedWord(language, "Tofu", "Tofu", "Tofu", "Tofu"), LocalizedHint(language, "durch Tofu ersetzt", "replaced with tofu", "sustituido por tofu", "substituído por tofu"), highlights);
                    ReplaceVariableValue(stepPlan, new[] { "huhn", "hähn", "hühn", "chicken", "pollo", "frango", "rind", "beef", "ternera", "schinken", "ham", "jamón", "presunto", "schwein", "pork", "cerdo", "porco", "speck", "bacon", "wurst", "sausage", "pute", "turkey", "pavo", "peru", "lamm", "lamb", "cordero", "cordeiro", "fleisch", "meat", "carne" }, LocalizedWord(language, "Tofu", "tofu", "tofu", "tofu"));
                    ReplaceIngredient(ingredients, language, new[] { "milch", "milk", "leche", "leite", "sahne", "cream", "nata", "butter", "mantequilla", "manteiga", "joghurt", "yogurt", "yogur", "iogurte", "quark", "skyr" }, LocalizedWord(language, "Hafer-Cuisine", "oat cream", "crema de avena", "creme de aveia"), LocalizedHint(language, "veganisiert", "made dairy-free", "versión vegana", "versão vegana"), highlights);
                    ReplaceVariableValue(stepPlan, new[] { "milch", "milk", "leche", "leite", "sahne", "cream", "nata", "butter", "mantequilla", "manteiga", "joghurt", "yogurt", "yogur", "iogurte", "quark", "skyr" }, LocalizedWord(language, "Hafer-Cuisine", "oat cream", "crema de avena", "creme de aveia"));
                    ReplaceIngredient(ingredients, language, new[] { "käse", "kaese", "cheese", "queso", "queijo", "parmesan" }, LocalizedWord(language, "vegane Alternative", "vegan alternative", "alternativa vegana", "alternativa vegana"), LocalizedHint(language, "tierfrei ersetzt", "swapped for vegan alternative", "reemplazado por una alternativa vegana", "substituído por alternativa vegana"), highlights);
                    ReplaceVariableValue(stepPlan, new[] { "käse", "kaese", "cheese", "queso", "queijo", "parmesan" }, LocalizedWord(language, "vegane Alternative", "vegan alternative", "alternativa vegana", "alternativa vegana"));
                    ReplaceIngredient(ingredients, language, new[] { "ei ", "eier", "egg", "huevo", "ovo" }, LocalizedWord(language, "Ei-Ersatz", "egg substitute", "sustituto de huevo", "substituto de ovo"), LocalizedHint(language, "veganisiert", "made egg-free", "sin huevo", "sem ovo"), highlights);
                    ReplaceVariableValue(stepPlan, new[] { "ei ", "eier", "egg", "huevo", "ovo" }, LocalizedWord(language, "Ei-Ersatz", "egg substitute", "sustituto de huevo", "substituto de ovo"));
                    ReplaceIngredient(ingredients, language, new[] { "honig", "honey", "miel" }, LocalizedWord(language, "Ahornsirup", "maple syrup", "sirope de arce", "xarope de ácer"), LocalizedHint(language, "veganisiert", "made vegan", "versión vegana", "versão vegana"), highlights);
                    ReplaceVariableValue(stepPlan, new[] { "honig", "honey", "miel" }, LocalizedWord(language, "Ahornsirup", "maple syrup", "sirope de arce", "xarope de ácer"));
                    summaryParts.Add(LocalizedWord(language, "Tierische Zutaten wurden möglichst schonend ersetzt.", "Animal-based ingredients were swapped as gently as possible.", "Se sustituyeron los ingredientes de origen animal con cuidado.", "Os ingredientes de origem animal foram substituídos com cuidado."));
                    break;
                case "mealprep":
                    title = PrefixTitle(language, recipe.Title, "Meal Prep");
                    summaryParts.Add(LocalizedWord(language, "Die Variante ist auf gutes Vorbereiten und entspanntes Aufwärmen ausgelegt.", "This version is tuned for prepping ahead and easy reheating.", "Esta versión está pensada para preparar con antelación y recalentar fácilmente.", "Esta versão foi ajustada para preparar antes e aquecer facilmente."));
                    highlights.Add(LocalizedWord(language, "Meal-Prep-freundliche Reihenfolge", "meal-prep-friendly workflow", "flujo pensado para meal prep", "fluxo pensado para meal prep"));
                    break;
                case "lowcarb":
                    title = PrefixTitle(language, recipe.Title, "Low Carb");
                    ReplaceIngredient(ingredients, language, new[] { "reis", "rice", "arroz", "risotto" }, LocalizedWord(language, "Blumenkohlreis", "cauliflower rice", "arroz de coliflor", "arroz de couve-flor"), LocalizedHint(language, "KH reduziert", "lower-carb swap", "menos carbohidratos", "menos carboidratos"), highlights);
                    ReplaceVariableValue(stepPlan, new[] { "reis", "rice", "arroz", "risotto" }, LocalizedWord(language, "Blumenkohlreis", "cauliflower rice", "arroz de coliflor", "arroz de couve-flor"));
                    ReplaceIngredient(ingredients, language, new[] { "nudel", "pasta", "spaghetti", "penne", "fusilli", "tagliatelle", "fettuccine", "makkaroni", "macaroni" }, LocalizedWord(language, "Zucchini-Nudeln", "zucchini noodles", "fideos de calabacín", "macarrão de curgete"), LocalizedHint(language, "leichtere Beilage", "lighter side", "guarnición ligera", "acompanhamento leve"), highlights);
                    ReplaceVariableValue(stepPlan, new[] { "nudel", "pasta", "spaghetti", "penne", "fusilli", "tagliatelle", "fettuccine", "makkaroni", "macaroni" }, LocalizedWord(language, "Zucchini-Nudeln", "zucchini noodles", "fideos de calabacín", "macarrão de curgete"));
                    ReplaceIngredient(ingredients, language, new[] { "kartoffel", "potato", "patata", "batata" }, LocalizedWord(language, "Ofengemüse", "roasted vegetables", "verduras asadas", "legumes assados"), LocalizedHint(language, "stärkearme Alternative", "lower-starch alternative", "alternativa baja en almidón", "alternativa com menos amido"), highlights);
                    ReplaceVariableValue(stepPlan, new[] { "kartoffel", "potato", "patata", "batata" }, LocalizedWord(language, "Ofengemüse", "roasted vegetables", "verduras asadas", "legumes assados"));
                    ReplaceIngredient(ingredients, language, new[] { "brot", "bread", "pan ", "pão", "toast", "brötchen", "semmel" }, LocalizedWord(language, "Salatblätter", "lettuce wraps", "hojas de lechuga", "folhas de alface"), LocalizedHint(language, "KH reduziert", "lower-carb swap", "menos carbohidratos", "menos carboidratos"), highlights);
                    ReplaceVariableValue(stepPlan, new[] { "brot", "bread", "pan ", "pão", "toast", "brötchen", "semmel" }, LocalizedWord(language, "Salatblätter", "lettuce wraps", "hojas de lechuga", "folhas de alface"));
                    summaryParts.Add(LocalizedWord(language, "Stärkereiche Bestandteile wurden soweit möglich gegen leichtere Alternativen getauscht.", "Starchy parts were swapped for lighter alternatives where possible.", "Los componentes ricos en almidón se cambiaron por opciones más ligeras cuando fue posible.", "Os componentes ricos em amido foram trocados por opções mais leves sempre que possível."));
                    break;
                case "highprotein":
                    title = PrefixTitle(language, recipe.Title, LocalizedWord(language, "Protein", "Protein", "Proteína", "Proteína"));
                    highlights.Add(LocalizedWord(language, "Proteinquelle verstärkt", "protein source boosted", "fuente de proteína reforzada", "fonte de proteína reforçada"));
                    ingredients.Add(new RecipeAiTransformIngredientPreview
                    {
                        Name = primarySelectedIngredient?.Name ?? LocalizedWord(language, "Skyr oder Extra-Protein", "skyr or extra protein", "skyr o proteína extra", "skyr ou proteína extra"),
                        Quantity = primarySelectedIngredient != null ? LocalizedWord(language, "150 g", "150 g", "150 g", "150 g") : LocalizedWord(language, "1 Portion", "1 portion", "1 porción", "1 porção"),
                        ChangeHint = primarySelectedIngredient != null
                            ? LocalizedHint(language, "aus deinem Zutatenstamm ausgewählt", "selected from your ingredient database", "seleccionado de tu base de ingredientes", "selecionado da tua base de ingredientes")
                            : LocalizedHint(language, "optional ergänzt", "optional addition", "añadido opcional", "adição opcional"),
                        IsModified = true
                    });
                    summaryParts.Add(LocalizedWord(language, "Die Variante legt den Fokus stärker auf Sättigung und Protein pro Portion.", "This version focuses more on satiety and protein per serving.", "Esta versión pone más foco en saciedad y proteína por porción.", "Esta versão foca mais em saciedade e proteína por porção."));
                    break;
            }


            foreach (var selectedIngredient in selectedIngredients)
            {
                highlights.Add(LocalizedWord(language, "Gewählte DB-Zutat: ", "Selected DB ingredient: ", "Ingrediente elegido de la BD: ", "Ingrediente escolhido da BD: ") + selectedIngredient.Name);
                summaryParts.Add(LocalizedWord(language, "Die Variante arbeitet bewusst mit ", "This version intentionally uses ", "Esta versión usa de forma intencional ", "Esta versão usa de forma intencional ") + selectedIngredient.Name + ".");

                if (!ingredients.Any(x => string.Equals(x.Name, selectedIngredient.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    ingredients.Add(new RecipeAiTransformIngredientPreview
                    {
                        Name = selectedIngredient.Name,
                        Quantity = LocalizedWord(language, "nach Bedarf", "as needed", "al gusto", "a gosto"),
                        ChangeHint = LocalizedHint(language, "AI-Auswahl aus deinen Zutaten", "AI-selected from your ingredients", "selección AI de tus ingredientes", "seleção AI dos teus ingredientes"),
                        IsModified = true
                    });
                }
            }

            if (!string.IsNullOrWhiteSpace(userNote))
            {
                highlights.Add(LocalizedWord(language, "User-Wunsch eingearbeitet", "user request applied", "cambio del usuario aplicado", "pedido do utilizador aplicado"));
                summaryParts.Add(LocalizedWord(language, "Zusätzlicher Änderungswunsch: ", "Additional change request: ", "Cambio adicional: ", "Pedido adicional: ") + userNote);
                ApplySimpleUserNoteAdjustment(ingredients, stepPlan, language, userNote);
            }

            if (highlights.Count == 0)
            {
                highlights.Add(LocalizedWord(language, "Sanfte AI-Anpassung", "gentle AI adaptation", "adaptación suave", "adaptação suave"));
            }

            var preview = new RecipeAiTransformPreview
            {
                VariantType = variantType,
                VariantLabel = GetVariantLabel(variantType, language),
                Title = title,
                Summary = string.Join(" ", summaryParts.Where(x => !string.IsNullOrWhiteSpace(x))),
                UsedFallback = true,
                UserNoteApplied = !string.IsNullOrWhiteSpace(userNote),
                RemainingChanges = Math.Max(0, MaxChangeRounds - appliedChangeCount - (string.IsNullOrWhiteSpace(userNote) ? 0 : 1)),
                Ingredients = ingredients,
                StepPlan = stepPlan,
                Highlights = highlights.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            };

            NormalizePreview(preview, recipe, variantType, language, userNote, appliedChangeCount, true, sourceStepPlan, stepSelectionContext.AllowedStepKeys, allowOnlySourceKeys: sourceStepPlan.Count > 0);
            return preview;
        }

        private void NormalizePreview(
            RecipeAiTransformPreview preview,
            RecipeBaseData recipe,
            string variantType,
            string language,
            string userNote,
            int appliedChangeCount,
            bool usedFallback,
            IReadOnlyList<RecipeAiTransformStepPlanItem> sourceStepPlan,
            IReadOnlyCollection<string> additionalAllowedStepKeys,
            bool allowOnlySourceKeys)
        {
            preview.VariantType = string.IsNullOrWhiteSpace(preview.VariantType) ? variantType : NormalizeVariantType(preview.VariantType);
            preview.VariantLabel = string.IsNullOrWhiteSpace(preview.VariantLabel) ? GetVariantLabel(variantType, language) : preview.VariantLabel.Trim();
            preview.Title = string.IsNullOrWhiteSpace(preview.Title) ? recipe.Title : preview.Title.Trim();
            preview.Summary = (preview.Summary ?? string.Empty).Trim();
            preview.UsedFallback = usedFallback || preview.UsedFallback;
            preview.UserNoteApplied = preview.UserNoteApplied || !string.IsNullOrWhiteSpace(userNote);
            preview.RemainingChanges = Math.Clamp(preview.RemainingChanges, 0, MaxChangeRounds);
            preview.Ingredients ??= new List<RecipeAiTransformIngredientPreview>();
            preview.StepPlan ??= new List<RecipeAiTransformStepPlanItem>();
            preview.Steps ??= new List<RecipeAiTransformStepPreview>();
            preview.Highlights ??= new List<string>();

            if (preview.Ingredients.Count == 0)
            {
                preview.Ingredients = BuildIngredientPreview(recipe, language);
            }

            var validatedPlan = ValidateAndRenderStepPlan(preview.StepPlan, language, sourceStepPlan, additionalAllowedStepKeys, allowOnlySourceKeys);
            if (validatedPlan.Count == 0)
            {
                validatedPlan = sourceStepPlan.Select(CloneStepPlanItem).ToList();
            }

            preview.StepPlan = validatedPlan;
            preview.Steps = validatedPlan.Count > 0
                ? validatedPlan.Select((item, index) => new RecipeAiTransformStepPreview
                {
                    Index = index + 1,
                    Text = PolishStepText(item.RenderedText, language)
                }).ToList()
                : BuildStepPreview(recipe, language);

            if (preview.RemainingChanges > MaxChangeRounds - appliedChangeCount)
            {
                preview.RemainingChanges = Math.Max(0, MaxChangeRounds - appliedChangeCount - (string.IsNullOrWhiteSpace(userNote) ? 0 : 1));
            }

            if (preview.Highlights.Count == 0)
            {
                preview.Highlights.Add(LocalizedWord(language, "Originale Template-Schritte übernommen", "original template steps retained", "se mantuvieron los pasos de plantilla", "passos de template mantidos"));
            }
        }

        private List<RecipeAiTransformStepPlanItem> BuildSourceStepPlan(RecipeBaseData recipe, string language)
        {
            if (recipe.SmartSteps == null)
            {
                return new List<RecipeAiTransformStepPlanItem>();
            }

            return recipe.SmartSteps
                .OrderBy(x => x.StepIndex)
                .Select(x => CreateStepPlanItem(
                    x.SmartRecipeStep?.MasterStepKey,
                    x.SmartRecipeStep?.Phase,
                    ParseVariablesJson(x.SmartRecipeStep?.VariablesJson),
                    language))
                .Where(x => x != null)
                .Cast<RecipeAiTransformStepPlanItem>()
                .ToList();
        }

        private RecipeAiTransformStepPlanItem? CreateStepPlanItem(string? masterStepKey, int? phase, Dictionary<string, string>? variables, string language)
        {
            var normalizedKey = (masterStepKey ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                return null;
            }

            if (!_masterStepsByKey.Value.TryGetValue(normalizedKey, out var definition))
            {
                return null;
            }

            var mergedVariables = variables ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return new RecipeAiTransformStepPlanItem
            {
                MasterStepKey = normalizedKey,
                Phase = phase ?? definition.Phase,
                Variables = ToVariableItems(mergedVariables),
                RenderedText = RenderMasterStep(definition, mergedVariables, language)
            };
        }


        private List<RecipeAiTransformStepPlanItem> ValidateAndRenderStepPlan(
            IEnumerable<RecipeAiTransformStepPlanItem>? stepPlan,
            string language,
            IReadOnlyList<RecipeAiTransformStepPlanItem> sourceStepPlan,
            IReadOnlyCollection<string> additionalAllowedStepKeys,
            bool allowOnlySourceKeys)
        {
            if (stepPlan == null)
            {
                return new List<RecipeAiTransformStepPlanItem>();
            }

            var sourceByKey = sourceStepPlan
                .GroupBy(x => x.MasterStepKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var allowedKeys = allowOnlySourceKeys
                ? new HashSet<string>(sourceByKey.Keys.Concat(additionalAllowedStepKeys ?? Array.Empty<string>()), StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(_masterStepsByKey.Value.Keys, StringComparer.OrdinalIgnoreCase);

            var result = new List<RecipeAiTransformStepPlanItem>();
            foreach (var item in stepPlan)
            {
                var masterStepKey = (item.MasterStepKey ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(masterStepKey) || !allowedKeys.Contains(masterStepKey))
                {
                    continue;
                }

                if (!_masterStepsByKey.Value.TryGetValue(masterStepKey, out var definition))
                {
                    continue;
                }

                var mergedVariables = sourceByKey.TryGetValue(masterStepKey, out var sourceItem)
                    ? ToVariableDictionary(sourceItem.Variables)
                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var pair in ToVariableDictionary(item.Variables))
                {
                    var trimmedValue = (pair.Value ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(trimmedValue))
                    {
                        mergedVariables[pair.Key] = trimmedValue;
                    }
                }

                result.Add(new RecipeAiTransformStepPlanItem
                {
                    MasterStepKey = masterStepKey,
                    Phase = item.Phase > 0 ? item.Phase : (sourceItem?.Phase ?? definition.Phase),
                    Variables = ToVariableItems(mergedVariables),
                    RenderedText = RenderMasterStep(definition, mergedVariables, language)
                });
            }

            return result;
        }

        private object BuildStepPromptContext(MasterStepTemplateDefinition definition, string language)
        {
            var variableNames = GetVariableNames(definition);
            return new
            {
                masterStepKey = definition.MasterId,
                phase = definition.Phase,
                action = definition.Action,
                template = GetLocalizedTemplate(definition, language),
                requiredVariables = definition.RequiredVariables,
                optionalVariables = definition.OptionalVariables,
                availableVariables = variableNames,
                variableOptions = BuildVariableOptionContext(variableNames, language),
                ingredientArticleHint = GetIngredientArticleHint(language)
            };
        }

        private static List<string> GetVariableNames(MasterStepTemplateDefinition definition)
        {
            return (definition.Variables ?? new List<string>())
                .Concat(definition.RequiredVariables ?? new List<string>())
                .Concat(definition.OptionalVariables ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private StepSelectionContext BuildStepSelectionContext(
            IReadOnlyList<RecipeAiTransformStepPlanItem> sourceStepPlan,
            IReadOnlyList<RecipeAiIngredientSuggestionItem> selectedIngredients,
            string variantType)
        {
            var candidateSteps = GetSelectedIngredientCandidateSteps(selectedIngredients, variantType).ToList();
            var keepExistingSteps = sourceStepPlan
                .Where(x => !ShouldPreferReplacingStep(x.MasterStepKey, selectedIngredients))
                .ToList();
            var replaceExistingSteps = sourceStepPlan
                .Where(x => ShouldPreferReplacingStep(x.MasterStepKey, selectedIngredients))
                .ToList();

            return new StepSelectionContext
            {
                AllowedStepKeys = new HashSet<string>(
                    sourceStepPlan.Select(x => x.MasterStepKey).Concat(candidateSteps.Select(x => x.MasterId)),
                    StringComparer.OrdinalIgnoreCase),
                CandidateSteps = candidateSteps,
                KeepExistingSteps = keepExistingSteps,
                ReplaceExistingSteps = replaceExistingSteps
            };
        }

        private IEnumerable<MasterStepTemplateDefinition> GetSelectedIngredientCandidateSteps(IReadOnlyList<RecipeAiIngredientSuggestionItem> selectedIngredients, string variantType)
        {
            if (selectedIngredients == null || selectedIngredients.Count == 0)
            {
                return Array.Empty<MasterStepTemplateDefinition>();
            }

            var candidateKeys = new List<string>();
            var categoryKeys = selectedIngredients.Select(x => NormalizeCategoryKey(x.CategoryKey)).ToList();
            var ingredientNames = selectedIngredients.Select(x => (x.Name ?? string.Empty).ToLowerInvariant()).ToList();

            if (categoryKeys.Any(x => x.Contains("lentil") || x.Contains("legume") || x.Contains("bean")) || ingredientNames.Any(x => x.Contains("lins")))
            {
                candidateKeys.AddRange(new[] { "PREP_WASH_01", "PREP_SOAK_01", "COOK_BOIL_01", "COOK_SIMMER_02", "COOK_STRAIN_01", "COOK_STIR_01", "COOK_ADD_01" });
            }
            else if (categoryKeys.Any(x => x.Contains("tofu") || x.Contains("tempeh")))
            {
                candidateKeys.AddRange(new[] { "PREP_CUT_01", "COOK_SAUTE_01", "COOK_FRY_01", "COOK_ADD_01", "COOK_STIR_01" });
            }
            else if (categoryKeys.Any(x => x.Contains("mushroom") || x.Contains("vegetable")))
            {
                candidateKeys.AddRange(new[] { "PREP_CUT_01", "COOK_SAUTE_01", "COOK_ADD_01", "COOK_SIMMER_01", "COOK_STIR_01" });
            }
            else
            {
                candidateKeys.AddRange(new[] { "COOK_ADD_01", "COOK_STIR_01", "COOK_SIMMER_01" });
            }

            if (string.Equals(variantType, "highprotein", StringComparison.OrdinalIgnoreCase))
            {
                candidateKeys.Add("COOK_ADD_01");
            }

            return candidateKeys
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(key => _masterStepsByKey.Value.TryGetValue(key, out var definition) ? definition : null)
                .Where(x => x != null)
                .Select(x => x!);
        }

        private bool ShouldPreferReplacingStep(string masterStepKey, IReadOnlyList<RecipeAiIngredientSuggestionItem> selectedIngredients)
        {
            if (selectedIngredients == null || selectedIngredients.Count == 0 || !_masterStepsByKey.Value.TryGetValue(masterStepKey, out var definition))
            {
                return false;
            }

            var categoryKeys = selectedIngredients.Select(x => NormalizeCategoryKey(x.CategoryKey)).ToList();
            if (categoryKeys.Any(x => x.Contains("lentil") || x.Contains("legume") || x.Contains("bean")))
            {
                return definition.SelectionTags.Contains("family:meat", StringComparer.OrdinalIgnoreCase)
                    || definition.SelectionTags.Contains("action:fry", StringComparer.OrdinalIgnoreCase)
                    || definition.SelectionTags.Contains("action:sear", StringComparer.OrdinalIgnoreCase)
                    || definition.SelectionTags.Contains("action:braise", StringComparer.OrdinalIgnoreCase);
            }

            if (categoryKeys.Any(x => x.Contains("tofu") || x.Contains("tempeh")))
            {
                return definition.SelectionTags.Contains("family:meat", StringComparer.OrdinalIgnoreCase);
            }

            return false;
        }

        private Dictionary<string, MasterStepTemplateDefinition> LoadMasterSteps()
        {
            var fullPath = Path.Combine(_environment.ContentRootPath, MasterStepDataPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                throw new InvalidOperationException($"Master-Step-Katalog fehlt: {fullPath}");
            }

            var json = File.ReadAllText(fullPath);
            var document = JsonSerializer.Deserialize<MasterStepCatalogDocument>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return (document?.MasterSteps ?? new List<MasterStepTemplateDefinition>())
                .Where(x => !string.IsNullOrWhiteSpace(x.MasterId))
                .ToDictionary(x => x.MasterId, StringComparer.OrdinalIgnoreCase);
        }

        private Dictionary<string, MasterStepVariableDefinition> LoadMasterStepVariables()
        {
            var fullPath = Path.Combine(_environment.ContentRootPath, MasterStepVariableDataPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                return new Dictionary<string, MasterStepVariableDefinition>(StringComparer.OrdinalIgnoreCase);
            }

            var json = File.ReadAllText(fullPath);
            var document = JsonSerializer.Deserialize<MasterStepVariableCatalogDocument>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return (document?.Variables ?? new Dictionary<string, MasterStepVariableDefinition>(StringComparer.OrdinalIgnoreCase))
                .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        }

        private List<object> BuildVariableOptionContext(IEnumerable<string> variableNames, string language)
        {
            var normalizedNames = variableNames
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedNames.Contains("ingredient", StringComparer.OrdinalIgnoreCase)
                && !normalizedNames.Contains("articles", StringComparer.OrdinalIgnoreCase))
            {
                normalizedNames.Add("articles");
            }

            var result = new List<object>();
            foreach (var variableName in normalizedNames)
            {
                if (!_masterStepVariablesByName.Value.TryGetValue(variableName, out var definition))
                {
                    continue;
                }

                result.Add(new
                {
                    variable = variableName,
                    description = definition.Description,
                    options = definition.Options.Select(option => new
                    {
                        key = option.Key,
                        label = GetLocalizedVariableOptionLabel(option, language),
                        tags = option.Tags
                    }).ToList()
                });
            }

            return result;
        }

        private string RenderMasterStep(MasterStepTemplateDefinition definition, IReadOnlyDictionary<string, string> variables, string language)
        {
            var template = GetLocalizedTemplate(definition, language);
            if (string.IsNullOrWhiteSpace(template))
            {
                return definition.MasterId;
            }

            string processed = template;
            while (OptionalTemplateRegex.IsMatch(processed))
            {
                processed = OptionalTemplateRegex.Replace(processed, match =>
                {
                    var inner = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                    var variableNames = TemplateVariableRegex.Matches(inner)
                        .Select(x => x.Groups[1].Value.Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    return variableNames.Any(name => HasTemplateValue(variables, name)) ? inner : string.Empty;
                });
            }

            processed = FallbackTokenRegex.Replace(processed, match =>
            {
                var fallbackText = match.Groups[1].Value;
                var variablesRaw = match.Groups[2].Value;
                var hasAnyValue = variablesRaw
                    .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Any(name => HasTemplateValue(variables, name));
                return hasAnyValue ? string.Empty : fallbackText;
            });

            processed = TemplateVariableRegex.Replace(processed, match =>
            {
                var variableName = match.Groups[1].Value.Trim();
                return variables.TryGetValue(variableName, out var value) && !string.IsNullOrWhiteSpace(value)
                    ? value.Trim()
                    : variableName;
            });

            processed = SpaceBeforePunctuationRegex.Replace(processed, "$1");
            processed = MultiWhitespaceRegex.Replace(processed, " ");
            return PolishStepText(processed.Replace("( ", "(").Replace(" )", ")").Trim(), language);
        }

        private static bool HasTemplateValue(IReadOnlyDictionary<string, string> values, string key)
            => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value);

        private static string PolishStepText(string? rawText, string language)
        {
            var text = (rawText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            text = MultiWhitespaceRegex.Replace(text, " ");
            text = SpaceBeforePunctuationRegex.Replace(text, "$1");

            if (string.Equals(language, "de", StringComparison.OrdinalIgnoreCase))
            {
                var eggMatch = EggPoachGermanRegex.Match(text);
                if (eggMatch.Success)
                {
                    var quantity = eggMatch.Groups[1].Value.Trim();
                    var minutes = eggMatch.Groups[2].Value.Trim();
                    text = $"Gib die Eier ({quantity}) vorsichtig dazu und lasse sie etwa {minutes} Minuten im Sud garen, bis sie gestockt sind.";
                }

                text = GermanTimeAfterDazuRegex.Replace(text, "dazu und lasse sie etwa $1 Minuten");
                text = MissingGermanVerbRegex.Replace(text, "bis sie gestockt sind");
                text = text.Replace(" inschwimmen lassen", " im Sud garen");
                text = text.Replace(" vorsichtig aufschlagen", " vorsichtig hineingleiten");
            }

            if (!char.IsUpper(text[0]))
            {
                text = char.ToUpperInvariant(text[0]) + text[1..];
            }

            if (!text.EndsWith('.') && !text.EndsWith('!') && !text.EndsWith('?'))
            {
                text += ".";
            }

            return text;
        }

        private static Dictionary<string, string> ParseVariablesJson(string? variablesJson)
        {
            if (string.IsNullOrWhiteSpace(variablesJson))
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                using var document = JsonDocument.Parse(variablesJson);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    var value = property.Value.ValueKind switch
                    {
                        JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                        JsonValueKind.Number => property.Value.ToString(),
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        _ => property.Value.ToString()
                    };

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        result[property.Name] = value.Trim();
                    }
                }

                return result;
            }
            catch
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static RecipeAiTransformStepPlanItem CloneStepPlanItem(RecipeAiTransformStepPlanItem item)
            => new()
            {
                MasterStepKey = item.MasterStepKey,
                Phase = item.Phase,
                Variables = ToVariableItems(ToVariableDictionary(item.Variables)),
                RenderedText = item.RenderedText
            };

        private static Dictionary<string, string> ToVariableDictionary(IEnumerable<RecipeAiTransformStepVariableItem>? variables)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (variables == null)
            {
                return result;
            }

            foreach (var item in variables)
            {
                var key = (item?.Key ?? string.Empty).Trim();
                var value = (item?.Value ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                result[key] = value;
            }

            return result;
        }

        private static List<RecipeAiTransformStepVariableItem> ToVariableItems(IEnumerable<KeyValuePair<string, string>> variables)
        {
            return variables
                .Where(x => !string.IsNullOrWhiteSpace(x.Key) && !string.IsNullOrWhiteSpace(x.Value))
                .Select(x => new RecipeAiTransformStepVariableItem
                {
                    Key = x.Key.Trim(),
                    Value = x.Value.Trim()
                })
                .ToList();
        }

        private static void SetVariableValue(RecipeAiTransformStepPlanItem item, string key, string value)
        {
            var existing = item.Variables.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Value = value;
                return;
            }

            item.Variables.Add(new RecipeAiTransformStepVariableItem { Key = key, Value = value });
        }

        private static List<RecipeAiTransformIngredientPreview> BuildIngredientPreview(RecipeBaseData recipe, string language)
        {
            return recipe.Ingredients?
                .Select(link =>
                {
                    var ingredient = link.Ingredient;
                    var nutrient = ingredient?.IngredientsAndNutrients;
                    var measure = ingredient?.Measure;
                    var quantity = ingredient?.Quantity?.Quantitys;

                    return new RecipeAiTransformIngredientPreview
                    {
                        Name = GetIngredientName(nutrient, language),
                        Quantity = FormatQuantity(quantity, measure, language),
                        ChangeHint = null,
                        IsModified = false
                    };
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .ToList() ?? new List<RecipeAiTransformIngredientPreview>();
        }

        private static List<RecipeAiTransformStepPreview> BuildStepPreview(RecipeBaseData recipe, string language)
        {
            return recipe.Steps?
                .OrderBy(x => x.StepIndex)
                .Select(x => new RecipeAiTransformStepPreview { Index = x.StepIndex, Text = PolishStepText(GetStepText(x.RecipePreparationStep, language), language) })
                .Where(x => !string.IsNullOrWhiteSpace(x.Text))
                .ToList() ?? new List<RecipeAiTransformStepPreview>();
        }


        private static void ApplySimpleUserNoteAdjustment(List<RecipeAiTransformIngredientPreview> ingredients, List<RecipeAiTransformStepPlanItem> stepPlan, string language, string userNote)
        {
            var lower = userNote.ToLowerInvariant();
            if (lower.Contains("kartoffel") || lower.Contains("potato"))
            {
                ReplaceIngredient(ingredients, language, new[] { "reis", "rice", "nudel", "pasta" }, LocalizedWord(language, "Kartoffeln", "potatoes", "patatas", "batatas"), LocalizedHint(language, "nach User-Wunsch", "per user request", "según el usuario", "a pedido do utilizador"), new List<string>());
                ReplaceVariableValue(stepPlan, new[] { "reis", "rice", "pasta", "nudeln", "noodles" }, LocalizedWord(language, "Kartoffeln", "potatoes", "patatas", "batatas"));
            }
            else if (lower.Contains("reis") || lower.Contains("rice"))
            {
                ReplaceIngredient(ingredients, language, new[] { "kartoffel", "potato" }, LocalizedWord(language, "Reis", "rice", "arroz", "arroz"), LocalizedHint(language, "nach User-Wunsch", "per user request", "según el usuario", "a pedido do utilizador"), new List<string>());
                ReplaceVariableValue(stepPlan, new[] { "kartoffel", "potato" }, LocalizedWord(language, "Reis", "rice", "arroz", "arroz"));
            }
        }

        private static void ReplaceVariableValue(List<RecipeAiTransformStepPlanItem> stepPlan, IEnumerable<string> searchTerms, string replacement)
        {
            foreach (var item in stepPlan)
            {
                foreach (var variable in item.Variables)
                {
                    var current = variable.Value ?? string.Empty;
                    if (searchTerms.Any(term => current.Contains(term, StringComparison.OrdinalIgnoreCase)))
                    {
                        variable.Value = replacement;
                    }
                }
            }
        }

        private static void ReplaceIngredient(List<RecipeAiTransformIngredientPreview> ingredients, string language, string[] searchTerms, string replacementName, string hint, List<string> highlights)
        {
            var matched = false;
            foreach (var ingredient in ingredients)
            {
                if (ingredient.IsModified) continue;

                var lowerName = ingredient.Name.ToLowerInvariant();
                if (!searchTerms.Any(term => lowerName.Contains(term)))
                {
                    continue;
                }

                ingredient.Name = replacementName;
                ingredient.ChangeHint = hint;
                ingredient.IsModified = true;
                matched = true;
            }

            if (matched && !string.IsNullOrWhiteSpace(hint))
            {
                highlights.Add(hint);
            }
        }

        private static string FormatQuantity(double? quantity, Measure? measure, string language)
        {
            if (quantity == null && measure == null) return string.Empty;

            var culture = language switch
            {
                "en" => CultureInfo.GetCultureInfo("en-US"),
                "es" => CultureInfo.GetCultureInfo("es-ES"),
                "pt" => CultureInfo.GetCultureInfo("pt-PT"),
                "id" => CultureInfo.GetCultureInfo("id-ID"),
                "nl" => CultureInfo.GetCultureInfo("nl-NL"),
                "sv" => CultureInfo.GetCultureInfo("sv-SE"),
                "da" => CultureInfo.GetCultureInfo("da-DK"),
                "no" => CultureInfo.GetCultureInfo("nb-NO"),
                "ms" => CultureInfo.GetCultureInfo("ms-MY"),
                _ => CultureInfo.GetCultureInfo("de-DE")
            };

            var qty = quantity.HasValue ? quantity.Value.ToString("0.##", culture) : string.Empty;
            var unit = measure?.GetLocalized(LanguageMapForMeasure(language)) ?? string.Empty;
            return string.Join(" ", new[] { qty, unit }.Where(x => !string.IsNullOrWhiteSpace(x)));

            static string LanguageMapForMeasure(string currentLanguage)
                => currentLanguage switch
                {
                    "es" => "esp",
                    "pt" => "prt",
                    "sv" => "sv",
                    "da" => "da",
                    _ => currentLanguage
                };
        }

        private static string GetIngredientName(IngredientsAndNutrients? ingredient, string language)
        {
            if (ingredient == null) return string.Empty;
            return language switch
            {
                "en" => ingredient.Name_EN,
                "es" => ingredient.Name_ESP,
                "pt" => ingredient.Name_PRT,
                "id" => ingredient.Name_ID,
                "nl" => ingredient.Name_NL,
                "sv" => ingredient.Name_SE,
                "da" => ingredient.Name_DK,
                "no" => ingredient.Name_NO,
                "ms" => ingredient.Name_MS,
                _ => ingredient.Name_DE
            } ?? ingredient.Name_DE ?? string.Empty;
        }

        private static string GetStepText(RecipePreparationSteps? step, string language)
        {
            if (step == null) return string.Empty;
            // Steps only have DE/EN/ESP/PRT - other languages fall back to EN
            return language switch
            {
                "en" or "id" or "nl" or "sv" or "da" or "no" or "ms" => step.Step_EN,
                "es" => step.Step_ESP,
                "pt" => step.Step_PRT,
                _ => step.Step_DE
            } ?? step.Step_DE ?? string.Empty;
        }

        private static string NormalizeVariantType(string? variantType)
        {
            var normalized = (variantType ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "vegan" => "vegan",
                "mealprep" => "mealprep",
                "meal-prep" => "mealprep",
                "lowcarb" => "lowcarb",
                "low-carb" => "lowcarb",
                "highprotein" => "highprotein",
                "mehrprotein" => "highprotein",
                "protein" => "highprotein",
                _ => string.Empty
            };
        }

        private static string NormalizeLanguage(string? language)
        {
            var normalized = (language ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "en" => "en",
                "es" or "esp" => "es",
                "pt" or "prt" => "pt",
                "id" => "id",
                "nl" => "nl",
                "sv" or "se" => "sv",
                "da" or "dk" => "da",
                "no" => "no",
                "ms" => "ms",
                _ => "de"
            };
        }

        private static string GetVariantLabel(string variantType, string language)
        {
            return variantType switch
            {
                "vegan" => LocalizedWord(language, "Vegan", "Vegan", "Vegano", "Vegan"),
                "mealprep" => LocalizedWord(language, "Meal Prep", "Meal Prep", "Meal Prep", "Meal Prep"),
                "lowcarb" => LocalizedWord(language, "Low Carb", "Low Carb", "Low Carb", "Low Carb"),
                "highprotein" => LocalizedWord(language, "Mehr Protein", "More Protein", "Más proteína", "Mais proteína"),
                _ => LocalizedWord(language, "AI-Variante", "AI Variant", "Variante AI", "Variante AI")
            };
        }

        private static string GetLanguageLabel(string language)
            => language switch
            {
                "en" => "English",
                "es" => "Español",
                "pt" => "Português",
                "id" => "Bahasa Indonesia",
                "nl" => "Nederlands",
                "sv" => "Svenska",
                "da" => "Dansk",
                "no" => "Norsk",
                "ms" => "Bahasa Melayu",
                _ => "Deutsch"
            };

        private static string PrefixTitle(string language, string title, string prefix)
            => language switch
            {
                "en" or "id" or "nl" or "sv" or "da" or "no" or "ms" => $"{prefix} {title}",
                "es" => $"{title} · {prefix}",
                "pt" => $"{title} · {prefix}",
                _ => $"{prefix} {title}"
            };

        private static string LocalizedWord(string language, string de, string en, string es, string pt)
            => language switch
            {
                "en" or "id" or "nl" or "sv" or "da" or "no" or "ms" => en,
                "es" => es,
                "pt" => pt,
                _ => de
            };

        private static string LocalizedHint(string language, string de, string en, string es, string pt)
            => LocalizedWord(language, de, en, es, pt);


        private static string BuildApiErrorMessage(System.Net.HttpStatusCode statusCode, string? responseContent, string? fallbackMessage = null)
        {
            var prefix = string.IsNullOrWhiteSpace(fallbackMessage)
                ? "AI-Vorschau konnte nicht geladen werden."
                : fallbackMessage;

            var compactBody = (responseContent ?? string.Empty).Trim();
            if (compactBody.Length > 400)
            {
                compactBody = compactBody[..400] + "...";
            }

            compactBody = compactBody
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();

            return string.IsNullOrWhiteSpace(compactBody)
                ? $"{prefix} (HTTP {(int)statusCode})"
                : $"{prefix} (HTTP {(int)statusCode}): {compactBody}";
        }

        private static string GetIngredientArticleHint(string language)
            => language switch
            {
                "en" => "{{ingredient}} should include the fitting article when useful, e.g. 'the eggs'.",
                "es" => "{{ingredient}} debe incluir el artículo correcto cuando tenga sentido, por ejemplo 'los huevos'.",
                "pt" => "{{ingredient}} deve incluir o artigo correto quando fizer sentido, por exemplo 'os ovos'.",
                _ => "{{ingredient}} soll den passenden Artikel enthalten, z. B. 'die Eier'."
            };

        private string GetLocalizedVariableOptionLabel(MasterStepVariableOptionDefinition option, string language)
        {
            var key = language switch
            {
                "es" => "esp",
                "pt" => "prt",
                _ => language
            };

            if (option.Labels.TryGetValue(key, out var localized) && !string.IsNullOrWhiteSpace(localized))
            {
                return localized;
            }

            return option.Labels.TryGetValue("de", out var german) && !string.IsNullOrWhiteSpace(german)
                ? german
                : option.Key;
        }

        private string GetLocalizedTemplate(MasterStepTemplateDefinition definition, string language)
        {
            var key = language switch
            {
                "es" => "esp",
                "pt" => "prt",
                _ => language
            };

            if (definition.Templates.TryGetValue(key, out var template) && !string.IsNullOrWhiteSpace(template))
            {
                return template;
            }

            return definition.Templates.TryGetValue("de", out var germanTemplate) ? germanTemplate : definition.MasterId;
        }

        private static string? TryExtractOutputText(JsonElement root)
        {
            if (root.TryGetProperty("output_text", out var outputTextElement) && outputTextElement.ValueKind == JsonValueKind.String)
            {
                return outputTextElement.GetString();
            }

            if (!root.TryGetProperty("output", out var outputArray) || outputArray.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var outputItem in outputArray.EnumerateArray())
            {
                if (!outputItem.TryGetProperty("content", out var contentArray) || contentArray.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var contentItem in contentArray.EnumerateArray())
                {
                    if (contentItem.TryGetProperty("type", out var typeElement)
                        && string.Equals(typeElement.GetString(), "output_text", StringComparison.OrdinalIgnoreCase)
                        && contentItem.TryGetProperty("text", out var textElement)
                        && textElement.ValueKind == JsonValueKind.String)
                    {
                        return textElement.GetString();
                    }
                }
            }

            return null;
        }

        private sealed class MasterStepCatalogDocument
        {
            [JsonPropertyName("master_steps")]
            public List<MasterStepTemplateDefinition> MasterSteps { get; set; } = new();
        }

        private sealed class MasterStepVariableCatalogDocument
        {
            [JsonPropertyName("variables")]
            public Dictionary<string, MasterStepVariableDefinition> Variables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class IngredientCategoryPlanResponse
        {
            public string Strategy { get; set; } = string.Empty;
            public string Reasoning { get; set; } = string.Empty;
            public List<string> PreferredCategoryKeys { get; set; } = new();
        }

        private sealed class IngredientPickResponse
        {
            public List<int> SelectedIngredientIds { get; set; } = new();
            public List<string> Reasons { get; set; } = new();
        }

        private sealed class IngredientSelectionContext
        {
            public bool HasPrimaryProtein { get; set; }
            public bool HasCarbSide { get; set; }
            public bool HasPotatoSide { get; set; }
            public bool HasRiceOrPastaSide { get; set; }
            public bool PreferSideReplacement { get; set; }
        }

        private sealed class MasterStepTemplateDefinition
        {
            [JsonPropertyName("master_id")]
            public string MasterId { get; set; } = string.Empty;

            [JsonPropertyName("phase")]
            public int Phase { get; set; }

            [JsonPropertyName("sub_group")]
            public string SubGroup { get; set; } = string.Empty;

            [JsonPropertyName("action")]
            public string Action { get; set; } = string.Empty;

            [JsonPropertyName("description")]
            public string Description { get; set; } = string.Empty;

            [JsonPropertyName("variables")]
            [JsonConverter(typeof(StringOrArrayConverter))]
            public List<string> Variables { get; set; } = new();

            [JsonPropertyName("required_variables")]
            public List<string> RequiredVariables { get; set; } = new();

            [JsonPropertyName("optional_variables")]
            [JsonConverter(typeof(NullableStringOrArrayConverter))]
            public List<string> OptionalVariables { get; set; } = new();

            [JsonPropertyName("selection_tags")]
            public List<string> SelectionTags { get; set; } = new();

            [JsonPropertyName("templates")]
            public Dictionary<string, string> Templates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class StepSelectionContext
        {
            public HashSet<string> AllowedStepKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public List<MasterStepTemplateDefinition> CandidateSteps { get; set; } = new();
            public List<RecipeAiTransformStepPlanItem> KeepExistingSteps { get; set; } = new();
            public List<RecipeAiTransformStepPlanItem> ReplaceExistingSteps { get; set; } = new();
        }

        private sealed class MasterStepVariableDefinition
        {
            [JsonPropertyName("description")]
            public string Description { get; set; } = string.Empty;

            [JsonPropertyName("options")]
            public List<MasterStepVariableOptionDefinition> Options { get; set; } = new();
        }

        private sealed class MasterStepVariableOptionDefinition
        {
            [JsonPropertyName("key")]
            public string Key { get; set; } = string.Empty;

            [JsonPropertyName("labels")]
            public Dictionary<string, string> Labels { get; set; } = new(StringComparer.OrdinalIgnoreCase);

            [JsonPropertyName("tags")]
            public List<string> Tags { get; set; } = new();
        }

        private sealed class StringOrArrayConverter : JsonConverter<List<string>>
        {
            public override List<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    var value = reader.GetString();
                    return string.IsNullOrWhiteSpace(value) ? new List<string>() : new List<string> { value };
                }

                if (reader.TokenType == JsonTokenType.StartArray)
                {
                    var values = JsonSerializer.Deserialize<List<string>>(ref reader, options);
                    return values?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new List<string>();
                }

                return new List<string>();
            }

            public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
                => JsonSerializer.Serialize(writer, value, options);
        }

        private sealed class NullableStringOrArrayConverter : JsonConverter<List<string>>
        {
            public override List<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Null)
                {
                    return new List<string>();
                }

                if (reader.TokenType == JsonTokenType.String)
                {
                    var value = reader.GetString();
                    return string.IsNullOrWhiteSpace(value) ? new List<string>() : new List<string> { value };
                }

                if (reader.TokenType == JsonTokenType.StartArray)
                {
                    var values = JsonSerializer.Deserialize<List<string>>(ref reader, options);
                    return values?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new List<string>();
                }

                return new List<string>();
            }

            public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
                => JsonSerializer.Serialize(writer, value, options);
        }
    }
}













