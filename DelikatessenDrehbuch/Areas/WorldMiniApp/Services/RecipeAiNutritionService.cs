using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    public sealed class RecipeAiNutritionService : IRecipeAiNutritionService
    {
        private readonly ApplicationDbContext _context;

        public RecipeAiNutritionService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<RecipeAiTransformNutritionPreview> BuildAiPreviewNutritionAsync(
            RecipeAiTransformPreview preview,
            string language,
            CancellationToken cancellationToken)
        {
            var ingredients = preview?.Ingredients ?? new List<RecipeAiTransformIngredientPreview>();
            if (ingredients.Count == 0)
            {
                return new RecipeAiTransformNutritionPreview();
            }

            var ids = ingredients.Where(x => x != null && x.IngredientId > 0).Select(x => x.IngredientId).Distinct().ToList();
            if (ids.Count == 0)
            {
                return new RecipeAiTransformNutritionPreview();
            }

            var sources = await _context.IngredientsAndNutrients
                .AsNoTracking()
                .Where(x => ids.Contains(x.Id))
                .ToListAsync(cancellationToken);

            var srcById = sources.GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.First());

            decimal totalCalories = 0, totalProtein = 0, totalFat = 0, totalCarbs = 0, totalSugar = 0;

            foreach (var item in ingredients)
            {
                if (item == null || item.IngredientId <= 0) continue;
                if (!srcById.TryGetValue(item.IngredientId, out var src)) continue;

                var amount = TryParseDecimal(item.Quantity);
                if (amount <= 0m) continue;

                var gramsOrMlOrPieces = TryConvertToGramsOrMillilitersOrPieces(amount, item.Measure, item.Quantity, src);
                if (gramsOrMlOrPieces <= 0m) continue;

                var factor = gramsOrMlOrPieces / 100m;
                totalCalories += (src.Calories_a_100g) * factor;
                totalProtein += (src.Protein_a_100g) * factor;
                totalFat += (src.Fat_a_100g) * factor;
                totalCarbs += (src.Carbohydrates_a_100g) * factor;
                totalSugar += (src.Sugar_a_100g) * factor;
            }

            return new RecipeAiTransformNutritionPreview
            {
                Calories = totalCalories,
                Protein = totalProtein,
                Carbs = totalCarbs,
                Fat = totalFat,
                Sugar = totalSugar
            };
        }

        private static decimal TryParseDecimal(string? value)
        {
            var raw = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw)) return 0m;
            raw = raw.Replace(',', '.');
            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }
            return 0m;
        }

        private static decimal TryConvertToGramsOrMillilitersOrPieces(
            decimal amount,
            string? measure,
            string? quantityText,
            IngredientsAndNutrients nutrientSource)
        {
            var unit = (measure ?? string.Empty).Trim().ToLowerInvariant();
            unit = unit.Replace(".", string.Empty);

            var q = (quantityText ?? string.Empty).Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(unit))
            {
                if (q.Contains("kg")) unit = "kg";
                else if (q.Contains("ml")) unit = "ml";
                else if (q.Contains(" l") || q.EndsWith("l") || q.Contains("liter")) unit = "l";
                else if (q.Contains(" g") || q.EndsWith("g")) unit = "g";
                else if (q.Contains("stk") || q.Contains("stück") || q.Contains("stueck") || q.Contains("piece") || q.Contains("pcs")) unit = "stk";
            }

            if (unit is "stk" or "stuck" or "stück" or "stueck" or "piece" or "pcs")
            {
                var weight = nutrientSource?.Weight_per_piece ?? 0m;
                if (weight <= 0m) return 0m;
                return amount * weight;
            }

            return TryConvertToGramsOrMilliliters(amount, unit, quantityText);
        }

        private static decimal TryConvertToGramsOrMilliliters(decimal amount, string? measure, string? quantityText)
        {
            var unit = (measure ?? string.Empty).Trim().ToLowerInvariant();
            unit = unit.Replace(".", string.Empty);

            var q = (quantityText ?? string.Empty).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(unit))
            {
                if (q.Contains("kg")) unit = "kg";
                else if (q.Contains(" g") || q.EndsWith("g")) unit = "g";
                else if (q.Contains("ml")) unit = "ml";
                else if (q.Contains(" l") || q.EndsWith("l") || q.Contains("liter")) unit = "l";
            }

            return unit switch
            {
                "g" or "gram" => amount,
                "kg" or "kilogramm" => amount * 1000m,
                "ml" or "milliliter" => amount,
                "l" or "liter" => amount * 1000m,
                _ => 0m
            };
        }
    }
}

