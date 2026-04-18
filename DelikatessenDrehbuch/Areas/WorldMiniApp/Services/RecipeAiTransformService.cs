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

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    public sealed class RecipeAiTransformService : IRecipeAiTransformService
    {
        private const string OpenAiEndpoint = "https://api.openai.com/v1/responses";
        private const string ModelName = "gpt-5-mini";
        private const int MaxChangeRounds = 2;
        private const string MasterStepDataPath = "wwwroot/data/master_steps.json";

        private static readonly Regex TemplateVariableRegex = new(@"{{\s*([^}]+?)\s*}}", RegexOptions.Compiled);
        private static readonly Regex OptionalTemplateRegex = new(@"\(([^()]*{{\s*[^}]+?\s*}}[^()]*)\)|\[([^\[\]]*{{\s*[^}]+?\s*}}[^\[\]]*)\]", RegexOptions.Compiled);
        private static readonly Regex FallbackTokenRegex = new(@"\{~([^~]+)~([^~]+)~\}", RegexOptions.Compiled);
        private static readonly Regex MultiWhitespaceRegex = new(@"\s{2,}", RegexOptions.Compiled);
        private static readonly Regex SpaceBeforePunctuationRegex = new(@"\s+([,.;:!?])", RegexOptions.Compiled);

        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<RecipeAiTransformService> _logger;
        private readonly Lazy<Dictionary<string, MasterStepTemplateDefinition>> _masterStepsByKey;

        public RecipeAiTransformService(
            HttpClient httpClient,
            IConfiguration configuration,
            IHostEnvironment environment,
            ILogger<RecipeAiTransformService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _environment = environment;
            _logger = logger;
            _masterStepsByKey = new Lazy<Dictionary<string, MasterStepTemplateDefinition>>(LoadMasterSteps, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public async Task<RecipeAiTransformPreview> BuildPreviewAsync(
            RecipeBaseData recipe,
            string variantType,
            string language,
            string? userNote,
            int appliedChangeCount,
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

            var sourceStepPlan = BuildSourceStepPlan(recipe, normalizedLanguage);
            var apiKey = Environment.GetEnvironmentVariable("SecretKeyOpenAi") ?? _configuration["SecretKeyOpenAi"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                if (_environment.IsDevelopment())
                {
                    _logger.LogWarning("SecretKeyOpenAi fehlt. Verwende lokalen Rezept-AI-Fallback für RecipeId={RecipeId} VariantType={VariantType}.", recipe.Id, normalizedVariantType);
                    return BuildFallbackPreview(recipe, normalizedVariantType, normalizedLanguage, trimmedUserNote, safeChangeCount, sourceStepPlan);
                }

                throw new InvalidOperationException("Die Umgebungsvariable oder Konfiguration 'SecretKeyOpenAi' ist nicht gesetzt.");
            }

            var requestBody = BuildOpenAiRequestBody(recipe, normalizedVariantType, normalizedLanguage, trimmedUserNote, safeChangeCount, sourceStepPlan);
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

            NormalizePreview(preview, recipe, normalizedVariantType, normalizedLanguage, trimmedUserNote, safeChangeCount, false, sourceStepPlan, allowOnlySourceKeys: sourceStepPlan.Count > 0);
            return preview;
        }

        private object BuildOpenAiRequestBody(
            RecipeBaseData recipe,
            string variantType,
            string language,
            string userNote,
            int appliedChangeCount,
            IReadOnlyList<RecipeAiTransformStepPlanItem> sourceStepPlan)
        {
            var sourceIngredients = BuildIngredientPreview(recipe, language);
            var variantLabel = GetVariantLabel(variantType, language);
            var languageLabel = GetLanguageLabel(language);
            var allowedStepKeys = sourceStepPlan.Select(x => x.MasterStepKey).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var serializedSourceStepPlan = sourceStepPlan.Select(x => new
            {
                masterStepKey = x.MasterStepKey,
                phase = x.Phase,
                renderedText = x.RenderedText,
                variables = x.Variables
            }).ToList();

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
                                    "Du transformierst Rezepte für eine Food-App. " +
                                    "Antworte ausschließlich als valides JSON im vorgegebenen Schema. " +
                                    "Wichtig: Du darfst KEINE freien Schritttexte schreiben. " +
                                    "Du darfst nur masterStepKeys aus der erlaubten Liste zurückgeben. " +
                                    "stepPlan ist nur eine strukturierte Auswahl vorhandener Steps mit Variablen. " +
                                    "Wenn keine erlaubten Schritt-Keys vorhanden sind, gib ein leeres stepPlan zurück. " +
                                    "Zutaten, Titel, Summary und Highlights dürfen sprachlich formuliert sein, die Steps aber nie als Freitext."
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
                                       $"Erlaubte masterStepKeys: {JsonSerializer.Serialize(allowedStepKeys)}\n" +
                                       $"Aktueller StepPlan: {JsonSerializer.Serialize(serializedSourceStepPlan)}\n" +
                                       $"User-Hinweis: {(string.IsNullOrWhiteSpace(userNote) ? "Kein zusätzlicher Hinweis" : userNote)}\n" +
                                       "Gib eine umgesetzte Rezeptvorschau zurück. stepPlan darf nur Keys aus der erlaubten Liste enthalten. Variablenwerte sollen konkrete, kurze Strings sein."
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

        private RecipeAiTransformPreview BuildFallbackPreview(
            RecipeBaseData recipe,
            string variantType,
            string language,
            string userNote,
            int appliedChangeCount,
            IReadOnlyList<RecipeAiTransformStepPlanItem> sourceStepPlan)
        {
            var ingredients = BuildIngredientPreview(recipe, language);
            var highlights = new List<string>();
            var summaryParts = new List<string>();
            var title = recipe.Title;
            var stepPlan = sourceStepPlan.Select(CloneStepPlanItem).ToList();

            switch (variantType)
            {
                case "vegan":
                    title = PrefixTitle(language, recipe.Title, "Vegan");
                    ReplaceIngredient(ingredients, language, new[] { "huhn", "chicken", "rind", "beef", "schinken", "ham" }, LocalizedWord(language, "Tofu", "Tofu", "Tofu", "Tofu"), LocalizedHint(language, "durch Tofu ersetzt", "replaced with tofu", "sustituido por tofu", "substituído por tofu"), highlights);
                    ReplaceIngredient(ingredients, language, new[] { "milch", "milk", "sahne", "cream" }, LocalizedWord(language, "Hafer-Cuisine", "oat cream", "crema de avena", "creme de aveia"), LocalizedHint(language, "veganisiert", "made dairy-free", "versión vegana", "versão vegana"), highlights);
                    ReplaceIngredient(ingredients, language, new[] { "käse", "kaese", "cheese", "parmesan" }, LocalizedWord(language, "vegane Alternative", "vegan alternative", "alternativa vegana", "alternativa vegana"), LocalizedHint(language, "tierfrei ersetzt", "swapped for vegan alternative", "reemplazado por una alternativa vegana", "substituído por alternativa vegana"), highlights);
                    summaryParts.Add(LocalizedWord(language, "Tierische Zutaten wurden möglichst schonend ersetzt.", "Animal-based ingredients were swapped as gently as possible.", "Se sustituyeron los ingredientes de origen animal con cuidado.", "Os ingredientes de origem animal foram substituídos com cuidado."));
                    break;
                case "mealprep":
                    title = PrefixTitle(language, recipe.Title, "Meal Prep");
                    summaryParts.Add(LocalizedWord(language, "Die Variante ist auf gutes Vorbereiten und entspanntes Aufwärmen ausgelegt.", "This version is tuned for prepping ahead and easy reheating.", "Esta versión está pensada para preparar con antelación y recalentar fácilmente.", "Esta versão foi ajustada para preparar antes e aquecer facilmente."));
                    highlights.Add(LocalizedWord(language, "Meal-Prep-freundliche Reihenfolge", "meal-prep-friendly workflow", "flujo pensado para meal prep", "fluxo pensado para meal prep"));
                    break;
                case "lowcarb":
                    title = PrefixTitle(language, recipe.Title, "Low Carb");
                    ReplaceIngredient(ingredients, language, new[] { "reis", "rice" }, LocalizedWord(language, "Blumenkohlreis", "cauliflower rice", "arroz de coliflor", "arroz de couve-flor"), LocalizedHint(language, "KH reduziert", "lower-carb swap", "menos carbohidratos", "menos carboidratos"), highlights);
                    ReplaceIngredient(ingredients, language, new[] { "nudel", "pasta", "spaghetti" }, LocalizedWord(language, "Zucchini-Nudeln", "zucchini noodles", "fideos de calabacín", "macarrão de curgete"), LocalizedHint(language, "leichtere Beilage", "lighter side", "guarnición ligera", "acompanhamento leve"), highlights);
                    ReplaceIngredient(ingredients, language, new[] { "kartoffel", "potato" }, LocalizedWord(language, "Ofengemüse", "roasted vegetables", "verduras asadas", "legumes assados"), LocalizedHint(language, "stärkearme Alternative", "lower-starch alternative", "alternativa baja en almidón", "alternativa com menos amido"), highlights);
                    summaryParts.Add(LocalizedWord(language, "Stärkereiche Bestandteile wurden soweit möglich gegen leichtere Alternativen getauscht.", "Starchy parts were swapped for lighter alternatives where possible.", "Los componentes ricos en almidón se cambiaron por opciones más ligeras cuando fue posible.", "Os componentes ricos em amido foram trocados por opções mais leves sempre que possível."));
                    break;
                case "highprotein":
                    title = PrefixTitle(language, recipe.Title, LocalizedWord(language, "Protein", "Protein", "Proteína", "Proteína"));
                    highlights.Add(LocalizedWord(language, "Proteinquelle verstärkt", "protein source boosted", "fuente de proteína reforzada", "fonte de proteína reforçada"));
                    ingredients.Add(new RecipeAiTransformIngredientPreview
                    {
                        Name = LocalizedWord(language, "Skyr oder Extra-Protein", "skyr or extra protein", "skyr o proteína extra", "skyr ou proteína extra"),
                        Quantity = LocalizedWord(language, "1 Portion", "1 portion", "1 porción", "1 porção"),
                        ChangeHint = LocalizedHint(language, "optional ergänzt", "optional addition", "añadido opcional", "adição opcional"),
                        IsModified = true
                    });
                    summaryParts.Add(LocalizedWord(language, "Die Variante legt den Fokus stärker auf Sättigung und Protein pro Portion.", "This version focuses more on satiety and protein per serving.", "Esta versión pone más foco en saciedad y proteína por porción.", "Esta versão foca mais em saciedade e proteína por porção."));
                    break;
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

            NormalizePreview(preview, recipe, variantType, language, userNote, appliedChangeCount, true, sourceStepPlan, allowOnlySourceKeys: sourceStepPlan.Count > 0);
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

            var validatedPlan = ValidateAndRenderStepPlan(preview.StepPlan, language, sourceStepPlan, allowOnlySourceKeys);
            if (validatedPlan.Count == 0)
            {
                validatedPlan = sourceStepPlan.Select(CloneStepPlanItem).ToList();
            }

            preview.StepPlan = validatedPlan;
            preview.Steps = validatedPlan.Count > 0
                ? validatedPlan.Select((item, index) => new RecipeAiTransformStepPreview
                {
                    Index = index + 1,
                    Text = item.RenderedText
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
                ? new HashSet<string>(sourceByKey.Keys, StringComparer.OrdinalIgnoreCase)
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
            return processed.Replace("( ", "(").Replace(" )", ")").Trim();
        }

        private static bool HasTemplateValue(IReadOnlyDictionary<string, string> values, string key)
            => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value);

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
                .Select(x => new RecipeAiTransformStepPreview { Index = x.StepIndex, Text = GetStepText(x.RecipePreparationStep, language).Trim() })
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
            foreach (var ingredient in ingredients)
            {
                var lowerName = ingredient.Name.ToLowerInvariant();
                if (!searchTerms.Any(term => lowerName.Contains(term)))
                {
                    continue;
                }

                ingredient.Name = replacementName;
                ingredient.ChangeHint = hint;
                ingredient.IsModified = true;
                if (!string.IsNullOrWhiteSpace(hint))
                {
                    highlights.Add(hint);
                }
                return;
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
                _ => ingredient.Name_DE
            } ?? ingredient.Name_DE ?? string.Empty;
        }

        private static string GetStepText(RecipePreparationSteps? step, string language)
        {
            if (step == null) return string.Empty;
            return language switch
            {
                "en" => step.Step_EN,
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
                "es" => "es",
                "pt" => "pt",
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
                _ => "Deutsch"
            };

        private static string PrefixTitle(string language, string title, string prefix)
            => language switch
            {
                "en" => $"{prefix} {title}",
                "es" => $"{title} · {prefix}",
                "pt" => $"{title} · {prefix}",
                _ => $"{prefix} {title}"
            };

        private static string LocalizedWord(string language, string de, string en, string es, string pt)
            => language switch
            {
                "en" => en,
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

        private sealed class MasterStepTemplateDefinition
        {
            [JsonPropertyName("master_id")]
            public string MasterId { get; set; } = string.Empty;

            [JsonPropertyName("phase")]
            public int Phase { get; set; }

            [JsonPropertyName("templates")]
            public Dictionary<string, string> Templates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }
    }
}






