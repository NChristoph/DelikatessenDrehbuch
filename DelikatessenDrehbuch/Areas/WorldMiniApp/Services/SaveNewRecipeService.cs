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

                
                ProcessPreparationSteps(recipeBaseData, recipesModel);

                
                ProcessRecipeImage(recipeBaseData, recipesModel,wordUserImage);

              
                await _context.SaveChangesAsync();

             
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
               
                await transaction.RollbackAsync();

                throw ex;
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
                    PreperationTime = (int)model.Recipes.PreparationTime
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
            foreach (var incomingIngredient in model.IngredientMeasureQuantity)
            {
                var ingredientToUse = await GetOrCreateIngredientMeasureQuantityAsync(incomingIngredient);

                var joinEntity = new RecipeJoinIngredientMeasureQuantity
                {
                    Recipe = recipe, 
                    Ingredient = ingredientToUse
                };

                await _context.RecipeJoinIngredientMeasureQuantity.AddAsync(joinEntity);
            }
        }

        private async Task<IngredientMeasureQuantity> GetOrCreateIngredientMeasureQuantityAsync(IngredientMeasureQuantity incoming)
        {
            var existing = await _context.IngredientMeasureQuantity
                .FirstOrDefaultAsync(x => x.IngredientsAndNutrients.Name_DE == incoming.IngredientsAndNutrients.Name_DE
                                       && x.Measure.Metriks_DE == incoming.Measure.Metriks_DE
                                       && x.Quantity.Quantitys == incoming.Quantity.Quantitys);

            if (existing != null) return existing;

            if (incoming.Measure.Metriks_DE == "Gramm")
                incoming.Measure.Metriks_DE = "g.";

            var nutrientRef = await _context.IngredientsAndNutrients.FirstOrDefaultAsync(x => x.Id == incoming.IngredientsAndNutrients.Id);
            var measureRef = await _context.Metrics.FirstOrDefaultAsync(x => x.Metriks_DE.ToLower() == incoming.Measure.Metriks_DE.ToLower());
            var quantityRef = await _context.Quantities.FirstOrDefaultAsync(x => x.Quantitys == incoming.Quantity.Quantitys);

            if (nutrientRef == null || measureRef == null || quantityRef == null)
                throw new InvalidOperationException("Referenzdaten für Zutat nicht gefunden.");

            var newIngredient = new IngredientMeasureQuantity
            {
                IngredientsAndNutrients = nutrientRef,
                Measure = measureRef,
                Quantity = quantityRef
            };

            await _context.IngredientMeasureQuantity.AddAsync(newIngredient);
            return newIngredient;
        }

        private void ProcessPreparationSteps(RecipeBaseData recipe, SaveNewRecipeModel model)
        {
            foreach (var step in model.RecipeJoyinPreperationSteps ?? Enumerable.Empty<RecipeJoyinPreperationSteps>())
            {
                step.Recipe = recipe;
                step.RecipePreperationStep = ResolvePreparationStep(step);
                if (step.RecipePreperationStep == null)
                {
                    continue;
                }

                _context.RecipeJoinPreperationSteps.Add(step);
            }
        }

        private RecipePreperationSteps ResolvePreparationStep(RecipeJoyinPreperationSteps joinStep)
        {
            var postedStep = joinStep.RecipePreperationStep;
            var hasPostedText = !string.IsNullOrWhiteSpace(postedStep?.Step_DE)
                || !string.IsNullOrWhiteSpace(postedStep?.Step_EN)
                || !string.IsNullOrWhiteSpace(postedStep?.Step_ESP)
                || !string.IsNullOrWhiteSpace(postedStep?.Step_PRT);

            if (joinStep.PreperationStepId > 0 && !hasPostedText)
            {
                return _context.RecipePreperationSteps.FirstOrDefault(x => x.Id == joinStep.PreperationStepId);
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

            var existing = _context.RecipePreperationSteps.FirstOrDefault(x => x.Step_DE == de
                && x.Step_EN == en
                && x.Step_ESP == esp
                && x.Step_PRT == prt
                && x.Phase == phase
                && x.Equipment == equipment);

            if (existing != null)
            {
                joinStep.PreperationStepId = existing.Id;
                return existing;
            }

            var newStep = new RecipePreperationSteps
            {
                Step_DE = de,
                Step_EN = en,
                Step_ESP = esp,
                Step_PRT = prt,
                Phase = phase,
                Equipment = equipment
            };

            _context.RecipePreperationSteps.Add(newStep);
            return newStep;
        }

        private void ProcessRecipeImage(RecipeBaseData recipe, SaveNewRecipeModel model,bool wordlUserImage)
        {
            var imagePath = !string.IsNullOrWhiteSpace(model.Recipes.ImagePath)
                ? model.Recipes.ImagePath
                : _context.Recipes
                    .Where(x => x.Preparation == model.Recipes.Preparation)
                    .Select(x => x.ImagePath)
                    .FirstOrDefault();

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
