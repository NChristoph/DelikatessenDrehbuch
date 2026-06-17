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

                // WICHTIG: SaveChanges hier um RecipeId zu bekommen
                await _context.SaveChangesAsync();

                await ProcessIngredientsAsync(recipeBaseData, recipesModel);

                await ProcessPreparationStepsAsync(recipeBaseData, recipesModel);

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
            // WICHTIG: Alte Steps löschen bevor neue hinzugefügt werden!
            // Sonst werden Steps bei jedem Save dupliziert
            var existingSteps = await _context.RecipeSteps
                .Where(s => s.RecipeId == recipe.Id)
                .ToListAsync();

            if (existingSteps.Any())
            {
                _context.RecipeSteps.RemoveRange(existingSteps);
            }

            // NEW SYSTEM: RecipeSteps (normalized, Culture + Text)
            // Old translation system removed
            // Steps are handled by RecipeTranslationService in the new system
            if (model.RecipeSteps != null && model.RecipeSteps.Any())
            {
                foreach (var step in model.RecipeSteps)
                {
                    step.RecipeId = recipe.Id;
                    step.CreatedAt = DateTime.UtcNow;
                    _context.RecipeSteps.Add(step);
                }
            }
            // SaveChanges wird von der Outer-Transaction gehandled
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
