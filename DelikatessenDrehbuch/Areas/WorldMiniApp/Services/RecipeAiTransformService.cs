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
        private const string DefaultOpenAiCheapModel = "gpt-4o-mini"; // fast/cheap, supports Structured Outputs
        private const string DefaultOpenAiPremiumModel = "gpt-4o";    // higher quality; can be overridden via config
        private const int MaxChangeRounds = 2;
        private const int MaxIngredientSuggestions = 5;
        private const int AiRequestTimeoutSeconds = 45;
        private const bool EnableQualityPass = true;
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

        private static readonly string[] ToolCategoryKeys =
        {
            "poultry","beef","pork","lamb","fish","seafood","eggs","cheese","yogurt","dairy_product",
            "tofu","tempeh","legumes","grains","pseudograins","pasta","rice","potato","vegetables","leafy_greens",
            "mushrooms","fruit","nuts","seeds","oils","fats","spices","herbs","sauces","sweeteners",
            "baking_ingredients","broths"
        };

        private static readonly HashSet<string> ToolCategoryKeySet =
            new(ToolCategoryKeys, StringComparer.OrdinalIgnoreCase);

        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<RecipeAiTransformService> _logger;
        private readonly IIngredientResolverService _ingredientResolverService;
        private readonly Lazy<Dictionary<string, MasterStepTemplateDefinition>> _masterStepsByKey;
        private readonly Lazy<Dictionary<string, MasterStepVariableDefinition>> _masterStepVariablesByName;

        private string OpenAiCheapModel =>
            (_configuration["WorldMiniApp:Ai:OpenAi:CheapModel"] ?? string.Empty).Trim()
            is { Length: > 0 } cheap
                ? cheap
                : DefaultOpenAiCheapModel;

        private string OpenAiPremiumModel =>
            (_configuration["WorldMiniApp:Ai:OpenAi:PremiumModel"] ?? string.Empty).Trim()
            is { Length: > 0 } premium
                ? premium
                : DefaultOpenAiPremiumModel;

        private bool AllowLocalAiFallback =>
            _configuration.GetValue<bool>("WorldMiniApp:Ai:AllowLocalFallback", false);

        public RecipeAiTransformService(
            HttpClient httpClient,
            IConfiguration configuration,
            IHostEnvironment environment,
            IIngredientResolverService ingredientResolverService,
            ILogger<RecipeAiTransformService> logger)
        {
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromSeconds(AiRequestTimeoutSeconds);
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
            string aiProvider,
            string language,
            string? userNote,
            int appliedChangeCount,
            RecipeAiIngredientSuggestionItem? selectedConcept = null,
            IReadOnlyList<RecipeAiIngredientSuggestionItem>? selectedIngredients = null,
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
                .Take(MaxIngredientSuggestions)
                .ToList();
            var ingredientSelectionContext = AnalyzeIngredientSelectionContext(recipe);
            var existingIngredientIds = recipe.Ingredients?
                .Select(x => x.Ingredient?.IngredientsAndNutrients?.Id ?? 0)
                .Where(x => x > 0)
                .Distinct()
                .ToList() ?? new List<int>();
            var defaultCategoryKeys = GetDefaultCategoryKeysForVariant(normalizedVariantType, ingredientSelectionContext);
             var availableVariantCandidates = await _ingredientResolverService.GetCandidatesForGoalAsync(
                 normalizedVariantType,
                 normalizedLanguage,
                 defaultCategoryKeys,
                 existingIngredientIds,
                 take: 1500,
                 cancellationToken: cancellationToken);
 
             availableVariantCandidates = FilterCandidatesForRecipeContext(availableVariantCandidates, normalizedVariantType, ingredientSelectionContext);
             availableVariantCandidates = DiversifyCandidatesForPrompt(availableVariantCandidates, normalizedVariantType, availableVariantCandidates.Count);
             var availableVariantIngredients = availableVariantCandidates
                 .Select(x => new RecipeAiIngredientSuggestionItem
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
                })
                .ToList();

            var sourceStepPlan = BuildSourceStepPlan(recipe, normalizedLanguage);
            var stepSelectionContext = BuildStepSelectionContext(sourceStepPlan, selectedIngredientList, normalizedVariantType);

            var pantryIngredients = await BuildPantryIngredientHintsAsync(normalizedLanguage, existingIngredientIds, cancellationToken);
            var availableVariantIngredientsForPrompt = MergeIngredientSuggestionLists(availableVariantIngredients, pantryIngredients);

            // If a selected concept references ingredientIds outside the current candidate pool (e.g. quinoa from a tool call),
            // we still want them to be "allowed" for the preview prompt. We resolve them by ID and merge them into the prompt list.
            var conceptPlanIds = (selectedConcept?.ConceptIngredientPlan ?? new List<RecipeAiConceptIngredientPlanItem>())
                .SelectMany(x =>
                {
                    var ids = new List<int>();
                    if (x != null && x.IngredientId > 0) ids.Add(x.IngredientId);
                    if (x?.ReplacesIngredientId.GetValueOrDefault() > 0) ids.Add(x!.ReplacesIngredientId!.Value);
                    return ids;
                })
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            if (conceptPlanIds.Count > 0)
            {
                var extras = await _ingredientResolverService.GetCandidatesByIdsAsync(
                    ingredientIds: conceptPlanIds,
                    language: normalizedLanguage,
                    cancellationToken: cancellationToken);

                if (extras.Count > 0)
                {
                    var extraItems = extras.Select(x => new RecipeAiIngredientSuggestionItem
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
                        AiReason = "concept_plan"
                    }).ToList();

                    availableVariantIngredientsForPrompt = MergeIngredientSuggestionLists(availableVariantIngredientsForPrompt, extraItems);
                }
            }

            var normalizedAiProvider = NormalizeAiProvider(aiProvider);
            var apiKey = ResolveApiKey(normalizedAiProvider);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                if (_environment.IsDevelopment() && AllowLocalAiFallback)
                {
                    _logger.LogWarning("SecretKeyOpenAi fehlt. Verwende lokalen Rezept-AI-Fallback für RecipeId={RecipeId} VariantType={VariantType}.", recipe.Id, normalizedVariantType);
                    return BuildFallbackPreview(recipe, normalizedVariantType, normalizedLanguage, trimmedUserNote, safeChangeCount, sourceStepPlan, stepSelectionContext, selectedIngredientList);
                }

                throw new InvalidOperationException(GetMissingApiKeyMessage(normalizedAiProvider));
            }

            var promptRequest = BuildPreviewPromptRequest(recipe, normalizedVariantType, normalizedLanguage, trimmedUserNote, safeChangeCount, sourceStepPlan, selectedConcept, selectedIngredientList, availableVariantIngredientsForPrompt);

            // Collect ingredientPlan IDs that must be protected from pruning throughout all NormalizePreview passes.
            var protectedIngredientIds = selectedConcept?.ConceptIngredientPlan?
                .Where(x => x != null && x.IngredientId > 0 && !string.Equals((x.Action ?? string.Empty).Trim(), "remove", StringComparison.OrdinalIgnoreCase))
                .Select(x => x.IngredientId)
                .ToHashSet() as IReadOnlyCollection<int>;

            var preview = await ExecuteStructuredAiCallAsync<RecipeAiTransformPreview>(promptRequest, normalizedAiProvider, apiKey, cancellationToken);
            if (preview == null)
            {
                _logger.LogWarning("AI preview generation returned null. Using fallback. Provider={Provider} RecipeId={RecipeId} VariantType={VariantType}", normalizedAiProvider, recipe.Id, normalizedVariantType);
                return BuildFallbackPreview(recipe, normalizedVariantType, normalizedLanguage, trimmedUserNote, safeChangeCount, sourceStepPlan, stepSelectionContext, selectedIngredientList);
            }

            // If the user selected a concept with an explicit ingredient plan, ensure those planned ingredients
            // are actually used in the preparation text. Otherwise the prune step would remove them and the user
            // sees "concept had X, recipe only used Y".
            if (selectedConcept?.ConceptIngredientPlan?.Count > 0 && !preview.UsedFallback)
            {
                var planned = selectedConcept.ConceptIngredientPlan
                    .Where(x => x != null && x.IngredientId > 0 && !string.Equals((x.Action ?? string.Empty).Trim(), "remove", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (planned.Count > 0)
                {
                    var missingPlanned = FindMissingPlannedIngredients(preview, planned, availableVariantIngredientsForPrompt, normalizedLanguage);
                    if (missingPlanned.Count > 0)
                    {
                        var applied = await EnsureConceptPlanIngredientsUsedAsync(
                            preview,
                            recipe,
                            normalizedVariantType,
                            normalizedAiProvider,
                            normalizedLanguage,
                            trimmedUserNote,
                            safeChangeCount,
                            sourceStepPlan,
                            stepSelectionContext.AllowedStepKeys,
                            selectedConcept,
                            planned,
                            missingPlanned,
                            selectedIngredientList,
                            availableVariantIngredientsForPrompt,
                            apiKey,
                            cancellationToken);

                        if (applied != null)
                        {
                            preview = applied;
                        }
                    }
                }
            }

            NormalizePreview(preview, recipe, normalizedVariantType, normalizedLanguage, trimmedUserNote, safeChangeCount, false, sourceStepPlan, stepSelectionContext.AllowedStepKeys, availableVariantIngredientsForPrompt, allowOnlySourceKeys: sourceStepPlan.Count > 0, protectedIngredientIds: protectedIngredientIds);
            ApplyCookabilitySafeguards(preview, normalizedLanguage, selectedIngredientList);

            if (EnableQualityPass && !preview.UsedFallback)
            {
                var repaired = await QualityCheckAndRepairPreviewAsync(
                    preview,
                    recipe,
                    normalizedVariantType,
                    normalizedAiProvider,
                    normalizedLanguage,
                    trimmedUserNote,
                    safeChangeCount,
                    sourceStepPlan,
                    stepSelectionContext.AllowedStepKeys,
                    selectedIngredientList,
                    availableVariantIngredientsForPrompt,
                    apiKey,
                    cancellationToken);

                if (repaired != null)
                {
                    preview = repaired;
                    NormalizePreview(preview, recipe, normalizedVariantType, normalizedLanguage, trimmedUserNote, safeChangeCount, false, sourceStepPlan, stepSelectionContext.AllowedStepKeys, availableVariantIngredientsForPrompt, allowOnlySourceKeys: sourceStepPlan.Count > 0, protectedIngredientIds: protectedIngredientIds);
                    ApplyCookabilitySafeguards(preview, normalizedLanguage, selectedIngredientList);
                }
            }

            if (selectedConcept != null && !preview.UsedFallback)
            {
                var aligned = await EnsureSelectedConceptAppliedAsync(
                    preview,
                    recipe,
                    normalizedVariantType,
                    normalizedAiProvider,
                    normalizedLanguage,
                    trimmedUserNote,
                    safeChangeCount,
                    sourceStepPlan,
                    stepSelectionContext.AllowedStepKeys,
                    selectedConcept,
                    selectedIngredientList,
                    availableVariantIngredientsForPrompt,
                    apiKey,
                    cancellationToken);

                if (aligned != null)
                {
                    preview = aligned;
                    NormalizePreview(preview, recipe, normalizedVariantType, normalizedLanguage, trimmedUserNote, safeChangeCount, false, sourceStepPlan, stepSelectionContext.AllowedStepKeys, availableVariantIngredientsForPrompt, allowOnlySourceKeys: sourceStepPlan.Count > 0, protectedIngredientIds: protectedIngredientIds);
                    ApplyCookabilitySafeguards(preview, normalizedLanguage, selectedIngredientList);
                }
            }

            if (!preview.UsedFallback)
            {
                var pantryRepaired = await EnsurePantryConsistencyAsync(
                    preview,
                    recipe,
                    normalizedVariantType,
                    normalizedAiProvider,
                    normalizedLanguage,
                    trimmedUserNote,
                    safeChangeCount,
                    sourceStepPlan,
                    stepSelectionContext.AllowedStepKeys,
                    selectedIngredientList,
                    availableVariantIngredientsForPrompt,
                    apiKey,
                    cancellationToken);

                if (pantryRepaired != null)
                {
                    preview = pantryRepaired;
                    NormalizePreview(preview, recipe, normalizedVariantType, normalizedLanguage, trimmedUserNote, safeChangeCount, false, sourceStepPlan, stepSelectionContext.AllowedStepKeys, availableVariantIngredientsForPrompt, allowOnlySourceKeys: sourceStepPlan.Count > 0, protectedIngredientIds: protectedIngredientIds);
                    ApplyCookabilitySafeguards(preview, normalizedLanguage, selectedIngredientList);
                }
            }

            // Final safety: after all QA/repair passes, enforce that the selected concept's ingredientPlan
            // is still present in ingredients[] AND explicitly used in preparationText. This prevents late QA passes
            // from "undoing" the concept (e.g., dropping chickpeas and reverting to the base recipe).
            if (selectedConcept?.ConceptIngredientPlan?.Count > 0 && !preview.UsedFallback)
            {
                var enforced = await EnforceConceptIngredientPlanContractAsync(
                    preview,
                    recipe,
                    normalizedVariantType,
                    normalizedAiProvider,
                    normalizedLanguage,
                    trimmedUserNote,
                    safeChangeCount,
                    sourceStepPlan,
                    stepSelectionContext.AllowedStepKeys,
                    selectedConcept,
                    selectedIngredientList,
                    availableVariantIngredientsForPrompt,
                    apiKey,
                    cancellationToken);

                if (enforced != null)
                {
                    preview = enforced;
                    NormalizePreview(preview, recipe, normalizedVariantType, normalizedLanguage, trimmedUserNote, safeChangeCount, false, sourceStepPlan, stepSelectionContext.AllowedStepKeys, availableVariantIngredientsForPrompt, allowOnlySourceKeys: sourceStepPlan.Count > 0, protectedIngredientIds: protectedIngredientIds);
                    ApplyCookabilitySafeguards(preview, normalizedLanguage, selectedIngredientList);
                }
            }

            // Post-generation protein audit: if the protein delta is insufficient, boost plan ingredient quantities.
            if (string.Equals(normalizedVariantType, "highprotein", StringComparison.OrdinalIgnoreCase) && !preview.UsedFallback)
            {
                BoostProteinIfInsufficient(preview, recipe, availableVariantIngredientsForPrompt, protectedIngredientIds);
            }

            return preview;
        }

        /// <summary>
        /// Estimates total protein (in grams) for the preview's ingredient list by matching
        /// each ingredient to its ProteinPer100g value and the parsed quantity in grams.
        /// </summary>
        private static decimal EstimatePreviewProteinTotal(
            RecipeAiTransformPreview preview,
            RecipeBaseData recipe,
            IReadOnlyList<RecipeAiIngredientSuggestionItem> availableVariantIngredients)
        {
            if (preview?.Ingredients == null || preview.Ingredients.Count == 0) return 0m;

            // Build protein lookup: ingredientId → ProteinPer100g
            var proteinLookup = new Dictionary<int, decimal>();

            foreach (var item in availableVariantIngredients ?? Array.Empty<RecipeAiIngredientSuggestionItem>())
            {
                if (item != null && item.IngredientId > 0 && !proteinLookup.ContainsKey(item.IngredientId))
                {
                    proteinLookup[item.IngredientId] = item.ProteinPer100g;
                }
            }

            foreach (var link in recipe?.Ingredients ?? Array.Empty<RecipeJoinIngredientMeasureQuantity>())
            {
                var nutrient = link?.Ingredient?.IngredientsAndNutrients;
                if (nutrient != null && nutrient.Id > 0 && !proteinLookup.ContainsKey(nutrient.Id))
                {
                    proteinLookup[nutrient.Id] = nutrient.Protein_a_100g;
                }
            }

            decimal total = 0m;
            foreach (var ing in preview.Ingredients)
            {
                if (ing == null || ing.IngredientId <= 0) continue;
                if (!proteinLookup.TryGetValue(ing.IngredientId, out var proteinPer100g) || proteinPer100g <= 0m) continue;

                var gramsEstimate = EstimateGrams(ing.Quantity, ing.Measure);
                if (gramsEstimate <= 0m) continue;

                total += proteinPer100g * (gramsEstimate / 100m);
            }

            return total;
        }

        /// <summary>
        /// Rough gram estimate from quantity + measure strings.
        /// Only handles common units; returns 0 for unknown units (conservative).
        /// </summary>
        private static decimal EstimateGrams(string? quantity, string? measure)
        {
            var qStr = (quantity ?? string.Empty).Trim().Replace(',', '.');
            if (!decimal.TryParse(qStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var qty) || qty <= 0m)
            {
                return 0m;
            }

            var m = (measure ?? string.Empty).Trim().ToLowerInvariant().TrimEnd('.');
            return m switch
            {
                "g" or "gramm" => qty,
                "kg" => qty * 1000m,
                "ml" => qty,               // rough: 1ml ≈ 1g for most liquids/sauces
                "l" or "liter" => qty * 1000m,
                "el" => qty * 15m,          // 1 EL ≈ 15g
                "tl" => qty * 5m,           // 1 TL ≈ 5g
                "stk" or "stück" or "stk." => qty * 80m,  // rough average piece weight
                _ => 0m
            };
        }

        /// <summary>
        /// If the protein increase is below 15%, boost the main protein plan ingredient quantity
        /// so that the target of ~20% increase is met.
        /// </summary>
        private static void BoostProteinIfInsufficient(
            RecipeAiTransformPreview preview,
            RecipeBaseData recipe,
            IReadOnlyList<RecipeAiIngredientSuggestionItem> availableVariantIngredients,
            IReadOnlyCollection<int>? protectedIngredientIds)
        {
            if (preview?.Ingredients == null || protectedIngredientIds == null || protectedIngredientIds.Count == 0) return;

            var (baselineTotal, baselinePerPortion, portions) = TryEstimateBaselineProtein(recipe);
            if (baselineTotal <= 0m || portions <= 0) return;

            var previewTotal = EstimatePreviewProteinTotal(preview, recipe, availableVariantIngredients);
            if (previewTotal <= 0m) return;

            var deltaPercent = baselineTotal > 0m ? ((previewTotal - baselineTotal) / baselineTotal) * 100m : 0m;

            // If already >= 15% increase, no boost needed.
            if (deltaPercent >= 15m) return;

            // Target: 22% increase (midpoint between 20% and 25%).
            var targetTotal = baselineTotal * 1.22m;
            var deficit = targetTotal - previewTotal;
            if (deficit <= 0m) return;

            // Find the best plan ingredient to boost (highest ProteinPer100g among plan items).
            var proteinLookup = (availableVariantIngredients ?? Array.Empty<RecipeAiIngredientSuggestionItem>())
                .Where(x => x != null && x.IngredientId > 0 && protectedIngredientIds.Contains(x.IngredientId) && x.ProteinPer100g > 0m)
                .GroupBy(x => x.IngredientId)
                .ToDictionary(g => g.Key, g => g.First());

            var bestCandidate = preview.Ingredients
                .Where(ing => ing != null && ing.IngredientId > 0 && proteinLookup.ContainsKey(ing.IngredientId))
                .OrderByDescending(ing => proteinLookup[ing.IngredientId].ProteinPer100g)
                .FirstOrDefault();

            if (bestCandidate == null) return;

            var candidateInfo = proteinLookup[bestCandidate.IngredientId];
            if (candidateInfo.ProteinPer100g <= 0m) return;

            // Calculate how many extra grams we need: deficit_g_protein / (protein_per_100g / 100)
            var extraGramsNeeded = (deficit / candidateInfo.ProteinPer100g) * 100m;

            var currentGrams = EstimateGrams(bestCandidate.Quantity, bestCandidate.Measure);
            var currentMeasure = (bestCandidate.Measure ?? string.Empty).Trim().ToLowerInvariant().TrimEnd('.');

            // Only boost gram-based quantities (safe to modify).
            if (currentMeasure is not ("g" or "gramm" or "ml")) return;

            var boostedGrams = Math.Round(currentGrams + extraGramsNeeded, 0);
            // Cap at reasonable maximum (don't add 2kg of chickpeas).
            var maxGrams = Math.Max(currentGrams * 2.5m, 500m);
            boostedGrams = Math.Min(boostedGrams, maxGrams);

            if (boostedGrams <= currentGrams) return;

            bestCandidate.Quantity = boostedGrams.ToString("0");
        }

        private async Task<RecipeAiTransformPreview?> EnsurePantryConsistencyAsync(
            RecipeAiTransformPreview draft,
            RecipeBaseData recipe,
            string variantType,
            string aiProvider,
            string language,
            string userNote,
            int appliedChangeCount,
            IReadOnlyList<RecipeAiTransformStepPlanItem> sourceStepPlan,
            IReadOnlyCollection<string> allowedStepKeys,
            IReadOnlyList<RecipeAiIngredientSuggestionItem> selectedIngredients,
            IReadOnlyList<RecipeAiIngredientSuggestionItem> availableVariantIngredients,
            string apiKey,
            CancellationToken cancellationToken)
        {
            try
            {
                // First try a deterministic fix: if ingredientsText mentions an ingredient (with a quantity),
                // but ingredients[] missed it, we patch it locally. This avoids an extra AI call and prevents
                // "Zutat im Text, aber nicht in der Liste" failures.
                PatchMissingIngredientsFromIngredientsText(draft, recipe, language, availableVariantIngredients);

                var missing = FindMissingPantryIngredients(draft, availableVariantIngredients, language);
                if (missing.Count == 0)
                {
                    return null;
                }

                // Give the model one focused pass to add the missing pantry ingredients with IDs + quantities,
                // and to update the relevant steps (without rewriting the whole recipe).
                var variantLabel = GetVariantLabel(variantType, language);
                var languageLabel = GetLanguageLabel(language);
                var compactTitle = TruncateForPrompt(recipe.Title, 180);

                // Keep the "allowed ingredient universe" very small in repair mode to avoid the model
                // pulling in random pantry items just because they are technically allowed.
                var allowedVariantIngredientContext = BuildAllowedIngredientContextForRepair(
                    recipe,
                    language,
                    draft,
                    requiredExtraIds: missing.Select(x => x.IngredientId),
                    availableVariantIngredients ?? Array.Empty<RecipeAiIngredientSuggestionItem>());

                var proteinGoalInstruction = BuildHighProteinGoalInstruction(recipe, variantType, language);
                var promptRequest = new AiPromptRequest
                {
                    SchemaName = "recipe_ai_transform_pantry_repair",
                    Schema = BuildSchema(),
                    MaxOutputTokens = 2500,
                    GeminiThinkingBudget = 0,
                    ModelTier = AiModelTier.Premium,
                    SystemPrompt =
                        BuildPromptPack(variantType, language, sideSupportInstruction: string.Empty, replacementInstruction: string.Empty, targetConceptCount: 1).QualityPassSystemPrompt +
                        "Zusatz-Aufgabe: Der Entwurf verwendet Zutaten im Text, die nicht in ingredients[] stehen. " +
                        "Ergaenze genau diese fehlenden Zutaten in ingredients[] (mit ingredientId + quantity + measure) und passe nur die betroffenen Schritte so an, dass es zusammenpasst. " +
                        "Aendere sonst so wenig wie moeglich (kein kompletter Rewrite).",
                    UserPrompt =
                        $"Sprache: {languageLabel}\n" +
                        $"Variante: {variantLabel} ({variantType})\n" +
                        $"Runde: {appliedChangeCount}/{MaxChangeRounds}\n" +
                        $"Titel: {compactTitle}\n" +
                        $"Fehlende Zutaten (mussen in ingredients[] auftauchen, weil sie im Text verwendet werden): {JsonSerializer.Serialize(missing.Select(x => new { id = x.IngredientId, name = x.Name }).ToList())}\n" +
                        $"Erlaubte Zutaten (id->name): {(allowedVariantIngredientContext.Count == 0 ? "keine" : JsonSerializer.Serialize(allowedVariantIngredientContext))}\n" +
                        (string.IsNullOrWhiteSpace(userNote) ? string.Empty : $"User-Hinweis: {userNote}\n") +
                        // Keep prompt small and unambiguous: ingredientsText often contains "nice-to-have" items and confuses the repair step.
                        $"Entwurf (zu reparieren):\n{JsonSerializer.Serialize(new { draft.Title, draft.Summary, draft.Ingredients, draft.PreparationText, draft.Highlights })}\n" +
                        (string.IsNullOrWhiteSpace(proteinGoalInstruction) ? string.Empty : $"Protein-Ziel: {proteinGoalInstruction}\n") +
                        "Gib die korrigierte, kochbare Rezeptvariante zurueck."
                };

                return await ExecuteStructuredAiCallAsync<RecipeAiTransformPreview>(promptRequest, aiProvider, apiKey, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Pantry repair pass failed. Provider={Provider} RecipeId={RecipeId} VariantType={VariantType}", NormalizeAiProvider(aiProvider), recipe?.Id, variantType);
                return null;
            }
        }

        private static List<RecipeAiConceptIngredientPlanItem> FindMissingPlannedIngredients(
            RecipeAiTransformPreview draft,
            IReadOnlyList<RecipeAiConceptIngredientPlanItem> plannedItems,
            IReadOnlyList<RecipeAiIngredientSuggestionItem> availableVariantIngredients,
            string language)
        {
            var result = new List<RecipeAiConceptIngredientPlanItem>();
            if (draft == null || plannedItems == null || plannedItems.Count == 0)
            {
                return result;
            }

            var existingIngredientIds = (draft.Ingredients ?? new List<RecipeAiTransformIngredientPreview>())
                .Where(x => x != null && x.IngredientId > 0)
                .Select(x => x.IngredientId)
                .ToHashSet();

            var haystack = string.Join("\n", new[]
            {
                draft.Title ?? string.Empty,
                draft.Summary ?? string.Empty,
                draft.PreparationText ?? string.Empty,
                string.Join("\n", (draft.Steps ?? new List<RecipeAiTransformStepPreview>()).Select(x => x?.Text ?? string.Empty))
            });

            if (string.IsNullOrWhiteSpace(haystack))
            {
                return plannedItems.ToList();
            }

            var nameById = (availableVariantIngredients ?? Array.Empty<RecipeAiIngredientSuggestionItem>())
                .Where(x => x?.IngredientId > 0 && !string.IsNullOrWhiteSpace(x.Name))
                .GroupBy(x => x.IngredientId)
                .ToDictionary(g => g.Key, g => g.First().Name, EqualityComparer<int>.Default);

            foreach (var item in plannedItems)
            {
                if (item == null || item.IngredientId <= 0)
                {
                    continue;
                }

                // If it is planned but missing from ingredients[], treat it as missing even if mentioned in text.
                if (!existingIngredientIds.Contains(item.IngredientId))
                {
                    result.Add(item);
                    continue;
                }

                // Prefer the plan name, fall back to our allowed-ingredient universe name.
                var name = (item.IngredientName ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name) && nameById.TryGetValue(item.IngredientId, out var n))
                {
                    name = n ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(name))
                {
                    result.Add(item);
                    continue;
                }

                // Use robust matching that handles German declension, parenthetical qualifiers, and token fallback.
                if (!ContainsIngredientReference(haystack, name))
                {
                    result.Add(item);
                }
            }

            return result;
        }

        private async Task<RecipeAiTransformPreview?> EnforceConceptIngredientPlanContractAsync(
            RecipeAiTransformPreview preview,
            RecipeBaseData recipe,
            string variantType,
            string aiProvider,
            string language,
            string userNote,
            int appliedChangeCount,
            IReadOnlyList<RecipeAiTransformStepPlanItem> sourceStepPlan,
            IReadOnlyCollection<string> allowedStepKeys,
            RecipeAiIngredientSuggestionItem selectedConcept,
            IReadOnlyList<RecipeAiIngredientSuggestionItem> selectedIngredients,
            IReadOnlyList<RecipeAiIngredientSuggestionItem> availableVariantIngredients,
            string apiKey,
            CancellationToken cancellationToken)
        {
            if (selectedConcept?.ConceptIngredientPlan == null || selectedConcept.ConceptIngredientPlan.Count == 0)
            {
                return null;
            }

            if (preview == null || preview.UsedFallback)
            {
                return null;
            }

            var planned = selectedConcept.ConceptIngredientPlan
                .Where(x => x != null && x.IngredientId > 0 && !string.Equals((x.Action ?? string.Empty).Trim(), "remove", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (planned.Count == 0)
            {
                return null;
            }

            var missingPlanned = FindMissingPlannedIngredients(preview, planned, availableVariantIngredients, language);
            if (missingPlanned.Count == 0)
            {
                return null;
            }

            // Single corrective pass: make planned ingredients visible in preparationText AND present in ingredients[].
            var applied = await EnsureConceptPlanIngredientsUsedAsync(
                preview,
                recipe,
                variantType,
                aiProvider,
                language,
                userNote,
                appliedChangeCount,
                sourceStepPlan,
                allowedStepKeys,
                selectedConcept,
                planned,
                missingPlanned,
                selectedIngredients,
                availableVariantIngredients,
                apiKey,
                cancellationToken);

            return applied;
        }

        private static void CleanupUndefinedGenericIngredientMentions(RecipeAiTransformPreview preview, string language)
        {
            if (preview == null)
            {
                return;
            }

            // Keep this narrowly-scoped (DE only) to avoid damaging other locales.
            if (!string.Equals((language ?? string.Empty).Trim(), "de", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var hasAnyHerb = (preview.Ingredients ?? new List<RecipeAiTransformIngredientPreview>())
                .Any(x =>
                    x != null
                    && x.IngredientId > 0
                    && !string.IsNullOrWhiteSpace(x.Name)
                    && (x.Name.Contains("Petersilie", StringComparison.OrdinalIgnoreCase)
                        || x.Name.Contains("Thymian", StringComparison.OrdinalIgnoreCase)
                        || x.Name.Contains("Rosmarin", StringComparison.OrdinalIgnoreCase)
                        || x.Name.Contains("Oregano", StringComparison.OrdinalIgnoreCase)
                        || x.Name.Contains("Basilikum", StringComparison.OrdinalIgnoreCase)
                        || x.Name.Contains("Dill", StringComparison.OrdinalIgnoreCase)
                        || x.Name.Contains("Koriander", StringComparison.OrdinalIgnoreCase)));

            if (hasAnyHerb)
            {
                return;
            }

            // Avoid "… und Kräutern abschmecken" when no herb ingredient exists.
            var prep = preview.PreparationText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(prep))
            {
                return;
            }

            prep = Regex.Replace(
                prep,
                @"(\bmit\b[^.\n]*?)\bund\s+(frischen\s+)?kräutern\b",
                "$1",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            prep = Regex.Replace(
                prep,
                @"\b(nach\s+belieben\s+)?mit\s+(frischen\s+)?kräutern\b",
                "",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            prep = MultiWhitespaceRegex.Replace(prep, " ");
            preview.PreparationText = prep.Trim();
        }

        private async Task<RecipeAiTransformPreview?> EnsureConceptPlanIngredientsUsedAsync(
            RecipeAiTransformPreview draft,
            RecipeBaseData recipe,
            string variantType,
            string aiProvider,
            string language,
            string userNote,
            int appliedChangeCount,
            IReadOnlyList<RecipeAiTransformStepPlanItem> sourceStepPlan,
            IReadOnlyCollection<string> allowedStepKeys,
            RecipeAiIngredientSuggestionItem selectedConcept,
            IReadOnlyList<RecipeAiConceptIngredientPlanItem> plannedItems,
            IReadOnlyList<RecipeAiConceptIngredientPlanItem> missingItems,
            IReadOnlyList<RecipeAiIngredientSuggestionItem> selectedIngredients,
            IReadOnlyList<RecipeAiIngredientSuggestionItem> availableVariantIngredients,
            string apiKey,
            CancellationToken cancellationToken)
        {
            try
            {
                var variantLabel = GetVariantLabel(variantType, language);
                var languageLabel = GetLanguageLabel(language);
                var compactTitle = TruncateForPrompt(recipe.Title, 180);

                var sourceIngredients = BuildIngredientPreview(recipe, language)
                    .Select(x => new { ingredientId = x.IngredientId, name = x.Name, quantity = x.Quantity, measure = x.Measure })
                    .ToList();

                var allowedVariantIngredientContext = DetermineAllowedVariantIngredientContext(
                    recipe,
                    variantType,
                    language,
                    selectedConcept,
                    selectedIngredients ?? Array.Empty<RecipeAiIngredientSuggestionItem>(),
                    availableVariantIngredients ?? Array.Empty<RecipeAiIngredientSuggestionItem>());

                // When applying a concept plan, keep the allowed universe tight: current draft + base + plan items.
                // This prevents the model from "solving" missing mentions by introducing extra pantry ingredients.
                allowedVariantIngredientContext = BuildAllowedIngredientContextForRepair(
                    recipe,
                    language,
                    draft,
                    requiredExtraIds: plannedItems.Select(x => x.IngredientId).Concat(plannedItems.Where(x => x.ReplacesIngredientId.HasValue).Select(x => x.ReplacesIngredientId!.Value)),
                    availableVariantIngredients ?? Array.Empty<RecipeAiIngredientSuggestionItem>());

                var planContext = plannedItems.Select(x => new
                {
                    ingredientId = x.IngredientId,
                    action = x.Action,
                    replacesIngredientId = x.ReplacesIngredientId,
                    quantity = x.Quantity,
                    measure = x.Measure,
                    reason = x.Reason,
                    isMainProtein = x.IsMainProtein,
                    name = x.IngredientName
                }).ToList();

                var missingContext = missingItems.Select(x => new
                {
                    ingredientId = x.IngredientId,
                    name = x.IngredientName
                }).ToList();

                var proteinGoalInstruction = BuildHighProteinGoalInstruction(recipe, variantType, language);
                var promptRequest = new AiPromptRequest
                {
                    SchemaName = "recipe_ai_transform_plan_apply_pass",
                    Schema = BuildSchema(),
                    MaxOutputTokens = 7000,
                    GeminiThinkingBudget = 0,
                    ModelTier = AiModelTier.Premium,
                    SystemPrompt =
                        BuildPromptPack(variantType, language, sideSupportInstruction: string.Empty, replacementInstruction: string.Empty, targetConceptCount: 1).AlignmentPassSystemPrompt +
                        "Zusatz-Aufgabe: Der User hat ein Konzept mit einem ingredientPlan ausgewaehlt. " +
                        "Alle Zutaten aus dem ingredientPlan muessen im finalen preparationText wirklich vorkommen (nicht nur in der Zutatenliste). " +
                        "Wenn eine geplante Zutat unmoeglich sinnvoll einzubauen ist, entferne sie konsistent aus ingredients[] UND aus title/summary/approach (aber das ist nur die Notfall-Option). " +
                        "Aendere ansonsten so wenig wie moeglich; halte den Rezeptstil und die Reihenfolge sauber. " +
                        "preparationText muss fuer Anfaenger ausfuehrbar sein (mit Zeiten/Hitze/Kriterium).",
                    UserPrompt =
                        $"Sprache: {languageLabel}\n" +
                        $"Variante: {variantLabel} ({variantType})\n" +
                        $"Runde: {appliedChangeCount}/{MaxChangeRounds}\n" +
                        $"Titel: {compactTitle}\n" +
                        $"Original-Zutaten: {JsonSerializer.Serialize(sourceIngredients)}\n" +
                        $"Erlaubte Zutaten (id->name): {(allowedVariantIngredientContext.Count == 0 ? "keine" : JsonSerializer.Serialize(allowedVariantIngredientContext))}\n" +
                        $"Gewaehltes Konzept: {JsonSerializer.Serialize(new { key = selectedConcept.ConceptKey, title = selectedConcept.ConceptTitle, summary = selectedConcept.ConceptSummary, approach = selectedConcept.ConceptApproach })}\n" +
                        $"ingredientPlan (verbindlich): {JsonSerializer.Serialize(planContext)}\n" +
                        $"Fehlend im Text (muss sichtbar eingebaut werden): {JsonSerializer.Serialize(missingContext)}\n" +
                        $"Entwurf (zu reparieren):\n{JsonSerializer.Serialize(new { draft.Title, draft.Summary, draft.Ingredients, draft.PreparationText, draft.Highlights })}\n" +
                        (string.IsNullOrWhiteSpace(proteinGoalInstruction) ? string.Empty : $"Protein-Ziel: {proteinGoalInstruction}\n") +
                        "Gib die korrigierte, kochbare Rezeptvariante zurueck (vollstaendig: Zutaten + preparationText)."
                };

                return await ExecuteStructuredAiCallAsync<RecipeAiTransformPreview>(promptRequest, aiProvider, apiKey, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Concept plan apply pass failed. Provider={Provider} RecipeId={RecipeId} VariantType={VariantType}", NormalizeAiProvider(aiProvider), recipe?.Id, variantType);
                return null;
            }
        }

        private static List<RecipeAiIngredientSuggestionItem> FindMissingPantryIngredients(
            RecipeAiTransformPreview draft,
            IReadOnlyList<RecipeAiIngredientSuggestionItem> availableVariantIngredients,
            string language)
        {
            var result = new List<RecipeAiIngredientSuggestionItem>();
            if (draft == null)
            {
                return result;
            }

            var prep = (draft.PreparationText ?? string.Empty);
            if (string.IsNullOrWhiteSpace(prep))
            {
                return result;
            }

            var existingIds = (draft.Ingredients ?? new List<RecipeAiTransformIngredientPreview>())
                .Where(x => x != null && x.IngredientId > 0)
                .Select(x => x.IngredientId)
                .ToHashSet();

            // Only consider pantry items that we resolved from the DB (AiReason="pantry").
            var pantry = (availableVariantIngredients ?? Array.Empty<RecipeAiIngredientSuggestionItem>())
                .Where(x => x != null && x.IngredientId > 0 && (x.AiReason ?? string.Empty).StartsWith("pantry", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var item in pantry)
            {
                if (existingIds.Contains(item.IngredientId))
                {
                    continue;
                }

                var name = (item.Name ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (ContainsIngredientReference(prep, name))
                {
                    result.Add(item);
                }
            }

            // Extra heuristic: if we fry/saute/roast something but have no fat/oil in ingredients,
            // ensure at least one pantry fat is added (oil or butter).
            if (ImpliesFatUsage(prep, language))
            {
                var hasFatAlready = (draft.Ingredients ?? new List<RecipeAiTransformIngredientPreview>())
                    .Any(x =>
                        (x?.Name ?? string.Empty).Contains("öl", StringComparison.OrdinalIgnoreCase)
                        || (x?.Name ?? string.Empty).Contains("oel", StringComparison.OrdinalIgnoreCase)
                        || (x?.Name ?? string.Empty).Contains("butter", StringComparison.OrdinalIgnoreCase)
                        || (x?.Name ?? string.Empty).Contains("schmalz", StringComparison.OrdinalIgnoreCase));

                if (!hasFatAlready)
                {
                    var preferred = pantry.FirstOrDefault(x => (x.Name ?? string.Empty).Contains("Öl", StringComparison.OrdinalIgnoreCase) || (x.Name ?? string.Empty).Contains("Oel", StringComparison.OrdinalIgnoreCase))
                                   ?? pantry.FirstOrDefault(x => (x.Name ?? string.Empty).Contains("Butter", StringComparison.OrdinalIgnoreCase));
                    if (preferred != null && result.All(x => x.IngredientId != preferred.IngredientId))
                    {
                        result.Add(preferred);
                    }
                }
            }

            return result;
        }

        private static void PatchMissingIngredientsFromIngredientsText(
            RecipeAiTransformPreview draft,
            RecipeBaseData recipe,
            string language,
            IReadOnlyList<RecipeAiIngredientSuggestionItem> availableVariantIngredients)
        {
            if (draft == null)
            {
                return;
            }

            var ingredientsText = (draft.IngredientsText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(ingredientsText))
            {
                return;
            }

            draft.Ingredients ??= new List<RecipeAiTransformIngredientPreview>();

            var existingIds = draft.Ingredients
                .Where(x => x != null && x.IngredientId > 0)
                .Select(x => x.IngredientId)
                .ToHashSet();

            // Build a stable id->name map (prefer candidate list naming, fall back to base recipe names).
            var candidateNameById = (availableVariantIngredients ?? Array.Empty<RecipeAiIngredientSuggestionItem>())
                .Where(x => x != null && x.IngredientId > 0 && !string.IsNullOrWhiteSpace(x.Name))
                .GroupBy(x => x.IngredientId)
                .ToDictionary(g => g.Key, g => (g.First().Name ?? string.Empty).Trim(), EqualityComparer<int>.Default);

            var baseNameById = recipe?.Ingredients?
                .Where(x => x?.Ingredient?.IngredientsAndNutrients != null)
                .Select(x => x!.Ingredient!.IngredientsAndNutrients!)
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => GetIngredientName(g.First(), language), EqualityComparer<int>.Default)
                ?? new Dictionary<int, string>();

            var idNamePairs = candidateNameById
                .Select(kv => (id: kv.Key, name: kv.Value))
                .Concat(baseNameById.Select(kv => (id: kv.Key, name: kv.Value)))
                .Where(x => x.id > 0 && !string.IsNullOrWhiteSpace(x.name))
                .GroupBy(x => x.id)
                .Select(g => (id: g.Key, name: g.First().name))
                .OrderByDescending(x => x.name.Length)
                .ToList();

            // Parse each line and add missing entries only if the ingredient is also referenced in the preparation.
            // Rationale: The model may list "nice-to-have" pantry items in ingredientsText that are never used in steps.
            // We only auto-add when the cooking instructions actually need it.
            var preparationHaystack = string.Join("\n",
                new[] { draft.PreparationText ?? string.Empty }
                .Concat((draft.Steps ?? new List<RecipeAiTransformStepPreview>()).Select(s => s?.Text ?? string.Empty)))
                .Trim();

            foreach (var rawLine in ingredientsText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var line = rawLine.Trim().TrimStart('-', '•').Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                // Find the best matching ingredient name for this line.
                var match = idNamePairs.FirstOrDefault(p => ContainsIngredientReference(line, p.name));
                if (match.id <= 0 || string.IsNullOrWhiteSpace(match.name))
                {
                    continue;
                }

                if (existingIds.Contains(match.id))
                {
                    continue;
                }

                // Only auto-add if it is actually needed by the instructions.
                if (string.IsNullOrWhiteSpace(preparationHaystack) || !ContainsIngredientReference(preparationHaystack, match.name))
                {
                    continue;
                }

                if (!TryParseLeadingQuantityAndMeasure(line, out var qty, out var measure))
                {
                    continue;
                }

                draft.Ingredients.Add(new RecipeAiTransformIngredientPreview
                {
                    IngredientId = match.id,
                    Name = match.name,
                    Quantity = qty,
                    Measure = measure,
                    // Do not show "auto-added" labels in the UI; users found it noisy/confusing.
                    ChangeHint = null,
                    IsModified = true
                });

                existingIds.Add(match.id);
            }

            // Keep ingredientsText canonical, so UI and saving use the same truth.
            draft.IngredientsText = BuildIngredientsText(draft.Ingredients);
        }

        private static bool TryParseLeadingQuantityAndMeasure(string line, out string quantity, out string measure)
        {
            quantity = string.Empty;
            measure = string.Empty;

            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            // Examples:
            // "600 g Grüne Linsen"
            // "1 Prise Salz"
            // "0,5 TL Limettensaft"
            // "4 EL Sojasauce"
            var m = Regex.Match(line.Trim(), @"^(?<qty>\d+(?:[.,]\d+)?)(?:\s*)(?<unit>[A-Za-zÄÖÜäöü\.\-]+)\b");
            if (!m.Success)
            {
                return false;
            }

            quantity = (m.Groups["qty"].Value ?? string.Empty).Trim().Replace(',', '.');
            measure = (m.Groups["unit"].Value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(quantity) || string.IsNullOrWhiteSpace(measure))
            {
                return false;
            }

            return true;
        }

        private static bool ContainsIngredientReference(string haystack, string needle)
        {
            if (string.IsNullOrWhiteSpace(haystack) || string.IsNullOrWhiteSpace(needle))
            {
                return false;
            }

            // Fast path
            if (haystack.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Normalize umlauts + punctuation so we match "Öl" vs "Oel" and similar DB naming differences.
            var normalizedHaystack = NormalizeForContains(haystack);
            var normalizedNeedle = NormalizeForContains(needle);
            if (!string.IsNullOrWhiteSpace(normalizedNeedle) && normalizedHaystack.Contains(normalizedNeedle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Token fallback: allow matching on meaningful sub-words, but avoid overly generic category words
            // ("samen" would otherwise match Hanfsamen -> Chia-Samen and auto-add wrong ingredients).
            var tokens = Regex.Split(normalizedNeedle, "[^a-z0-9]+")
                .Select(x => x.Trim())
                .Where(x => x.Length >= 3)
                .Where(x => !GenericIngredientTokens.Contains(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(x => x.Length)
                .ToList();

            if (tokens.Count == 0)
            {
                return false;
            }

            foreach (var token in tokens)
            {
                // Require a word-ish match (we normalized punctuation to spaces).
                if (Regex.IsMatch(normalizedHaystack, $"\\b{Regex.Escape(token)}\\b", RegexOptions.IgnoreCase))
                {
                    return true;
                }

                // German declension tolerance: "rote" should match "roten", "rotem", "roter", "rotes".
                // Allow prefix match where the token starts a word and the remaining suffix is short (≤3 chars).
                if (token.Length >= 3 && Regex.IsMatch(normalizedHaystack, $"\\b{Regex.Escape(token)}[a-z]{{1,3}}\\b", RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static readonly HashSet<string> GenericIngredientTokens = new(StringComparer.OrdinalIgnoreCase)
        {
            // German (normalized by NormalizeForContains)
            "samen", "kerne", "korn", "bohnen", "linsen", "kaese", "joghurt", "milch", "sahne",
            "oel", "fett", "pulver", "mehl", "mus", "saft", "bruehe", "fond", "sauce",
            "frisch", "getrocknet", "gemahlen", "ganz", "dose", "dosen", "konserve", "naturell",
            "filet", "brust", "keule", "schenkel",

            // English helpers (some DB entries may be EN-like)
            "seeds", "nuts", "beans", "lentils", "cheese", "yogurt", "oil", "fat", "powder", "flour", "sauce", "broth", "stock",
            "fresh", "dried", "ground", "whole", "canned"
        };

        private static string NormalizeForContains(string input)
        {
            var value = (input ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            // German umlauts
            value = value
                .Replace("ä", "ae", StringComparison.OrdinalIgnoreCase)
                .Replace("ö", "oe", StringComparison.OrdinalIgnoreCase)
                .Replace("ü", "ue", StringComparison.OrdinalIgnoreCase)
                .Replace("ß", "ss", StringComparison.OrdinalIgnoreCase);

            // Strip common punctuation
            value = Regex.Replace(value, "[\\u2019\\u2018\\u201c\\u201d\"'`´]", string.Empty);
            value = value.Replace("(", " ").Replace(")", " ").Replace(",", " ").Replace(";", " ").Replace(":", " ").Replace(".", " ").Replace("/", " ");
            value = Regex.Replace(value, "\\s+", " ").Trim();
            return value;
        }

        private static bool ImpliesFatUsage(string preparationText, string language)
        {
            var text = (preparationText ?? string.Empty).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            // Works across languages reasonably well; we don't need perfect NLP here.
            string[] triggers =
            {
                "anbrat", "anschwitz", "schwitz", "brat ", "brate", "scharf an", "röst", "roest", "schwenk", "pfanne", "bräter", "braeter", "sear", "saute", "fry", "roast"
            };

            return triggers.Any(t => text.Contains(t, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<RecipeAiIngredientSuggestionResponse> BuildIngredientSuggestionsAsync(
            RecipeBaseData recipe,
            string variantType,
            string aiProvider,
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
            var normalizedAiProvider = NormalizeAiProvider(aiProvider);
            var trimmedUserNote = (userNote ?? string.Empty).Trim();
            var apiKey = ResolveApiKey(normalizedAiProvider);

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                if (_environment.IsDevelopment() && AllowLocalAiFallback)
                {
                    _logger.LogWarning("SecretKeyOpenAi fehlt. Verwende lokalen Zutaten-Fallback für RecipeId={RecipeId} VariantType={VariantType}.", recipe.Id, normalizedVariantType);
                    return await BuildIngredientSuggestionFallbackAsync(recipe, normalizedVariantType, normalizedLanguage, trimmedUserNote, cancellationToken);
                }

                throw new InvalidOperationException(GetMissingApiKeyMessage(normalizedAiProvider));
            }

            var ingredientSelectionContext = AnalyzeIngredientSelectionContext(recipe);
            var defaultCategoryKeys = GetDefaultCategoryKeysForVariant(normalizedVariantType, ingredientSelectionContext);

            var existingIngredientIds = recipe.Ingredients?
                .Select(x => x.Ingredient?.IngredientsAndNutrients?.Id ?? 0)
                .Where(x => x > 0)
                .Distinct()
                .ToList() ?? new List<int>();

            var recipeContext = BuildCompactCategorySelectionContext(recipe, normalizedLanguage, ingredientSelectionContext);
            var categoryPlan = await SelectIngredientCategoriesAsync(
                recipe,
                normalizedVariantType,
                normalizedAiProvider,
                normalizedLanguage,
                trimmedUserNote,
                defaultCategoryKeys,
                recipeContext,
                apiKey,
                cancellationToken);

            var effectiveCategoryKeys = categoryPlan.PreferredCategoryKeys.Count > 0
                ? categoryPlan.PreferredCategoryKeys
                : defaultCategoryKeys;

            // Build an "allowed-id universe" for concept planning + server-side validation.
            // The AI will fetch concrete ingredients via tool calling (get_ingredients), so we don't have to ship
            // a giant candidate list in the prompt anymore.
            var conceptUniverseCategoryKeys = effectiveCategoryKeys
                .Concat(new[]
                {
                    "oils","fats","spices","herbs","sauces","broths","sweeteners","baking_ingredients"
                })
                .Select(NormalizeCategoryKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var candidates = await _ingredientResolverService.GetCandidatesForCategoriesAsync(
                language: normalizedLanguage,
                allowedCategoryKeys: conceptUniverseCategoryKeys,
                excludeIngredientIds: existingIngredientIds,
                take: 400,
                cancellationToken: cancellationToken);

            if (candidates.Count == 0)
            {
                return await BuildIngredientSuggestionFallbackAsync(recipe, normalizedVariantType, normalizedLanguage, trimmedUserNote, cancellationToken);
            }

             // For ingredient selection we want the AI to see the full DB candidate space for the chosen categories
             // (the UI/AI can still rank; we avoid pre-trimming to the same tiny set over and over).
             candidates = FilterCandidatesForRecipeContext(candidates, normalizedVariantType, ingredientSelectionContext);
             // Diversify ordering to avoid "same first page" effects without trimming the candidate space.
             candidates = DiversifyCandidatesForPrompt(candidates, normalizedVariantType, candidates.Count);
             if (candidates.Count == 0)
             {
                 return await BuildIngredientSuggestionFallbackAsync(recipe, normalizedVariantType, normalizedLanguage, trimmedUserNote, cancellationToken);
             }

            var normalizedStrategy = NormalizeIngredientStrategy(categoryPlan.Strategy, normalizedVariantType, ingredientSelectionContext);
            var concepts = await SelectRecipeConceptsAsync(
                recipe,
                normalizedVariantType,
                normalizedAiProvider,
                normalizedLanguage,
                trimmedUserNote,
                normalizedStrategy,
                ingredientSelectionContext,
                effectiveCategoryKeys,
                candidates,
                apiKey,
                cancellationToken);

            var conceptCount = GetTargetConceptCount(normalizedVariantType, ingredientSelectionContext);
            var responseReasoning = string.IsNullOrWhiteSpace(categoryPlan.Reasoning)
                ? LocalizedWord(
                    normalizedLanguage,
                    $"{conceptCount} stimmige Rezeptideen wurden vorbereitet.",
                    $"{conceptCount} fitting recipe ideas were prepared.",
                    $"Se prepararon {conceptCount} ideas de receta coherentes.",
                    $"Foram preparadas {conceptCount} ideias de receita coerentes.")
                : categoryPlan.Reasoning;

            return new RecipeAiIngredientSuggestionResponse
            {
                VariantType = normalizedVariantType,
                VariantLabel = GetVariantLabel(normalizedVariantType, normalizedLanguage),
                Strategy = normalizedStrategy,
                SelectionMode = "concepts",
                Reasoning = responseReasoning,
                UsedFallback = false,
                AutoGenerateWithoutSelection = false,
                PreferredCategoryKeys = effectiveCategoryKeys,
                PreferredCategoryLabels = effectiveCategoryKeys
                    .Select(x => GetDisplayCategoryLabel(x, x, normalizedLanguage))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                Suggestions = concepts.Select(x => new RecipeAiIngredientSuggestionItem
                {
                    Name = x.Title,
                    AiReason = x.Summary,
                    ConceptKey = x.Key,
                    ConceptTitle = x.Title,
                    ConceptSummary = x.Summary,
                    ConceptApproach = x.Approach,
                    ConceptIngredientPlan = x.IngredientPlan ?? new List<RecipeAiConceptIngredientPlanItem>()
                }).ToList()
            };
        }


        private AiPromptRequest BuildPreviewPromptRequest(
            RecipeBaseData recipe,
            string variantType,
            string language,
            string userNote,
            int appliedChangeCount,
            IReadOnlyList<RecipeAiTransformStepPlanItem> sourceStepPlan,
            RecipeAiIngredientSuggestionItem? selectedConcept,
            IReadOnlyList<RecipeAiIngredientSuggestionItem> selectedIngredients,
            IReadOnlyList<RecipeAiIngredientSuggestionItem> availableVariantIngredients)
        {
            var sourceIngredients = BuildIngredientPreview(recipe, language)
                .Select(x => new
                {
                    ingredientId = x.IngredientId,
                    name = x.Name,
                    quantity = x.Quantity,
                    measure = x.Measure
                })
                .ToList();
            var variantLabel = GetVariantLabel(variantType, language);
            var languageLabel = GetLanguageLabel(language);
            var compactTitle = TruncateForPrompt(recipe.Title, 180);
            var selectedIngredientContext = selectedIngredients
                .Select(selectedIngredient => new
                {
                    id = selectedIngredient.IngredientId,
                    name = selectedIngredient.Name,
                    cat = selectedIngredient.CategoryKey,
                    reason = selectedIngredient.AiReason
                })
                .ToList();
            // Keep prompt small: the preview model gets full base-ingredient JSON already.
            // For concept-driven variants (ingredientPlan), we only need a compact cooking context.
            var hasConceptPlan = (selectedConcept?.ConceptIngredientPlan?.Count ?? 0) > 0;
            var currentPreparationText = hasConceptPlan
                ? BuildCompactPreparationContext(recipe, language)
                : TruncateForPrompt(BuildPreparationPromptInput(recipe, sourceStepPlan, language), 650);
            var cookingNotes = BuildSelectedIngredientCookingNotes(selectedIngredients, language);
            // Keep the prompt small: only send IDs the model is allowed to reference.
            var allowedVariantIngredientContext = DetermineAllowedVariantIngredientContext(
                recipe,
                variantType,
                language,
                selectedConcept,
                selectedIngredients,
                availableVariantIngredients);
            var selectedConceptContext = selectedConcept == null
                ? null
                : new
                {
                    key = selectedConcept.ConceptKey,
                    title = selectedConcept.ConceptTitle,
                    summary = selectedConcept.ConceptSummary,
                    approach = selectedConcept.ConceptApproach,
                    ingredientPlan = selectedConcept.ConceptIngredientPlan
                        .Select(x => new
                        {
                            ingredientId = x.IngredientId,
                            action = x.Action,
                            replacesIngredientId = x.ReplacesIngredientId,
                            quantity = x.Quantity,
                            measure = x.Measure,
                            reason = x.Reason,
                            isMainProtein = x.IsMainProtein
                        })
                        .ToList()
                };
            var proteinGoalInstruction = BuildHighProteinGoalInstruction(recipe, variantType, language);
            var promptPack = BuildPromptPack(variantType, language, sideSupportInstruction: string.Empty, replacementInstruction: string.Empty, targetConceptCount: 1);

            return new AiPromptRequest
            {
                SchemaName = "recipe_ai_transform_preview",
                Schema = BuildSchema(),
                MaxOutputTokens = 7000,
                GeminiThinkingBudget = 0,
                ModelTier = AiModelTier.Premium,
                  SystemPrompt = promptPack.PreviewSystemPrompt,
                UserPrompt =
                    $"Sprache: {languageLabel}\n" +
                    $"Variante: {variantLabel} ({variantType})\n" +
                    $"Runde: {appliedChangeCount}/{MaxChangeRounds}\n" +
                    $"Titel: {compactTitle}\n" +
                    $"Portionen: {recipe.PersonCount}, Zeit: {recipe.PreparationTime}min\n" +
                    $"Original-Zutaten: {JsonSerializer.Serialize(sourceIngredients)}\n" +
                    $"Erlaubte Zutaten (id->name): {(allowedVariantIngredientContext.Count == 0 ? "keine" : JsonSerializer.Serialize(allowedVariantIngredientContext))}\n" +
                    (selectedConceptContext == null ? string.Empty : $"Gewaehltes Konzept: {JsonSerializer.Serialize(selectedConceptContext)}\n") +
                    (selectedConceptContext == null || (selectedConcept?.ConceptIngredientPlan?.Count ?? 0) == 0
                        ? string.Empty
                        : "Wichtig: Wenn ein ingredientPlan im Konzept enthalten ist, ist dieser Plan verbindlich: " +
                          "Du darfst KEINE weiteren Zutaten hinzufuegen oder erfinden. " +
                          "Alle Zutaten im finalen Rezept muessen entweder Original-Zutaten sein oder im ingredientPlan stehen oder aus den vom User gewaehlten DB-Zutaten stammen.\n") +
                    $"Gewaehlte DB-Zutaten: {(selectedIngredientContext.Count == 0 ? "keine" : JsonSerializer.Serialize(selectedIngredientContext))}\n" +
                    $"Pflicht: Wenn du eine dieser gewaehlten DB-Zutaten in der Zubereitung verwendest, muss der Text auch erklaeren, wie sie vorbereitet/gekocht wird (kein stilles 'gekocht' ohne Kochschritt).\n" +
                    (cookingNotes.Count == 0 ? string.Empty : $"Kochhinweise zu den gewaehlten Zutaten: {JsonSerializer.Serialize(cookingNotes)}\n") +
                    $"Aktuelle Zubereitung:\n{currentPreparationText}\n" +
                    (string.IsNullOrWhiteSpace(userNote) ? string.Empty : $"User-Hinweis: {userNote}\n") +
                    (string.IsNullOrWhiteSpace(proteinGoalInstruction) ? string.Empty : $"Protein-Ziel: {proteinGoalInstruction}\n") +
                    "Gib eine stimmige Rezeptvariante zurueck. " +
                    "ingredientsText = finale Zutatenliste als lesbarer Freitext. " +
                    "preparationText = komplette Zubereitung als klarer Freitext. " +
                    "Halte die Formulierungen kompakt und vermeide unnoetige Wiederholungen, damit die komplette JSON-Antwort sicher hineinpasst. " +
                    "Wenn du stepPlan nicht brauchst, liefere ein leeres Array."
            };
        }

        private async Task<RecipeAiTransformPreview?> QualityCheckAndRepairPreviewAsync(
            RecipeAiTransformPreview draft,
            RecipeBaseData recipe,
            string variantType,
            string aiProvider,
            string language,
            string userNote,
            int appliedChangeCount,
            IReadOnlyList<RecipeAiTransformStepPlanItem> sourceStepPlan,
            IReadOnlyCollection<string> allowedStepKeys,
            IReadOnlyList<RecipeAiIngredientSuggestionItem> selectedIngredients,
            IReadOnlyList<RecipeAiIngredientSuggestionItem> availableVariantIngredients,
            string apiKey,
            CancellationToken cancellationToken)
        {
            try
            {
                var variantLabel = GetVariantLabel(variantType, language);
                var languageLabel = GetLanguageLabel(language);
                var compactTitle = TruncateForPrompt(recipe.Title, 180);
                var sourceIngredients = BuildIngredientPreview(recipe, language)
                    .Select(x => new
                    {
                        ingredientId = x.IngredientId,
                        name = x.Name,
                        quantity = x.Quantity,
                        measure = x.Measure
                    })
                    .ToList();

                var selectedIngredientContext = (selectedIngredients ?? Array.Empty<RecipeAiIngredientSuggestionItem>())
                    .Select(x => new { id = x.IngredientId, name = x.Name, cat = x.CategoryKey, reason = x.AiReason })
                    .ToList();

                var allowedVariantIngredientContext = DetermineAllowedVariantIngredientContext(
                    recipe,
                    variantType,
                    language,
                    selectedConcept: null,
                    selectedIngredients ?? Array.Empty<RecipeAiIngredientSuggestionItem>(),
                    availableVariantIngredients ?? Array.Empty<RecipeAiIngredientSuggestionItem>());

                // QA/quality pass should not introduce new ingredients. Restrict to what's already in the draft + base recipe.
                allowedVariantIngredientContext = BuildAllowedIngredientContextForRepair(
                    recipe,
                    language,
                    draft,
                    requiredExtraIds: Array.Empty<int>(),
                    availableVariantIngredients ?? Array.Empty<RecipeAiIngredientSuggestionItem>());

                var proteinGoalInstruction = BuildHighProteinGoalInstruction(recipe, variantType, language);

                var promptRequest = new AiPromptRequest
                {
                    SchemaName = "recipe_ai_transform_quality_pass",
                    Schema = BuildSchema(),
                    MaxOutputTokens = 7000,
                    GeminiThinkingBudget = 0,
                    ModelTier = AiModelTier.Premium,
                    SystemPrompt = BuildPromptPack(variantType, language, sideSupportInstruction: string.Empty, replacementInstruction: string.Empty, targetConceptCount: 1).QualityPassSystemPrompt +
                                  "Nutze fuer neu hinzugefuegte oder ersetzte Zutaten nur ingredientId-Werte aus der Liste Verfuegbare Varianten-Zutaten. " +
                                  "Gib fuer jede finale Zutat ingredientId, quantity und measure zurueck (quantity nur Zahl/Angabe, measure nur Einheit). " +
                                  "Formatiere preparationText gut lesbar mit nummerierten Schritten und Absätzen. " +
                                  "Schreibe jeden Schritt als vollstaendigen Satz mit Verb (keine Satzfragmente wie 'Abschmecken mit ...'). " +
                                  "Vermeide starre Mini-Mengen in freien Tipps (z.B. 1 TL Zucker); nutze stattdessen nach Geschmack. " +
                                  proteinGoalInstruction +
                                  "Wenn Linsen oder aehnliche Huelsenfruechte neu dazukommen, entscheide dich fuer genau eine klare Kochlogik: entweder direkt im Braeter mit zusaetzlicher Fluessigkeit und etwas Salz oder separat in einem eigenen Topf. Mische diese beiden Wege nicht im selben Rezepttext. " +
                                  "Jede neu gebaute Komponente (Pueree/Beilage/Sauce) muss kurz abgeschmeckt werden (mind. Salz+Pfeffer, plus 1-2 passende Gewuerze/Kraeuter, plus optional Balance nach Geschmack). " +
                                  "Wenn dafuer keine passenden Zutaten vorhanden sind, formuliere allgemeiner statt neue Zutaten zu erfinden.",
                    UserPrompt =
                        $"Sprache: {languageLabel}\n" +
                        $"Variante: {variantLabel} ({variantType})\n" +
                        $"Runde: {appliedChangeCount}/{MaxChangeRounds}\n" +
                        $"Titel: {compactTitle}\n" +
                        $"Portionen: {recipe.PersonCount}, Zeit: {recipe.PreparationTime}min\n" +
                        $"Original-Zutaten: {JsonSerializer.Serialize(sourceIngredients)}\n" +
                        $"Gewaehlte DB-Zutaten: {(selectedIngredientContext.Count == 0 ? "keine" : JsonSerializer.Serialize(selectedIngredientContext))}\n" +
                        $"Erlaubte Zutaten (id->name): {(allowedVariantIngredientContext.Count == 0 ? "keine" : JsonSerializer.Serialize(allowedVariantIngredientContext))}\n" +
                        (string.IsNullOrWhiteSpace(userNote) ? string.Empty : $"User-Hinweis: {userNote}\n") +
                        // Only ship the fields we actually want repaired; shipping full nested step arrays wastes tokens.
                        $"Entwurf (zu pruefen und zu reparieren):\n{JsonSerializer.Serialize(new { draft.Title, draft.Summary, draft.Ingredients, draft.PreparationText, draft.Highlights })}\n" +
                        "Gib die reparierte, kochbare Rezeptvariante zurueck (vollstaendig: Zutaten + preparationText)."
                };

                return await ExecuteStructuredAiCallAsync<RecipeAiTransformPreview>(promptRequest, aiProvider, apiKey, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Quality pass failed. Provider={Provider} RecipeId={RecipeId} VariantType={VariantType}", aiProvider, recipe.Id, variantType);
                return null;
            }
        }

        private AiPromptRequest BuildConceptPickPromptRequest(
            RecipeBaseData recipe,
            string variantType,
            string language,
            string userNote,
            string strategy,
            IngredientSelectionContext context,
            IReadOnlyList<string> preferredCategoryKeys)
        {
            var targetConceptCount = GetTargetConceptCount(variantType, context);
            var preparationContext = BuildCompactPreparationContext(recipe, language);
            var ingredientLines = BuildPromptIngredientLines(recipe, language);
            var strategyHint = GetIngredientSelectionStrategyHint(variantType, strategy, context, language);
            var sideSupportInstruction = GetDietVariantSideSupportInstruction(variantType, context);
            var replacementInstruction = GetSwapConceptInstruction(variantType, strategy, context, language);
            var proteinGoalInstruction = BuildHighProteinGoalInstruction(recipe, variantType, language);
            var diversityNonce = Guid.NewGuid().ToString("N");
            var promptPack = BuildPromptPack(variantType, language, sideSupportInstruction, replacementInstruction, targetConceptCount);

            var normalizedPreferredKeys = (preferredCategoryKeys ?? Array.Empty<string>())
                .Select(NormalizeCategoryKey)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(6)
                .ToList();

            return new AiPromptRequest
            {
                SchemaName = "recipe_ai_concept_pick",
                ModelTier = AiModelTier.Cheap,
                AllowTools = true,
                MaxToolRounds = 4,
                AllowedToolCategoryKeys = normalizedPreferredKeys
                    .Concat(new[] { "oils", "fats", "spices", "herbs", "sauces", "broths" })
                    .Select(NormalizeCategoryKey)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                Schema = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "concepts" },
                    properties = new
                    {
                        concepts = new
                        {
                            type = "array",
                            minItems = targetConceptCount,
                            maxItems = targetConceptCount,
                            items = new
                            {
                                type = "object",
                                additionalProperties = false,
                                required = new[] { "key", "title", "summary", "approach", "ingredients" },
                                properties = new
                                {
                                    key = new { type = "string" },
                                    title = new { type = "string" },
                                    summary = new { type = "string" },
                                    approach = new { type = "string" },
                                    ingredients = new
                                    {
                                        type = "array",
                                        minItems = 2,
                                        maxItems = 12,
                                        items = new
                                        {
                                            type = "object",
                                            additionalProperties = false,
                                            // OpenAI Structured Outputs requires required[] to include every key in properties.
                                            // Optional values should be represented via nullable types, not omitted from required.
                                            required = new[] { "ingredientId", "action", "replacesIngredientId", "quantity", "measure", "reason", "isMainProtein" },
                                            properties = new
                                            {
                                                ingredientId = new { type = "integer" },
                                                action = new { type = "string", @enum = new[] { "add", "replace", "remove" } },
                                                replacesIngredientId = new { type = new[] { "integer", "null" } },
                                                quantity = new { type = "string" }, // numeric only
                                                measure = new { type = "string" },  // unit only
                                                reason = new { type = "string" },
                                                isMainProtein = new { type = "boolean" }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                MaxOutputTokens = 4000,
                GeminiThinkingBudget = 0,
                SystemPrompt = promptPack.ConceptPickSystemPrompt,
                UserPrompt =
                    $"Variante: {GetVariantLabel(variantType, language)} ({variantType})\n" +
                    $"Strategie: {strategy}\n" +
                    $"Rezepttitel: {recipe.Title}\n" +
                    $"Aktuelle Zutaten:\n{ingredientLines}\n" +
                    $"Rezeptkontext:\n{preparationContext}\n" +
                    $"Strategie-Hinweis: {strategyHint}\n" +
                    $"User-Hinweis: {(string.IsNullOrWhiteSpace(userNote) ? "Kein Hinweis" : userNote)}\n" +
                    $"Bevorzugte Kategorien (rufe get_ingredients damit auf): {JsonSerializer.Serialize(normalizedPreferredKeys)}\n" +
                    "Vorgehen: Rufe zuerst get_ingredients mit den bevorzugten Kategorien auf (take ca. 200-300). " +
                    "Rufe get_ingredients fuer oils,fats,spices,herbs,sauces,broths nur dann zusaetzlich auf, wenn ein Konzept ohne diese Stuetzzutaten technisch nicht schluessig waere. " +
                    "Nutze in ingredients[] nur ingredientId, die dir ueber get_ingredients zurueckgegeben wurden (oder bestehende Original-Zutaten-IDs fuer remove/replace).\n" +
                    (string.IsNullOrWhiteSpace(proteinGoalInstruction) ? string.Empty : $"Protein-Ziel: {proteinGoalInstruction}\n") +
                    $"Diversity-Nonce: {diversityNonce}\n" +
                    $"Gib genau {targetConceptCount} verschiedene Rezeptideen zur Auswahl zurueck. " +
                    "Die summary soll fuer den User lesbar sein. " +
                    "Die approach-Zeile soll knapp erklaeren, wie das Originalrezept in diese Richtung umgebaut wird. " +
                    "Achte streng darauf, dass ingredientId, Name im Text und reason inhaltlich zusammenpassen. " +
                    GetDietVariantSideSelectionUserInstruction(variantType, context)
            };
        }

        private sealed record PromptPack(
            string ConceptPickSystemPrompt,
            string PreviewSystemPrompt,
            string QualityPassSystemPrompt,
            string AlignmentPassSystemPrompt);

        private static PromptPack BuildPromptPack(
            string variantType,
            string language,
            string sideSupportInstruction,
            string replacementInstruction,
            int targetConceptCount)
        {
            var normalizedVariantType = NormalizeVariantType(variantType);
            var normalizedLanguage = NormalizeLanguage(language);

            var commonConceptPick =
                $"Entwerfe genau {Math.Max(1, targetConceptCount)} unterschiedliche, kochbare Rezeptkonzepte fuer dieses konkrete Rezept und die gewaehlte Variante. " +
                "Antworte ausschliesslich als valides JSON. " +
                "Die Konzepte muessen sich klar unterscheiden (Technik/Baustein/Textur/Beilage-Idee), aber realistisch bleiben. " +
                "Nutze Zutaten nur aus get_ingredients. Erfinde keine Zutaten/Cuts. " +
                "Wenn du im title/summary/approach ein Lebensmittel nennst, muss es als ingredientId im ingredients[]-Plan stehen (oder Original-Zutat sein). " +
                "Zu JEDEM Konzept: ingredients[]-Plan mit ingredientId, action=add|replace|remove, replacesIngredientId (nur bei replace), quantity (nur Zahl), measure (nur Einheit), reason (kurz). " +
                "Ein Konzept muss als Gerichtsidee komplett wirken, aber fuege Stuetzzutaten nur hinzu, wenn sie technisch wirklich noetig sind. " +
                "Nenne in reason immer die echte Zutat zur ingredientId; beschreibe niemals ein anderes Lebensmittel als die ausgewaehlte ingredientId. " +
                "Wenn du unsicher bist, schreibe einen generischen Grund wie 'passt geschmacklich und technisch zur Variante'. ";

            var variantConceptPick = normalizedVariantType switch
            {
                "highprotein" =>
                    "High-Protein Fokus: Denke zuerst in echten, kulinarisch passenden Protein-Erweiterungen, die das Originalgericht klar erkennbar lassen. Hauptzutat, Stil und Charakter des Gerichts sollen erhalten bleiben, solange der Nutzer keinen ausdruecklichen Austausch will. Bevorzuge Kandidaten mit hohem Eiweiss pro 100 g (Faustregel: ab ca. 15 g/100 g), aber schliesse nicht starr nach Kalorienverteilung aus. " +
                    sideSupportInstruction +
                    replacementInstruction,
                "lowcarb" =>
                    "Low-Carb Fokus: Konzepte muessen klar zeigen, was reduziert oder ersetzt wird, ohne das Gericht auszuduennen. Das Originalgericht soll klar erkennbar bleiben und weiterhin vollstaendig, saettigend und kulinarisch stimmig wirken. Bevorzuge zuerst nahe, alltagstaugliche Low-Carb-Umbauten wie passende Gemuese-, Pilz- oder Salatblatt-Ersatzideen, reduzierte Staerkeanteile oder geschickte Struktur-Aenderungen statt bloss irgendeine Beilage wegzulassen. Vermeide Konzepte, die nur auf Weglassen beruhen oder wie ein anderes Gericht wirken. " +
                    sideSupportInstruction +
                    replacementInstruction,
                "vegan" =>
                    "Vegan Fokus: Konzepte muessen klar zeigen, welche tierischen Bestandteile ersetzt werden und wie Umami, Saftigkeit, Bindung, Struktur und Saettigung erhalten bleiben. Das Originalgericht soll klar erkennbar bleiben. Bevorzuge zuerst stilnahe, kulinarisch plausible Ersatzideen, die Funktion und Geschmack des Originals ernst nehmen, statt generische Standardloesungen ueber jedes Gericht zu legen. Vermeide beliebige Tofu-, Cashew- oder Pilzkonzepte, wenn sie nicht wirklich zum Gerichtsstil und zur Funktion der ersetzten Zutat passen. " +
                    sideSupportInstruction,
                "mealprep" =>
                    "Meal-Prep Fokus: Konzepte muessen das bestehende Gericht in eine klar alltagstaugliche Meal-Prep-Version uebersetzen. Bevorzuge Varianten, die sich gut vorkochen, sauber portionieren, 2-4 Tage lagern und ohne Qualitaetsverlust aufwaermen lassen. Denke zuerst an Workflow, Haltbarkeit, Textur-Stabilitaet und sinnvolle Trennung von Sauce/Beilage/Crunch, statt bloss beliebige Zusatz-Zutaten vorzuschlagen. " +
                    replacementInstruction,
                _ => string.Empty
            };

            var conceptPickSystemPrompt =
                commonConceptPick +
                variantConceptPick +
                "Jedes Konzept braucht einen stabilen key, einen kurzen starken Titel, eine kurze Auswahlbeschreibung und einen Umsetzungsansatz. " +
                "Verwende Pantry-Zutaten aus oils/fats/spices/herbs/sauces/broths nur dann, wenn das Konzept ohne sie nicht sauber erklaerbar waere. " +
                "Vermeide beliebige Mini-Zusaetze wie etwas Kraut, etwas Sauce oder etwas Fett ohne klaren kulinarischen Zweck. " +
                "Vermeide Konzepte, die eher wie ein neues Nebenrezept wirken als wie eine Optimierung des Ausgangsgerichts. " +
                "Ordne die Konzepte nach Naehe zum Originalgericht: zuerst sehr nahe und alltagstaugliche Varianten, spaeter hoechstens eine mutigere Idee. " +
                "Bei High-Protein zaehlen Kraeuter, Gewuerze, Oele und Saucen nicht als Protein-Hebel.";

            var commonPreview =
                "Du schreibst eine kochbare Rezeptvariante fuer eine Food-App. " +
                "Antworte nur als valides JSON nach Schema. " +
                "Wichtigster Output: finale Zutatenliste und komplette Zubereitung in der Sprache des Nutzers (Anfaenger-tauglich). " +
                "Arbeite frei im Rezepttext ohne Templates. " +
                "Wenn ein ausgewaehltes Konzept mitgegeben wird, setze genau diese Richtung konsequent um. " +
                "Die Rezeptidee soll sich klar im finalen Titel, in den Zutaten und in der Zubereitung wiederfinden. " +
                "Nutze fuer neu hinzugefuegte oder ersetzte Zutaten nur ingredientId-Werte aus der Liste Verfuegbare Varianten-Zutaten. " +
                "Gib fuer jede finale Zutat ingredientId, quantity und measure zurueck. " +
                "quantity soll nur die Mengenangabe enthalten, measure nur die Einheit. " +
                "Wenn du eine Zutat im preparationText verwendest, muss sie in ingredients[] stehen. " +
                "Jede Zutat in ingredients[] MUSS auch im preparationText tatsaechlich verwendet werden. Entferne Zutaten aus ingredients[], die du nicht im Text verwendest. " +
                "Fuege keine weiteren Pantry-Zutaten hinzu, nur um den Text schoener zu machen. " +
                "Wenn keine Zutat zum Binden vorhanden ist, schreibe nicht 'binden' oder 'abbinden', sondern arbeite mit einkochen/reduzieren. " +
                "Passe Mengen/Fluessigkeit/Garzeiten so an, dass es wirklich funktioniert. " +
                "preparationText: klare Reihenfolge, Zeiten/Hitze/Temperaturen, Ergebnis-Kriterien und am Ende Abschmecken (nach Geschmack, nicht starre TL-Tipps). " +
                "Gib stepPlan immer als leeres Array [] zurueck. ";

            var variantPreview = normalizedVariantType switch
            {
                "highprotein" =>
                    "High-Protein Regeln: Erhoehe Protein pro Portion mind. 20% (ideal 25%) ohne Kochbarkeit zu gefaehrden. Vermeide reine Mini-Toppings als einzigen Protein-Hebel. " +
                    "Wichtig: Protein-Hebel muessen mengenrelevant sein. Verwende keine Mini-Mengen wie 2 g Mehl als 'Protein-Upgrade'. Wenn eine neue Protein-Zutat der Haupthebel ist, plane eine realistische Menge (Faustregel: >= 30 g pro Portion bzw. >= 120 g gesamt, oder 2 Eier gesamt/mehr je nach Gericht). " +
                    "Halte das Originalgericht klar erkennbar: Hauptzutat, Stil und Gerichtstyp sollen erhalten bleiben, ausser der Nutzer will ausdruecklich einen Austausch. " +
                    "Bevorzuge zuerst passende, herzhafte Erweiterungen im selben Kuechenstil statt stilfremder Protein-Zusaetze. " +
                    "Wenn ein Gewaehltes Konzept ein Protein-Swap ist, muss die Hauptproteinquelle wirklich getauscht werden (Titel/Zutaten/Schritte passen zusammen; die alte Hauptproteinquelle bleibt nicht einfach stehen). ",
                "lowcarb" =>
                    "Low-Carb Regeln: Reduziere oder ersetze staerkehaltige Teile konsequent und halte das Ergebnis alltagstauglich, saettigend und kulinarisch rund. Streiche nicht einfach nur Reis, Kartoffeln, Pasta, Brot oder aehnliche Komponenten, ohne einen stimmigen Ersatz oder eine sinnvolle neue Struktur zu schaffen. Das Gericht muss auch in der Low-Carb-Version vollstaendig wirken und klar als das gleiche Grundgericht erkennbar bleiben. Bevorzuge nahe Ersatzideen mit passender Textur und Funktion statt radikale Umbauten. ",
                "vegan" =>
                    "Vegan Regeln: Keine tierischen Zutaten im finalen Rezept. Ersetze tierische Bestandteile funktionsgerecht: Denke an Geschmack, Textur, Bindung, Saftigkeit und Rolle im Gericht statt nur an irgendeinen veganen Ersatz. Das Ergebnis muss wie eine stimmige vegane Version desselben Grundgerichts wirken, nicht wie ein anderes Rezept. Wenn du Umami oder Rundung brauchst, nutze passende vegane Alternativen, aber vermeide generische Standardersatzstoffe ohne klaren Stilbezug. ",
                "mealprep" =>
                    "Meal-Prep Regeln: Das finale Rezept muss klar als Meal-Prep-Rezept erkennbar sein. Formuliere so, dass Portionieren, Lagern und Aufwaermen konkret und einfach werden. " +
                    "Die Schritte muessen zeigen, was zusammen vorbereitet wird, was separat bleiben soll (z.B. Sauce, Beilage, frische Komponenten), wann portioniert wird und wie das Gericht spaeter wieder erwaermt bzw. serviert wird. " +
                    "Vermeide Meal-Prep-Konzepte, die im Kern nur das Originalrezept wiederholen. Das Ergebnis soll fuer mehrere Portionen praktikabel sein und darf keine empfindlichen, beim Lagern schnell leidenden Komponenten ohne klaren Plan enthalten. ",
                _ => string.Empty
            };

            var previewSystemPrompt = commonPreview + variantPreview;

            var qualitySystemPrompt =
                "Du bist QA fuer Rezepttexte in einer Food-App. " +
                "Antworte nur als valides JSON nach Schema. " +
                "Du bekommst einen Rezept-Entwurf (Zutaten + Zubereitung). " +
                "Aufgabe: Mache den Entwurf kochbar fuer komplette Anfaenger, ohne die Rezeptidee zu aendern. " +
                "Repariere Reihenfolge/Grammatik/Klarheit und fuege fehlende Vorbereitungsschritte ein. " +
                "Wichtig: Keine Zutat darf im Text auftauchen, ohne in ingredients[] zu stehen. Jede Zutat in ingredients[] muss auch tatsaechlich im preparationText vorkommen. Keine 'gekocht' ohne Kochschritt. " +
                "Pruefe Zutatenliste und Zubereitung hart auf Konsistenz: Wenn ein Schritt eine Zutat, Form, Menge oder Verarbeitungsart nennt, muss das zu ingredients[] passen. Repariere Widersprueche wie 'gemahlen' vs 'ganz', fehlende Essig-/Bruehe-/Gewuerz-Zutaten, oder Zutatennamen, die nur im Schritt auftauchen. " +
                "Entferne Schrittbegriffe, die nicht sauber vorbereitet oder spaeter nicht wiederverwendet werden. Nenne keine Beilage, Sauce oder Gemueseform, wenn sie im Ablauf nicht wirklich hergestellt wird. " +
                "Das Variantenziel muss im finalen Rezept sichtbar erfuellt sein. Wenn die Variante im Ergebnis kaum erkennbar ist, repariere den Entwurf konsequent in Richtung des gewaehltens Ziels, ohne ein neues Gericht daraus zu machen. Das Originalgericht muss klar erkennbar bleiben. " +
                "Fuege keine neuen Zutaten hinzu, nur um Sprache oder Geschmack aufzuwerten. Entferne oder formuliere problematische Textstellen lieber um. " +
                "Wenn keine Bindezutat vorhanden ist, schreibe nie 'binden/abbinden', sondern formuliere mit einkochen/reduzieren. " +
                "Wenn schon ein Fett im Rezept vorhanden ist, fuehre kein zusaetzliches Fett nur zum Abschmecken ein. " +
                "Wenn die Variante Meal-Prep ist, pruefe streng: Portionierung, Lagerlogik und Aufwaermen muessen explizit im Text vorkommen. Ein reines Originalrezept mit minimalen Aenderungen ist kein gueltiges Meal-Prep-Ergebnis. " +
                "Wenn die Variante High-Protein ist, muss die Proteinerhoehung kulinarisch plausibel und im Gericht selbst spürbar sein, nicht nur als zufaellige Deko oder stilfremde Zutat. " +
                "Wenn die Variante Low-Carb ist, darf nicht einfach nur eine Beilage gestrichen werden; das Gericht muss weiterhin vollstaendig und stimmig wirken. " +
                "Wenn die Variante Vegan ist, muss der Ersatz geschmacklich und texturbezogen zum Ursprungsgericht passen; vermeide generische Standardersatzstoffe ohne Stilbezug. " +
                "Gib stepPlan immer als leeres Array [] zurueck. ";

            var alignmentSystemPrompt =
                "Du bist QA und Editor fuer AI-Rezeptvarianten. Antworte nur als valides JSON nach Schema. " +
                "Aufgabe: Der Entwurf setzt das gewaehlte Konzept noch nicht konsequent um. Repariere den Entwurf so, dass das Konzept eindeutig umgesetzt ist. " +
                "Wenn das Konzept ein Protein-Swap ist, MUSS die bisherige Hauptproteinquelle ersetzt werden (die alte bleibt nicht im finalen Zutaten-Array). " +
                "Sprachqualitaet ist Pflicht: Schreibe idiomatisch, grammatikalisch korrekt und in der angegebenen Sprache (Imperativ in Rezeptstil). " +
                "Wenn eine Formulierung unnatuerlich klingt oder semantisch schief ist, formuliere sie um. " +
                "Erfinde keine unueblichen Zuschnitte wie 'Haehnchenschulter'. Verwende realistische Cuts und bevorzugt Kandidaten-Namen. " +
                "Pruefe Zutatenliste und Zubereitung immer gemeinsam. Schritte duerfen keine Zutaten, Formen oder Mengen erzaehlen, die im finalen ingredients[] nicht gedeckt sind. Repariere Inkonsistenzen direkt statt sie stehen zu lassen. " +
                "Das Ziel ist nicht nur irgendeine Variante, sondern eine stimmige Variante des Originalgerichts. Das Originalgericht muss erkennbar bleiben; Beilagen oder Zusatzzutaten duerfen das Hauptgericht nicht verdraengen. " +
                "Wenn das gewaehlte Konzept Meal-Prep ist, muss der Entwurf sichtbar meal-prep-tauglich werden: mehrere Portionen, klare Lager-/Trennlogik und konkrete Aufwaerm-Hinweise. " +
                "ingredientPlan ist ein Vertrag: Zutaten aus dem Plan duerfen nicht durch andere Zutaten umgedeutet oder ersetzt werden. ";

            return new PromptPack(conceptPickSystemPrompt, previewSystemPrompt, qualitySystemPrompt, alignmentSystemPrompt);
        }

        private string BuildCompactPreparationContext(RecipeBaseData recipe, string language)
        {
            var preparation = BuildPreparationTextFromRecipe(recipe, language);
            if (string.IsNullOrWhiteSpace(preparation))
            {
                return "Keine Zubereitung vorhanden.";
            }

            var cleaned = preparation
                .Replace("♨️", string.Empty)
                .Replace("🍲", "Bräter")
                .Replace("…", ".")
                .Replace("\r", string.Empty);

            var lines = cleaned
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Take(4)
                .ToList();

            if (lines.Count == 0)
            {
                return TruncateForPrompt(cleaned, 250);
            }

            return TruncateForPrompt(string.Join(" ", lines), 250);
        }

        private string BuildPromptIngredientLines(RecipeBaseData recipe, string language)
        {
            var lines = BuildIngredientPreview(recipe, language)
                .Select(x => $"- {x.Name}")
                .Take(12)
                .ToList();

            return lines.Count == 0 ? "- keine" : string.Join("\n", lines);
        }

         private static string BuildPromptCandidateLines(IReadOnlyList<IngredientResolutionCandidate> candidates, string variantType, string language)
         {
             if (candidates == null || candidates.Count == 0)
             {
                 return "- keine";
             }

             // Keep candidate formatting compact; we may send many candidates to the AI.
             // We still cap the prompt list to avoid giant prompts and timeouts; the list is diversified upstream.
             const int maxCandidatesInPrompt = 500;
             var slice = candidates.Take(maxCandidatesInPrompt).ToList();

             var header = candidates.Count > slice.Count
                 ? $"- (zeige {slice.Count} von {candidates.Count} Kandidaten)\n"
                 : string.Empty;

             return header + string.Join("\n", slice.Select(x =>
             {
                 var categoryLabel = GetDisplayCategoryLabel(x.CategoryKey, x.CategoryLabel, language);
                 return variantType switch
                 {
                     "highprotein" => $"- id={x.IngredientId}; n={x.Name}; c={categoryLabel}; p={x.ProteinPer100g:0.##}",
                     "lowcarb" => $"- id={x.IngredientId}; n={x.Name}; c={categoryLabel}; carb={x.CarbsPer100g:0.##}; p={x.ProteinPer100g:0.##}",
                     _ => $"- id={x.IngredientId}; n={x.Name}; c={categoryLabel}; p={x.ProteinPer100g:0.##}; carb={x.CarbsPer100g:0.##}"
                 };
             }));
         }

        private static object BuildSchema()
        {
            return new
            {
                type = "object",
                additionalProperties = false,
                    required = new[] { "variantType", "variantLabel", "title", "summary", "ingredientsText", "preparationText", "usedFallback", "userNoteApplied", "remainingChanges", "ingredients", "stepPlan", "highlights" },
                    properties = new
                    {
                    variantType = new { type = "string" },
                    variantLabel = new { type = "string" },
                    title = new { type = "string" },
                    summary = new { type = "string" },
                    ingredientsText = new { type = "string" },
                    preparationText = new { type = "string" },
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
                                required = new[] { "ingredientId", "name", "quantity", "measure", "changeHint", "isModified" },
                                properties = new
                                {
                                    ingredientId = new { type = "integer" },
                                    name = new { type = new[] { "string", "null" } },
                                    quantity = new { type = "string" },
                                    measure = new { type = "string" },
                                    changeHint = new { type = new[] { "string", "null" } },
                                    isModified = new { type = "boolean" }
                                }
                        }
                    },
                    stepPlan = new
                    {
                        type = "array",
                        // stepPlan is intentionally unused in the app UI now (we render free-text steps).
                        // Force empty output to save tokens and avoid invalid masterStepKey spam.
                        maxItems = 0,
                        items = new
                        {
                            type = "object",
                            additionalProperties = false,
                            properties = new { }
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
            string aiProvider,
            string language,
            string userNote,
            IReadOnlyList<string> defaultCategoryKeys,
            object recipeContext,
            string apiKey,
            CancellationToken cancellationToken)
        {
            var selectionContext = AnalyzeIngredientSelectionContext(recipe);
            var promptRequest = new AiPromptRequest
            {
                SchemaName = "recipe_ai_ingredient_category_plan",
                AllowTools = false,
                ModelTier = AiModelTier.Cheap,
                Schema = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "strategy", "preferredCategoryKeys" },
                    properties = new
                    {
                        strategy = new { type = "string" },
                        preferredCategoryKeys = new
                        {
                            type = "array",
                            items = new { type = "string" }
                        }
                    }
                },
                // Tiny response (strategy + up to 4 keys). Keep max output small to avoid pointless truncation/timeouts.
                MaxOutputTokens = 350,
                GeminiThinkingBudget = 0,
                SystemPrompt =
                    "Antworte ausschliesslich als valides JSON. " +
                    "Waehle nur Kategorien aus der erlaubten Liste. " +
                    "Gib nur strategy und preferredCategoryKeys zurueck. " +
                    "Waehle hoechstens 4 Kategorien. " +
                    "strategy muss genau einer von add, replace, remove oder hybrid sein. " +
                    "Bei lowcarb bevorzuge haeufig replace, remove oder hybrid. " +
                    "Bei highprotein bevorzuge haeufig add oder hybrid, ohne eine unpassende zweite Hauptproteinart einzufuehren. " +
                    "Wenn das Rezept bereits klar Fleisch als Hauptprotein hat, dann bevorzuge fuer highprotein zuerst proteinreiche Add-ons/Beilagen-Kategorien (z.B. legumes, eggs, tofu, yogurt, seeds) statt Fisch.",
                UserPrompt =
                    $"Variante: {GetVariantLabel(variantType, language)} ({variantType})\n" +
                    $"Rezeptkontext: {BuildCompactCategorySelectionContext(recipe, language, AnalyzeIngredientSelectionContext(recipe))}\n" +
                    $"Erlaubte Kategorien (Key -> Label): {string.Join(", ", defaultCategoryKeys.Select(x => $"{x} -> {GetDisplayCategoryLabel(x, x, language)}"))}\n" +
                    $"User-Hinweis: {(string.IsNullOrWhiteSpace(userNote) ? "Kein Hinweis" : userNote)}\n" +
                    "Waehle die sinnvollsten Kategorien fuer dieses Rezept. Danach durchsucht die Datenbank diese Kategorien, und die AI waehlt daraus konkrete Zutaten."
            };

            var promptResult = await ExecuteStructuredAiCallAsync<IngredientCategoryPlanResponse>(promptRequest, aiProvider, apiKey, cancellationToken);
            if (promptResult == null)
            {
                return new IngredientCategoryPlanResponse
                {
                    Strategy = GetDefaultIngredientStrategy(variantType, AnalyzeIngredientSelectionContext(recipe)),
                    Reasoning = string.Empty,
                    PreferredCategoryKeys = defaultCategoryKeys.ToList()
                };
            }

            promptResult.PreferredCategoryKeys = promptResult.PreferredCategoryKeys?
                .Select(NormalizeCategoryKey)
                .Where(x => defaultCategoryKeys.Contains(x, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(4)
                .ToList() ?? new List<string>();
            promptResult.Strategy = NormalizeIngredientStrategy(promptResult.Strategy, variantType, selectionContext);

            // Guardrails: for meat-based recipes, "highprotein" should not collapse into "fish-only" suggestions.
            // We keep the swap concept possible via poultry, but push the categories towards realistic add-ons.
            if (string.Equals(variantType, "highprotein", StringComparison.OrdinalIgnoreCase)
                && selectionContext.HasMeatMainProtein
                && !selectionContext.HasFishMainProtein)
            {
                var keys = promptResult.PreferredCategoryKeys.ToList();

                if (string.Equals(promptResult.Strategy, "add", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(promptResult.Strategy, "hybrid", StringComparison.OrdinalIgnoreCase))
                {
                    // Fish in a pork/beer roast is almost always a culinary mismatch for "add"; keep poultry for swap.
                    keys.RemoveAll(x => string.Equals(x, "fish", StringComparison.OrdinalIgnoreCase));
                    keys.RemoveAll(x => string.Equals(x, "seafood", StringComparison.OrdinalIgnoreCase));
                }

                // Ensure legumes are considered when available.
                if (defaultCategoryKeys.Contains("legumes", StringComparer.OrdinalIgnoreCase)
                    && keys.All(x => !string.Equals(x, "legumes", StringComparison.OrdinalIgnoreCase)))
                {
                    keys.Insert(0, "legumes");
                }

                // Fill up with sensible high-protein add categories (in stable priority order).
                var fillOrder = new[] { "eggs", "tofu", "tempeh", "yogurt", "seeds", "mushrooms", "cheese", "poultry" };
                foreach (var fill in fillOrder)
                {
                    if (keys.Count >= 4)
                    {
                        break;
                    }

                    if (!defaultCategoryKeys.Contains(fill, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (keys.Any(x => string.Equals(x, fill, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    keys.Add(fill);
                }

                // If the selection still collapses into only "legumes/seeds/nuts", force at least one "different" category
                // so the DB candidate pool becomes diverse enough for 4 distinct concepts.
                var isOnlyPlantAddOns = keys.All(x =>
                    string.Equals(x, "legumes", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x, "seeds", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x, "nuts", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x, "mushrooms", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x, "vegetables", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x, "leafy_greens", StringComparison.OrdinalIgnoreCase));

                if (isOnlyPlantAddOns)
                {
                    if (defaultCategoryKeys.Contains("cheese", StringComparer.OrdinalIgnoreCase)
                        && keys.All(x => !string.Equals(x, "cheese", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (keys.Count >= 4) keys.RemoveAt(keys.Count - 1);
                        keys.Add("cheese");
                    }

                    if (defaultCategoryKeys.Contains("eggs", StringComparer.OrdinalIgnoreCase)
                        && keys.All(x => !string.Equals(x, "eggs", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (keys.Count >= 4) keys.RemoveAt(keys.Count - 1);
                        keys.Add("eggs");
                    }
                }

                promptResult.PreferredCategoryKeys = keys
                    .Select(NormalizeCategoryKey)
                    .Where(x => defaultCategoryKeys.Contains(x, StringComparer.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(4)
                    .ToList();
            }

            return promptResult;

        }

        private async Task<List<RecipeConceptSuggestion>> SelectRecipeConceptsAsync(
            RecipeBaseData recipe,
            string variantType,
            string aiProvider,
            string language,
            string userNote,
            string strategy,
            IngredientSelectionContext context,
            IReadOnlyList<string> preferredCategoryKeys,
            IReadOnlyList<IngredientResolutionCandidate> candidates,
            string apiKey,
            CancellationToken cancellationToken)
        {
            var targetConceptCount = GetTargetConceptCount(variantType, context);
            var promptRequest = BuildConceptPickPromptRequest(recipe, variantType, language, userNote, strategy, context, preferredCategoryKeys);
            promptRequest.AllowTools = true;

            RecipeConceptPickResponse? result = null;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    result = await ExecuteStructuredAiCallAsync<RecipeConceptPickResponse>(promptRequest, aiProvider, apiKey, cancellationToken);
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning(ex,
                        "Recipe concept pick failed. Provider={Provider} RecipeId={RecipeId} VariantType={VariantType} Attempt={Attempt}",
                        NormalizeAiProvider(aiProvider),
                        recipe.Id,
                        variantType,
                        attempt + 1);
                    result = null;
                }

                if (result?.Concepts != null && result.Concepts.Count > 0)
                {
                    break;
                }

                promptRequest.UserPrompt += "\nWichtig: Rufe zuerst get_ingredients auf und verwende nur ingredientId, die dir als Tool-Output geliefert werden. Antworte danach mit genau 4 Konzepten als JSON.";
            }

            var candidatesById = (candidates ?? Array.Empty<IngredientResolutionCandidate>())
                .Where(x => x != null && x.IngredientId > 0)
                .GroupBy(x => x.IngredientId)
                .ToDictionary(g => g.Key, g => g.First(), EqualityComparer<int>.Default);
            var allowedCandidateIdSet = new HashSet<int>(candidatesById.Keys);

            var baseIngredientIds = recipe.Ingredients?
                .Where(x => x?.Ingredient?.IngredientsAndNutrients != null)
                .Select(x => x!.Ingredient!.IngredientsAndNutrients!.Id)
                .Where(x => x > 0)
                .Distinct()
                .ToHashSet() ?? new HashSet<int>();

            var concepts = result?.Concepts?
                .Where(x => !string.IsNullOrWhiteSpace(x.Title))
                .Take(targetConceptCount)
                .Select((x, index) => new RecipeConceptSuggestion
                {
                    Key = string.IsNullOrWhiteSpace(x.Key) ? $"{variantType}-concept-{index + 1}" : SlugifyConceptKey(x.Key),
                    Title = SanitizeConceptText(x.Title, language).Trim(),
                    Summary = string.IsNullOrWhiteSpace(x.Summary)
                        ? LocalizedWord(language, "Stimmige Variante fuer dieses Rezept.", "A fitting variation for this recipe.", "Una variación coherente para esta receta.", "Uma variação coerente para esta receita.")
                        : SanitizeConceptText(x.Summary, language).Trim(),
                    Approach = string.IsNullOrWhiteSpace(x.Approach)
                        ? LocalizedWord(language, "passt den Stil des Gerichts sauber an", "adapts the style of the dish cleanly", "adapta bien el estilo del plato", "adapta bem o estilo do prato")
                        : SanitizeConceptText(x.Approach, language).Trim(),
                    IngredientPlan = (x.Ingredients ?? new List<RecipeConceptPickIngredientItem>())
                        .Take(12)
                        .Select(item =>
                        {
                            if (item == null) return null;

                            var ingredientId = item.IngredientId;
                            var action = string.IsNullOrWhiteSpace(item.Action) ? "add" : item.Action.Trim().ToLowerInvariant();
                            var replacesId = item.ReplacesIngredientId;

                            if (action != "add" && action != "replace" && action != "remove")
                            {
                                action = "add";
                                replacesId = null;
                            }

                            if (action == "remove")
                            {
                                replacesId = null;
                                // Remove can only target a real base ingredient.
                                if (ingredientId <= 0 || !baseIngredientIds.Contains(ingredientId))
                                {
                                    return null;
                                }

                                return new RecipeAiConceptIngredientPlanItem
                                {
                                    IngredientId = ingredientId,
                                    IngredientName = string.Empty,
                                    Action = action,
                                    ReplacesIngredientId = null,
                                    Quantity = item.Quantity ?? string.Empty,
                                    Measure = item.Measure ?? string.Empty,
                                    Reason = item.Reason ?? string.Empty,
                                    IsMainProtein = item.IsMainProtein
                                };
                            }

                            // add/replace must reference an ingredient the tool can return (our server-side universe).
                            if (ingredientId <= 0 || !allowedCandidateIdSet.Contains(ingredientId))
                            {
                                return null;
                            }

                            var cand = candidatesById.TryGetValue(ingredientId, out var c) ? c : null;
                            if (cand == null) return null;

                            // Only allow replace/remove for real base ingredients.
                            if ((action == "replace" || action == "remove")
                                && (!replacesId.HasValue || replacesId.Value <= 0 || !baseIngredientIds.Contains(replacesId.Value)))
                            {
                                action = "add";
                                replacesId = null;
                            }

                            // High-protein concepts should not add large sweeteners; keep those out of the plan.
                            if (string.Equals(variantType, "highprotein", StringComparison.OrdinalIgnoreCase))
                            {
                                var nameLower = (cand.Name ?? string.Empty).ToLowerInvariant();
                                if (cand.CategoryKey.Equals("sweeteners", StringComparison.OrdinalIgnoreCase) || nameLower.Contains("zucker"))
                                {
                                    return null;
                                }
                            }

                            return new RecipeAiConceptIngredientPlanItem
                            {
                                IngredientId = ingredientId,
                                IngredientName = cand.Name,
                                Action = action,
                                ReplacesIngredientId = replacesId,
                                Quantity = item.Quantity ?? string.Empty,
                                Measure = item.Measure ?? string.Empty,
                                Reason = item.Reason ?? string.Empty,
                                IsMainProtein = item.IsMainProtein
                            };
                        })
                        .Where(x => x != null)
                        .Cast<RecipeAiConceptIngredientPlanItem>()
                        .ToList()
                })
                .Where(x => !ContainsBannedConceptTokens(x.Title, language)
                         && !ContainsBannedConceptTokens(x.Summary, language)
                         && !ContainsBannedConceptTokens(x.Approach, language))
                .ToList() ?? new List<RecipeConceptSuggestion>();

            if (concepts.Count == 0)
            {
                _logger.LogWarning("Recipe concept pick returned no usable concepts. Provider={Provider} RecipeId={RecipeId} VariantType={VariantType}", NormalizeAiProvider(aiProvider), recipe.Id, variantType);

                // In production-like runs we prefer a visible error over silently returning the same local fallback concepts.
                if (!AllowLocalAiFallback)
                {
                    throw new InvalidOperationException("AI-Rezeptideen konnten nicht geladen werden. Bitte versuche es erneut.");
                }

                concepts = BuildLocalRecipeConceptsFallback(recipe, variantType, language, strategy, context, candidates);
            }
            else if (concepts.Count < targetConceptCount && AllowLocalAiFallback)
            {
                // Only pad with local fallback concepts when explicitly allowed.
                foreach (var fallback in BuildLocalRecipeConceptsFallback(recipe, variantType, language, strategy, context, candidates))
                {
                    if (concepts.Count >= targetConceptCount)
                    {
                        break;
                    }

                    if (concepts.All(x => !string.Equals(x.Key, fallback.Key, StringComparison.OrdinalIgnoreCase)))
                    {
                        concepts.Add(fallback);
                    }
                }
            }

            // Enrich concept ingredient plans with display names so the UI can show all planned ingredients
            // without having to ship the full candidate list back to the browser.
            var planIdsForLookup = concepts
                .SelectMany(x => x.IngredientPlan ?? new List<RecipeAiConceptIngredientPlanItem>())
                .SelectMany(x =>
                {
                    var ids = new List<int>();
                    if (x != null && x.IngredientId > 0) ids.Add(x.IngredientId);
                    if (x?.ReplacesIngredientId.GetValueOrDefault() > 0) ids.Add(x!.ReplacesIngredientId!.Value);
                    return ids;
                })
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            var hasDbLookup = planIdsForLookup.Count > 0;
            var dbCandidatesById = !hasDbLookup
                ? new Dictionary<int, IngredientResolutionCandidate>()
                : (await _ingredientResolverService.GetCandidatesByIdsAsync(
                        ingredientIds: planIdsForLookup,
                        language: language,
                        cancellationToken: cancellationToken))
                    .GroupBy(x => x.IngredientId)
                    .ToDictionary(g => g.Key, g => g.First(), EqualityComparer<int>.Default);

            var candidateNameById = (candidates ?? Array.Empty<IngredientResolutionCandidate>())
                .Where(x => x != null && x.IngredientId > 0 && !string.IsNullOrWhiteSpace(x.Name))
                .GroupBy(x => x.IngredientId)
                .ToDictionary(g => g.Key, g => g.First().Name, EqualityComparer<int>.Default);

            var baseNameById = recipe.Ingredients?
                .Where(x => x?.Ingredient?.IngredientsAndNutrients != null)
                .Select(x => x!.Ingredient!.IngredientsAndNutrients!)
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => GetIngredientName(g.First(), language), EqualityComparer<int>.Default)
                ?? new Dictionary<int, string>();

            foreach (var concept in concepts)
            {
                concept.IngredientPlan ??= new List<RecipeAiConceptIngredientPlanItem>();

                // Drop ingredient ids that do not exist in DB (prevents hallucinated ids from polluting the UI).
                concept.IngredientPlan = concept.IngredientPlan
                    .Where(p => p != null && p.IngredientId > 0 && (!hasDbLookup || dbCandidatesById.ContainsKey(p.IngredientId)))
                    .ToList();

                foreach (var plan in concept.IngredientPlan)
                {
                    if (plan == null) continue;

                    if (string.IsNullOrWhiteSpace(plan.IngredientName)
                        && plan.IngredientId > 0
                        && dbCandidatesById.TryGetValue(plan.IngredientId, out var dbCandidate)
                        && !string.IsNullOrWhiteSpace(dbCandidate.Name))
                    {
                        plan.IngredientName = dbCandidate.Name;
                    }
                    else if (string.IsNullOrWhiteSpace(plan.IngredientName)
                        && plan.IngredientId > 0
                        && candidateNameById.TryGetValue(plan.IngredientId, out var candidateName))
                    {
                        plan.IngredientName = candidateName;
                    }

                    if (string.IsNullOrWhiteSpace(plan.ReplacesIngredientName)
                        && plan.ReplacesIngredientId.GetValueOrDefault() > 0)
                    {
                        var replaceId = plan.ReplacesIngredientId!.Value;
                        if (baseNameById.TryGetValue(replaceId, out var baseName))
                        {
                            plan.ReplacesIngredientName = baseName;
                        }
                        else if (dbCandidatesById.TryGetValue(replaceId, out var dbReplace) && !string.IsNullOrWhiteSpace(dbReplace.Name))
                        {
                            plan.ReplacesIngredientName = dbReplace.Name;
                        }
                        else if (candidateNameById.TryGetValue(replaceId, out var replaceName))
                        {
                            plan.ReplacesIngredientName = replaceName;
                        }
                    }

                    plan.Reason = BuildSafeConceptPlanReason(plan, language);
                }

                FixConceptTextAgainstIngredientPlan(concept, language);
            }

            return concepts.Take(targetConceptCount).ToList();
        }

        private static void FixConceptTextAgainstIngredientPlan(RecipeConceptSuggestion concept, string language)
        {
            if (concept == null)
            {
                return;
            }

            var normalizedLanguage = NormalizeLanguage(language);
            if (!string.Equals(normalizedLanguage, "de", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var plan = concept.IngredientPlan ?? new List<RecipeAiConceptIngredientPlanItem>();
            if (plan.Count == 0)
            {
                return;
            }

            var planNames = plan
                .Select(x => (x?.IngredientName ?? string.Empty).Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
            if (planNames.Count == 0)
            {
                return;
            }

            // If the concept text mentions quinoa but quinoa is not in the planned ingredient names,
            // rewrite it to the main planned ingredient so UI doesn't show mismatched ideas.
            var mentionsQuinoa =
                (concept.Title ?? string.Empty).Contains("quinoa", StringComparison.OrdinalIgnoreCase) ||
                (concept.Summary ?? string.Empty).Contains("quinoa", StringComparison.OrdinalIgnoreCase) ||
                (concept.Approach ?? string.Empty).Contains("quinoa", StringComparison.OrdinalIgnoreCase);

            if (!mentionsQuinoa)
            {
                return;
            }

            var planHasQuinoa = planNames.Any(x => x.Contains("quinoa", StringComparison.OrdinalIgnoreCase));
            if (planHasQuinoa)
            {
                return;
            }

            var main = plan.FirstOrDefault(x => string.Equals(x?.Action, "add", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(x?.IngredientName))
                       ?? plan.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x?.IngredientName));
            var mainName = (main?.IngredientName ?? planNames[0]).Trim();
            if (string.IsNullOrWhiteSpace(mainName))
            {
                return;
            }

            concept.Title = ReplaceIgnoreCase(concept.Title, "Quinoa-Salat", $"{mainName}-Beilage");
            concept.Title = ReplaceIgnoreCase(concept.Title, "Quinoasalat", $"{mainName}-Beilage");
            concept.Title = ReplaceIgnoreCase(concept.Title, "Quinoa", mainName);

            concept.Summary = ReplaceIgnoreCase(concept.Summary, "Quinoa-Salat", $"{mainName}-Beilage");
            concept.Summary = ReplaceIgnoreCase(concept.Summary, "Quinoasalat", $"{mainName}-Beilage");
            concept.Summary = ReplaceIgnoreCase(concept.Summary, "Quinoa", mainName);

            concept.Approach = ReplaceIgnoreCase(concept.Approach, "Quinoa-Salat", $"{mainName}-Beilage");
            concept.Approach = ReplaceIgnoreCase(concept.Approach, "Quinoasalat", $"{mainName}-Beilage");
            concept.Approach = ReplaceIgnoreCase(concept.Approach, "Quinoa", mainName);
        }

        private static string BuildSafeConceptPlanReason(RecipeAiConceptIngredientPlanItem? plan, string language)
        {
            if (plan == null)
            {
                return string.Empty;
            }

            var lang = NormalizeLanguage(language);
            var ingredientName = (plan.IngredientName ?? string.Empty).Trim();
            var replacesName = (plan.ReplacesIngredientName ?? string.Empty).Trim();
            var action = (plan.Action ?? string.Empty).Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(ingredientName) && string.IsNullOrWhiteSpace(replacesName))
            {
                return string.Empty;
            }

            if (string.Equals(lang, "de", StringComparison.OrdinalIgnoreCase))
            {
                if (action == "replace" && !string.IsNullOrWhiteSpace(ingredientName) && !string.IsNullOrWhiteSpace(replacesName))
                {
                    return $"{ingredientName} ersetzt {replacesName} in dieser Variante.";
                }

                if (action == "remove")
                {
                    return !string.IsNullOrWhiteSpace(replacesName)
                        ? $"{replacesName} wird in dieser Variante entfernt."
                        : !string.IsNullOrWhiteSpace(ingredientName)
                            ? $"{ingredientName} wird in dieser Variante entfernt."
                            : string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(ingredientName))
                {
                    return plan.IsMainProtein
                        ? $"{ingredientName} dient hier als neuer Proteinbaustein."
                        : $"{ingredientName} ergänzt das Gericht passend zur Variante.";
                }

                return string.Empty;
            }

            if (action == "replace" && !string.IsNullOrWhiteSpace(ingredientName) && !string.IsNullOrWhiteSpace(replacesName))
            {
                return $"{ingredientName} replaces {replacesName} in this variation.";
            }

            if (action == "remove")
            {
                return !string.IsNullOrWhiteSpace(replacesName)
                    ? $"{replacesName} is removed in this variation."
                    : !string.IsNullOrWhiteSpace(ingredientName)
                        ? $"{ingredientName} is removed in this variation."
                        : string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(ingredientName))
            {
                return plan.IsMainProtein
                    ? $"{ingredientName} acts as the new protein element here."
                    : $"{ingredientName} complements the dish for this variation.";
            }

            return string.Empty;
        }

        private static string SanitizeConceptText(string? value, string language)
        {
            var text = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var lang = NormalizeLanguage(language);
            if (string.Equals(lang, "de", StringComparison.OrdinalIgnoreCase))
            {
                // Common failure: literal cut-name mapping from Schweineschulter -> "Hähnchenschulter".
                text = text
                    .Replace("Hähnchenschulter", "Hähnchenoberkeule", StringComparison.OrdinalIgnoreCase)
                    .Replace("Haehnchenschulter", "Hähnchenoberkeule", StringComparison.OrdinalIgnoreCase);
            }
            else if (string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Replace("chicken shoulder", "chicken thigh", StringComparison.OrdinalIgnoreCase);
            }

            return text;
        }

        private static bool ContainsBannedConceptTokens(string? value, string language)
        {
            var text = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var lang = NormalizeLanguage(language);
            if (string.Equals(lang, "de", StringComparison.OrdinalIgnoreCase))
            {
                // Anything that survives sanitization is treated as a hard-fail.
                return text.Contains("hähnchenschulter", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("haehnchenschulter", StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase))
            {
                return text.Contains("chicken shoulder", StringComparison.OrdinalIgnoreCase);
            }

            return false;
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
                 take: 200,
                 cancellationToken: cancellationToken);
 
             candidates = FilterCandidatesForRecipeContext(candidates, variantType, ingredientSelectionContext);
             candidates = DiversifyCandidatesForPrompt(candidates, variantType, candidates.Count);
             var fallbackConcepts = BuildLocalRecipeConceptsFallback(
                 recipe,
                 variantType,
                 language,
                 GetDefaultIngredientStrategy(variantType, ingredientSelectionContext),
                ingredientSelectionContext,
                candidates);

            return new RecipeAiIngredientSuggestionResponse
            {
                VariantType = variantType,
                VariantLabel = GetVariantLabel(variantType, language),
                Strategy = GetDefaultIngredientStrategy(variantType, ingredientSelectionContext),
                SelectionMode = "concepts",
                Reasoning = LocalizedWord(
                    language,
                    $"{GetTargetConceptCount(variantType, ingredientSelectionContext)} lokale Rezeptideen wurden aus deiner DB und der Variante vorbereitet.",
                    $"{GetTargetConceptCount(variantType, ingredientSelectionContext)} local recipe ideas were prepared from your DB and the selected variant.",
                    $"Se prepararon {GetTargetConceptCount(variantType, ingredientSelectionContext)} ideas locales de receta a partir de tu BD y la variante elegida.",
                    $"Foram preparadas {GetTargetConceptCount(variantType, ingredientSelectionContext)} ideias locais de receita a partir da tua BD e da variante escolhida."),
                UsedFallback = true,
                AutoGenerateWithoutSelection = false,
                PreferredCategoryKeys = categoryKeys.ToList(),
                PreferredCategoryLabels = categoryKeys.Select(x => GetDisplayCategoryLabel(x, x, language)).ToList(),
                Suggestions = fallbackConcepts.Select(x => new RecipeAiIngredientSuggestionItem
                {
                    Name = x.Title,
                    AiReason = x.Summary,
                    ConceptKey = x.Key,
                    ConceptTitle = x.Title,
                    ConceptSummary = x.Summary,
                    ConceptApproach = x.Approach
                }).ToList()
            };
        }

        private List<RecipeConceptSuggestion> BuildLocalRecipeConceptsFallback(
            RecipeBaseData recipe,
            string variantType,
            string language,
            string strategy,
            IngredientSelectionContext context,
            IReadOnlyList<IngredientResolutionCandidate> candidates)
        {
            var topNames = candidates
                .Select(x => x.Name)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(6)
                .ToList();

            var leadOne = topNames.ElementAtOrDefault(0) ?? LocalizedWord(language, "passenden Zutaten", "suitable ingredients", "ingredientes adecuados", "ingredientes adequados");
            var leadTwo = topNames.ElementAtOrDefault(1) ?? leadOne;
            var leadThree = topNames.ElementAtOrDefault(2) ?? leadTwo;
            var leadFour = topNames.ElementAtOrDefault(3) ?? leadThree;
            var concepts = new List<RecipeConceptSuggestion>
            {
                new()
                {
                    Key = $"{variantType}-concept-rustic",
                    Title = LocalizedWord(language, "Rustikale Variante", "Rustic version", "Versión rústica", "Versão rústica"),
                    Summary = LocalizedWord(language, $"Herzhaft und nah am Original, mit Fokus auf {leadOne} und einer bodenständigen Beilage oder Ergänzung.", $"Hearty and close to the original, centered around {leadOne} and a grounded add-on or side.", $"Sustanciosa y cercana al original, centrada en {leadOne} y en un acompañamiento o añadido sólido.", $"Reconfortante e próxima do original, centrada em {leadOne} e num acompanhamento ou reforço equilibrado."),
                    Approach = LocalizedWord(language, "behält den Charakter des Gerichts und stärkt ihn gezielt", "keeps the original character and strengthens it deliberately", "mantiene el carácter del plato y lo refuerza con intención", "mantém o caráter do prato e reforça-o de forma intencional")
                },
                new()
                {
                    Key = $"{variantType}-concept-fresh",
                    Title = LocalizedWord(language, "Leichtere Moderne", "Lighter modern version", "Versión moderna ligera", "Versão moderna mais leve"),
                    Summary = LocalizedWord(language, $"Etwas frischer und strukturierter gedacht, mit {leadTwo} als klarer Richtung und saubererem Umbau des Originals.", $"Slightly fresher and more structured, with {leadTwo} as a clear direction and a cleaner transformation of the original.", $"Pensada de forma más fresca y estructurada, con {leadTwo} como dirección clara y una transformación más limpia del original.", $"Pensada de forma mais fresca e estruturada, com {leadTwo} como direção clara e uma transformação mais limpa do original."),
                    Approach = LocalizedWord(language, "arbeitet stärker mit Austausch, Struktur und klaren Texturen", "leans more on swaps, structure, and clear textures", "trabaja más con sustituciones, estructura y texturas claras", "trabalha mais com substituições, estrutura e texturas definidas")
                },
                new()
                {
                    Key = $"{variantType}-concept-bold",
                    Title = LocalizedWord(language, "Kräftige Neuinterpretation", "Bold reinterpretation", "Reinterpretación intensa", "Reinterpretação intensa"),
                    Summary = LocalizedWord(language, $"Die markanteste Richtung mit {leadThree} und stärkerem Umbau, damit die Variante deutlich eigenständig wirkt.", $"The boldest route, using {leadThree} and a stronger rebuild so the variant feels clearly distinct.", $"La dirección más marcada, con {leadThree} y una transformación más fuerte para que la variante se sienta claramente distinta.", $"A direção mais marcada, com {leadThree} e uma transformação mais forte para que a variante se sinta claramente distinta."),
                    Approach = context.PreferSideReplacement || string.Equals(strategy, "replace", StringComparison.OrdinalIgnoreCase)
                        ? LocalizedWord(language, "setzt stärker auf Ersetzen und Neuaufbau der Beilage", "leans more on replacing and rebuilding the side", "se apoya más en sustituir y rehacer la guarnición", "apoia-se mais em substituir e reconstruir o acompanhamento")
                        : LocalizedWord(language, "setzt stärker auf Ergänzen und gezielte neue Akzente", "leans more on adding and creating deliberate new accents", "se apoya más en añadir y crear nuevos acentos", "apoia-se mais em adicionar e criar novos acentos")
                }
            };

            if (NormalizeVariantType(variantType) is "highprotein" or "lowcarb" or "mealprep")
            {
                var replacementLead = candidates.FirstOrDefault(x =>
                {
                    var categoryKey = NormalizeCategoryKey(x.CategoryKey);
                    return context.HasMeatMainProtein
                        ? categoryKey is "poultry" or "fish" or "seafood"
                        : context.HasFishMainProtein
                            ? categoryKey is "poultry" or "pork" or "beef" or "lamb"
                            : categoryKey is "poultry" or "fish" or "seafood" or "pork" or "beef" or "lamb" or "tofu" or "tempeh";
                })?.Name ?? leadFour;

                concepts.Add(new RecipeConceptSuggestion
                {
                    Key = $"{variantType}-concept-protein-swap",
                    Title = LocalizedWord(language, "Protein-Swap", "Protein swap", "Cambio de proteína", "Troca de proteína"),
                    Summary = LocalizedWord(language, $"Die Hauptproteinquelle wird bewusst ersetzt, zum Beispiel in Richtung {replacementLead}, damit die Variante klar eiweißreicher und eigenständiger wird.", $"The main protein is deliberately swapped, for example towards {replacementLead}, so the variant feels clearly higher in protein and more distinct.", $"La proteína principal se sustituye deliberadamente, por ejemplo hacia {replacementLead}, para que la variante resulte claramente más rica en proteína y más distinta.", $"A proteína principal é trocada de forma deliberada, por exemplo para {replacementLead}, para que a variante fique claramente mais proteica e mais distinta."),
                    Approach = LocalizedWord(language, "ersetzt die bisherige Hauptproteinquelle durch eine andere, sehr proteinreiche Leitidee", "replaces the current main protein with a different high-protein lead idea", "sustituye la proteína principal actual por otra idea principal muy rica en proteína", "substitui a proteína principal atual por outra ideia central muito rica em proteína")
                });
            }

            return concepts.Take(GetTargetConceptCount(variantType, context)).ToList();
        }

        private static string SlugifyConceptKey(string value)
        {
            var cleaned = Regex.Replace((value ?? string.Empty).Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
            return string.IsNullOrWhiteSpace(cleaned) ? "concept" : cleaned;
        }

        private static bool IsProteinSwapConcept(RecipeAiIngredientSuggestionItem concept)
        {
            if (concept == null)
            {
                return false;
            }

            var key = (concept.ConceptKey ?? string.Empty).Trim();
            if (key.Contains("protein-swap", StringComparison.OrdinalIgnoreCase)
                || key.Contains("swap", StringComparison.OrdinalIgnoreCase)
                || key.Contains("replace", StringComparison.OrdinalIgnoreCase)
                || key.Contains("erset", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var title = (concept.ConceptTitle ?? concept.Name ?? string.Empty).Trim();
            if (title.Contains("swap", StringComparison.OrdinalIgnoreCase)
                || title.Contains("protein-swap", StringComparison.OrdinalIgnoreCase)
                || title.Contains("erset", StringComparison.OrdinalIgnoreCase)
                || title.Contains("tausch", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var approach = (concept.ConceptApproach ?? string.Empty).Trim();
            if (approach.Contains("hauptprotein", StringComparison.OrdinalIgnoreCase)
                && (approach.Contains("erset", StringComparison.OrdinalIgnoreCase)
                    || approach.Contains("replace", StringComparison.OrdinalIgnoreCase)
                    || approach.Contains("swap", StringComparison.OrdinalIgnoreCase)
                    || approach.Contains("tausch", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            // Fallback: if the concept plan explicitly marks a main protein item, treat it as swap-capable.
            return (concept.ConceptIngredientPlan ?? new List<RecipeAiConceptIngredientPlanItem>())
                .Any(x => x != null && x.IsMainProtein && x.IngredientId > 0);
        }

        private static int? TryDetectMainProteinIngredientId(RecipeBaseData recipe)
        {
            if (recipe?.Ingredients == null || recipe.Ingredients.Count == 0)
            {
                return null;
            }

            var proteinCandidates = recipe.Ingredients
                .Select(x => x.Ingredient?.IngredientsAndNutrients)
                .Where(x => x != null && x.Id > 0)
                .Select(x => new
                {
                    x!.Id,
                    Cat = NormalizeCategoryKey(x.FoodCategory?.CategoryKey),
                    Protein = x.Protein_a_100g
                })
                .Where(x => x.Cat is "poultry" or "fish" or "seafood" or "pork" or "beef" or "lamb" or "meat")
                .OrderByDescending(x => x.Protein)
                .ToList();

            return proteinCandidates.FirstOrDefault()?.Id;
        }

        private static bool PreviewStillContainsMainProtein(RecipeAiTransformPreview preview, int mainProteinIngredientId)
        {
            if (preview?.Ingredients == null || preview.Ingredients.Count == 0)
            {
                return false;
            }

            return preview.Ingredients.Any(x => x != null && x.IngredientId == mainProteinIngredientId);
        }

        private static bool PreviewContainsReplacementProtein(
            RecipeAiTransformPreview preview,
            IReadOnlyList<RecipeAiIngredientSuggestionItem> candidates)
        {
            if (preview?.Ingredients == null || preview.Ingredients.Count == 0)
            {
                return false;
            }

            var allowedReplacementIds = (candidates ?? Array.Empty<RecipeAiIngredientSuggestionItem>())
                .Where(x =>
                {
                    var cat = NormalizeCategoryKey(x.CategoryKey);
                    return cat is "poultry" or "fish" or "seafood";
                })
                .Select(x => x.IngredientId)
                .Where(x => x > 0)
                .ToHashSet();

            if (allowedReplacementIds.Count == 0)
            {
                return false;
            }

            return preview.Ingredients.Any(x => x != null && x.IngredientId > 0 && allowedReplacementIds.Contains(x.IngredientId));
        }

        private async Task<RecipeAiTransformPreview?> EnsureSelectedConceptAppliedAsync(
            RecipeAiTransformPreview draft,
            RecipeBaseData recipe,
            string variantType,
            string aiProvider,
            string language,
            string userNote,
            int appliedChangeCount,
            IReadOnlyList<RecipeAiTransformStepPlanItem> sourceStepPlan,
            IReadOnlyCollection<string> allowedStepKeys,
            RecipeAiIngredientSuggestionItem selectedConcept,
            IReadOnlyList<RecipeAiIngredientSuggestionItem> selectedIngredients,
            IReadOnlyList<RecipeAiIngredientSuggestionItem> availableVariantIngredients,
            string apiKey,
            CancellationToken cancellationToken)
        {
            // Only enforce when the user chose a swap concept and the result didn't actually swap.
            if (!IsProteinSwapConcept(selectedConcept))
            {
                return null;
            }

            var mainProteinId = TryDetectMainProteinIngredientId(recipe);
            if (mainProteinId == null || mainProteinId.Value <= 0)
            {
                return null;
            }

            var stillHasMainProtein = PreviewStillContainsMainProtein(draft, mainProteinId.Value);
            var hasReplacement = PreviewContainsReplacementProtein(draft, availableVariantIngredients);
            if (!stillHasMainProtein && hasReplacement)
            {
                return null;
            }

            try
            {
                var variantLabel = GetVariantLabel(variantType, language);
                var languageLabel = GetLanguageLabel(language);
                var compactTitle = TruncateForPrompt(recipe.Title, 180);

                var sourceIngredients = BuildIngredientPreview(recipe, language)
                    .Select(x => new { ingredientId = x.IngredientId, name = x.Name, quantity = x.Quantity, measure = x.Measure })
                    .ToList();

                var availableVariantIngredientContext = (availableVariantIngredients ?? Array.Empty<RecipeAiIngredientSuggestionItem>())
                    .Where(x => x.IngredientId > 0 && !string.IsNullOrWhiteSpace(x.Name))
                    .Select(x => new { id = x.IngredientId, name = x.Name, cat = NormalizeCategoryKey(x.CategoryKey) })
                    .Distinct()
                    .ToList();

                var conceptContext = new
                {
                    key = selectedConcept.ConceptKey,
                    title = selectedConcept.ConceptTitle,
                    summary = selectedConcept.ConceptSummary,
                    approach = selectedConcept.ConceptApproach
                };

                var promptRequest = new AiPromptRequest
                {
                    SchemaName = "recipe_ai_transform_concept_alignment_pass",
                    Schema = BuildSchema(),
                    MaxOutputTokens = 7000,
                    GeminiThinkingBudget = 0,
                    ModelTier = AiModelTier.Premium,
                    SystemPrompt = BuildPromptPack(variantType, language, sideSupportInstruction: string.Empty, replacementInstruction: string.Empty, targetConceptCount: 1).AlignmentPassSystemPrompt +
                                  "Nutze fuer neu hinzugefuegte oder ersetzte Zutaten nur ingredientId-Werte aus der Liste Verfuegbare Varianten-Zutaten. " +
                                  "Passe Zeiten/Temperaturen/Fluessigkeiten so an, dass das Rezept kochbar bleibt. " +
                                  "preparationText muss fuer Anfaenger ausfuehrbar sein und die Reihenfolge muss stimmen.",
                    UserPrompt =
                        $"Sprache: {languageLabel}\n" +
                        $"Variante: {variantLabel} ({variantType})\n" +
                        $"Titel: {compactTitle}\n" +
                        $"Original-Zutaten: {JsonSerializer.Serialize(sourceIngredients)}\n" +
                        $"Verfuegbare Varianten-Zutaten: {JsonSerializer.Serialize(availableVariantIngredientContext)}\n" +
                        $"Gewaehltes Konzept: {JsonSerializer.Serialize(conceptContext)}\n" +
                        $"Entwurf (zu reparieren): {JsonSerializer.Serialize(draft)}\n" +
                        "Gib die korrigierte, kochbare Rezeptvariante zurueck (vollstaendig: Zutaten + preparationText)."
                };

                var repaired = await ExecuteStructuredAiCallAsync<RecipeAiTransformPreview>(promptRequest, aiProvider, apiKey, cancellationToken);
                return repaired;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Concept alignment pass failed. Provider={Provider} RecipeId={RecipeId} VariantType={VariantType}", NormalizeAiProvider(aiProvider), recipe?.Id, variantType);
                return null;
            }
        }


        private async Task<T?> ExecuteStructuredOpenAiCallAsync<T>(object requestBody, string apiKey, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, OpenAiEndpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            // Create linked CancellationToken with timeout to prevent hanging requests
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(AiRequestTimeoutSeconds));

            try
            {
                using var response = await _httpClient.SendAsync(request, timeoutCts.Token);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OpenAI structured ingredient call failed: {StatusCode} {Body}", response.StatusCode, responseContent);
                throw new InvalidOperationException(BuildApiErrorMessage(response.StatusCode, responseContent));
            }

            using var document = JsonDocument.Parse(responseContent);

            var wasIncomplete = false;

            // Detect incomplete responses (e.g. max_output_tokens hit)
            if (document.RootElement.TryGetProperty("status", out var statusProp)
                && statusProp.GetString() == "incomplete")
            {
                var reason = document.RootElement.TryGetProperty("incomplete_details", out var details)
                    && details.TryGetProperty("reason", out var reasonProp)
                    ? reasonProp.GetString() ?? "unknown"
                    : "unknown";
                wasIncomplete = true;
                _logger.LogWarning("OpenAI structured call incomplete (reason={Reason}). Trying to parse partial output before fallback.", reason);
            }

            var outputText = TryExtractOutputText(document.RootElement);
            if (string.IsNullOrWhiteSpace(outputText))
            {
                _logger.LogError("OpenAI structured ingredient call returned no usable output text. Response: {Body}", responseContent);
                return default;
            }

                try
                {
                    var parsed = JsonSerializer.Deserialize<T>(outputText, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    if (parsed == null && wasIncomplete)
                    {
                        _logger.LogWarning("OpenAI structured call was incomplete and deserialized to null. Falling back.");
                    }

                    return parsed;
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "OpenAI structured call returned invalid JSON. Falling back. Output: {Output}", outputText);
                    return default;
                }
            }
            catch (OperationCanceledException ex) when (timeoutCts.Token.IsCancellationRequested)
            {
                _logger.LogError(ex, "OpenAI request timed out after {Timeout} seconds.", AiRequestTimeoutSeconds);
                throw new InvalidOperationException($"AI request timed out after {AiRequestTimeoutSeconds} seconds. Please try again.", ex);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error while calling OpenAI API.");
                throw new InvalidOperationException("Network error connecting to AI provider. Please check your connection and try again.", ex);
            }
        }

        private async Task<T?> ExecuteStructuredAiCallAsync<T>(AiPromptRequest promptRequest, string aiProvider, string apiKey, CancellationToken cancellationToken)
        {
            // OpenAI-only at the moment. Tool calling is handled inside the OpenAI runner.
            return await ExecuteStructuredOpenAiPromptCallAsync<T>(promptRequest, apiKey, cancellationToken);
        }

        private async Task<T?> ExecuteStructuredOpenAiPromptCallAsync<T>(AiPromptRequest promptRequest, string apiKey, CancellationToken cancellationToken)
        {
            if (!promptRequest.AllowTools)
            {
                return await ExecuteStructuredOpenAiPromptCallNoToolsAsync<T>(promptRequest, apiKey, cancellationToken);
            }

            return await ExecuteStructuredOpenAiPromptCallWithToolsAsync<T>(promptRequest, apiKey, cancellationToken);
        }

        private sealed record OpenAiToolCall(string CallId, string Name, string ArgumentsJson);

        private async Task<T?> ExecuteStructuredOpenAiPromptCallNoToolsAsync<T>(AiPromptRequest promptRequest, string apiKey, CancellationToken cancellationToken)
        {
            var inputItems = BuildOpenAiInitialInput(promptRequest);
            var requestBody = BuildOpenAiRequestBodyWithoutTools(promptRequest, inputItems);
            var responseMaybe = await ExecuteStructuredOpenAiRawAsync(requestBody, apiKey, cancellationToken);
            var response = responseMaybe!.Value;

            if (!TryExtractFinalOutputText(response, out var outputText) || string.IsNullOrWhiteSpace(outputText))
            {
                throw new InvalidOperationException($"AI returned empty output for schema '{promptRequest.SchemaName}'.");
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<T>(outputText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (parsed == null)
                {
                    throw new InvalidOperationException($"AI returned empty output for schema '{promptRequest.SchemaName}'.");
                }

                return parsed;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "OpenAI structured call returned invalid JSON. Output: {Output}", outputText);
                throw new InvalidOperationException($"AI returned invalid JSON for schema '{promptRequest.SchemaName}'. Output starts with: {(outputText.Length <= 220 ? outputText : outputText.Substring(0, 220) + "...")}");
            }
        }

        private async Task<T?> ExecuteStructuredOpenAiPromptCallWithToolsAsync<T>(AiPromptRequest promptRequest, string apiKey, CancellationToken cancellationToken)
        {
            // Tool calling loop: the model can request DB ingredients (spices, oils, grains, etc.).
            //
            // IMPORTANT: We do NOT rely on previous_response_id continuations here, because they can easily result
            // in "function_call" without the subsequent "function_call_output" showing up (tool outputs get dropped),
            // which matches the "Kein Ausgang" symptom in the OpenAI dashboard.
            //
            // Instead, we keep a compact running input:
            //   system + user + function_call + function_call_output
            // and resend that as `input` on each round until we receive the final JSON-schema output.
            var maxToolRounds = Math.Max(1, promptRequest.MaxToolRounds);

            var runningInput = BuildOpenAiInitialInput(promptRequest);

            for (var round = 0; round <= maxToolRounds; round++)
            {
                var requestBody = BuildOpenAiRequestBody(promptRequest, runningInput);
                var responseMaybe = await ExecuteStructuredOpenAiRawAsync(requestBody, apiKey, cancellationToken);
                var response = responseMaybe!.Value;

                if (TryExtractFinalOutputText(response, out var outputText) && !string.IsNullOrWhiteSpace(outputText))
                {
                    try
                    {
                        var parsed = JsonSerializer.Deserialize<T>(outputText, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        if (parsed == null)
                        {
                            throw new InvalidOperationException($"AI returned empty output for schema '{promptRequest.SchemaName}'.");
                        }

                        // Some failure modes still deserialize but are clearly not our schema output
                        // (e.g., API response JSON got extracted instead of the model JSON). Catch that early with a helpful message.
                        if (string.Equals(promptRequest.SchemaName, "recipe_ai_concept_pick", StringComparison.OrdinalIgnoreCase)
                            && parsed is RecipeConceptPickResponse conceptPick
                            && (conceptPick.Concepts == null || conceptPick.Concepts.Count == 0))
                        {
                            var snippet = outputText.Length <= 900 ? outputText : outputText.Substring(0, 900) + "...";
                            throw new InvalidOperationException($"AI returned no concepts for schema '{promptRequest.SchemaName}'. Output starts with: {snippet}");
                        }

                        if (string.Equals(promptRequest.SchemaName, "recipe_ai_ingredient_category_plan", StringComparison.OrdinalIgnoreCase)
                            && parsed is IngredientCategoryPlanResponse categoryPlan
                            && (categoryPlan.PreferredCategoryKeys == null || categoryPlan.PreferredCategoryKeys.Count == 0)
                            && string.IsNullOrWhiteSpace(categoryPlan.Strategy))
                        {
                            var snippet = outputText.Length <= 900 ? outputText : outputText.Substring(0, 900) + "...";
                            throw new InvalidOperationException($"AI returned an empty category plan for schema '{promptRequest.SchemaName}'. Output starts with: {snippet}");
                        }

                        return parsed;
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "OpenAI tool-call loop returned invalid JSON. Output: {Output}", outputText);
                        var snippet = outputText.Length <= 800 ? outputText : outputText.Substring(0, 800) + "...";
                        throw new InvalidOperationException($"AI returned invalid JSON for schema '{promptRequest.SchemaName}'. Output starts with: {snippet}", ex);
                    }
                }

                var toolCalls = ExtractToolCalls(response);
                if (toolCalls.Count == 0)
                {
                    _logger.LogWarning("OpenAI response had neither final output nor tool calls. Schema={SchemaName}", promptRequest.SchemaName);
                    throw new InvalidOperationException($"AI returned neither final output nor tool calls for schema '{promptRequest.SchemaName}'.");
                }

                foreach (var call in toolCalls)
                {
                    // Echo the function call into the next input so OpenAI can map call_id -> output reliably.
                    runningInput.Add(new
                    {
                        type = "function_call",
                        call_id = call.CallId,
                        name = call.Name,
                        arguments = call.ArgumentsJson
                    });

                    var output = await ExecuteToolCallAsync(call, cancellationToken);
                    runningInput.Add(new
                    {
                        type = "function_call_output",
                        call_id = call.CallId,
                        output = output
                    });
                }
            }

            _logger.LogWarning("OpenAI tool calling exceeded max rounds for Schema={SchemaName}.", promptRequest.SchemaName);
            throw new InvalidOperationException($"AI tool-calling exceeded max rounds for schema '{promptRequest.SchemaName}'.");
        }

        private List<object> BuildOpenAiInitialInput(AiPromptRequest promptRequest)
        {
            var allowedToolCategoryKeys = (promptRequest.AllowedToolCategoryKeys ?? Array.Empty<string>())
                .Select(NormalizeCategoryKey)
                .Where(x => ToolCategoryKeySet.Contains(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (allowedToolCategoryKeys.Length == 0)
            {
                allowedToolCategoryKeys = ToolCategoryKeys;
            }

            var categoryKeyListForPrompt = string.Join(", ", allowedToolCategoryKeys);
            return new List<object>
            {
                new
                {
                    role = "system",
                    content = new[]
                    {
                        new
                        {
                            type = "input_text",
                            text = promptRequest.SystemPrompt +
                                   " Du darfst bei Bedarf die Funktion get_ingredients(categoryKeys, take) aufrufen, um zusaetzliche DB-Zutaten (Gewuerze/Kraeuter/Oel/Fette/Getreide usw.) zu laden. " +
                                    $"Erlaubte categoryKeys fuer get_ingredients sind genau diese: {categoryKeyListForPrompt}. " +
                                    "Wenn du eine Zutat verwendest, muss sie als ingredientId in ingredients[] vorkommen; nutze dafuer get_ingredients statt frei zu erfinden."
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
                            text = promptRequest.UserPrompt
                        }
                    }
                }
            };
        }

        private string ResolveOpenAiModel(AiModelTier tier)
            => tier == AiModelTier.Premium ? OpenAiPremiumModel : OpenAiCheapModel;

        private object BuildOpenAiRequestBody(AiPromptRequest promptRequest, List<object> inputItems)
        {
            var allowedToolCategoryKeys = (promptRequest.AllowedToolCategoryKeys ?? Array.Empty<string>())
                .Select(NormalizeCategoryKey)
                .Where(x => ToolCategoryKeySet.Contains(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (allowedToolCategoryKeys.Length == 0)
            {
                allowedToolCategoryKeys = ToolCategoryKeys;
            }

            var tools = new object[]
            {
                new
                {
                    type = "function",
                    name = "get_ingredients",
                    description = "Fetch ingredients from the app database by category keys (e.g. spices, herbs, oils, fats, grains, rice, pasta). Use this when you need additional pantry items or sides. Return only IDs and short nutrition fields.",
                    strict = true,
                    parameters = new
                    {
                        type = "object",
                        additionalProperties = false,
                        // OpenAI function/tool schemas require `required` to include every key in `properties`.
                        // Optional values must be represented as nullable types, not by omitting them.
                        required = new[] { "language", "categoryKeys", "take", "excludeIngredientIds" },
                        properties = new
                        {
                            language = new { type = new[] { "string", "null" } },
                            categoryKeys = new
                            {
                                type = "array",
                                items = new { type = "string", @enum = allowedToolCategoryKeys },
                                minItems = 1,
                                maxItems = 12
                            },
                            take = new { type = new[] { "integer", "null" } },
                            excludeIngredientIds = new
                            {
                                type = new[] { "array", "null" },
                                items = new { type = "integer" }
                            }
                        }
                    }
                }
            };

            return new
            {
                model = ResolveOpenAiModel(promptRequest.ModelTier),
                max_output_tokens = promptRequest.MaxOutputTokens,
                input = inputItems.ToArray(),
                tools,
                text = new
                {
                    format = new
                    {
                        type = "json_schema",
                        name = promptRequest.SchemaName,
                        strict = true,
                        schema = promptRequest.Schema
                    }
                }
            };
        }

        private object BuildOpenAiRequestBodyWithoutTools(AiPromptRequest promptRequest, List<object> inputItems)
        {
            return new
            {
                model = ResolveOpenAiModel(promptRequest.ModelTier),
                max_output_tokens = promptRequest.MaxOutputTokens,
                input = inputItems.ToArray(),
                text = new
                {
                    format = new
                    {
                        type = "json_schema",
                        name = promptRequest.SchemaName,
                        strict = true,
                        schema = promptRequest.Schema
                    }
                }
            };
        }

        private object BuildOpenAiRequestBody(AiPromptRequest promptRequest, string? previousResponseId, List<object>? pendingToolOutputs)
        {
            var categoryKeyListForPrompt = string.Join(", ", ToolCategoryKeys);
            var tools = new object[]
            {
                new
                {
                    type = "function",
                    name = "get_ingredients",
                    description = "Fetch ingredients from the app database by category keys (e.g. spices, herbs, oils, fats, grains, rice, pasta). Use this when you need additional pantry items or sides. Return only IDs and short nutrition fields.",
                    parameters = new
                    {
                        type = "object",
                        additionalProperties = false,
                        required = new[] { "categoryKeys" },
                        properties = new
                        {
                            language = new { type = "string" },
                            categoryKeys = new
                            {
                                type = "array",
                                items = new { type = "string", @enum = ToolCategoryKeys },
                                minItems = 1,
                                maxItems = 12
                            },
                            take = new { type = "integer" },
                            excludeIngredientIds = new
                            {
                                type = "array",
                                items = new { type = "integer" }
                            }
                        }
                    }
                }
            };

            if (!string.IsNullOrWhiteSpace(previousResponseId))
            {
                // Continuation request: ONLY provide tool outputs.
                // The response already carries the original instructions/schema/tool definitions.
                // Including tools/text.format again can lead to 400s and "no tool output" in the OpenAI dashboard.
                var toolOutputs = pendingToolOutputs ?? new List<object>();

                return new
                {
                    model = ResolveOpenAiModel(promptRequest.ModelTier),
                    previous_response_id = previousResponseId,
                    input = toolOutputs
                };
            }

            // First request: send system + user prompt.
            return new
            {
                model = ResolveOpenAiModel(promptRequest.ModelTier),
                max_output_tokens = promptRequest.MaxOutputTokens,
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
                                text = promptRequest.SystemPrompt +
                                       " Du darfst bei Bedarf die Funktion get_ingredients(categoryKeys, take) aufrufen, um zusaetzliche DB-Zutaten (Gewuerze/Kraeuter/Oel/Fette/Getreide usw.) zu laden. " +
                                       $"Erlaubte categoryKeys fuer get_ingredients sind genau diese: {categoryKeyListForPrompt}. " +
                                       "Wenn du eine Zutat verwendest, muss sie als ingredientId in ingredients[] vorkommen; nutze dafuer get_ingredients statt frei zu erfinden."
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
                                text = promptRequest.UserPrompt
                            }
                        }
                    }
                },
                tools,
                text = new
                {
                    format = new
                    {
                        type = "json_schema",
                        name = promptRequest.SchemaName,
                        strict = true,
                        schema = promptRequest.Schema
                    }
                }
            };
        }

        private async Task<JsonElement?> ExecuteStructuredOpenAiRawAsync(object requestBody, string apiKey, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, OpenAiEndpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(AiRequestTimeoutSeconds));

            using var response = await _httpClient.SendAsync(request, timeoutCts.Token);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OpenAI tool-call request failed: {StatusCode} {Body}", response.StatusCode, responseContent);
                throw new InvalidOperationException(BuildApiErrorMessage(response.StatusCode, responseContent));
            }

            using var doc = JsonDocument.Parse(responseContent);
            return doc.RootElement.Clone();
        }

        private static string? TryExtractResponseId(JsonElement root)
        {
            if (root.TryGetProperty("id", out var idProp))
            {
                return idProp.GetString();
            }
            return null;
        }

        private static bool TryExtractFinalOutputText(JsonElement root, out string? outputText)
        {
            outputText = null;

            // Reuse existing extraction logic first.
            outputText = TryExtractOutputText(root);
            if (!string.IsNullOrWhiteSpace(outputText))
            {
                return true;
            }

            return false;
        }

        private static List<OpenAiToolCall> ExtractToolCalls(JsonElement root)
        {
            var result = new List<OpenAiToolCall>();
            if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var item in output.EnumerateArray())
            {
                if (!item.TryGetProperty("type", out var typeProp))
                {
                    continue;
                }

                var type = typeProp.GetString() ?? string.Empty;
                if (!string.Equals(type, "function_call", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var callId = item.TryGetProperty("call_id", out var callIdProp) ? (callIdProp.GetString() ?? string.Empty) : string.Empty;
                var name = item.TryGetProperty("name", out var nameProp) ? (nameProp.GetString() ?? string.Empty) : string.Empty;
                var args = item.TryGetProperty("arguments", out var argsProp) ? (argsProp.GetString() ?? "{}") : "{}";

                if (!string.IsNullOrWhiteSpace(callId) && !string.IsNullOrWhiteSpace(name))
                {
                    result.Add(new OpenAiToolCall(callId, name, args));
                }
            }

            return result;
        }

        private async Task<string> ExecuteToolCallAsync(OpenAiToolCall call, CancellationToken cancellationToken)
        {
            if (!string.Equals(call.Name, "get_ingredients", StringComparison.OrdinalIgnoreCase))
            {
                return "[]";
            }

            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson);
                var root = doc.RootElement;

                var rawCategoryKeys = new List<string>();
                var categoryKeys = new List<string>();
                if (root.TryGetProperty("categoryKeys", out var cats) && cats.ValueKind == JsonValueKind.Array)
                {
                    foreach (var c in cats.EnumerateArray())
                    {
                        var key = (c.GetString() ?? string.Empty).Trim();
                        if (!string.IsNullOrWhiteSpace(key))
                        {
                            rawCategoryKeys.Add(key);
                            var normalized = NormalizeCategoryKey(key);
                            if (ToolCategoryKeySet.Contains(normalized))
                            {
                                categoryKeys.Add(normalized);
                            }
                        }
                    }
                }

                categoryKeys = categoryKeys
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(12)
                    .ToList();

                _logger.LogInformation(
                    "Tool get_ingredients received raw categoryKeys=[{RawKeys}] normalized categoryKeys=[{NormalizedKeys}]",
                    string.Join(", ", rawCategoryKeys),
                    string.Join(", ", categoryKeys));

                if (categoryKeys.Count == 0)
                {
                    _logger.LogWarning(
                        "Tool get_ingredients returned empty because no valid categoryKeys remained after normalization. Raw categoryKeys=[{RawKeys}]",
                        string.Join(", ", rawCategoryKeys));
                    return "[]";
                }

                var language = "de";
                if (root.TryGetProperty("language", out var langProp) && langProp.ValueKind == JsonValueKind.String)
                {
                    var rawLang = (langProp.GetString() ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(rawLang))
                    {
                        language = rawLang;
                    }
                }

                var take = 200;
                if (root.TryGetProperty("take", out var takeProp) && takeProp.ValueKind == JsonValueKind.Number && takeProp.TryGetInt32(out var t))
                {
                    take = t;
                }
                take = Math.Clamp(take, 1, 400);

                var excludeIds = new List<int>();
                if (root.TryGetProperty("excludeIngredientIds", out var ex) && ex.ValueKind == JsonValueKind.Array)
                {
                    foreach (var v in ex.EnumerateArray())
                    {
                        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var id) && id > 0)
                        {
                            excludeIds.Add(id);
                        }
                    }
                }

                var candidates = await _ingredientResolverService.GetCandidatesForCategoriesAsync(
                    language: language,
                    allowedCategoryKeys: categoryKeys,
                    excludeIngredientIds: excludeIds,
                    take: take,
                    cancellationToken: cancellationToken);

                _logger.LogInformation(
                    "Tool get_ingredients resolved {CandidateCount} candidates for language={Language}, take={Take}, excludeCount={ExcludeCount}, categories=[{Categories}]",
                    candidates.Count,
                    language,
                    take,
                    excludeIds.Count,
                    string.Join(", ", categoryKeys));

                if (candidates.Count == 0)
                {
                    _logger.LogWarning(
                        "Tool get_ingredients returned 0 candidates for categories=[{Categories}]",
                        string.Join(", ", categoryKeys));
                }
                else
                {
                    _logger.LogInformation(
                        "Tool get_ingredients sample candidates: {Sample}",
                        string.Join(", ", candidates.Take(8).Select(x => $"{x.IngredientId}:{x.Name}:{x.CategoryKey}")));
                }

                var compact = candidates.Select(x => new
                {
                    id = x.IngredientId,
                    name = x.Name,
                    cat = x.CategoryKey,
                    p = x.ProteinPer100g,
                    carb = x.CarbsPer100g,
                    fat = x.FatPer100g,
                    kcal = x.CaloriesPer100g
                }).ToList();

                return JsonSerializer.Serialize(compact);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Tool get_ingredients failed.");
                return "[]";
            }
        }

        private static string NormalizeAiProvider(string? aiProvider)
        {
            return "openai";
        }

        private string? ResolveApiKey(string aiProvider)
        {
            return Environment.GetEnvironmentVariable("SecretKeyOpenAi")
                ?? _configuration["SecretKeyOpenAi"];
        }

        private static string GetMissingApiKeyMessage(string aiProvider)
        {
            return "Die Umgebungsvariable oder Konfiguration 'SecretKeyOpenAi' ist nicht gesetzt.";
        }

        private static string GetProviderDisplayName(string aiProvider)
        {
            return "OpenAI";
        }

        private string BuildCompactCategorySelectionContext(RecipeBaseData recipe, string language, IngredientSelectionContext context)
        {
            var ingredientNames = recipe.Ingredients?
                .Where(x => x.Ingredient?.IngredientsAndNutrients != null)
                .Select(x => language switch
                {
                    "en" => x.Ingredient!.IngredientsAndNutrients!.Name_EN,
                    "pt" => x.Ingredient!.IngredientsAndNutrients!.Name_PRT,
                    "es" => x.Ingredient!.IngredientsAndNutrients!.Name_ESP,
                    _ => x.Ingredient!.IngredientsAndNutrients!.Name_DE
                })
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(16)
                .ToList() ?? new List<string>();

            var proteinHint = context.MainProteinCategory switch
            {
                "meat" => LocalizedWord(language, "Hauptprotein: Fleisch", "main protein: meat", "proteína principal: carne", "proteína principal: carne"),
                "fish" => LocalizedWord(language, "Hauptprotein: Fisch", "main protein: fish", "proteína principal: pescado", "proteína principal: peixe"),
                "protein" => LocalizedWord(language, "Hauptprotein vorhanden", "main protein present", "hay proteína principal", "há proteína principal"),
                _ => LocalizedWord(language, "kein klares Hauptprotein", "no clear main protein", "sin proteína principal clara", "sem proteína principal clara")
            };

            var carbHint = context.HasCarbSide
                ? LocalizedWord(language, "Stärkebeilage vorhanden", "starchy side present", "hay guarnición con almidón", "há acompanhamento com amido")
                : LocalizedWord(language, "keine klare Stärkebeilage", "no clear starchy side", "sin guarnición clara con almidón", "sem acompanhamento rico em amido");

            return $"Titel: {recipe.Title}; Kategorie: {recipe.Category}; Portionen: {recipe.PersonCount}; Zeit: {recipe.PreparationTime} min; {proteinHint}; {carbHint}; Zutaten: {string.Join(", ", ingredientNames)}";
        }

        private static List<string> GetDefaultCategoryKeysForVariant(string variantType, IngredientSelectionContext? context = null)
        {
            return variantType switch
            {
                // High-protein: keep a wide pool (DB will filter later), but still prioritize add-ons over a second main protein.
                // We include nuts/seafood so the AI can diversify concepts (guards later prevent weird combos like fish-in-pork-roast for add).
                "highprotein" when context?.HasPrimaryProtein == true => new List<string>
                {
                    "legumes", "eggs", "yogurt", "cheese",
                    "tofu", "tempeh",
                    "seeds", "nuts", "mushrooms",
                    "poultry", "fish", "seafood"
                },
                "highprotein" => new List<string>
                {
                    "legumes", "eggs", "yogurt", "cheese",
                    "tofu", "tempeh",
                    "seeds", "nuts",
                    "poultry", "fish", "seafood"
                },
                "lowcarb" when context?.PreferSideReplacement == true => new List<string> { "vegetables", "leafy_greens", "mushrooms" },
                // Low-carb: allow realistic "fat/protein helpers" too (cheese/yogurt/nuts/seeds) so the AI can create satisfying swaps.
                "lowcarb" => new List<string> { "vegetables", "leafy_greens", "mushrooms", "eggs", "tofu", "cheese", "yogurt", "nuts", "seeds", "fish", "seafood", "poultry" },
                "vegan" => new List<string> { "legumes", "tofu", "tempeh", "vegetables", "leafy_greens", "mushrooms", "nuts", "seeds" },
                "mealprep" when context?.HasCarbSide == true => new List<string> { "vegetables", "leafy_greens", "mushrooms", "legumes" },
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

            var hasFishMainProtein = categoryKeys.Any(x => x is "fish" or "seafood");
            var hasMeatMainProtein = categoryKeys.Any(x => x is "beef" or "pork" or "lamb" or "poultry") || names.Any(ContainsAnyKeyword("schwein", "pork", "rind", "beef", "kalb", "veal", "huhn", "hähn", "huehn", "chicken", "pute", "turkey", "lamm", "lamb"));
            var hasPrimaryProtein = hasPrimaryProteinCategory || hasFishMainProtein || hasMeatMainProtein;
            var mainProteinCategory = hasFishMainProtein ? "fish" : hasMeatMainProtein ? "meat" : hasPrimaryProteinCategory ? "protein" : string.Empty;

            return new IngredientSelectionContext
            {
                HasPrimaryProtein = hasPrimaryProtein,
                HasMeatMainProtein = hasMeatMainProtein,
                HasFishMainProtein = hasFishMainProtein,
                HasPotatoSide = hasPotatoSide,
                HasRiceOrPastaSide = hasRiceOrPastaSide,
                HasCarbSide = hasPotatoSide || hasRiceOrPastaSide,
                PreferSideReplacement = hasPrimaryProtein && (hasPotatoSide || hasRiceOrPastaSide),
                MainProteinCategory = mainProteinCategory
            };
        }

        private static string GetDefaultIngredientStrategy(string variantType, IngredientSelectionContext? context)
        {
            return variantType switch
            {
                "lowcarb" when context?.PreferSideReplacement == true => "replace",
                "lowcarb" => "hybrid",
                "highprotein" when context?.HasPrimaryProtein == true => "hybrid",
                "highprotein" => "add",
                "vegan" => "replace",
                "mealprep" when context?.HasCarbSide == true => "replace",
                "mealprep" => "add",
                _ => "add"
            };
        }

        private static string NormalizeIngredientStrategy(string? strategy, string variantType, IngredientSelectionContext? context)
        {
            var normalized = (strategy ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "add" => "add",
                "replace" => "replace",
                "remove" => "remove",
                "hybrid" => "hybrid",
                _ => GetDefaultIngredientStrategy(variantType, context)
            };
        }

        private static string GetIngredientSelectionStrategyHint(string variantType, string strategy, IngredientSelectionContext context, string language)
        {
            if (string.Equals(variantType, "lowcarb", StringComparison.OrdinalIgnoreCase) && context.PreferSideReplacement)
            {
                return LocalizedWord(language,
                    "Bevorzuge passende staerkearme Ersatzideen fuer bestehende Beilagen oder Strukturen und halte das Gericht dabei vollstaendig und saettigend. Einfaches Weglassen ohne stimmigen Ersatz ist nur die Notfall-Option.",
                    "Prefer fitting low-carb replacements for existing sides or structures while keeping the dish complete and satisfying. Simply removing parts without a coherent replacement should only be the fallback option.",
                    "Prefiere sustituciones bajas en carbohidratos para las guarniciones o estructuras existentes y mantén el plato completo y saciante. Quitar partes sin un reemplazo coherente solo debe ser la opción de emergencia.",
                    "Prefere substituições low carb para acompanhamentos ou estruturas já existentes e mantém o prato completo e saciante. Remover partes sem um substituto coerente deve ser apenas a opção de recurso.");
            }

            if (string.Equals(variantType, "highprotein", StringComparison.OrdinalIgnoreCase) && context.HasPrimaryProtein)
            {
                if (!context.HasCarbSide)
                {
                    return LocalizedWord(language,
                        "Staerke das bestehende Gericht mit passenden Protein-Ergaenzungen und halte das Original klar erkennbar. Wenn noch keine klare Beilage vorhanden ist, darf hoechstens ein Konzept eine proteinreiche Beilage oder Saettigungs-Idee vorschlagen; die uebrigen Konzepte sollen das Hauptgericht selbst gezielt verbessern und nicht in ein neues Nebenrezept abdriften.",
                        "Strengthen the existing dish with fitting protein additions and keep the original dish clearly recognizable. If no clear side is present yet, at most one concept may propose a protein-rich side or hearty add-on; the remaining concepts should improve the main dish itself and must not drift into a new side recipe.",
                        "Refuerza el plato con proteínas complementarias y mantén el plato original claramente reconocible. Si aún no hay una guarnición clara, como mucho un concepto puede proponer una guarnición o acompañamiento rico en proteína; los demás deben mejorar el plato principal y no desviarse hacia una receta lateral nueva.",
                        "Reforça o prato com proteínas complementares e mantém o prato original claramente reconhecível. Se ainda não existir um acompanhamento claro, no máximo um conceito pode propor um acompanhamento ou extra rico em proteína; os restantes devem melhorar o prato principal e não desviar para uma nova receita secundária.");
                }

                return LocalizedWord(language,
                    "Staerke das bestehende Gericht mit passenden Protein-Ergaenzungen. Die Konzepte sollen das Originalgericht respektieren, kulinarisch nah bleiben und zuerst echte Verbesserungen am bestehenden Gericht zeigen statt die Hauptproteinquelle vorschnell auszutauschen.",
                    "Strengthen the existing dish with fitting protein additions. The concepts should respect the original dish, stay culinarly close, and first show real improvements to the existing dish instead of rushing into swapping the main protein.",
                    "Refuerza el plato con proteínas complementarias. Los conceptos deben respetar el plato original, mantenerse culinariamente cerca y mostrar primero mejoras reales del plato existente en lugar de cambiar demasiado pronto la proteína principal.",
                    "Reforça o prato com proteínas complementares. Os conceitos devem respeitar o prato original, manter-se culinariamente próximos e mostrar primeiro melhorias reais ao prato existente em vez de trocar cedo demais a proteína principal.");
            }

            if (string.Equals(variantType, "lowcarb", StringComparison.OrdinalIgnoreCase) && !context.HasCarbSide)
            {
                return LocalizedWord(language,
                    "Da noch keine klare Beilage vorhanden ist, duerfen hoechstens zwei Vorschlaege als saettigende low-carb-Beilagen- oder Zusatzideen funktionieren. Die anderen Konzepte sollen das Hauptgericht selbst low-carb-tauglicher machen und nicht in ein Nebenrezept abdriften.",
                    "Since no clear side is present yet, at most two suggestions may work as satisfying low-carb side or add-on ideas. The remaining concepts should make the main dish itself more low-carb-friendly instead of drifting into a side recipe.",
                    "Como aún no hay una guarnición clara, como mucho dos sugerencias pueden funcionar como guarniciones o complementos low carb y saciantes. Los demás conceptos deben hacer que el plato principal sea más apto para low carb y no desviarse hacia una receta secundaria.",
                    "Como ainda não existe um acompanhamento claro, no máximo duas sugestões podem funcionar como acompanhamentos ou extras low carb e saciantes. Os restantes conceitos devem tornar o prato principal mais adequado a low carb e não desviar para uma receita secundária.");
            }

            if (string.Equals(variantType, "vegan", StringComparison.OrdinalIgnoreCase) && !context.HasCarbSide)
            {
                return LocalizedWord(language,
                    "Da noch keine klare Beilage vorhanden ist, duerfen hoechstens zwei Vorschlaege als vegane, saettigende Beilagen- oder Zusatzideen funktionieren. Die anderen Konzepte sollen vor allem zeigen, wie das Hauptgericht selbst stilnah und kulinarisch plausibel veganisiert wird.",
                    "Since no clear side is present yet, at most two suggestions may work as vegan, satisfying side or add-on ideas. The remaining concepts should mainly show how the main dish itself can be veganized in a style-faithful and culinarily plausible way.",
                    "Como aún no hay una guarnición clara, como mucho dos sugerencias pueden funcionar como guarniciones o complementos veganos y saciantes. Los demás conceptos deben mostrar sobre todo cómo veganizar el plato principal de forma fiel al estilo y culinariamente plausible.",
                    "Como ainda não existe um acompanhamento claro, no máximo duas sugestões podem funcionar como acompanhamentos ou extras veganos e saciantes. Os restantes conceitos devem mostrar sobretudo como veganizar o prato principal de forma fiel ao estilo e culinariamente plausível.");
            }

            if (string.Equals(variantType, "mealprep", StringComparison.OrdinalIgnoreCase))
            {
                if (context.HasCarbSide)
                {
                    return LocalizedWord(language,
                        "Das Rezept hat bereits eine Beilage. Denke meal-prep-freundlich und optimiere vor allem Ablauf, Portionierbarkeit, Haltbarkeit und Aufwaermbarkeit, statt neue Zusatzzutaten zu erzwingen. Zeige moeglichst nah am Original, wie das Gericht fuer mehrere Mahlzeiten vorbereitet und gelagert werden kann.",
                        "The recipe already has a side. Think in a meal-prep-friendly way and optimize workflow, portionability, storage life and reheating instead of forcing extra ingredients. Stay as close as possible to the original while showing how the dish can be prepared and stored for several meals.",
                        "La receta ya tiene guarnición. Piensa en meal prep y optimiza sobre todo flujo, facilidad de porcionado, conservación y recalentado en lugar de forzar ingredientes extra. Mantente lo más cerca posible del original mostrando cómo preparar y guardar el plato para varias comidas.",
                        "A receita já tem acompanhamento. Pensa em meal prep e otimiza sobretudo fluxo, facilidade de porcionamento, conservação e reaquecimento em vez de forçar ingredientes extra. Mantém-te o mais próximo possível do original e mostra como preparar e guardar o prato para várias refeições.");
                }

                return LocalizedWord(language,
                    "Wenn noch keine Beilage vorhanden ist, schlage vor allem passende meal-prep-freundliche Komponenten vor, die das Gericht als vorbereitbare Mehrportionen-Mahlzeit staerker machen. Die Idee soll trotzdem wie das gleiche Gericht wirken und nicht in ein anderes Rezept kippen.",
                    "If no side is present yet, mainly suggest suitable meal-prep-friendly components that make the dish work better as a prep-ahead multi-portion meal. The idea should still feel like the same dish rather than turning into a different recipe.",
                    "Si aún no hay guarnición, sugiere sobre todo componentes aptos para meal prep que hagan que el plato funcione mejor como comida de varias porciones preparada con antelación. La idea debe seguir sintiéndose como el mismo plato y no convertirse en otra receta.",
                    "Se ainda não existir acompanhamento, sugere sobretudo componentes adequados para meal prep que façam o prato funcionar melhor como refeição de várias porções preparada com antecedência. A ideia deve continuar a parecer o mesmo prato e não transformar-se noutra receita.");
            }

            if (string.Equals(strategy, "replace", StringComparison.OrdinalIgnoreCase))
            {
                return LocalizedWord(language,
                    "Denke vor allem in sinnvollen Ersatzideen fuer bestehende Bestandteile des Gerichts.",
                    "Think primarily in sensible replacements for existing parts of the dish.",
                    "Piensa sobre todo en sustituciones sensatas para componentes existentes del plato.",
                    "Pensa sobretudo em substituições sensatas para componentes já existentes do prato.");
            }

            if (string.Equals(strategy, "remove", StringComparison.OrdinalIgnoreCase))
            {
                return LocalizedWord(language,
                    "Waehle Zutaten, die nur dann sinnvoll sind, wenn sie das Gericht vereinfachen oder ueberfluessige Teile ersetzen helfen.",
                    "Choose ingredients only when they help simplify the dish or replace unnecessary parts.",
                    "Elige ingredientes solo si ayudan a simplificar el plato o a sustituir partes innecesarias.",
                    "Escolhe ingredientes apenas se ajudarem a simplificar o prato ou a substituir partes desnecessárias.");
            }

            return LocalizedWord(language,
                "Waehle Zutaten, die das Gericht sinnvoll unterstuetzen und kulinarisch glaubwuerdig erweitern.",
                "Choose ingredients that support the dish sensibly and extend it in a culinarily believable way.",
                "Elige ingredientes que apoyen el plato de forma sensata y lo amplíen de manera creíble.",
                "Escolhe ingredientes que apoiem o prato de forma sensata e o ampliem de modo culinariamente convincente.");
        }

        private static string GetDietVariantSideSupportInstruction(string variantType, IngredientSelectionContext context)
        {
            if (context.HasCarbSide)
            {
                return string.Empty;
            }

            return variantType.ToLowerInvariant() switch
            {
                "highprotein" => "Wenn bei highprotein noch keine klare Beilage im Rezept vorhanden ist, soll mindestens ein Konzept bewusst eine sehr proteinreiche Beilage oder Sattmacher-Idee enthalten. ",
                "lowcarb" => "Wenn bei lowcarb noch keine klare Beilage im Rezept vorhanden ist, soll mindestens ein Konzept bewusst eine saettigende low-carb-Beilage oder Zusatzidee enthalten. ",
                "vegan" => "Wenn bei vegan noch keine klare Beilage im Rezept vorhanden ist, soll mindestens ein Konzept bewusst eine vegane, saettigende Beilage oder Zusatzidee enthalten. ",
                _ => string.Empty
            };
        }

        private static string GetDietVariantSideSelectionUserInstruction(string variantType, IngredientSelectionContext context)
        {
            if (context.HasCarbSide)
            {
                return string.Empty;
            }

            return variantType.ToLowerInvariant() switch
            {
                "highprotein" => "Da noch keine klare Beilage vorhanden ist, soll mindestens eine Rezeptidee eine sehr proteinreiche Beilage oder Sattmacher-Idee fuer das Gericht vorschlagen. ",
                "lowcarb" => "Da noch keine klare Beilage vorhanden ist, soll mindestens eine Rezeptidee eine saettigende low-carb-Beilage oder Zusatzidee fuer das Gericht vorschlagen. ",
                "vegan" => "Da noch keine klare Beilage vorhanden ist, soll mindestens eine Rezeptidee eine vegane, saettigende Beilage oder Zusatzidee fuer das Gericht vorschlagen. ",
                _ => string.Empty
            };
        }

        private static int GetTargetConceptCount(string variantType, IngredientSelectionContext context)
        {
            var normalized = NormalizeVariantType(variantType);
            return (normalized is "highprotein" or "lowcarb" or "mealprep") && context.HasPrimaryProtein
                ? 4
                : 3;
        }

        private static string GetSwapConceptInstruction(string variantType, string strategy, IngredientSelectionContext context, string language)
        {
            var normalized = NormalizeVariantType(variantType);
            if (normalized == "vegan" || !context.HasPrimaryProtein)
            {
                return string.Empty;
            }

            if (normalized == "highprotein" && string.Equals(strategy, "add", StringComparison.OrdinalIgnoreCase))
            {
                return LocalizedWord(
                    language,
                    "Bei High-Protein mit Ergaenzungsfokus darf die Hauptproteinquelle nicht einfach ausgetauscht werden. Bewahre das urspruengliche Hauptprotein und staerke das Gericht zuerst ueber passende, mengenrelevante Protein-Ergaenzungen. Ein kompletter Protein-Swap ist hier nicht gewuenscht. ",
                    "For high-protein with an add/boost focus, do not simply replace the main protein. Keep the original lead protein and strengthen the dish first through fitting, quantity-relevant protein additions. A full protein swap is not desired here. ",
                    "En high protein con enfoque de complemento, no sustituyas simplemente la proteína principal. Mantén la proteína original y refuerza el plato primero con añadidos proteicos adecuados y relevantes en cantidad. Aquí no se desea un cambio completo de proteína. ",
                    "Em high protein com foco em complemento, não substituas simplesmente a proteína principal. Mantém a proteína original e reforça o prato primeiro com adições proteicas adequadas e relevantes em quantidade. Aqui não se pretende uma troca completa da proteína. ");
            }

            return LocalizedWord(
                language,
                "Eines der Konzepte muss bewusst als Swap gedacht sein und die bisherige Hauptproteinquelle durch eine andere passende Leitidee ersetzen (z.B. Schwein zu Hähnchen oder Fisch). Die übrigen Konzepte sollen das ursprüngliche Hauptprotein nicht grundlos verdrängen. ",
                "One concept must deliberately act as a swap and replace the current main protein with a different fitting lead idea (e.g. pork to chicken or fish). The remaining concepts should not displace the original main protein without a good reason. ",
                "Uno de los conceptos debe funcionar deliberadamente como cambio y sustituir la proteína principal actual por otra idea principal adecuada (p.ej. cerdo por pollo o pescado). Los demás conceptos no deben desplazar la proteína principal original sin una buena razón. ",
                "Um dos conceitos deve funcionar deliberadamente como troca e substituir a proteína principal atual por outra ideia central adequada (ex.: porco por frango ou peixe). Os restantes conceitos não devem deslocar a proteína principal original sem uma boa razão. ");
        }

        private static string BuildHighProteinGoalInstruction(RecipeBaseData recipe, string variantType, string language)
        {
            if (!string.Equals(variantType, "highprotein", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            var (baselineProteinTotal, baselineProteinPerPortion, portions) = TryEstimateBaselineProtein(recipe);
            if (baselineProteinPerPortion <= 0m || portions <= 0)
            {
                return LocalizedWord(
                    language,
                    "Ziel: Erhoehe den Proteinanteil pro Portion mindestens um 15% (besser 20%) gegenueber dem Original, ohne die Kochbarkeit zu gefaehrden. Das Ausgangsgericht muss klar erkennbar bleiben. Vermeide reine Garnituren (z.B. nur Parmesan/Seeds) als einzigen Proteinhebel; nimm stattdessen echte, mengenrelevante Anpassungen vor. Wenn du etwas nur als Topping in Mini-Mengen (<10 g pro Portion) einsetzen wuerdest, zaehlt es nicht als Protein-Hebel, sondern hoechstens als Bonus. Schlage keine Ideen vor, die eher wie ein neues Beilagen-Rezept oder Stilbruch wirken als wie ein proteinreicheres Originalgericht. ",
                    "Goal: Increase protein per serving by at least 15% (preferably 20%) versus the original without harming cookability. The base dish must remain clearly recognizable. Avoid using only garnish-level changes (e.g. just parmesan/seeds) as the sole protein lever; make meaningful, quantity-relevant adjustments instead. If something would only be used as a tiny topping (<10 g per serving), it does not count as the protein lever, only as a bonus. Do not propose ideas that feel more like a new side recipe or a style break than a higher-protein version of the original dish. ",
                    "Objetivo: Aumenta la proteína por ración al menos un 15% (mejor 20%) frente al original sin comprometer la cocinabilidad. El plato base debe seguir siendo claramente reconocible. Evita cambios solo de guarnición (p.ej. solo parmesano/semillas) como único impulso de proteína; haz ajustes reales y relevantes en cantidad. Si algo solo se usaría como topping en cantidades mínimas (<10 g por ración), no cuenta como palanca de proteína, solo como extra. No propongas ideas que parezcan más una receta lateral nueva o una ruptura de estilo que una versión más proteica del plato original. ",
                    "Objetivo: Aumenta a proteína por porção pelo menos 15% (idealmente 20%) face ao original sem comprometer a exequibilidade. O prato base deve continuar claramente reconhecível. Evita usar apenas alterações de guarnição (ex.: só parmesão/sementes) como único impulso; faz ajustes reais e relevantes em quantidade. Se algo só seria usado como topping em quantidades pequenas (<10 g por porção), não conta como alavanca de proteína, apenas como bónus. Não proponhas ideias que pareçam mais uma nova receita de acompanhamento ou uma quebra de estilo do que uma versão mais proteica do prato original. ");
            }

            // High-protein mode: require a meaningful jump so we don't "game" the goal with tiny additions.
            var minTarget = Math.Round(baselineProteinPerPortion * 1.20m, 1);
            var preferredTarget = Math.Round(baselineProteinPerPortion * 1.25m, 1);
            var deltaMin = Math.Max(0m, minTarget - baselineProteinPerPortion);
            var deltaPreferred = Math.Max(0m, preferredTarget - baselineProteinPerPortion);

            return LocalizedWord(
                language,
                $"Ziel: Protein pro Portion von ca. {baselineProteinPerPortion:0.#} g (Basis: {baselineProteinTotal:0.#} g gesamt / {portions} Portionen) auf mindestens {minTarget:0.#} g (besser {preferredTarget:0.#} g) erhoehen, ohne die Kochbarkeit zu gefaehrden. Das bedeutet grob +{deltaMin:0.#} g Protein pro Portion (besser +{deltaPreferred:0.#} g). Das Ausgangsgericht muss klar erkennbar bleiben. Vermeide reine Garnituren (z.B. nur Parmesan/Seeds) als einzigen Proteinhebel; nimm stattdessen echte, mengenrelevante Anpassungen vor. Wenn du etwas nur als Topping in Mini-Mengen (<10 g pro Portion) einsetzen wuerdest, zaehlt es nicht als Protein-Hebel, sondern hoechstens als Bonus. Schlage keine Ideen vor, die eher wie ein neues Beilagen-Rezept oder Stilbruch wirken als wie ein proteinreicheres Originalgericht. ",
                $"Goal: Raise protein per serving from about {baselineProteinPerPortion:0.#} g (baseline: {baselineProteinTotal:0.#} g total / {portions} servings) to at least {minTarget:0.#} g (preferably {preferredTarget:0.#} g) without harming cookability. That is roughly +{deltaMin:0.#} g protein per serving (preferably +{deltaPreferred:0.#} g). The base dish must remain clearly recognizable. Avoid garnish-only changes (e.g. just parmesan/seeds) as the sole protein lever; make meaningful, quantity-relevant adjustments instead. If something would only be used as a tiny topping (<10 g per serving), it does not count as the protein lever, only as a bonus. Do not propose ideas that feel more like a new side recipe or a style break than a higher-protein version of the original dish. ",
                $"Objetivo: Subir la proteína por ración de aprox. {baselineProteinPerPortion:0.#} g (base: {baselineProteinTotal:0.#} g total / {portions} raciones) a al menos {minTarget:0.#} g (mejor {preferredTarget:0.#} g) sin comprometer la cocinabilidad. Eso es aprox. +{deltaMin:0.#} g de proteína por ración (mejor +{deltaPreferred:0.#} g). El plato base debe seguir siendo claramente reconocible. Evita cambios solo de guarnición (p.ej. solo parmesano/semillas) como único impulso; haz ajustes reales y relevantes en cantidad. Si algo solo se usaría como topping en cantidades mínimas (<10 g por ración), no cuenta como palanca de proteína, solo como extra. No propongas ideas que parezcan más una receta lateral nueva o una ruptura de estilo que una versión más proteica del plato original. ",
                $"Objetivo: Aumentar a proteína por porção de cerca de {baselineProteinPerPortion:0.#} g (base: {baselineProteinTotal:0.#} g no total / {portions} porções) para pelo menos {minTarget:0.#} g (idealmente {preferredTarget:0.#} g) sem comprometer a exequibilidade. Isso é aprox. +{deltaMin:0.#} g de proteína por porção (idealmente +{deltaPreferred:0.#} g). O prato base deve continuar claramente reconhecível. Evita alterações apenas de guarnição (ex.: só parmesão/sementes) como único impulso; faz ajustes reais e relevantes em quantidade. Se algo só seria usado como topping em quantidades pequenas (<10 g por porção), não conta como alavanca de proteína, apenas como bónus. Não proponhas ideias que pareçam mais uma nova receita de acompanhamento ou uma quebra de estilo do que uma versão mais proteica do prato original. ");
        }

        private static (decimal totalProtein, decimal proteinPerPortion, int portions) TryEstimateBaselineProtein(RecipeBaseData recipe)
        {
            if (recipe == null)
            {
                return (0m, 0m, 0);
            }

            var portions = recipe.PersonCount <= 0 ? 1 : recipe.PersonCount;
            decimal totalProtein = 0m;

            foreach (var link in recipe.Ingredients ?? Array.Empty<RecipeJoinIngredientMeasureQuantity>())
            {
                var nutrient = link?.Ingredient?.IngredientsAndNutrients;
                if (nutrient == null)
                {
                    continue;
                }

                var qtyRaw = link.Ingredient?.Quantity?.Quantitys;
                var qty = qtyRaw.HasValue ? Convert.ToDecimal(qtyRaw.Value) : 100m;
                if (qty <= 0m)
                {
                    continue;
                }

                var factor = qty / 100m;
                totalProtein += nutrient.Protein_a_100g * factor;
            }

            var proteinPerPortion = portions > 0 ? totalProtein / portions : 0m;
            return (Math.Max(0m, totalProtein), Math.Max(0m, proteinPerPortion), portions);
        }

        private static bool ShouldAutoGenerateWithoutIngredientSelection(string variantType, IngredientSelectionContext context)
        {
            return string.Equals(variantType, "mealprep", StringComparison.OrdinalIgnoreCase) && context.HasCarbSide;
        }

         private static List<IngredientResolutionCandidate> FilterCandidatesForRecipeContext(
             IReadOnlyList<IngredientResolutionCandidate> candidates,
             string variantType,
             IngredientSelectionContext context)
         {
             if (string.Equals(variantType, "highprotein", StringComparison.OrdinalIgnoreCase))
             {
                 // Do not over-filter high-protein candidates here. The AI needs a broad pool to build
                 // genuinely different concepts (add-ons, sides, swaps). Hard filters cause repetition.
                 return candidates.ToList();
             }

             if (!string.Equals(variantType, "lowcarb", StringComparison.OrdinalIgnoreCase) || !context.PreferSideReplacement)
             {
                 return candidates.ToList();
             }

             var filtered = candidates
                 .Where(x => x.CategoryKey is "vegetables" or "leafy_greens" or "mushrooms")
                 .Where(x => x.CarbsPer100g <= 15m)
                 .Where(x => !LooksLikeFlavorOnlyIngredient(x.Name))
                 .ToList();

             return filtered.Count > 0 ? filtered : candidates.ToList();
         }

        private static bool IsHighProteinLeadCandidate(IngredientResolutionCandidate candidate)
        {
            var categoryKey = NormalizeCategoryKey(candidate.CategoryKey);
            var protein = candidate.ProteinPer100g;
            var calories = candidate.CaloriesPer100g;
            var normalizedName = (candidate.Name ?? string.Empty).Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return false;
            }

            if (normalizedName.Contains("brokkoli") || normalizedName.Contains("broccoli") || normalizedName.Contains("blumenkohl") || normalizedName.Contains("cauliflower") || normalizedName.Contains("spinat") || normalizedName.Contains("spinach"))
            {
                return false;
            }

            _ = calories; // left for potential future heuristics

            // High-protein heuristic: don't enforce "protein calorie share" (too restrictive for real DB data).
            if (protein < 15m)
            {
                return false;
            }

            if (categoryKey is not "legumes" and not "tofu" and not "tempeh" and not "eggs" and not "yogurt" and not "cheese" and not "seeds" and not "nuts" and not "fish" and not "seafood" and not "poultry" and not "beef" and not "pork" and not "lamb" and not "mushrooms" and not "vegetables" and not "leafy_greens")
            {
                return true;
            }

            return true;
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
                "pulver", "powder", "gewurz", "gewÃ¼rz", "kraut", "herb", "sauce", "sosse", "soÃŸe", "fond", "brÃ¼he", "bruhe", "milch", "cream", "sahne", "essig", "oil", "Ã¶l"
            };

            return keywords.Any(keyword => normalized.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        private async Task<List<RecipeAiIngredientSuggestionItem>> BuildPantryIngredientHintsAsync(
            string language,
            IReadOnlyCollection<int> excludeIngredientIds,
            CancellationToken cancellationToken)
        {
            var excluded = excludeIngredientIds?.ToHashSet() ?? new HashSet<int>();

            // A small, practical set of pantry/balance items that often appear in "abschmecken" steps.
            // We resolve them against the DB (by name) so the AI can reference ingredientIds instead of free text.
            var pantryNames = new[]
            {
                "Salz",
                "Pfeffer",
                "Butter",
                "Olivenoel",
                "Oel",
                "Essig",
                "Zitronensaft",
                "Petersilie",
                "Thymian",
                "Rosmarin",
                "Paprikapulver",
                "Senf",
                "Gemuesebruehe",
                "Rinderbruehe",
                "Zucker"
            };

            // These categories are filtered out for some goals; we intentionally allow them here.
            var pantryCategoryKeys = new[] { "spices", "herbs", "broths", "oils", "fats", "sauces", "sweeteners" };

            var results = new List<RecipeAiIngredientSuggestionItem>();

            foreach (var rawName in pantryNames)
            {
                if (string.IsNullOrWhiteSpace(rawName))
                {
                    continue;
                }

                var resolved = await _ingredientResolverService.ResolveByNameAsync(
                    rawName,
                    language,
                    pantryCategoryKeys,
                    cancellationToken);

                // Some DBs may categorize pantry items differently; try again without category restrictions.
                resolved ??= await _ingredientResolverService.ResolveByNameAsync(
                    rawName,
                    language,
                    allowedCategoryKeys: null,
                    cancellationToken: cancellationToken);

                if (resolved == null || resolved.IngredientId <= 0 || string.IsNullOrWhiteSpace(resolved.Name))
                {
                    continue;
                }

                if (excluded.Contains(resolved.IngredientId))
                {
                    continue;
                }

                if (results.Any(x => x.IngredientId == resolved.IngredientId))
                {
                    continue;
                }

                results.Add(new RecipeAiIngredientSuggestionItem
                {
                    IngredientId = resolved.IngredientId,
                    Name = resolved.Name,
                    CategoryKey = resolved.CategoryKey,
                    CategoryLabel = resolved.CategoryLabel,
                    ProteinPer100g = resolved.ProteinPer100g,
                    CarbsPer100g = resolved.CarbsPer100g,
                    FatPer100g = resolved.FatPer100g,
                    FiberPer100g = resolved.FiberPer100g,
                    CaloriesPer100g = resolved.CaloriesPer100g,
                    Score = resolved.Score,
                    AiReason = "pantry"
                });
            }

            // Add a larger "pantry pool" so the AI can freely pick herbs/spices/acids/fats without extra API roundtrips.
            // This keeps control in the DB (ids only) while still giving the model creative freedom.
            var pool = await _ingredientResolverService.GetCandidatesForCategoriesAsync(
                language: language,
                allowedCategoryKeys: pantryCategoryKeys,
                excludeIngredientIds: excluded,
                take: 250,
                cancellationToken: cancellationToken);

            foreach (var item in pool)
            {
                if (item == null || item.IngredientId <= 0 || string.IsNullOrWhiteSpace(item.Name))
                {
                    continue;
                }

                if (results.Any(x => x.IngredientId == item.IngredientId))
                {
                    continue;
                }

                results.Add(new RecipeAiIngredientSuggestionItem
                {
                    IngredientId = item.IngredientId,
                    Name = item.Name,
                    CategoryKey = item.CategoryKey,
                    CategoryLabel = item.CategoryLabel,
                    ProteinPer100g = item.ProteinPer100g,
                    CarbsPer100g = item.CarbsPer100g,
                    FatPer100g = item.FatPer100g,
                    FiberPer100g = item.FiberPer100g,
                    CaloriesPer100g = item.CaloriesPer100g,
                    Score = item.Score,
                    AiReason = "pantry_pool"
                });
            }

            return results;
        }

        private static IReadOnlyList<RecipeAiIngredientSuggestionItem> MergeIngredientSuggestionLists(
            IReadOnlyList<RecipeAiIngredientSuggestionItem> primary,
            IReadOnlyList<RecipeAiIngredientSuggestionItem> secondary)
        {
            var result = new List<RecipeAiIngredientSuggestionItem>();

            foreach (var item in primary ?? Array.Empty<RecipeAiIngredientSuggestionItem>())
            {
                if (item == null || item.IngredientId <= 0 || string.IsNullOrWhiteSpace(item.Name))
                {
                    continue;
                }

                if (result.All(x => x.IngredientId != item.IngredientId))
                {
                    result.Add(item);
                }
            }

            foreach (var item in secondary ?? Array.Empty<RecipeAiIngredientSuggestionItem>())
            {
                if (item == null || item.IngredientId <= 0 || string.IsNullOrWhiteSpace(item.Name))
                {
                    continue;
                }

                if (result.All(x => x.IngredientId != item.IngredientId))
                {
                    result.Add(item);
                }
            }

            return result;
        }

        private static Func<string, bool> ContainsAnyKeyword(params string[] keywords)
        {
            return value => keywords.Any(keyword => value.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeCategoryKey(string? categoryKey)
        {
            var normalized = (categoryKey ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "leguminosen" => "legumes",
                "hülsenfrüchte" => "legumes",
                "huelsenfruechte" => "legumes",
                "hülsen" => "legumes",
                "linsen" => "legumes",
                "leafy greens" => "leafy_greens",
                "blattgemüse" => "leafy_greens",
                "blattgemuese" => "leafy_greens",
                "gemüse" => "vegetables",
                "gemuese" => "vegetables",
                "pilze" => "mushrooms",
                "eier" => "eggs",
                "käse" => "cheese",
                "kaese" => "cheese",
                "geflügel" => "poultry",
                "gefluegel" => "poultry",
                "samen" => "seeds",
                "nüsse" => "nuts",
                "nuesse" => "nuts",
                _ => normalized
            };
        }

        private static string GetDisplayCategoryLabel(string? categoryKey, string? currentLabel, string language)
        {
            var normalizedKey = NormalizeCategoryKey(categoryKey);
            var normalizedLabel = (currentLabel ?? string.Empty).Trim();

            if (!string.IsNullOrWhiteSpace(normalizedLabel)
                && !string.Equals(normalizedLabel, "Setzplätze", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(normalizedLabel, "Setzplaetze", StringComparison.OrdinalIgnoreCase))
            {
                return normalizedLabel;
            }

            return normalizedKey switch
            {
                "legumes" => LocalizedWord(language, "Hülsenfrüchte", "legumes", "legumbres", "leguminosas"),
                "vegetables" => LocalizedWord(language, "Gemüse", "vegetables", "verduras", "legumes"),
                "leafy_greens" => LocalizedWord(language, "Blattgemüse", "leafy greens", "hojas verdes", "folhas verdes"),
                "mushrooms" => LocalizedWord(language, "Pilze", "mushrooms", "setas", "cogumelos"),
                "eggs" => LocalizedWord(language, "Eier", "eggs", "huevos", "ovos"),
                "tofu" => "Tofu",
                "tempeh" => "Tempeh",
                "yogurt" => LocalizedWord(language, "Joghurt", "yogurt", "yogur", "iogurte"),
                "cheese" => LocalizedWord(language, "Käse", "cheese", "queso", "queijo"),
                "fish" => LocalizedWord(language, "Fisch", "fish", "pescado", "peixe"),
                "poultry" => LocalizedWord(language, "Geflügel", "poultry", "aves", "aves"),
                "seeds" => LocalizedWord(language, "Samen", "seeds", "semillas", "sementes"),
                "nuts" => LocalizedWord(language, "Nüsse", "nuts", "frutos secos", "frutos secos"),
                "rice" => LocalizedWord(language, "Reis", "rice", "arroz", "arroz"),
                "potato" => LocalizedWord(language, "Kartoffeln", "potatoes", "patatas", "batatas"),
                _ => string.IsNullOrWhiteSpace(normalizedLabel) ? normalizedKey : normalizedLabel
            };
        }

        private static List<IngredientResolutionCandidate> DiversifyCandidatesForPrompt(
            IReadOnlyList<IngredientResolutionCandidate> candidates,
            string variantType,
            int take)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return new List<IngredientResolutionCandidate>();
            }

            var maxCount = Math.Max(1, take);
            var grouped = candidates
                .GroupBy(x => NormalizeCategoryKey(x.CategoryKey))
                .OrderByDescending(g => g.Max(x => x.Score))
                .ToList();

            var result = new List<IngredientResolutionCandidate>();

            for (var round = 0; result.Count < maxCount; round++)
            {
                var addedInRound = false;
                foreach (var group in grouped)
                {
                    var item = group.Skip(round).FirstOrDefault();
                    if (item == null || result.Any(x => x.IngredientId == item.IngredientId))
                    {
                        continue;
                    }

                    result.Add(item);
                    addedInRound = true;
                    if (result.Count >= maxCount)
                    {
                        break;
                    }
                }

                if (!addedInRound)
                {
                    break;
                }
            }

            // Important: don't re-sort the diversified list back into the same "top score" order.
            // The AI sees candidates in the given order, so this is a cheap but effective way to avoid
            // getting the same 4 suggestions every time (parmesan/lentils/seeds/etc.).

            foreach (var candidate in candidates)
            {
                if (result.Count >= maxCount)
                {
                    break;
                }

                if (result.All(x => x.IngredientId != candidate.IngredientId))
                {
                    result.Add(candidate);
                }
            }

            return result.Take(maxCount).ToList();
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
                    ReplaceIngredient(ingredients, language, new[] { "huhn", "hÃ¤hn", "hÃ¼hn", "chicken", "pollo", "frango", "rind", "beef", "ternera", "schinken", "ham", "jamÃ³n", "presunto", "schwein", "pork", "cerdo", "porco", "speck", "bacon", "wurst", "sausage", "pute", "turkey", "pavo", "peru", "lamm", "lamb", "cordero", "cordeiro", "fleisch", "meat", "carne" }, LocalizedWord(language, "Tofu", "Tofu", "Tofu", "Tofu"), LocalizedHint(language, "durch Tofu ersetzt", "replaced with tofu", "sustituido por tofu", "substituÃ­do por tofu"), highlights);
                    ReplaceVariableValue(stepPlan, new[] { "huhn", "hÃ¤hn", "hÃ¼hn", "chicken", "pollo", "frango", "rind", "beef", "ternera", "schinken", "ham", "jamÃ³n", "presunto", "schwein", "pork", "cerdo", "porco", "speck", "bacon", "wurst", "sausage", "pute", "turkey", "pavo", "peru", "lamm", "lamb", "cordero", "cordeiro", "fleisch", "meat", "carne" }, LocalizedWord(language, "Tofu", "tofu", "tofu", "tofu"));
                    ReplaceIngredient(ingredients, language, new[] { "milch", "milk", "leche", "leite", "sahne", "cream", "nata", "butter", "mantequilla", "manteiga", "joghurt", "yogurt", "yogur", "iogurte", "quark", "skyr" }, LocalizedWord(language, "Hafer-Cuisine", "oat cream", "crema de avena", "creme de aveia"), LocalizedHint(language, "veganisiert", "made dairy-free", "versiÃ³n vegana", "versÃ£o vegana"), highlights);
                    ReplaceVariableValue(stepPlan, new[] { "milch", "milk", "leche", "leite", "sahne", "cream", "nata", "butter", "mantequilla", "manteiga", "joghurt", "yogurt", "yogur", "iogurte", "quark", "skyr" }, LocalizedWord(language, "Hafer-Cuisine", "oat cream", "crema de avena", "creme de aveia"));
                    ReplaceIngredient(ingredients, language, new[] { "kÃ¤se", "kaese", "cheese", "queso", "queijo", "parmesan" }, LocalizedWord(language, "vegane Alternative", "vegan alternative", "alternativa vegana", "alternativa vegana"), LocalizedHint(language, "tierfrei ersetzt", "swapped for vegan alternative", "reemplazado por una alternativa vegana", "substituÃ­do por alternativa vegana"), highlights);
                    ReplaceVariableValue(stepPlan, new[] { "kÃ¤se", "kaese", "cheese", "queso", "queijo", "parmesan" }, LocalizedWord(language, "vegane Alternative", "vegan alternative", "alternativa vegana", "alternativa vegana"));
                    ReplaceIngredient(ingredients, language, new[] { "ei ", "eier", "egg", "huevo", "ovo" }, LocalizedWord(language, "Ei-Ersatz", "egg substitute", "sustituto de huevo", "substituto de ovo"), LocalizedHint(language, "veganisiert", "made egg-free", "sin huevo", "sem ovo"), highlights);
                    ReplaceVariableValue(stepPlan, new[] { "ei ", "eier", "egg", "huevo", "ovo" }, LocalizedWord(language, "Ei-Ersatz", "egg substitute", "sustituto de huevo", "substituto de ovo"));
                    ReplaceIngredient(ingredients, language, new[] { "honig", "honey", "miel" }, LocalizedWord(language, "Ahornsirup", "maple syrup", "sirope de arce", "xarope de Ã¡cer"), LocalizedHint(language, "veganisiert", "made vegan", "versiÃ³n vegana", "versÃ£o vegana"), highlights);
                    ReplaceVariableValue(stepPlan, new[] { "honig", "honey", "miel" }, LocalizedWord(language, "Ahornsirup", "maple syrup", "sirope de arce", "xarope de Ã¡cer"));
                    summaryParts.Add(LocalizedWord(language, "Tierische Zutaten wurden mÃ¶glichst schonend ersetzt.", "Animal-based ingredients were swapped as gently as possible.", "Se sustituyeron los ingredientes de origen animal con cuidado.", "Os ingredientes de origem animal foram substituÃ­dos com cuidado."));
                    break;
                case "mealprep":
                    title = PrefixTitle(language, recipe.Title, "Meal Prep");
                    summaryParts.Add(LocalizedWord(language, "Die Variante ist auf gutes Vorbereiten und entspanntes AufwÃ¤rmen ausgelegt.", "This version is tuned for prepping ahead and easy reheating.", "Esta versiÃ³n estÃ¡ pensada para preparar con antelaciÃ³n y recalentar fÃ¡cilmente.", "Esta versÃ£o foi ajustada para preparar antes e aquecer facilmente."));
                    highlights.Add(LocalizedWord(language, "Meal-Prep-freundliche Reihenfolge", "meal-prep-friendly workflow", "flujo pensado para meal prep", "fluxo pensado para meal prep"));
                    break;
                case "lowcarb":
                    title = PrefixTitle(language, recipe.Title, "Low Carb");
                    ReplaceIngredient(ingredients, language, new[] { "reis", "rice", "arroz", "risotto" }, LocalizedWord(language, "Blumenkohlreis", "cauliflower rice", "arroz de coliflor", "arroz de couve-flor"), LocalizedHint(language, "KH reduziert", "lower-carb swap", "menos carbohidratos", "menos carboidratos"), highlights);
                    ReplaceVariableValue(stepPlan, new[] { "reis", "rice", "arroz", "risotto" }, LocalizedWord(language, "Blumenkohlreis", "cauliflower rice", "arroz de coliflor", "arroz de couve-flor"));
                    ReplaceIngredient(ingredients, language, new[] { "nudel", "pasta", "spaghetti", "penne", "fusilli", "tagliatelle", "fettuccine", "makkaroni", "macaroni" }, LocalizedWord(language, "Zucchini-Nudeln", "zucchini noodles", "fideos de calabacÃ­n", "macarrÃ£o de curgete"), LocalizedHint(language, "leichtere Beilage", "lighter side", "guarniciÃ³n ligera", "acompanhamento leve"), highlights);
                    ReplaceVariableValue(stepPlan, new[] { "nudel", "pasta", "spaghetti", "penne", "fusilli", "tagliatelle", "fettuccine", "makkaroni", "macaroni" }, LocalizedWord(language, "Zucchini-Nudeln", "zucchini noodles", "fideos de calabacÃ­n", "macarrÃ£o de curgete"));
                    ReplaceIngredient(ingredients, language, new[] { "kartoffel", "potato", "patata", "batata" }, LocalizedWord(language, "OfengemÃ¼se", "roasted vegetables", "verduras asadas", "legumes assados"), LocalizedHint(language, "stÃ¤rkearme Alternative", "lower-starch alternative", "alternativa baja en almidÃ³n", "alternativa com menos amido"), highlights);
                    ReplaceVariableValue(stepPlan, new[] { "kartoffel", "potato", "patata", "batata" }, LocalizedWord(language, "OfengemÃ¼se", "roasted vegetables", "verduras asadas", "legumes assados"));
                    ReplaceIngredient(ingredients, language, new[] { "brot", "bread", "pan ", "pÃ£o", "toast", "brÃ¶tchen", "semmel" }, LocalizedWord(language, "SalatblÃ¤tter", "lettuce wraps", "hojas de lechuga", "folhas de alface"), LocalizedHint(language, "KH reduziert", "lower-carb swap", "menos carbohidratos", "menos carboidratos"), highlights);
                    ReplaceVariableValue(stepPlan, new[] { "brot", "bread", "pan ", "pÃ£o", "toast", "brÃ¶tchen", "semmel" }, LocalizedWord(language, "SalatblÃ¤tter", "lettuce wraps", "hojas de lechuga", "folhas de alface"));
                    summaryParts.Add(LocalizedWord(language, "StÃ¤rkereiche Bestandteile wurden soweit mÃ¶glich gegen leichtere Alternativen getauscht.", "Starchy parts were swapped for lighter alternatives where possible.", "Los componentes ricos en almidÃ³n se cambiaron por opciones mÃ¡s ligeras cuando fue posible.", "Os componentes ricos em amido foram trocados por opÃ§Ãµes mais leves sempre que possÃ­vel."));
                    break;
                case "highprotein":
                    title = PrefixTitle(language, recipe.Title, LocalizedWord(language, "Protein", "Protein", "ProteÃ­na", "ProteÃ­na"));
                    highlights.Add(LocalizedWord(language, "Proteinquelle verstÃ¤rkt", "protein source boosted", "fuente de proteÃ­na reforzada", "fonte de proteÃ­na reforÃ§ada"));
                    ingredients.Add(new RecipeAiTransformIngredientPreview
                    {
                        Name = primarySelectedIngredient?.Name ?? LocalizedWord(language, "Skyr oder Extra-Protein", "skyr or extra protein", "skyr o proteÃ­na extra", "skyr ou proteÃ­na extra"),
                        Quantity = primarySelectedIngredient != null ? LocalizedWord(language, "150 g", "150 g", "150 g", "150 g") : LocalizedWord(language, "1 Portion", "1 portion", "1 porciÃ³n", "1 porÃ§Ã£o"),
                        ChangeHint = primarySelectedIngredient != null
                            ? LocalizedHint(language, "aus deinem Zutatenstamm ausgewÃ¤hlt", "selected from your ingredient database", "seleccionado de tu base de ingredientes", "selecionado da tua base de ingredientes")
                            : LocalizedHint(language, "optional ergÃ¤nzt", "optional addition", "aÃ±adido opcional", "adiÃ§Ã£o opcional"),
                        IsModified = true
                    });
                    summaryParts.Add(LocalizedWord(language, "Die Variante legt den Fokus stÃ¤rker auf SÃ¤ttigung und Protein pro Portion.", "This version focuses more on satiety and protein per serving.", "Esta versiÃ³n pone mÃ¡s foco en saciedad y proteÃ­na por porciÃ³n.", "Esta versÃ£o foca mais em saciedade e proteÃ­na por porÃ§Ã£o."));
                    break;
            }


            foreach (var selectedIngredient in selectedIngredients)
            {
                highlights.Add(LocalizedWord(language, "GewÃ¤hlte DB-Zutat: ", "Selected DB ingredient: ", "Ingrediente elegido de la BD: ", "Ingrediente escolhido da BD: ") + selectedIngredient.Name);
                summaryParts.Add(LocalizedWord(language, "Die Variante arbeitet bewusst mit ", "This version intentionally uses ", "Esta versiÃ³n usa de forma intencional ", "Esta versÃ£o usa de forma intencional ") + selectedIngredient.Name + ".");

                if (!ingredients.Any(x => string.Equals(x.Name, selectedIngredient.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    ingredients.Add(new RecipeAiTransformIngredientPreview
                    {
                        Name = selectedIngredient.Name,
                        Quantity = LocalizedWord(language, "nach Bedarf", "as needed", "al gusto", "a gosto"),
                        ChangeHint = LocalizedHint(language, "AI-Auswahl aus deinen Zutaten", "AI-selected from your ingredients", "selecciÃ³n AI de tus ingredientes", "seleÃ§Ã£o AI dos teus ingredientes"),
                        IsModified = true
                    });
                }
            }

            if (!string.IsNullOrWhiteSpace(userNote))
            {
                highlights.Add(LocalizedWord(language, "User-Wunsch eingearbeitet", "user request applied", "cambio del usuario aplicado", "pedido do utilizador aplicado"));
                summaryParts.Add(LocalizedWord(language, "ZusÃ¤tzlicher Ã„nderungswunsch: ", "Additional change request: ", "Cambio adicional: ", "Pedido adicional: ") + userNote);
                ApplySimpleUserNoteAdjustment(ingredients, stepPlan, language, userNote);
            }

            if (highlights.Count == 0)
            {
                highlights.Add(LocalizedWord(language, "Sanfte AI-Anpassung", "gentle AI adaptation", "adaptaciÃ³n suave", "adaptaÃ§Ã£o suave"));
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
                IngredientsText = BuildIngredientsText(ingredients),
                PreparationText = BuildPreparationTextFromStepPlan(stepPlan, language),
                Highlights = highlights.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            };

            NormalizePreview(preview, recipe, variantType, language, userNote, appliedChangeCount, true, sourceStepPlan, stepSelectionContext.AllowedStepKeys, selectedIngredients, allowOnlySourceKeys: sourceStepPlan.Count > 0);
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
            IReadOnlyList<RecipeAiIngredientSuggestionItem> availableVariantIngredients,
            bool allowOnlySourceKeys,
            IReadOnlyCollection<int>? protectedIngredientIds = null)
        {
            // The model may swap or localize these fields ("Mehr Protein" as variantType).
            // We always normalize to the canonical variantType; if normalization fails, fall back to the request variantType.
            var normalizedFromModel = string.IsNullOrWhiteSpace(preview.VariantType) ? string.Empty : NormalizeVariantType(preview.VariantType);
            preview.VariantType = string.IsNullOrWhiteSpace(normalizedFromModel) ? variantType : normalizedFromModel;
            preview.VariantLabel = GetVariantLabel(preview.VariantType, language);
            preview.Title = string.IsNullOrWhiteSpace(preview.Title) ? recipe.Title : preview.Title.Trim();
            preview.Summary = (preview.Summary ?? string.Empty).Trim();
            preview.UsedFallback = usedFallback || preview.UsedFallback;
            preview.UserNoteApplied = preview.UserNoteApplied || !string.IsNullOrWhiteSpace(userNote);
            preview.RemainingChanges = Math.Clamp(preview.RemainingChanges, 0, MaxChangeRounds);
            preview.IngredientsText = (preview.IngredientsText ?? string.Empty).Trim();
            preview.PreparationText = (preview.PreparationText ?? string.Empty).Trim();
            preview.Ingredients ??= new List<RecipeAiTransformIngredientPreview>();
            preview.StepPlan ??= new List<RecipeAiTransformStepPlanItem>();
            preview.Steps ??= new List<RecipeAiTransformStepPreview>();
            preview.Highlights ??= new List<string>();

            if (preview.Ingredients.Count == 0)
            {
                preview.Ingredients = BuildIngredientPreview(recipe, language);
            }
            else
            {
                preview.Ingredients = NormalizePreviewIngredients(preview.Ingredients, recipe, language, availableVariantIngredients);
            }

            // We operate in "free text" mode: stepPlan is not used (we render steps from preparationText).
            // Keeping stepPlan bloats tokens and causes invalid/meaningless masterStepKeys to slip in.
            preview.StepPlan = new List<RecipeAiTransformStepPlanItem>();

            // Ensure ingredients[] covers what the user sees in ingredientsText/preparationText.
            PatchMissingIngredientsFromIngredientsText(preview, recipe, language, availableVariantIngredients);

            // Remove generic DE-only phrases that reference undefined ingredients (e.g. "... mit Kräutern abschmecken" without any herb id).
            CleanupUndefinedGenericIngredientMentions(preview, language);

            var validatedPlan = ValidateAndRenderStepPlan(preview.StepPlan, language, sourceStepPlan, additionalAllowedStepKeys, allowOnlySourceKeys);
            preview.StepPlan = validatedPlan;
            var renderedSteps = validatedPlan.Count > 0
                ? validatedPlan.Select((item, index) => new RecipeAiTransformStepPreview
                {
                    Index = index + 1,
                    Text = PolishStepText(item.RenderedText, language)
                }).ToList()
                : BuildStepPreviewFromPreparationText(preview.PreparationText, language);

            if (renderedSteps.Count == 0)
            {
                renderedSteps = sourceStepPlan.Count > 0
                    ? sourceStepPlan.Select(CloneStepPlanItem).Select((item, index) => new RecipeAiTransformStepPreview
                    {
                        Index = index + 1,
                        Text = PolishStepText(item.RenderedText, language)
                    }).ToList()
                    : BuildStepPreview(recipe, language);
            }

            preview.Steps = renderedSteps;

            // Bidirectional sync: add ingredients mentioned in text but missing from list,
            // remove ingredients from list that are not referenced in text,
            // and clamp AI-added extras that aren't in original/plan/pantry.
            EnsureBidirectionalIngredientConsistency(preview, recipe, language, availableVariantIngredients, protectedIngredientIds);

            // Always keep ingredientsText in sync with the canonical ingredient rows (prevents "missing rows" in UI/saving).
            preview.IngredientsText = BuildIngredientsText(preview.Ingredients);

            if (string.IsNullOrWhiteSpace(preview.PreparationText))
            {
                preview.PreparationText = BuildPreparationTextFromSteps(preview.Steps);
            }

            ApplySemanticConsistencyGuards(preview, language);

            if (preview.RemainingChanges > MaxChangeRounds - appliedChangeCount)
            {
                preview.RemainingChanges = Math.Max(0, MaxChangeRounds - appliedChangeCount - (string.IsNullOrWhiteSpace(userNote) ? 0 : 1));
            }

            if (preview.Highlights.Count == 0)
            {
                preview.Highlights.Add(LocalizedWord(language, "Originale Template-Schritte übernommen", "original template steps retained", "se mantuvieron los pasos de plantilla", "passos de template mantidos"));
            }
        }

        // Pantry basics that are often used implicitly (seasoning "nach Geschmack") and should never be pruned.
        private static readonly HashSet<string> PantryBasicNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "salz", "pfeffer", "wasser", "salt", "pepper", "water",
            "sal", "pimienta", "agua", "pimenta", "água",
            "zout", "peppar", "vatten", "vand", "garam", "air"
        };

        private static bool IsPantryBasic(string? ingredientName)
        {
            if (string.IsNullOrWhiteSpace(ingredientName)) return false;
            var lower = ingredientName.Trim().ToLowerInvariant();
            return PantryBasicNames.Any(p => lower.Contains(p));
        }

        private static void PruneUnusedPreviewIngredients(
            RecipeAiTransformPreview preview,
            RecipeBaseData recipe,
            string language,
            IReadOnlyCollection<int>? protectedIngredientIds = null)
        {
            if (preview?.Ingredients == null || preview.Ingredients.Count == 0)
            {
                return;
            }

            var originalIds = new HashSet<int>();
            foreach (var link in recipe?.Ingredients ?? Array.Empty<RecipeJoinIngredientMeasureQuantity>())
            {
                var id = link?.Ingredient?.IngredientsAndNutrients?.Id ?? 0;
                if (id > 0)
                {
                    originalIds.Add(id);
                }
            }

            var text = string.Join("\n", new[]
            {
                preview.Title ?? string.Empty,
                preview.Summary ?? string.Empty,
                preview.PreparationText ?? string.Empty,
                string.Join("\n", (preview.Steps ?? new List<RecipeAiTransformStepPreview>()).Select(x => x?.Text ?? string.Empty))
            });

            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            preview.Ingredients = preview.Ingredients
                .Where(item =>
                {
                    if (item == null) return false;

                    // IngredientPlan items are NEVER pruned — they are the user's chosen concept.
                    if (item.IngredientId > 0 && protectedIngredientIds != null && protectedIngredientIds.Contains(item.IngredientId))
                    {
                        return true;
                    }

                    // Pantry basics (Salz, Pfeffer, Wasser) always survive — they are often used implicitly.
                    if (IsPantryBasic(item.Name)) return true;

                    // Use the robust ContainsIngredientReference which handles:
                    // - Umlaut normalization
                    // - Parenthetical qualifiers like "(frisch)", "(gekocht / aus der Dose)"
                    // - Token fallback with word-boundary + declension tolerance
                    return ContainsIngredientReference(text, item.Name ?? string.Empty);
                })
                .ToList();
        }

        /// <summary>
        /// Bidirectional consistency: ensures ingredients[] ↔ preparationText are in sync.
        /// A) Scans preparationText+Steps for known ingredients missing from ingredients[] → adds them.
        /// B) Prunes ingredients not referenced in text (respecting protectedIds).
        /// C) Clamps AI-added extras: only original + plan + pantry survive.
        /// </summary>
        private static void EnsureBidirectionalIngredientConsistency(
            RecipeAiTransformPreview preview,
            RecipeBaseData recipe,
            string language,
            IReadOnlyList<RecipeAiIngredientSuggestionItem> availableVariantIngredients,
            IReadOnlyCollection<int>? protectedIngredientIds = null)
        {
            if (preview == null) return;
            preview.Ingredients ??= new List<RecipeAiTransformIngredientPreview>();

            // Collect original recipe ingredient IDs for the clamp logic.
            var originalIds = new HashSet<int>();
            foreach (var link in recipe?.Ingredients ?? Array.Empty<RecipeJoinIngredientMeasureQuantity>())
            {
                var id = link?.Ingredient?.IngredientsAndNutrients?.Id ?? 0;
                if (id > 0) originalIds.Add(id);
            }

            // --- A) Add missing ingredients from preparationText ---
            var preparationHaystack = string.Join("\n",
                new[] { preview.PreparationText ?? string.Empty }
                .Concat((preview.Steps ?? new List<RecipeAiTransformStepPreview>()).Select(s => s?.Text ?? string.Empty)))
                .Trim();

            if (!string.IsNullOrWhiteSpace(preparationHaystack))
            {
                var existingIds = preview.Ingredients
                    .Where(x => x != null && x.IngredientId > 0)
                    .Select(x => x.IngredientId)
                    .ToHashSet();

                // Build candidate pool: availableVariantIngredients + base recipe ingredients
                var candidateNameById = (availableVariantIngredients ?? Array.Empty<RecipeAiIngredientSuggestionItem>())
                    .Where(x => x != null && x.IngredientId > 0 && !string.IsNullOrWhiteSpace(x.Name))
                    .GroupBy(x => x.IngredientId)
                    .ToDictionary(g => g.Key, g => (g.First().Name ?? string.Empty).Trim());

                var baseNameById = recipe?.Ingredients?
                    .Where(x => x?.Ingredient?.IngredientsAndNutrients != null)
                    .Select(x => x!.Ingredient!.IngredientsAndNutrients!)
                    .GroupBy(x => x.Id)
                    .ToDictionary(g => g.Key, g => GetIngredientName(g.First(), language))
                    ?? new Dictionary<int, string>();

                // Only add ingredients that are original, protected (plan), or pantry — prevents random extras.
                var addableCandidates = candidateNameById
                    .Select(kv => (id: kv.Key, name: kv.Value))
                    .Concat(baseNameById.Where(kv => !candidateNameById.ContainsKey(kv.Key)).Select(kv => (id: kv.Key, name: kv.Value)))
                    .Where(x => x.id > 0 && !string.IsNullOrWhiteSpace(x.name))
                    .Where(x => originalIds.Contains(x.id)
                             || (protectedIngredientIds != null && protectedIngredientIds.Contains(x.id))
                             || IsPantryBasic(x.name))
                    .OrderByDescending(x => x.name.Length)
                    .ToList();

                foreach (var (id, name) in addableCandidates)
                {
                    if (existingIds.Contains(id)) continue;

                    if (ContainsIngredientReference(preparationHaystack, name))
                    {
                        preview.Ingredients.Add(new RecipeAiTransformIngredientPreview
                        {
                            IngredientId = id,
                            Name = name,
                            Quantity = string.Empty,
                            Measure = string.Empty,
                            ChangeHint = null,
                            IsModified = true
                        });
                        existingIds.Add(id);
                    }
                }
            }

            // --- B) Prune ingredients not referenced in text (protected IDs survive) ---
            PruneUnusedPreviewIngredients(preview, recipe, language, protectedIngredientIds);

            // --- C) Ingredient clamp: remove AI-added extras not in original/plan/pantry ---
            // This prevents random additions like "Essig", "Kümmel (ganz)", "Butter" that the AI threw in.
            if (protectedIngredientIds != null && protectedIngredientIds.Count > 0)
            {
                preview.Ingredients = preview.Ingredients
                    .Where(item =>
                    {
                        if (item == null) return false;
                        if (item.IngredientId > 0 && originalIds.Contains(item.IngredientId)) return true;
                        if (item.IngredientId > 0 && protectedIngredientIds.Contains(item.IngredientId)) return true;
                        if (IsPantryBasic(item.Name)) return true;
                        return false;
                    })
                    .ToList();
            }
        }

        private static List<object> DetermineAllowedVariantIngredientContext(
            RecipeBaseData recipe,
            string variantType,
            string language,
            RecipeAiIngredientSuggestionItem? selectedConcept,
            IReadOnlyList<RecipeAiIngredientSuggestionItem> selectedIngredients,
            IReadOnlyList<RecipeAiIngredientSuggestionItem> availableVariantIngredients)
        {
            var allowedIds = new HashSet<int>();
            var hasConceptPlan = (selectedConcept?.ConceptIngredientPlan?.Count ?? 0) > 0;

            // If the user picked a concept with an ingredientPlan, we keep the allowed ingredient universe tight:
            // only base recipe + the plan + explicitly selected DB ingredients. This avoids "random extra spices"
            // that were never visible in the concept suggestion.
            if (!hasConceptPlan)
            {
                // Always allow pantry/balance items that we resolved from the DB.
                // This prevents "Zitronensaft/Essig/Oel" showing up in preparationText without being selectable as an ingredientId.
                foreach (var pantry in (availableVariantIngredients ?? Array.Empty<RecipeAiIngredientSuggestionItem>())
                             .Where(x => x?.IngredientId > 0 && (x.AiReason ?? string.Empty).StartsWith("pantry", StringComparison.OrdinalIgnoreCase))
                             .Take(80))
                {
                    allowedIds.Add(pantry.IngredientId);
                }
            }

            if (selectedConcept?.ConceptIngredientPlan != null)
            {
                foreach (var plan in selectedConcept.ConceptIngredientPlan)
                {
                    if (plan?.IngredientId > 0) allowedIds.Add(plan.IngredientId);
                    if (plan?.ReplacesIngredientId.GetValueOrDefault() > 0) allowedIds.Add(plan.ReplacesIngredientId!.Value);
                }
            }

            if (selectedIngredients != null)
            {
                foreach (var item in selectedIngredients)
                {
                    if (item?.IngredientId > 0) allowedIds.Add(item.IngredientId);
                }
            }

            // Always allow original recipe ingredientIds (so the model can "increase an amount" cleanly).
            var baseIds = recipe.Ingredients?
                .Where(x => x?.Ingredient?.IngredientsAndNutrients != null)
                .Select(x => x!.Ingredient!.IngredientsAndNutrients!.Id)
                .Where(x => x > 0)
                .Distinct()
                .Take(60)
                .ToList() ?? new List<int>();
            foreach (var id in baseIds) allowedIds.Add(id);

            // If we don't have a concept or selected ingredients, keep a small window of candidate IDs.
            if (allowedIds.Count == 0)
            {
                foreach (var item in availableVariantIngredients.Where(x => x?.IngredientId > 0).Take(80))
                {
                    allowedIds.Add(item.IngredientId);
                }
            }

            // Map ids to names from "available" list first (stable naming), fall back to base recipe names if needed.
            var availableNameById = (availableVariantIngredients ?? Array.Empty<RecipeAiIngredientSuggestionItem>())
                .Where(x => x != null && x.IngredientId > 0 && !string.IsNullOrWhiteSpace(x.Name))
                .GroupBy(x => x.IngredientId)
                .ToDictionary(g => g.Key, g => g.First().Name, EqualityComparer<int>.Default);

            var baseNameById = recipe.Ingredients?
                .Where(x => x?.Ingredient?.IngredientsAndNutrients != null)
                .Select(x => x!.Ingredient!.IngredientsAndNutrients!)
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => GetIngredientName(g.First(), language), EqualityComparer<int>.Default)
                ?? new Dictionary<int, string>();

            var context = new List<object>();
            foreach (var id in allowedIds.OrderBy(x => x).Take(120))
            {
                if (availableNameById.TryGetValue(id, out var candidateName) && !string.IsNullOrWhiteSpace(candidateName))
                {
                    context.Add(new { id, name = candidateName });
                    continue;
                }

                if (baseNameById.TryGetValue(id, out var baseName) && !string.IsNullOrWhiteSpace(baseName))
                {
                    context.Add(new { id, name = baseName });
                }
            }

            return context;
        }

        private static List<object> BuildAllowedIngredientContextForRepair(
            RecipeBaseData recipe,
            string language,
            RecipeAiTransformPreview draft,
            IEnumerable<int> requiredExtraIds,
            IReadOnlyList<RecipeAiIngredientSuggestionItem> availableVariantIngredients)
        {
            var ids = new HashSet<int>();

            // Current draft ingredients are always allowed.
            foreach (var id in (draft?.Ingredients ?? new List<RecipeAiTransformIngredientPreview>())
                         .Where(x => x != null && x.IngredientId > 0)
                         .Select(x => x.IngredientId))
            {
                ids.Add(id);
            }

            // Base recipe ids are always allowed (covers "increase amount" and avoids id drift).
            foreach (var id in recipe.Ingredients?
                         .Where(x => x?.Ingredient?.IngredientsAndNutrients != null)
                         .Select(x => x!.Ingredient!.IngredientsAndNutrients!.Id)
                         .Where(x => x > 0)
                         .Distinct()
                         .Take(120)
                         .ToList() ?? new List<int>())
            {
                ids.Add(id);
            }

            foreach (var id in (requiredExtraIds ?? Array.Empty<int>()).Where(x => x > 0))
            {
                ids.Add(id);
            }

            var availableNameById = (availableVariantIngredients ?? Array.Empty<RecipeAiIngredientSuggestionItem>())
                .Where(x => x != null && x.IngredientId > 0 && !string.IsNullOrWhiteSpace(x.Name))
                .GroupBy(x => x.IngredientId)
                .ToDictionary(g => g.Key, g => g.First().Name, EqualityComparer<int>.Default);

            var baseNameById = recipe.Ingredients?
                .Where(x => x?.Ingredient?.IngredientsAndNutrients != null)
                .Select(x => x!.Ingredient!.IngredientsAndNutrients!)
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => GetIngredientName(g.First(), language), EqualityComparer<int>.Default)
                ?? new Dictionary<int, string>();

            var context = new List<object>();
            foreach (var id in ids.OrderBy(x => x).Take(80))
            {
                if (availableNameById.TryGetValue(id, out var candidateName) && !string.IsNullOrWhiteSpace(candidateName))
                {
                    context.Add(new { id, name = candidateName });
                    continue;
                }

                if (baseNameById.TryGetValue(id, out var baseName) && !string.IsNullOrWhiteSpace(baseName))
                {
                    context.Add(new { id, name = baseName });
                }
            }

            return context;
        }

        private static void ApplySemanticConsistencyGuards(RecipeAiTransformPreview preview, string language)
        {
            if (preview == null)
            {
                return;
            }

            var normalizedLanguage = NormalizeLanguage(language);
            if (!string.Equals(normalizedLanguage, "de", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Guard: "Linsensalat" is frequently used as a vague label even when there is no salad/dressing logic.
            // If no salad-making pattern exists, downgrade to "Linsen" to keep the recipe coherent.
            if (LooksLikeOrphanLentilSalad(preview))
            {
                preview.Title = ReplaceIgnoreCase(preview.Title, "Linsensalat", "Linsen");
                preview.Summary = ReplaceIgnoreCase(preview.Summary, "Linsensalat", "Linsen");
                preview.PreparationText = ReplaceIgnoreCase(preview.PreparationText, "Linsensalat", "Linsen");

                if (preview.Steps != null)
                {
                    foreach (var step in preview.Steps)
                    {
                        if (step == null) continue;
                        step.Text = ReplaceIgnoreCase(step.Text, "Linsensalat", "Linsen");
                    }
                }
            }

            ApplyGermanVerbSanityFixes(preview);
        }

        private static void ApplyGermanVerbSanityFixes(RecipeAiTransformPreview preview)
        {
            // Common unhelpful/incorrect phrasing from the model:
            // "Reibe die Mischung/Masse/Sauce/Püree ... ein" (you don't "rub in" a mixture).
            // We rewrite it into a sensible cooking verb without changing the meaning.
            var rx = new Regex(@"\bReibe\s+(die|das)\s+(Mischung|Masse|Sauce|Creme|Püree|Pueree)\s+gleichmäßig\s+mit\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

            preview.PreparationText = rx.Replace(preview.PreparationText ?? string.Empty, "Würze $1 $2 mit");

            if (preview.Steps != null)
            {
                foreach (var step in preview.Steps)
                {
                    if (step == null || string.IsNullOrWhiteSpace(step.Text)) continue;
                    step.Text = rx.Replace(step.Text, "Würze $1 $2 mit");
                }
            }

            // "Mit Butter abschmecken" is semantically off; you finish with butter, but you season with salt/pepper.
            var butterRx = new Regex(@"\bSchmecke\s+(die\s+)?(Sauce|das\s+Gericht|den\s+Auflauf|alles)\s+mit\s+(einem\s+)?(Esslöffel\s+)?Butter\s+ab\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            preview.PreparationText = butterRx.Replace(preview.PreparationText ?? string.Empty, "Rühre zum Schluss die Butter ein und schmecke mit Salz und Pfeffer ab");

            if (preview.Steps != null)
            {
                foreach (var step in preview.Steps)
                {
                    if (step == null || string.IsNullOrWhiteSpace(step.Text)) continue;
                    step.Text = butterRx.Replace(step.Text, "Rühre zum Schluss die Butter ein und schmecke mit Salz und Pfeffer ab");
                }
            }
        }

        private static bool LooksLikeOrphanLentilSalad(RecipeAiTransformPreview preview)
        {
            var title = preview.Title ?? string.Empty;
            var summary = preview.Summary ?? string.Empty;
            var prep = preview.PreparationText ?? string.Empty;

            var mentionsSalad =
                title.Contains("linsensalat", StringComparison.OrdinalIgnoreCase) ||
                summary.Contains("linsensalat", StringComparison.OrdinalIgnoreCase) ||
                prep.Contains("linsensalat", StringComparison.OrdinalIgnoreCase);
            if (!mentionsSalad)
            {
                return false;
            }

            var lower = prep.ToLowerInvariant();
            var hasAcid = lower.Contains("essig") || lower.Contains("zitron") || lower.Contains("vinaig") || lower.Contains("limette");
            var hasOil = lower.Contains("öl") || lower.Contains("oel");
            var hasSaladAction =
                lower.Contains("marinier") ||
                lower.Contains("dressing") ||
                lower.Contains("vermeng") ||
                lower.Contains("mische") ||
                lower.Contains("kalt") ||
                lower.Contains("abkühlen") ||
                lower.Contains("abkuehlen");

            return !(hasAcid && hasOil && hasSaladAction);
        }

        private static string ReplaceIgnoreCase(string? input, string search, string replacement)
        {
            if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(search))
            {
                return input ?? string.Empty;
            }

            return Regex.Replace(input, Regex.Escape(search), replacement, RegexOptions.IgnoreCase);
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


        private string BuildPreparationPromptInput(
            RecipeBaseData recipe,
            IReadOnlyList<RecipeAiTransformStepPlanItem> sourceStepPlan,
            string language)
        {
            var fromStepPlan = BuildPreparationTextFromStepPlan(sourceStepPlan, language);
            if (!string.IsNullOrWhiteSpace(fromStepPlan))
            {
                return fromStepPlan;
            }

            var fromRecipeSteps = BuildPreparationTextFromRecipe(recipe, language);
            return string.IsNullOrWhiteSpace(fromRecipeSteps)
                ? LocalizedWord(language, "Keine bestehende Zubereitung vorhanden.", "No existing preparation available.", "No hay una preparaciÃ³n existente.", "Nao existe preparacao atual.")
                : fromRecipeSteps;
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

        private List<object> BuildVariableOptionContextCompact(IEnumerable<string> variableNames, string language)
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
                    v = variableName,
                    o = definition.Options.Select(option => GetLocalizedVariableOptionLabel(option, language)).ToList()
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

                // Common AI phrasing + typos: make it sound like a real recipe app.
                if (text.StartsWith("Probiere ", StringComparison.OrdinalIgnoreCase))
                {
                    text = "Schmecke " + text["Probiere ".Length..];
                }

                text = text.Replace("Schale ", "Schäle ", StringComparison.OrdinalIgnoreCase);
                text = text.Replace("Giese ", "Gieße ", StringComparison.OrdinalIgnoreCase);
                text = text.Replace("giese ", "gieße ", StringComparison.OrdinalIgnoreCase);
                text = text.Replace("kratz ", "kratze ", StringComparison.OrdinalIgnoreCase);

                // Don't "einreiben" mixtures/sauces/purees.
                text = Regex.Replace(
                    text,
                    @"\bReibe\s+(die|das)\s+(Mischung|Masse|Sauce|Creme|Püree|Pueree)\s+gleichmäßig\s+mit\b",
                    "Würze $1 $2 mit",
                    RegexOptions.IgnoreCase);
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
                        IngredientId = nutrient?.Id ?? 0,
                        Name = GetIngredientName(nutrient, language),
                        Quantity = FormatQuantity(quantity, measure, language),
                        Measure = GetMeasureText(measure, language),
                        ChangeHint = null,
                        IsModified = false
                    };
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .ToList() ?? new List<RecipeAiTransformIngredientPreview>();
        }

        private static List<RecipeAiTransformIngredientPreview> NormalizePreviewIngredients(
            IEnumerable<RecipeAiTransformIngredientPreview> ingredients,
            RecipeBaseData recipe,
            string language,
            IReadOnlyList<RecipeAiIngredientSuggestionItem> availableVariantIngredients)
        {
            var recipeLookup = recipe.Ingredients?
                .Where(x => x.Ingredient?.IngredientsAndNutrients != null)
                .Select(x => x.Ingredient!)
                .ToDictionary(x => x.IngredientsAndNutrients!.Id, x => x, EqualityComparer<int>.Default)
                ?? new Dictionary<int, IngredientMeasureQuantity>();

            var candidateLookup = availableVariantIngredients?
                .Where(x => x.IngredientId > 0)
                .GroupBy(x => x.IngredientId)
                .ToDictionary(x => x.Key, x => x.First())
                ?? new Dictionary<int, RecipeAiIngredientSuggestionItem>();

            var nameLookup = recipeLookup.Values
                .Select(x => new
                {
                    Id = x.IngredientsAndNutrients!.Id,
                    Name = NormalizeIngredientLookupName(GetIngredientName(x.IngredientsAndNutrients, language))
                })
                .Concat(candidateLookup.Values.Select(x => new
                {
                    Id = x.IngredientId,
                    Name = NormalizeIngredientLookupName(x.Name)
                }))
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .GroupBy(x => x.Name)
                .ToDictionary(x => x.Key, x => x.First().Id);

            return ingredients
                .Where(x => x != null)
                .Select(item =>
                {
                    if (item.IngredientId <= 0 && !string.IsNullOrWhiteSpace(item.Name))
                    {
                        var normalizedName = NormalizeIngredientLookupName(item.Name);
                        if (nameLookup.TryGetValue(normalizedName, out var resolvedId))
                        {
                            item.IngredientId = resolvedId;
                        }
                    }

                    if (item.IngredientId > 0)
                    {
                        if (recipeLookup.TryGetValue(item.IngredientId, out var recipeIngredient))
                        {
                            item.Name = string.IsNullOrWhiteSpace(item.Name)
                                ? GetIngredientName(recipeIngredient.IngredientsAndNutrients, language)
                                : item.Name.Trim();
                            if (string.IsNullOrWhiteSpace(item.Measure))
                            {
                                item.Measure = GetMeasureText(recipeIngredient.Measure, language);
                            }
                        }
                        else if (candidateLookup.TryGetValue(item.IngredientId, out var candidate))
                        {
                            item.Name = string.IsNullOrWhiteSpace(item.Name) ? candidate.Name : item.Name.Trim();
                        }
                    }

                    item.Measure = NormalizeMeasureToken((item.Measure ?? string.Empty).Trim(), language);
                    item.Quantity = (item.Quantity ?? string.Empty).Trim();

                    // Keep quantity/unit in separate fields: AI sometimes returns quantity already containing the unit.
                    var qty = item.Quantity;
                    var measure = item.Measure;
                    NormalizeQuantityAndMeasure(ref qty, ref measure);
                    item.Quantity = qty;
                    item.Measure = NormalizeMeasureToken(measure, language);
                    item.Name = (item.Name ?? string.Empty).Trim();
                    return item;
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .ToList();
        }

        private static void NormalizeQuantityAndMeasure(ref string? quantity, ref string? measure)
        {
            var q = (quantity ?? string.Empty).Trim();
            var m = (measure ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(q) && string.IsNullOrWhiteSpace(m))
            {
                quantity = string.Empty;
                measure = string.Empty;
                return;
            }

            // Normalize decimals in quantity (EU comma -> dot).
            q = q.Replace(',', '.');

            // If quantity contains both number + unit, split it (or strip unit when measure already set).
            // Examples: "1000 g" + "g" -> quantity "1000", measure "g"
            //           "1 Stk." + "Stk." -> quantity "1", measure "Stk."
            var mSplit = Regex.Match(q, @"^(?<qty>\d+(?:\.\d+)?)(?:\s*)(?<unit>[A-Za-zÄÖÜäöü\.\-]+)$");
            if (mSplit.Success)
            {
                var qtyPart = (mSplit.Groups["qty"].Value ?? string.Empty).Trim();
                var unitPart = (mSplit.Groups["unit"].Value ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(m) || NormalizeUnitToken(m) == NormalizeUnitToken(unitPart))
                {
                    q = qtyPart;
                    m = string.IsNullOrWhiteSpace(m) ? unitPart : m;
                }
            }

            // If measure is present but quantity still ends with it (e.g. "500 ml"), strip it.
            if (!string.IsNullOrWhiteSpace(m))
            {
                var normalizedUnit = NormalizeUnitToken(m);
                var tokens = q.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (tokens.Length >= 2)
                {
                    var lastToken = tokens.Last();
                    if (NormalizeUnitToken(lastToken) == normalizedUnit)
                    {
                        q = string.Join(' ', tokens.Take(tokens.Length - 1));
                    }
                }
            }

            quantity = q.Trim();
            measure = m.Trim();

            static string NormalizeUnitToken(string value)
                => (value ?? string.Empty).Trim().TrimEnd('.').ToLowerInvariant();
        }

        private static string GetMeasureText(Measure? measure, string language)
        {
            if (measure == null)
            {
                return string.Empty;
            }

            return language switch
            {
                "en" => measure.Metrics_EN,
                "es" => measure.Metrics_ESP,
                "pt" => measure.Metrics_PRT,
                _ => measure.Metrics_DE
            } ?? string.Empty;
        }

        private static string NormalizeMeasureToken(string? measure, string language)
        {
            var m = (measure ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(m))
            {
                return string.Empty;
            }

            // Conservative DE-only normalization for UI consistency.
            if (!string.Equals(NormalizeLanguage(language), "de", StringComparison.OrdinalIgnoreCase))
            {
                return m;
            }

            if (string.Equals(m, "stk", StringComparison.OrdinalIgnoreCase) || string.Equals(m, "stk.", StringComparison.OrdinalIgnoreCase))
            {
                return "Stk.";
            }

            if (string.Equals(m, "tl", StringComparison.OrdinalIgnoreCase) || string.Equals(m, "tl.", StringComparison.OrdinalIgnoreCase))
            {
                return "TL.";
            }

            if (string.Equals(m, "el", StringComparison.OrdinalIgnoreCase) || string.Equals(m, "el.", StringComparison.OrdinalIgnoreCase))
            {
                return "EL.";
            }

            if (string.Equals(m, "prise", StringComparison.OrdinalIgnoreCase))
            {
                return "Prise";
            }

            if (string.Equals(m, "ml", StringComparison.OrdinalIgnoreCase))
            {
                return "ml";
            }

            if (string.Equals(m, "g", StringComparison.OrdinalIgnoreCase))
            {
                return "g";
            }

            return m;
        }

        private static string NormalizeIngredientLookupName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value
                .Trim()
                .ToLowerInvariant()
                .Replace("ä", "ae")
                .Replace("ö", "oe")
                .Replace("ü", "ue")
                .Replace("ß", "ss")
                .Replace("(gemahlen)", string.Empty)
                .Replace("(in öl)", string.Empty)
                .Replace("(in oel)", string.Empty)
                .Replace("filets", "filet")
                .Replace("zehen", "zehe")
                .Replace(".", string.Empty)
                .Replace(",", string.Empty)
                .Trim();
        }

        private static string BuildIngredientsText(IEnumerable<RecipeAiTransformIngredientPreview>? ingredients)
        {
            if (ingredients == null)
            {
                return string.Empty;
            }

            return string.Join(Environment.NewLine, ingredients
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Name))
                .Select(x =>
                {
                    var quantity = (x.Quantity ?? string.Empty).Trim();
                    var measure = (x.Measure ?? string.Empty).Trim();
                    var name = (x.Name ?? string.Empty).Trim();
                    var quantityText = string.IsNullOrWhiteSpace(quantity)
                        ? string.Empty
                        : string.IsNullOrWhiteSpace(measure)
                            ? quantity
                            : $"{quantity} {measure}".Trim();
                    return string.IsNullOrWhiteSpace(quantityText) ? $"- {name}" : $"- {quantityText} {name}".TrimEnd();
                }));
        }

        private static List<RecipeAiTransformStepPreview> BuildStepPreview(RecipeBaseData recipe, string language)
        {
            return recipe.Steps?
                .OrderBy(x => x.StepIndex)
                .Select(x => new RecipeAiTransformStepPreview { Index = x.StepIndex, Text = PolishStepText(GetStepText(x.RecipePreparationStep, language), language) })
                .Where(x => !string.IsNullOrWhiteSpace(x.Text))
                .ToList() ?? new List<RecipeAiTransformStepPreview>();
        }

        private static string BuildPreparationTextFromStepPlan(IEnumerable<RecipeAiTransformStepPlanItem>? stepPlan, string language)
        {
            if (stepPlan == null)
            {
                return string.Empty;
            }

            var lines = stepPlan
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.RenderedText))
                .Select((x, index) => $"{index + 1}. {PolishStepText(x.RenderedText, language)}")
                .ToList();

            return lines.Count == 0 ? string.Empty : string.Join(Environment.NewLine, lines);
        }

        private static string BuildPreparationTextFromRecipe(RecipeBaseData recipe, string language)
        {
            var steps = BuildStepPreview(recipe, language);
            return BuildPreparationTextFromSteps(steps);
        }

        private static string TruncateForPrompt(string? text, int maxLength)
        {
            var trimmed = (text ?? string.Empty).Trim();
            if (trimmed.Length <= maxLength)
            {
                return trimmed;
            }

            return trimmed[..Math.Max(0, maxLength - 1)].TrimEnd() + "â€¦";
        }

        private List<string> BuildSelectedIngredientCookingNotes(IReadOnlyList<RecipeAiIngredientSuggestionItem> selectedIngredients, string language)
        {
            var notes = new List<string>();

            foreach (var ingredient in selectedIngredients)
            {
                var note = GetCookingNoteForIngredient(ingredient.Name, ingredient.CategoryKey, language);
                if (!string.IsNullOrWhiteSpace(note))
                {
                    notes.Add(note);
                }
            }

            return notes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private string GetCookingNoteForIngredient(string? ingredientName, string? categoryKey, string language)
        {
            var normalizedName = (ingredientName ?? string.Empty).Trim().ToLowerInvariant();
            var normalizedCategory = NormalizeCategoryKey(categoryKey);

            if (normalizedName.Contains("rote lins") || normalizedName.Contains("red lentil"))
            {
                return LocalizedWord(language,
                    "Rote Linsen garen schnell, binden stark und brauchen zusaetzliche Fluessigkeit; Sauce oder Schmorfluessigkeit entsprechend erhoehen oder separat garen.",
                    "Red lentils cook quickly, thicken strongly, and need extra liquid; increase sauce or braising liquid or cook them separately.",
                    "Las lentejas rojas se cuecen rápido, espesan mucho y necesitan líquido adicional; aumenta la salsa o cuécelas aparte.",
                    "As lentilhas vermelhas cozinham rápido, engrossam muito e precisam de mais líquido; aumenta o molho ou cozinha-as à parte.");
            }

            if (normalizedName.Contains("grüne lins") || normalizedName.Contains("gruene lins") || normalizedName.Contains("green lentil") || normalizedCategory.Contains("lentil") || normalizedCategory.Contains("legume"))
            {
                return LocalizedWord(language,
                    "Gruene oder feste Linsen brauchen deutlich mehr Fluessigkeit und oft laengere Garzeit; bei Bedarf vorgekocht oder separat gegart einarbeiten.",
                    "Green or firm lentils need noticeably more liquid and often more cooking time; precook or cook separately if needed.",
                    "Las lentejas verdes o firmes necesitan bastante más líquido y a menudo más tiempo; si hace falta, precocer o cocinar aparte.",
                    "Lentilhas verdes ou mais firmes precisam de bastante mais líquido e muitas vezes mais tempo; se necessário, pré-cozinha ou cozinha à parte.");
            }

            if (normalizedName.Contains("hühnerbrust") || normalizedName.Contains("huehnerbrust") || normalizedName.Contains("chicken breast"))
            {
                return LocalizedWord(language,
                    "Hühnerbrust gart schnell und trocknet leicht aus; eher spaeter zugeben oder nur kurz anbraten und sanft fertig garen.",
                    "Chicken breast cooks quickly and dries out easily; add it later or sear briefly and finish gently.",
                    "La pechuga de pollo se cocina rápido y se seca fácilmente; añádela más tarde o dórala brevemente y termina con cocción suave.",
                    "Peito de frango cozinha depressa e seca facilmente; junta mais tarde ou sela rapidamente e termina de forma suave.");
            }

            return string.Empty;
        }

        private void ApplyCookabilitySafeguards(RecipeAiTransformPreview preview, string language, IReadOnlyList<RecipeAiIngredientSuggestionItem> selectedIngredients)
        {
            if (selectedIngredients == null || selectedIngredients.Count == 0)
            {
                return;
            }

            var needsStepRefresh = false;

            foreach (var selectedIngredient in selectedIngredients)
            {
                if (!LooksLikeLentil(selectedIngredient.Name, selectedIngredient.CategoryKey))
                {
                    continue;
                }

                var extraLiquid = GetSuggestedLentilLiquid(selectedIngredient.Name, language);
                if (HasSeparateLentilCooking(preview, selectedIngredient.Name) || HasIntegratedLentilAdjustment(preview, selectedIngredient.Name))
                {
                    continue;
                }

                if (!HasLiquidAdjustment(preview, selectedIngredient.Name))
                {
                    preview.Highlights ??= new List<string>();
                    preview.Highlights.Add(LocalizedWord(language,
                        $"Kochbarkeits-Check: Für {selectedIngredient.Name} wurde zusätzliche Flüssigkeit berücksichtigt.",
                        $"Cookability check: extra liquid was accounted for for {selectedIngredient.Name}.",
                        $"Comprobación de cocción: se tuvo en cuenta líquido extra para {selectedIngredient.Name}.",
                        $"Verificação de cozedura: foi considerado líquido extra para {selectedIngredient.Name}."));

                    preview.Ingredients ??= new List<RecipeAiTransformIngredientPreview>();
                    preview.Ingredients.Add(new RecipeAiTransformIngredientPreview
                    {
                        Name = LocalizedWord(language, "Zusätzliche Brühe oder Wasser", "extra broth or water", "caldo o agua extra", "caldo ou água extra"),
                        Quantity = extraLiquid,
                        ChangeHint = LocalizedHint(language, $"für {selectedIngredient.Name}", $"for {selectedIngredient.Name}", $"para {selectedIngredient.Name}", $"para {selectedIngredient.Name}"),
                        IsModified = true
                    });

                    var liquidNote = LocalizedWord(language,
                        $"Gib {selectedIngredient.Name} direkt mit in den Bräter und plane zusätzlich etwa {extraLiquid} Brühe oder Wasser sowie etwas Salz ein, damit sie im Schmorfond garen, ohne die Sauce zu stark einzudicken.",
                        $"Add {selectedIngredient.Name} directly to the braiser and plan for about {extraLiquid} extra broth or water plus a little salt so they cook in the braising liquid without thickening the sauce too much.",
                        $"Añade {selectedIngredient.Name} directamente a la cocotte y calcula unos {extraLiquid} extra de caldo o agua junto con un poco de sal para que se cuezan en el fondo de cocción sin espesar demasiado la salsa.",
                        $"Junta {selectedIngredient.Name} diretamente ao tacho e conta com cerca de {extraLiquid} extra de caldo ou água, além de um pouco de sal, para que cozinhem no líquido sem engrossar demasiado o molho.");

                    preview.PreparationText = string.IsNullOrWhiteSpace(preview.PreparationText)
                        ? liquidNote
                        : $"{preview.PreparationText}{Environment.NewLine}{Environment.NewLine}{liquidNote}";

                    needsStepRefresh = true;
                }
            }

            if (needsStepRefresh)
            {
                preview.IngredientsText = BuildIngredientsText(preview.Ingredients);
                preview.Steps = BuildStepPreviewFromPreparationText(preview.PreparationText, language);
            }
        }

        private static bool LooksLikeLentil(string? ingredientName, string? categoryKey)
        {
            var normalizedName = (ingredientName ?? string.Empty).Trim().ToLowerInvariant();
            var normalizedCategory = NormalizeCategoryKey(categoryKey);
            return normalizedName.Contains("lins") || normalizedName.Contains("lentil") || normalizedCategory.Contains("lentil") || normalizedCategory.Contains("legume");
        }

        private static string GetSuggestedLentilLiquid(string? ingredientName, string language)
        {
            var normalizedName = (ingredientName ?? string.Empty).Trim().ToLowerInvariant();
            var quantity = normalizedName.Contains("rote lins") || normalizedName.Contains("red lentil")
                ? "300 ml"
                : "400 ml";

            return quantity;
        }

        private static bool HasLiquidAdjustment(RecipeAiTransformPreview preview, string? ingredientName)
        {
            if (HasSeparateLentilCooking(preview, ingredientName) || HasIntegratedLentilAdjustment(preview, ingredientName))
            {
                return true;
            }

            var ingredients = preview.Ingredients ?? new List<RecipeAiTransformIngredientPreview>();
            if (ingredients.Any(x =>
                    !string.IsNullOrWhiteSpace(x.Name) &&
                    (x.Name.Contains("Brühe", StringComparison.OrdinalIgnoreCase)
                     || x.Name.Contains("Bruehe", StringComparison.OrdinalIgnoreCase)
                     || x.Name.Contains("Wasser", StringComparison.OrdinalIgnoreCase)
                     || x.Name.Contains("broth", StringComparison.OrdinalIgnoreCase)
                     || x.Name.Contains("water", StringComparison.OrdinalIgnoreCase))))
            {
                return true;
            }

            var preparation = (preview.PreparationText ?? string.Empty).ToLowerInvariant();
            var ingredientsText = (preview.IngredientsText ?? string.Empty).ToLowerInvariant();
            var ingredientNameLower = (ingredientName ?? string.Empty).ToLowerInvariant();

            var mentionsLiquid = preparation.Contains("brühe") || preparation.Contains("bruhe") || preparation.Contains("wasser") || preparation.Contains("liquid") || preparation.Contains("caldo");
            var mentionsIngredient = string.IsNullOrWhiteSpace(ingredientNameLower) || preparation.Contains(ingredientNameLower);
            var ingredientListHasExtraLiquid = ingredientsText.Contains("zusätzliche brühe") || ingredientsText.Contains("zusatzliche bruhe") || ingredientsText.Contains("extra broth") || ingredientsText.Contains("extra water");

            return ingredientListHasExtraLiquid || (mentionsLiquid && mentionsIngredient);
        }

        private static bool HasSeparateLentilCooking(RecipeAiTransformPreview preview, string? ingredientName)
        {
            var preparation = (preview.PreparationText ?? string.Empty).ToLowerInvariant();
            var ingredientNameLower = (ingredientName ?? string.Empty).Trim().ToLowerInvariant();
            var mentionsIngredient = string.IsNullOrWhiteSpace(ingredientNameLower)
                || preparation.Contains(ingredientNameLower)
                || preparation.Contains("lins")
                || preparation.Contains("lentil");

            var mentionsSeparateCooking =
                preparation.Contains("separat")
                || preparation.Contains("separate")
                || preparation.Contains("separaten topf")
                || preparation.Contains("eigenen topf")
                || preparation.Contains("cook separately")
                || preparation.Contains("separate pot")
                || preparation.Contains("apart");

            return mentionsIngredient && mentionsSeparateCooking;
        }

        private static bool HasIntegratedLentilAdjustment(RecipeAiTransformPreview preview, string? ingredientName)
        {
            var preparation = (preview.PreparationText ?? string.Empty).ToLowerInvariant();
            var ingredientNameLower = (ingredientName ?? string.Empty).Trim().ToLowerInvariant();
            var mentionsIngredient = string.IsNullOrWhiteSpace(ingredientNameLower)
                || preparation.Contains(ingredientNameLower)
                || preparation.Contains("lins")
                || preparation.Contains("lentil");

            var mentionsBraiser =
                preparation.Contains("bräter")
                || preparation.Contains("braeter")
                || preparation.Contains("schmorfond")
                || preparation.Contains("schmor")
                || preparation.Contains("braiser")
                || preparation.Contains("braising liquid");

            var mentionsLiquid =
                preparation.Contains("brühe")
                || preparation.Contains("bruhe")
                || preparation.Contains("wasser")
                || preparation.Contains("broth")
                || preparation.Contains("water");

            return mentionsIngredient && mentionsBraiser && mentionsLiquid;
        }

        private static string BuildPreparationTextFromSteps(IEnumerable<RecipeAiTransformStepPreview>? steps)
        {
            if (steps == null)
            {
                return string.Empty;
            }

            var lines = steps
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Text))
                .Select((x, index) => $"{index + 1}. {(x.Text ?? string.Empty).Trim()}")
                .ToList();

            return lines.Count == 0 ? string.Empty : string.Join(Environment.NewLine + Environment.NewLine, lines);
        }

        private static List<RecipeAiTransformStepPreview> BuildStepPreviewFromPreparationText(string? preparationText, string language)
        {
            var normalized = (preparationText ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Trim();

            if (string.IsNullOrWhiteSpace(normalized))
            {
                return new List<RecipeAiTransformStepPreview>();
            }

            normalized = Regex.Replace(normalized, @"(?<!^)(\n?)(\d+\)\s+)", "\n\n$2");
            normalized = Regex.Replace(normalized, @"(?<!^)(\n?)(\d+\.\s+)", "\n\n$2");

            var lines = normalized
                .Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(CleanPreparationLine)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            return lines
                .Select((line, index) => new RecipeAiTransformStepPreview
                {
                    Index = index + 1,
                    Text = PolishStepText(line, language)
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Text))
                .ToList();
        }

        private static string CleanPreparationLine(string rawLine)
        {
            var line = (rawLine ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                return string.Empty;
            }

            var dotIndex = line.IndexOf('.');
            if (dotIndex > 0 && dotIndex < 4 && int.TryParse(line[..dotIndex], out _))
            {
                line = line[(dotIndex + 1)..].Trim();
            }

            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                line = line[2..].Trim();
            }

            return line;
        }


        private static void ApplySimpleUserNoteAdjustment(List<RecipeAiTransformIngredientPreview> ingredients, List<RecipeAiTransformStepPlanItem> stepPlan, string language, string userNote)
        {
            var lower = userNote.ToLowerInvariant();
            if (lower.Contains("kartoffel") || lower.Contains("potato"))
            {
                ReplaceIngredient(ingredients, language, new[] { "reis", "rice", "nudel", "pasta" }, LocalizedWord(language, "Kartoffeln", "potatoes", "patatas", "batatas"), LocalizedHint(language, "nach User-Wunsch", "per user request", "segÃºn el usuario", "a pedido do utilizador"), new List<string>());
                ReplaceVariableValue(stepPlan, new[] { "reis", "rice", "pasta", "nudeln", "noodles" }, LocalizedWord(language, "Kartoffeln", "potatoes", "patatas", "batatas"));
            }
            else if (lower.Contains("reis") || lower.Contains("rice"))
            {
                ReplaceIngredient(ingredients, language, new[] { "kartoffel", "potato" }, LocalizedWord(language, "Reis", "rice", "arroz", "arroz"), LocalizedHint(language, "nach User-Wunsch", "per user request", "segÃºn el usuario", "a pedido do utilizador"), new List<string>());
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
                "highprotein" => LocalizedWord(language, "Mehr Protein", "More Protein", "MÃ¡s proteÃ­na", "Mais proteÃ­na"),
                _ => LocalizedWord(language, "AI-Variante", "AI Variant", "Variante AI", "Variante AI")
            };
        }

        private static string GetLanguageLabel(string language)
            => language switch
            {
                "en" => "English",
                "es" => "EspaÃ±ol",
                "pt" => "PortuguÃªs",
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
                "es" => $"{title} Â· {prefix}",
                "pt" => $"{title} Â· {prefix}",
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
                "es" => "{{ingredient}} debe incluir el artÃ­culo correcto cuando tenga sentido, por ejemplo 'los huevos'.",
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
                var extracted = ExtractJsonPayloadFromString(outputTextElement.GetString());
                if (!string.IsNullOrWhiteSpace(extracted))
                {
                    return extracted;
                }
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
                        var extracted = ExtractJsonPayloadFromString(textElement.GetString());
                        if (!string.IsNullOrWhiteSpace(extracted))
                        {
                            return extracted;
                        }
                    }

                    if (contentItem.TryGetProperty("text", out var fallbackTextElement)
                        && fallbackTextElement.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(fallbackTextElement.GetString()))
                    {
                        // Only treat generic "text" fields as output when the content item itself is output_text.
                        if (contentItem.TryGetProperty("type", out var fallbackType)
                            && string.Equals(fallbackType.GetString(), "output_text", StringComparison.OrdinalIgnoreCase))
                        {
                            var extracted = ExtractJsonPayloadFromString(fallbackTextElement.GetString());
                            if (!string.IsNullOrWhiteSpace(extracted))
                            {
                                return extracted;
                            }
                        }
                    }

                    if (contentItem.TryGetProperty("json", out var jsonElement))
                    {
                        // Only accept embedded json when the content item is explicitly an output_json container.
                        if (contentItem.TryGetProperty("type", out var jsonType)
                            && string.Equals(jsonType.GetString(), "output_json", StringComparison.OrdinalIgnoreCase))
                        {
                            return jsonElement.ValueKind switch
                            {
                                JsonValueKind.Object => jsonElement.GetRawText(),
                                JsonValueKind.Array => jsonElement.GetRawText(),
                                JsonValueKind.String => jsonElement.GetString(),
                                _ => null
                            };
                        }
                    }
                }
            }

            // Do NOT scan the whole response for "some JSON-looking string" because that easily picks up
            // tool-call arguments (e.g. {"language":...,"categoryKeys":...}) and breaks the tool loop.
            return null;
        }

        // Intentionally no generic JSON-scanning fallback:
        // For tool-calling runs, scanning the whole response tends to capture tool argument JSON
        // instead of the model's final json_schema output.

        private static string? ExtractJsonPayloadFromString(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                trimmed = Regex.Replace(trimmed, "^```(?:json)?\\s*", string.Empty, RegexOptions.IgnoreCase);
                trimmed = Regex.Replace(trimmed, "\\s*```$", string.Empty, RegexOptions.IgnoreCase);
                trimmed = trimmed.Trim();
            }

            if ((trimmed.StartsWith("{") && trimmed.EndsWith("}")) || (trimmed.StartsWith("[") && trimmed.EndsWith("]")))
            {
                return trimmed;
            }

            var objectStart = trimmed.IndexOf('{');
            var objectEnd = trimmed.LastIndexOf('}');
            if (objectStart >= 0 && objectEnd > objectStart)
            {
                return trimmed.Substring(objectStart, objectEnd - objectStart + 1);
            }

            var arrayStart = trimmed.IndexOf('[');
            var arrayEnd = trimmed.LastIndexOf(']');
            if (arrayStart >= 0 && arrayEnd > arrayStart)
            {
                return trimmed.Substring(arrayStart, arrayEnd - arrayStart + 1);
            }

            // No JSON object/array found; treat as non-JSON so callers can keep searching.
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

        private sealed class RecipeConceptPickResponse
        {
            public List<RecipeConceptPickItem> Concepts { get; set; } = new();
        }

        private sealed class RecipeConceptPickItem
        {
            public string Key { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Summary { get; set; } = string.Empty;
            public string Approach { get; set; } = string.Empty;
            public List<RecipeConceptPickIngredientItem> Ingredients { get; set; } = new();
        }

        private sealed class RecipeConceptPickIngredientItem
        {
            public int IngredientId { get; set; }
            public string Action { get; set; } = "add"; // add|replace|remove
            public int? ReplacesIngredientId { get; set; }
            public string Quantity { get; set; } = string.Empty;
            public string Measure { get; set; } = string.Empty;
            public string Reason { get; set; } = string.Empty;
            public bool IsMainProtein { get; set; }
        }

        private sealed class RecipeConceptSuggestion
        {
            public string Key { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Summary { get; set; } = string.Empty;
            public string Approach { get; set; } = string.Empty;
            public List<RecipeAiConceptIngredientPlanItem> IngredientPlan { get; set; } = new();
        }

        private sealed class IngredientSelectionContext
        {
            public bool HasPrimaryProtein { get; set; }
            public bool HasMeatMainProtein { get; set; }
            public bool HasFishMainProtein { get; set; }
            public bool HasCarbSide { get; set; }
            public bool HasPotatoSide { get; set; }
            public bool HasRiceOrPastaSide { get; set; }
            public bool PreferSideReplacement { get; set; }
            public string MainProteinCategory { get; set; } = string.Empty;
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

        private sealed class AiPromptRequest
        {
            public string SystemPrompt { get; set; } = string.Empty;
            public string UserPrompt { get; set; } = string.Empty;
            public string SchemaName { get; set; } = string.Empty;
            public object Schema { get; set; } = new();
            public int MaxOutputTokens { get; set; } = 1200;
            public int GeminiThinkingBudget { get; set; } = 0;
            public bool AllowTools { get; set; } = false;
            public AiModelTier ModelTier { get; set; } = AiModelTier.Cheap;
            public int MaxToolRounds { get; set; } = 6;
            public IReadOnlyList<string>? AllowedToolCategoryKeys { get; set; }
        }

        private enum AiModelTier
        {
            Cheap = 0,
            Premium = 1
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


