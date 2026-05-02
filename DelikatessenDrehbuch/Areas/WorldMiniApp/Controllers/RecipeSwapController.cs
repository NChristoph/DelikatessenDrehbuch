using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Services;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Extensions;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    [Area("WorldMiniApp")]
    [ApiController]
    [Route("api/recipe-swap")]
    [IgnoreAntiforgeryToken] // JSON API - kein CSRF-Token nötig
    public class RecipeSwapController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IIngredientSwapAiService _aiService;
        private readonly IRecipeSwapStepAiService _stepAiService;
        private readonly IMemoryCache _cache;

        public RecipeSwapController(
            ApplicationDbContext context,
            IIngredientSwapAiService aiService,
            IRecipeSwapStepAiService stepAiService,
            IMemoryCache cache)
        {
            _context = context;
            _aiService = aiService;
            _stepAiService = stepAiService;
            _cache = cache;
        }

        [HttpPost("suggest")]
        public async Task<ActionResult<SwapSuggestionResponse>> SuggestSwap(
            [FromBody] SwapSuggestionRequest request)
        {
            try
            {
                // 1. Get original ingredient
                var original = await _context.IngredientsAndNutrients
                    .FirstOrDefaultAsync(i => i.Id == request.OriginalIngredientId);

                if (original == null)
                    return NotFound(new { error = "Ingredient not found" });

                // 2. Get recipe context
                var recipe = await _context.RecipeBaseData
                    .Include(r => r.Ingredients)
                        .ThenInclude(i => i.Ingredient)
                            .ThenInclude(i => i.IngredientsAndNutrients)
                    .Include(r => r.Ingredients)
                        .ThenInclude(i => i.Ingredient)
                            .ThenInclude(i => i.Quantity)
                    .Include(r => r.Ingredients)
                        .ThenInclude(i => i.Ingredient)
                            .ThenInclude(i => i.Measure)
                    .FirstOrDefaultAsync(r => r.Id == request.RecipeId);

                if (recipe == null)
                    return NotFound(new { error = "Recipe not found" });

                // 3. Find actual ingredient quantity in recipe
                var recipeIngredient = recipe.Ingredients
                    .FirstOrDefault(ri => ri.Ingredient.IngredientsAndNutrients.Id == request.OriginalIngredientId);

                decimal actualQuantity = original.Weight_per_piece; // Fallback
                string actualUnit = "g";

                if (recipeIngredient != null && recipeIngredient.Ingredient.Quantity != null)
                {
                    actualQuantity =(decimal) recipeIngredient.Ingredient.Quantity.Quantitys;
                    actualUnit = recipeIngredient.Ingredient.Measure?.UnitOfMeasurement ?? "g";
                }

                // 4. Get AI suggestions
                var result = await _aiService.GetSwapSuggestionsAsync(
                    original,
                    recipe,
                    request.Goal,
                    request.Language,
                    request.AiProvider
                );
                var suggestions = result.suggestions;
                var provider = result.provider;
                var processingTimeMs = result.processingTimeMs;

                // 5. Build response
                var response = new SwapSuggestionResponse
                {
                    Suggestions = suggestions,
                    AiProvider = provider,
                    ProcessingTimeMs = processingTimeMs,
                    Original = new OriginalIngredientInfo
                    {
                        Id = original.Id,
                        Name = GetIngredientName(original, request.Language),
                        Quantity = actualQuantity,
                        Unit = actualUnit,
                        Nutrition = new NutritionInfo
                        {
                            Calories = original.Calories_a_100g,
                            Protein = original.Protein_a_100g,
                            Carbs = original.Carbohydrates_a_100g,
                            Fat = original.Fat_a_100g
                        }
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("apply")]
        public async Task<ActionResult<RecipeUserVariant>> ApplySwap(
            [FromBody] ApplySwapRequest request)
        {
            try
            {
                var userHash = Request.Cookies["WorldMiniAppUserHash"];
                if (string.IsNullOrEmpty(userHash))
                    return Unauthorized(new { error = "User not authenticated" });

                // Get original recipe
                var originalRecipe = await _context.RecipeBaseData
                    .Include(r => r.Ingredients)
                    .FirstOrDefaultAsync(r => r.Id == request.RecipeId);

                if (originalRecipe == null)
                    return NotFound(new { error = "Recipe not found" });

                // Create or update user variant
                var variant = await _context.RecipeUserVariants
                    .FirstOrDefaultAsync(v =>
                        v.UserHash == userHash &&
                        v.OriginalRecipeId == request.RecipeId);

                if (variant == null)
                {
                    variant = new RecipeUserVariant
                    {
                        UserHash = userHash,
                        OriginalRecipeId = request.RecipeId,
                        Title = $"{originalRecipe.Title} (Angepasst)",
                        SwapsJson = "[]",
                        CreatedAtUtc = DateTime.UtcNow
                    };
                    _context.RecipeUserVariants.Add(variant);
                }

                // Add swap to JSON
                var swaps = JsonSerializer.Deserialize<List<IngredientSwap>>(variant.SwapsJson)
                    ?? new List<IngredientSwap>();

                swaps.Add(new IngredientSwap
                {
                    FromIngredientId = request.FromIngredientId,
                    FromName = request.FromName,
                    ToIngredientId = request.ToIngredientId,
                    ToName = request.ToName,
                    NewQuantity = request.NewQuantity,
                    Unit = request.Unit
                });

                variant.SwapsJson = JsonSerializer.Serialize(swaps);

                await _context.SaveChangesAsync();

                // Return DTO to avoid circular reference with OriginalRecipe navigation property
                return Ok(new
                {
                    success = true,
                    variant = new
                    {
                        id = variant.Id,
                        userHash = variant.UserHash,
                        originalRecipeId = variant.OriginalRecipeId,
                        title = variant.Title,
                        swapsJson = variant.SwapsJson,
                        createdAtUtc = variant.CreatedAtUtc
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"=== APPLY SWAP ERROR ===");
                Console.WriteLine($"Message: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"InnerException: {ex.InnerException.Message}");
                }
                return StatusCode(500, new { error = ex.Message, details = ex.StackTrace, inner = ex.InnerException?.Message });
            }
        }

        [HttpPost("rewrite-steps")]
        public async Task<ActionResult<RewriteStepsResponse>> RewriteSteps(
            [FromBody] RewriteStepsRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var userHash = Request.Cookies["WorldMiniAppUserHash"];
                if (string.IsNullOrEmpty(userHash))
                {
                    return Unauthorized(new { error = "User not authenticated" });
                }

                if (request == null || request.RecipeId <= 0)
                {
                    return BadRequest(new { error = "Invalid request" });
                }

                var language = (request.Language ?? "de").Trim().ToLowerInvariant();

                RecipeUserVariant? variant;
                if (request.SwapVariantId.HasValue && request.SwapVariantId.Value > 0)
                {
                    variant = await _context.RecipeUserVariants
                        .FirstOrDefaultAsync(v =>
                            v.Id == request.SwapVariantId.Value &&
                            v.UserHash == userHash &&
                            v.OriginalRecipeId == request.RecipeId,
                            cancellationToken);
                }
                else
                {
                    variant = await _context.RecipeUserVariants
                        .Where(v => v.UserHash == userHash && v.OriginalRecipeId == request.RecipeId)
                        .OrderByDescending(v => v.CreatedAtUtc)
                        .FirstOrDefaultAsync(cancellationToken);
                }

                if (variant == null)
                {
                    return NotFound(new { error = "No swap variant found" });
                }

                var swaps = JsonSerializer.Deserialize<List<IngredientSwap>>(variant.SwapsJson) ?? new List<IngredientSwap>();
                if (swaps.Count == 0)
                {
                    return Ok(new RewriteStepsResponse
                    {
                        Steps = new List<string>(),
                        UsedCache = true
                    });
                }

                var recipe = await _context.RecipeBaseData
                    .IncludeFullRecipeDetails()
                    .FirstOrDefaultAsync(r => r.Id == request.RecipeId, cancellationToken);

                if (recipe == null)
                {
                    return NotFound(new { error = "Recipe not found" });
                }

                var swapsKey = ComputeStableSwapKey(swaps);
                var cacheKey = $"swap_steps_v1_{variant.Id}_{language}_{swapsKey}";
                if (_cache.TryGetValue<IReadOnlyList<string>>(cacheKey, out var cachedSteps) && cachedSteps != null && cachedSteps.Count > 0)
                {
                    return Ok(new RewriteStepsResponse
                    {
                        Steps = cachedSteps.ToList(),
                        UsedCache = true
                    });
                }

                var originalSteps = recipe.Steps
                    .OrderBy(s => s.StepIndex)
                    .Select(s => GetStepText(s.RecipePreparationStep, language))
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                var swapByFromId = swaps
                    .Where(s => s.FromIngredientId > 0)
                    .GroupBy(s => s.FromIngredientId)
                    .ToDictionary(g => g.Key, g => g.Last());

                var ingredientsAfterSwap = recipe.Ingredients
                    .Select(link =>
                    {
                        var ing = link.Ingredient?.IngredientsAndNutrients;
                        var ingredientId = ing?.Id ?? 0;

                        var name = ing == null ? string.Empty : GetIngredientName(ing, language);
                        var qty = link.Ingredient?.Quantity?.Quantitys ?? 0;
                        var unit = link.Ingredient?.Measure?.UnitOfMeasurement ?? "g";

                        if (ingredientId > 0 && swapByFromId.TryGetValue(ingredientId, out var swap))
                        {
                            name = swap.ToName;
                            qty = (double)swap.NewQuantity;
                            unit = swap.Unit;
                        }

                        return (
                            Name: (name ?? string.Empty).Trim(),
                            Quantity: FormatDecimal(qty),
                            Unit: (unit ?? string.Empty).Trim()
                        );
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                    .ToList();

                var steps = await _stepAiService.RewriteStepsForSwapsAsync(
                    recipeTitle: recipe.Title,
                    language: language,
                    originalSteps: originalSteps,
                    ingredientsAfterSwap: ingredientsAfterSwap,
                    swaps: swaps,
                    cancellationToken: cancellationToken);

                _cache.Set(cacheKey, steps, TimeSpan.FromHours(6));

                return Ok(new RewriteStepsResponse
                {
                    Steps = steps.ToList(),
                    UsedCache = false
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private string GetIngredientName(IngredientsAndNutrients ingredient, string language)
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

        private static string GetStepText(RecipePreparationSteps step, string language)
        {
            return language switch
            {
                "en" => step.Step_EN,
                "es" => step.Step_ESP,
                "pt" => step.Step_PRT,
                _ => step.Step_DE
            };
        }

        private static string FormatDecimal(double value)
        {
            // Keep it compact for prompts ("0.25", "200", "1.5")
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string ComputeStableSwapKey(IReadOnlyList<IngredientSwap> swaps)
        {
            var parts = swaps
                .OrderBy(s => s.FromIngredientId)
                .ThenBy(s => s.ToIngredientId)
                .Select(s => $"{s.FromIngredientId}:{s.ToIngredientId}:{s.NewQuantity.ToString(CultureInfo.InvariantCulture)}:{s.Unit}".ToLowerInvariant());
            return string.Join("|", parts);
        }
    }

    public class ApplySwapRequest
    {
        public int RecipeId { get; set; }
        public int FromIngredientId { get; set; }
        public string FromName { get; set; }
        public int ToIngredientId { get; set; }
        public string ToName { get; set; }
        public decimal NewQuantity { get; set; }
        public string Unit { get; set; }
    }

    public sealed class RewriteStepsRequest
    {
        public int RecipeId { get; set; }
        public int? SwapVariantId { get; set; }
        public string? Language { get; set; }
    }

    public sealed class RewriteStepsResponse
    {
        public List<string> Steps { get; set; } = new();
        public bool UsedCache { get; set; }
    }
}
