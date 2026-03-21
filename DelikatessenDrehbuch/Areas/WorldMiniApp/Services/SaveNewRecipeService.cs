using DelikatessenDrehbuch.Areas.WorldMiniApp.Exceptions;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using Microsoft.EntityFrameworkCore;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces
{
    public class SaveNewRecipeService : ISaveNewRecipeService
    {
        private readonly ApplicationDbContext _context;

        public SaveNewRecipeService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task SaveNewAsync(SaveNewRecipeModel recipesModel,bool wordUserImage)
        {
            
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                
                var recipeBaseData = await GetOrUpdateRecipeAsync(recipesModel);

                
                await ProcessIngredientsAsync(recipeBaseData, recipesModel);

                
                await ProcessPreparationStepsAsync(recipeBaseData, recipesModel);


                await ProcessRecipeImageAsync(recipeBaseData, recipesModel,wordUserImage);

              
                await _context.SaveChangesAsync();

             
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
               
                await transaction.RollbackAsync();

                throw;
            }
        }



        private async Task<RecipeBaseData> GetOrUpdateRecipeAsync(SaveNewRecipeModel model)
        {
            var existingRecipe = await _context.RecipeBaseData
                .FirstOrDefaultAsync(r => r.Title == model.Recipes.Name);

            if (existingRecipe == null)
            {
                var newRecipe = new RecipeBaseData
                {
                    Title = model.Recipes.Name,
                    PersonCount = (int)model.Recipes.RecipePersonCount,
                    Preferences = model.Querys,
                    Category = model.Recipes.Category,
                    PreparationTime = (int)model.Recipes.PreparationTime
                };

               
                await _context.RecipeBaseData.AddAsync(newRecipe);
                return newRecipe;
            }
            else
            {
                existingRecipe.PersonCount = (int)model.Recipes.RecipePersonCount;
                return existingRecipe;
            }
        }

        private async Task ProcessIngredientsAsync(RecipeBaseData recipe, SaveNewRecipeModel model)
        {
            var nutrientIds = model.IngredientMeasureQuantity.Select(i => i.IngredientsAndNutrients.Id).Distinct().ToList();
            var measureNames = model.IngredientMeasureQuantity
                .Select(i => (i.Measure.Metrics_DE == "Gramm" ? "g." : i.Measure.Metrics_DE).ToLower())
                .Distinct().ToList();
            var quantities = model.IngredientMeasureQuantity.Select(i => i.Quantity.Quantitys).Distinct().ToList();

            var nutrientsDict = await _context.IngredientsAndNutrients
                .Where(n => nutrientIds.Contains(n.Id)).ToDictionaryAsync(n => n.Id);
            var measuresDict = await _context.Metrics
                .Where(m => measureNames.Contains(m.Metrics_DE.ToLower())).ToDictionaryAsync(m => m.Metrics_DE.ToLower());
            var quantitiesDict = await _context.Quantities
                .Where(q => quantities.Contains(q.Quantitys)).ToDictionaryAsync(q => q.Quantitys);

            foreach (var incoming in model.IngredientMeasureQuantity)
            {
                if (incoming.Measure.Metrics_DE == "Gramm")
                    incoming.Measure.Metrics_DE = "g.";

                var existing = await _context.IngredientMeasureQuantity
                    .FirstOrDefaultAsync(x => x.IngredientsAndNutrients.Name_DE == incoming.IngredientsAndNutrients.Name_DE
                                           && x.Measure.Metrics_DE == incoming.Measure.Metrics_DE
                                           && x.Quantity.Quantitys == incoming.Quantity.Quantitys);

                IngredientMeasureQuantity ingredientToUse;
                if (existing != null)
                {
                    ingredientToUse = existing;
                }
                else
                {
                    if (!nutrientsDict.TryGetValue(incoming.IngredientsAndNutrients.Id, out var nutrientRef)
                        || !measuresDict.TryGetValue(incoming.Measure.Metrics_DE.ToLower(), out var measureRef)
                        || !quantitiesDict.TryGetValue(incoming.Quantity.Quantitys, out var quantityRef))
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
