using DelikatessenDrehbuch.Areas.WorldMiniApp.Exceptions;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces
{
    public class SaveNewRecipeService : ISaveNewRecipeService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SaveNewRecipeService> _logger;

        public SaveNewRecipeService(ApplicationDbContext context, ILogger<SaveNewRecipeService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<RecipeBaseData> SaveNewAsync(SaveNewRecipeModel recipesModel,bool wordUserImage)
        {

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {

                var recipeBaseData = await GetOrUpdateRecipeAsync(recipesModel);


                await ProcessIngredientsAsync(recipeBaseData, recipesModel);


                await ProcessPreparationStepsAsync(recipeBaseData, recipesModel);
                await ProcessSmartStepsAsync(recipeBaseData, recipesModel);

                await ProcessRecipeImageAsync(recipeBaseData, recipesModel,wordUserImage);


                await _context.SaveChangesAsync();


                await transaction.CommitAsync();

                return recipeBaseData;
            }
            catch (Exception ex)
            {

                await transaction.RollbackAsync();

                throw;
            }
        }



        private async Task<RecipeBaseData> GetOrUpdateRecipeAsync(SaveNewRecipeModel model)
        {
            var recipeInput = model.Recipes ?? new Recipes();
            var recipeTitle = (recipeInput.Name ?? string.Empty).Trim();
            var recipeCategory = (recipeInput.Category ?? string.Empty).Trim();
            var recipePreferences = (model.Querys ?? string.Empty).Trim();
            var personCount = recipeInput.RecipePersonCount.GetValueOrDefault(2);
            var preparationTime = recipeInput.PreparationTime.GetValueOrDefault(0);

            var existingRecipe = await _context.RecipeBaseData
                .FirstOrDefaultAsync(r => r.Title == recipeTitle);

            if (existingRecipe == null)
            {
                var newRecipe = new RecipeBaseData
                {
                    Title = recipeTitle,
                    PersonCount = personCount,
                    Preferences = recipePreferences,
                    Category = recipeCategory,
                    PreparationTime = preparationTime
                };

               
                await _context.RecipeBaseData.AddAsync(newRecipe);
                return newRecipe;
            }
            else
            {
                existingRecipe.PersonCount = personCount;
                existingRecipe.Preferences = recipePreferences;
                existingRecipe.Category = recipeCategory;
                existingRecipe.PreparationTime = preparationTime;
                return existingRecipe;
            }
        }

        private async Task ProcessIngredientsAsync(RecipeBaseData recipe, SaveNewRecipeModel model)
        {
            static string NormalizeMeasureName(string? value)
            {
                var measureName = (value ?? string.Empty).Trim();
                if (string.Equals(measureName, "Gramm", StringComparison.OrdinalIgnoreCase))
                {
                    measureName = "g.";
                }

                return measureName.ToLowerInvariant();
            }

            var incomingItems = (model.IngredientMeasureQuantity ?? new List<IngredientMeasureQuantity>())
                .Where(i => i != null)
                .ToList();

            var nutrientIds = incomingItems
                .Select(i => i.IngredientsAndNutrients?.Id ?? 0)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            var measureNames = incomingItems
                .Select(i => NormalizeMeasureName(i.Measure?.Metrics_DE))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct()
                .ToList();

            var quantities = incomingItems
                .Select(i => i.Quantity?.Quantitys)
                .Where(quantity => quantity != null)
                .Select(quantity => quantity!.Value)
                .Distinct()
                .ToList();

            var nutrientsDict = await _context.IngredientsAndNutrients
                .Where(n => nutrientIds.Contains(n.Id)).ToDictionaryAsync(n => n.Id);
            var measuresDict = (await _context.Metrics
                    .Where(m => measureNames.Contains(m.Metrics_DE.ToLower()))
                    .ToListAsync())
                .GroupBy(m => m.Metrics_DE.ToLower())
                .ToDictionary(g => g.Key, g => g.First());
            var quantitiesDict = (await _context.Quantities
                    .Where(q => quantities.Contains(q.Quantitys))
                    .ToListAsync())
                .GroupBy(q => q.Quantitys)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var incoming in incomingItems)
            {
                var nutrientId = incoming.IngredientsAndNutrients?.Id ?? 0;
                var measureName = NormalizeMeasureName(incoming.Measure?.Metrics_DE);
                var quantityValue = incoming.Quantity?.Quantitys;

                if (nutrientId <= 0 || string.IsNullOrWhiteSpace(measureName) || quantityValue == null)
                {
                    throw new WorldMiniAppNotFoundException("Zutat, Menge oder Maßeinheit fehlt im Upload.");
                }

                if (incoming.Measure != null)
                {
                    incoming.Measure.Metrics_DE = measureName == "g." ? "g." : incoming.Measure.Metrics_DE;
                }

                var existing = await _context.IngredientMeasureQuantity
                    .FirstOrDefaultAsync(x => x.IngredientsAndNutrients.Id == nutrientId
                                           && x.Measure.Metrics_DE.ToLower() == measureName
                                           && x.Quantity.Quantitys == quantityValue);

                IngredientMeasureQuantity ingredientToUse;
                if (existing != null)
                {
                    ingredientToUse = existing;
                }
                else
                {
                    if (!nutrientsDict.TryGetValue(nutrientId, out var nutrientRef)
                        || !measuresDict.TryGetValue(measureName, out var measureRef)
                        || !quantitiesDict.TryGetValue((double)quantityValue, out var quantityRef))
                    {
                        throw new WorldMiniAppNotFoundException("Referenzdaten für Zutat nicht gefunden.");
                    }

                    ingredientToUse = new IngredientMeasureQuantity
                    {
                        IngredientsAndNutrients = nutrientRef,
                        Measure = measureRef,
                        Quantity = quantityRef
                    };

                    await _context.IngredientMeasureQuantity.AddAsync(ingredientToUse);
                }

                var joinEntity = new RecipeJoinIngredientMeasureQuantity
                {
                    Recipe = recipe,
                    Ingredient = ingredientToUse
                };

                await _context.RecipeJoinIngredientMeasureQuantity.AddAsync(joinEntity);
            }
        }

        private async Task ProcessPreparationStepsAsync(RecipeBaseData recipe, SaveNewRecipeModel model)
        {
            foreach (var step in model.RecipeJoinPreparationSteps ?? Enumerable.Empty<RecipeJoinPreparationSteps>())
            {
                step.Recipe = recipe;
                step.RecipePreparationStep = await ResolvePreparationStepAsync(step);
                if (step.RecipePreparationStep == null)
                {
                    continue;
                }

                _context.RecipeJoinPreparationSteps.Add(step);
            }
        }

        private async Task ProcessSmartStepsAsync(RecipeBaseData recipe, SaveNewRecipeModel model)
        {
            foreach (var stepRef in model.SmartStepReferences ?? Enumerable.Empty<SmartStepReferenceInput>())
            {
                var masterStepKey = (stepRef.MasterStepKey ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(masterStepKey))
                {
                    continue;
                }

                // Validate MasterStepKey format (should match pattern: CATEGORY_ACTION_XX)
                if (!Regex.IsMatch(masterStepKey, @"^[A-Z_]+_\d{2}$"))
                {
                    _logger.LogWarning("MasterStepKey '{MasterKey}' does not match expected pattern (CATEGORY_ACTION_XX). Allowing but flagging for review.", masterStepKey);
                }

                var parsed = ParseSmartStepMetadata(stepRef.MetadataJson);
                var normalizedVariablesJson = NormalizeJson(parsed.variablesJson);

                var existing = await _context.SmartRecipeStep
                    .Where(x =>
                        x.MasterStepKey == masterStepKey &&
                        x.Phase == parsed.phase &&
                        x.Equipment == parsed.equipment)
                    .ToListAsync();

                var match = existing.FirstOrDefault(x => NormalizeJson(x.VariablesJson) == normalizedVariablesJson);

                var smartStep = match;
                if (smartStep == null)
                {
                    smartStep = new SmartRecipeStep
                    {
                        MasterStepKey = masterStepKey,
                        VariablesJson = normalizedVariablesJson,
                        Phase = parsed.phase,
                        Equipment = parsed.equipment
                    };

                    await _context.SmartRecipeStep.AddAsync(smartStep);
                }

                var join = new RecipeJoinSmartStep
                {
                    Recipe = recipe,
                    SmartRecipeStep = smartStep,
                    StepIndex = stepRef.StepIndex > 0 ? stepRef.StepIndex : 1
                };

                await _context.RecipeJoinSmartStep.AddAsync(join);
            }
        }

        private static string NormalizeJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return "{}";
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                var options = new JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNamingPolicy = null
                };
                return JsonSerializer.Serialize(document.RootElement, options);
            }
            catch
            {
                return "{}";
            }
        }

        private (string variablesJson, int? phase, string? equipment) ParseSmartStepMetadata(string? metadataJson)
        {
            if (string.IsNullOrWhiteSpace(metadataJson))
            {
                return ("{}", null, null);
            }

            try
            {
                using var document = JsonDocument.Parse(metadataJson);
                var root = document.RootElement;

                var variablesJson = root.TryGetProperty("variables", out var variablesElement)
                    ? variablesElement.GetRawText()
                    : "{}";

                int? phase = null;
                if (root.TryGetProperty("phase_key", out var phaseElement))
                {
                    var phaseRaw = phaseElement.ValueKind == JsonValueKind.String
                        ? phaseElement.GetString()
                        : phaseElement.GetRawText();

                    if (int.TryParse(phaseRaw, out var parsedPhase))
                    {
                        phase = parsedPhase;
                    }
                }

                string? equipment = null;
                if (root.TryGetProperty("equipment_key", out var equipmentElement))
                {
                    equipment = equipmentElement.ValueKind == JsonValueKind.String
                        ? equipmentElement.GetString()
                        : equipmentElement.GetRawText();

                    equipment = string.IsNullOrWhiteSpace(equipment) ? null : equipment.Trim();
                }

                return (variablesJson, phase, equipment);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse smart step metadata JSON. Metadata: {MetadataJson}", metadataJson);
                return ("{}", null, null);
            }
        }

        private async Task<RecipePreparationSteps?> ResolvePreparationStepAsync(RecipeJoinPreparationSteps joinStep)
        {
            var postedStep = joinStep.RecipePreparationStep;
            var hasPostedText = !string.IsNullOrWhiteSpace(postedStep?.Step_DE)
                || !string.IsNullOrWhiteSpace(postedStep?.Step_EN)
                || !string.IsNullOrWhiteSpace(postedStep?.Step_ESP)
                || !string.IsNullOrWhiteSpace(postedStep?.Step_PRT);

            if (joinStep.PreparationStepId > 0 && !hasPostedText)
            {
                return await _context.RecipePreparationSteps.FirstOrDefaultAsync(x => x.Id == joinStep.PreparationStepId);
            }

            if (!hasPostedText)
            {
                return null;
            }

            var de = (postedStep.Step_DE ?? string.Empty).Trim();
            var en = (postedStep.Step_EN ?? string.Empty).Trim();
            var esp = (postedStep.Step_ESP ?? string.Empty).Trim();
            var prt = (postedStep.Step_PRT ?? string.Empty).Trim();
            var phase = postedStep.Phase;
            var equipment = postedStep.Equipment;

            if (string.IsNullOrWhiteSpace(de) && string.IsNullOrWhiteSpace(en))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(de))
            {
                de = en;
            }

            if (string.IsNullOrWhiteSpace(en))
            {
                en = de;
            }

            var existing = await _context.RecipePreparationSteps.FirstOrDefaultAsync(x => x.Step_DE == de
                && x.Step_EN == en
                && x.Step_ESP == esp
                && x.Step_PRT == prt
                && x.Phase == phase
                && x.Equipment == equipment);

            if (existing != null)
            {
                joinStep.PreparationStepId = existing.Id;
                return existing;
            }

            var newStep = new RecipePreparationSteps
            {
                Step_DE = de,
                Step_EN = en,
                Step_ESP = esp,
                Step_PRT = prt,
                Phase = phase,
                Equipment = equipment
            };

            _context.RecipePreparationSteps.Add(newStep);
            return newStep;
        }

        private async Task ProcessRecipeImageAsync(RecipeBaseData recipe, SaveNewRecipeModel model,bool wordlUserImage)
        {
            var imagePath = !string.IsNullOrWhiteSpace(model.Recipes.ImagePath)
                ? model.Recipes.ImagePath
                : await _context.Recipes
                    .Where(x => x.Preparation == model.Recipes.Preparation)
                    .Select(x => x.ImagePath)
                    .FirstOrDefaultAsync();

            if (imagePath != null)
            {
                var recipeImage = new RecipeBaseDataImage
                {
                    Recipe = recipe,
                    Image = imagePath,
                    WorldAppImage = wordlUserImage

                };
                _context.RecipeBaseDataImage.Add(recipeImage);
            }
        }
    }
}
