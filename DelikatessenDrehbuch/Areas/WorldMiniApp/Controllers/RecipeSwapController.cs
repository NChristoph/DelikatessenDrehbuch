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
    public class RecipeSwapController : WorldMiniAppBaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly IIngredientSwapAiService _aiService;
        private readonly IRecipeSwapStepAiService _stepAiService;
        private readonly IRecipeVariantSummaryAiService _variantSummaryAiService;
        private readonly IMemoryCache _cache;
        // Logger ergänzt, damit Fehlerdetails serverseitig protokolliert werden,
        // statt sie (wie zuvor) per ex.Message an den Client zu leaken.
        private readonly ILogger<RecipeSwapController> _logger;

        public RecipeSwapController(
            ApplicationDbContext context,
            IIngredientSwapAiService aiService,
            IRecipeSwapStepAiService stepAiService,
            IRecipeVariantSummaryAiService variantSummaryAiService,
            IMemoryCache cache,
            ILogger<RecipeSwapController> logger)
        {
            _context = context;
            _aiService = aiService;
            _stepAiService = stepAiService;
            _variantSummaryAiService = variantSummaryAiService;
            _cache = cache;
            _logger = logger;
        }

        [HttpPost("publish")]
        public async Task<ActionResult> PublishCommunityVariant(
            [FromBody] PublishCommunityVariantRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var userHash = ResolveUserHash(string.Empty);
                if (string.IsNullOrWhiteSpace(userHash))
                {
                    return Unauthorized(new { error = "User not authenticated" });
                }

                if (request == null || request.RecipeId <= 0 || request.SwapVariantId <= 0)
                {
                    return BadRequest(new { error = "Invalid request" });
                }

                var userVariant = await _context.RecipeUserVariants
                    .FirstOrDefaultAsync(v =>
                        v.Id == request.SwapVariantId &&
                        v.UserHash == userHash &&
                        v.OriginalRecipeId == request.RecipeId,
                        cancellationToken);

                if (userVariant == null)
                {
                    return NotFound(new { error = "Swap variant not found" });
                }

                var recipe = await _context.RecipeBaseData
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == request.RecipeId, cancellationToken);

                if (recipe == null)
                {
                    return NotFound(new { error = "Recipe not found" });
                }

                int? parentCommunityVariantId = null;
                if (request.ParentCommunityVariantId.HasValue && request.ParentCommunityVariantId.Value > 0)
                {
                    var parentExists = await _context.RecipeCommunityVariants
                        .AnyAsync(v => v.Id == request.ParentCommunityVariantId.Value && v.OriginalRecipeId == request.RecipeId, cancellationToken);
                    if (parentExists)
                    {
                        parentCommunityVariantId = request.ParentCommunityVariantId.Value;
                    }
                }

                var title = (request.Title ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(title))
                {
                    // Prefer AI-generated title that reflects the swaps (e.g. pork->chicken should not keep "Schweine...").
                    title = string.Empty;
                }

                var swaps = JsonSerializer.Deserialize<List<IngredientSwap>>(userVariant.SwapsJson ?? "[]") ?? new List<IngredientSwap>();
                var language = (request.Language ?? "de").Trim().ToLowerInvariant();
                if (language.Length > 12) language = language.Substring(0, 12);

                string? summary = (request.Summary ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(summary))
                {
                    summary = null;
                }

                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(summary))
                {
                    var meta = await _variantSummaryAiService.GenerateTitleAndSummaryAsync(recipe, swaps, language, cancellationToken);
                    if (meta.HasValue)
                    {
                        if (string.IsNullOrWhiteSpace(title)) title = meta.Value.title;
                        if (string.IsNullOrWhiteSpace(summary)) summary = meta.Value.summary;
                    }
                }

                // No heuristic title fallback: if AI didn't provide a title, fail fast so we don't store bad metadata.
                if (string.IsNullOrWhiteSpace(title))
                {
                    return UnprocessableEntity(new { error = "AI title generation failed. Please retry." });
                }

                summary ??= await _variantSummaryAiService.GenerateSummaryAsync(recipe, swaps, language, cancellationToken);
                if (string.IsNullOrWhiteSpace(summary))
                {
                    // Fallback: keep it short and useful.
                    var sample = swaps.Take(3).Select(s => $"{s.FromName}→{s.ToName}").ToList();
                    summary = sample.Count == 0 ? null : $"Anpassung: {string.Join(", ", sample)}";
                }

                // Sanitize encoding artifacts (we've seen "â†’" show up due to editor/encoding issues).
                if (!string.IsNullOrWhiteSpace(summary))
                {
                    summary = summary.Replace("â†’", "->", StringComparison.Ordinal);
                }

                // No heuristic summary rewrite either: if AI didn't provide a proper description, keep it as-is (or fail below).

                // Hard requirement: variants must have both title + summary to be publishable.
                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(summary))
                {
                    return UnprocessableEntity(new { error = "Variant metadata missing (title/summary). Please try again." });
                }

                var communityVariant = new RecipeCommunityVariant
                {
                    OriginalRecipeId = request.RecipeId,
                    ParentCommunityVariantId = parentCommunityVariantId,
                    Title = title.Length > 256 ? title.Substring(0, 256) : title,
                    Summary = summary != null && summary.Length > 400 ? summary.Substring(0, 400) : summary,
                    Language = language,
                    SwapsJson = userVariant.SwapsJson ?? "[]",
                    CreatedByUserHash = userHash,
                    CreatedAtUtc = DateTime.UtcNow,
                    LikeCount = 0
                };

                _context.RecipeCommunityVariants.Add(communityVariant);
                await _context.SaveChangesAsync(cancellationToken);

                return Ok(new
                {
                    success = true,
                    communityVariant = new
                    {
                        id = communityVariant.Id,
                        originalRecipeId = communityVariant.OriginalRecipeId,
                        parentCommunityVariantId = communityVariant.ParentCommunityVariantId,
                        title = communityVariant.Title,
                        summary = communityVariant.Summary,
                        swapsJson = communityVariant.SwapsJson,
                        createdAtUtc = communityVariant.CreatedAtUtc
                    }
                });
            }
            catch (Exception ex)
            {
                // Fehlerdetails nur serverseitig ins Log – dem Client nur eine
                // generische Meldung zurückgeben (kein Leak von ex.Message/Interna).
                _logger.LogError(ex, "RecipeSwap-Anfrage fehlgeschlagen.");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("community-variants")]
        public async Task<ActionResult<CommunityVariantsListResponse>> ListCommunityVariants(
            [FromQuery] int recipeId,
            [FromQuery] string? q = null,
            [FromQuery] string? sort = null,
            [FromQuery] string? lang = null,
            CancellationToken cancellationToken = default)
        {
            if (recipeId <= 0)
            {
                return BadRequest(new { error = "recipeId missing" });
            }

            var query = _context.RecipeCommunityVariants
                .AsNoTracking()
                .Where(v => v.OriginalRecipeId == recipeId);

            var preparationTime = await _context.RecipeBaseData
                .AsNoTracking()
                .Where(r => r.Id == recipeId)
                .Select(r => r.PreparationTime)
                .FirstOrDefaultAsync(cancellationToken);

            var language = (lang ?? string.Empty).Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(language))
            {
                query = query.Where(v => v.Language == language);
            }

            var search = (q ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(v =>
                    v.Title.Contains(search) ||
                    (v.Summary != null && v.Summary.Contains(search)));
            }

            // Sort: "top" by likes, otherwise newest.
            if (string.Equals(sort, "top", StringComparison.OrdinalIgnoreCase))
            {
                query = query.OrderByDescending(v => v.LikeCount).ThenByDescending(v => v.CreatedAtUtc);
            }
            else
            {
                query = query.OrderByDescending(v => v.CreatedAtUtc);
            }

            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .Take(80)
                .Select(v => new CommunityVariantListItem
                {
                    Id = v.Id,
                    Title = v.Title,
                    Summary = v.Summary,
                    Language = v.Language,
                    LikeCount = v.LikeCount,
                    PreparationTime = preparationTime,
                    CreatedAtUtc = v.CreatedAtUtc,
                    SwapCount = 0 // filled below (we avoid SQL JSON parsing here)
                })
                .ToListAsync(cancellationToken);

            // SwapCount (best-effort) from SwapsJson
            var ids = items.Select(x => x.Id).ToList();
            var swapsById = await _context.RecipeCommunityVariants
                .AsNoTracking()
                .Where(v => ids.Contains(v.Id))
                .Select(v => new { v.Id, v.SwapsJson })
                .ToListAsync(cancellationToken);

            var swapCountMap = new Dictionary<int, int>();
            foreach (var row in swapsById)
            {
                try
                {
                    var swaps = JsonSerializer.Deserialize<List<IngredientSwap>>(row.SwapsJson ?? "[]");
                    swapCountMap[row.Id] = swaps?.Count ?? 0;
                }
                catch
                {
                    swapCountMap[row.Id] = 0;
                }
            }

            foreach (var item in items)
            {
                if (swapCountMap.TryGetValue(item.Id, out var c))
                {
                    item.SwapCount = c;
                }
            }

            return Ok(new CommunityVariantsListResponse
            {
                Total = total,
                Items = items
            });
        }

        private static string BuildFallbackVariantTitle(string originalTitle, IReadOnlyList<IngredientSwap> swaps, string language)
        {
            var title = (originalTitle ?? string.Empty).Trim();
            if (swaps == null || swaps.Count == 0)
            {
                return string.IsNullOrWhiteSpace(title) ? "Variante" : $"{title} (Variante)";
            }

            // Prefer the first meaningful swap (usually the most recent user action).
            var main = swaps.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.FromName) && !string.IsNullOrWhiteSpace(s.ToName));
            if (main == null)
            {
                return string.IsNullOrWhiteSpace(title) ? "Variante" : $"{title} (Variante)";
            }

            var from = main.FromName.Trim();
            var to = main.ToName.Trim();

            // Try to replace "From" with "To" inside the existing title (case-insensitive).
            if (!string.IsNullOrWhiteSpace(title) && title.IndexOf(from, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ReplaceIgnoreCase(title, from, to);
            }

            // Heuristic for German meat/protein titles:
            // if the title says "Schweinebraten" but the swap is "Schweineschulter -> Putenkeule",
            // we still want "Putenbraten ..." rather than "... mit Putenkeule".
            if (language == "de" && !string.IsNullOrWhiteSpace(title))
            {
                var toPrefix = DetectGermanProteinPrefix(to);
                if (!string.IsNullOrWhiteSpace(toPrefix))
                {
                    // Replace common animal prefixes in German recipe titles.
                    // Keep it conservative: only rewrite if the "from" looks like that animal too.
                    if (LooksLikeGermanAnimal(from, "schwein") && ContainsGermanAnimalPrefix(title, "schwein"))
                    {
                        return RewriteGermanAnimalPrefixInTitle(title, "schwein", toPrefix);
                    }
                    if (LooksLikeGermanAnimal(from, "hähnchen") && ContainsGermanAnimalPrefix(title, "hähnchen"))
                    {
                        return RewriteGermanAnimalPrefixInTitle(title, "hähnchen", toPrefix);
                    }
                    if (LooksLikeGermanAnimal(from, "rind") && ContainsGermanAnimalPrefix(title, "rind"))
                    {
                        return RewriteGermanAnimalPrefixInTitle(title, "rind", toPrefix);
                    }
                    if (LooksLikeGermanAnimal(from, "kalb") && ContainsGermanAnimalPrefix(title, "kalb"))
                    {
                        return RewriteGermanAnimalPrefixInTitle(title, "kalb", toPrefix);
                    }
                    if (LooksLikeGermanAnimal(from, "lamm") && ContainsGermanAnimalPrefix(title, "lamm"))
                    {
                        return RewriteGermanAnimalPrefixInTitle(title, "lamm", toPrefix);
                    }
                }
            }

            // Otherwise append a short hint.
            return string.IsNullOrWhiteSpace(title) ? $"{to} (Variante)" : $"{title} mit {to}";
        }

        private static string? DetectGermanProteinPrefix(string toName)
        {
            var n = (toName ?? string.Empty).Trim().ToLowerInvariant();
            if (n.Length == 0) return null;

            if (n.Contains("pute")) return "Puten";
            if (n.Contains("hähnchen") || n.Contains("haehnchen")) return "Hähnchen";
            if (n.Contains("rind") || n.Contains("rinder")) return "Rinder";
            if (n.Contains("kalb") || n.Contains("kalbs")) return "Kalbs";
            if (n.Contains("lamm")) return "Lamm";
            if (n.Contains("fisch") || n.Contains("lachs") || n.Contains("thun")) return "Fisch";
            if (n.Contains("tofu")) return "Tofu";

            return null;
        }

        private static bool LooksLikeGermanAnimal(string ingredientName, string animalKey)
        {
            var n = (ingredientName ?? string.Empty).Trim().ToLowerInvariant();
            if (n.Length == 0) return false;

            return animalKey switch
            {
                "schwein" => n.Contains("schwein"),
                "hähnchen" => n.Contains("hähnchen") || n.Contains("haehnchen"),
                "rind" => n.Contains("rind"),
                "kalb" => n.Contains("kalb"),
                "lamm" => n.Contains("lamm"),
                _ => false
            };
        }

        private static bool ContainsGermanAnimalPrefix(string title, string animalKey)
        {
            var t = (title ?? string.Empty);
            if (t.Length == 0) return false;

            return animalKey switch
            {
                "schwein" => t.IndexOf("Schwein", StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf("Schweine", StringComparison.OrdinalIgnoreCase) >= 0,
                "hähnchen" => t.IndexOf("Hähnchen", StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf("Haehnchen", StringComparison.OrdinalIgnoreCase) >= 0,
                "rind" => t.IndexOf("Rind", StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf("Rinder", StringComparison.OrdinalIgnoreCase) >= 0,
                "kalb" => t.IndexOf("Kalb", StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf("Kalbs", StringComparison.OrdinalIgnoreCase) >= 0,
                "lamm" => t.IndexOf("Lamm", StringComparison.OrdinalIgnoreCase) >= 0,
                _ => false
            };
        }

        private static string RewriteGermanAnimalPrefixInTitle(string title, string fromAnimalKey, string toPrefix)
        {
            // Common compounds first.
            var t = title;
            if (fromAnimalKey == "schwein")
            {
                t = ReplaceIgnoreCase(t, "Schweinebraten", $"{toPrefix}braten");
                t = ReplaceIgnoreCase(t, "Schweine", toPrefix);
                t = ReplaceIgnoreCase(t, "Schwein", toPrefix);
                return t;
            }
            if (fromAnimalKey == "hähnchen")
            {
                t = ReplaceIgnoreCase(t, "Hähnchen", toPrefix);
                t = ReplaceIgnoreCase(t, "Haehnchen", toPrefix);
                return t;
            }
            if (fromAnimalKey == "rind")
            {
                t = ReplaceIgnoreCase(t, "Rinder", toPrefix);
                t = ReplaceIgnoreCase(t, "Rind", toPrefix);
                return t;
            }
            if (fromAnimalKey == "kalb")
            {
                t = ReplaceIgnoreCase(t, "Kalbs", toPrefix);
                t = ReplaceIgnoreCase(t, "Kalb", toPrefix);
                return t;
            }
            if (fromAnimalKey == "lamm")
            {
                t = ReplaceIgnoreCase(t, "Lamm", toPrefix);
                return t;
            }

            return t;
        }

        private static string BuildFallbackVariantSummary(string originalTitle, IReadOnlyList<IngredientSwap> swaps, string language)
        {
            if (swaps == null || swaps.Count == 0)
            {
                return language == "en"
                    ? "A variant of the original recipe."
                    : "Variante des Originalrezepts.";
            }

            // Keep it short and readable (not a raw swap log).
            if (language == "en")
            {
                var partsEn = swaps
                    .Where(s => !string.IsNullOrWhiteSpace(s.FromName) && !string.IsNullOrWhiteSpace(s.ToName))
                    .Take(2)
                    .Select(s => $"{s.ToName.Trim()} instead of {s.FromName.Trim()}")
                    .ToList();

                return partsEn.Count == 0
                    ? "A variant of the original recipe."
                    : $"{string.Join("; ", partsEn)}.";
            }

            var parts = swaps
                .Where(s => !string.IsNullOrWhiteSpace(s.FromName) && !string.IsNullOrWhiteSpace(s.ToName))
                .Take(2)
                .Select(s => $"{s.ToName.Trim()} statt {s.FromName.Trim()}")
                .ToList();

            return parts.Count == 0
                ? "Variante des Originalrezepts."
                : $"{string.Join("; ", parts)}.";
        }

        private static string ReplaceIgnoreCase(string input, string search, string replacement)
        {
            var idx = input.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return input;
            return input.Substring(0, idx) + replacement + input.Substring(idx + search.Length);
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

                // 4a. Merge context swaps (batch + saved draft variant) so suggestions respect the current recipe state.
                // Priority: request.ContextSwaps wins (it's the freshest client-side state).
                var mergedContextByFrom = new Dictionary<int, IngredientSwap>();

                if (request.SwapVariantId.HasValue && request.SwapVariantId.Value > 0)
                {
                    var userHash = ResolveUserHash(string.Empty);

                    if (!string.IsNullOrWhiteSpace(userHash))
                    {
                        var variant = await _context.RecipeUserVariants
                            .AsNoTracking()
                            .FirstOrDefaultAsync(v =>
                                v.Id == request.SwapVariantId.Value &&
                                v.UserHash == userHash &&
                                v.OriginalRecipeId == request.RecipeId);

                        if (variant != null)
                        {
                            try
                            {
                                var swaps = JsonSerializer.Deserialize<List<IngredientSwap>>(variant.SwapsJson ?? "[]") ?? new List<IngredientSwap>();
                                foreach (var s in swaps)
                                {
                                    if (s == null || s.FromIngredientId <= 0 || s.ToIngredientId <= 0) continue;
                                    mergedContextByFrom[s.FromIngredientId] = new IngredientSwap
                                    {
                                        FromIngredientId = s.FromIngredientId,
                                        FromName = s.FromName ?? string.Empty,
                                        ToIngredientId = s.ToIngredientId,
                                        ToName = s.ToName ?? string.Empty,
                                        NewQuantity = s.NewQuantity,
                                        Unit = s.Unit ?? "g"
                                    };
                                }
                            }
                            catch
                            {
                                // ignore malformed JSON in draft
                            }
                        }
                    }
                }

                if (request.ContextSwaps != null && request.ContextSwaps.Count > 0)
                {
                    foreach (var s in request.ContextSwaps)
                    {
                        if (s == null || s.FromIngredientId <= 0 || s.ToIngredientId <= 0) continue;
                        mergedContextByFrom[s.FromIngredientId] = new IngredientSwap
                        {
                            FromIngredientId = s.FromIngredientId,
                            FromName = s.FromName ?? string.Empty,
                            ToIngredientId = s.ToIngredientId,
                            ToName = s.ToName ?? string.Empty,
                            NewQuantity = s.NewQuantity,
                            Unit = string.IsNullOrWhiteSpace(s.Unit) ? "g" : s.Unit
                        };
                    }
                }

                var mergedContextSwaps = mergedContextByFrom.Values.ToList();

                // 4. Build hybrid suggestions:
                // - Up to 4 from already saved community variants ("non-AI", crowd wisdom)
                // - Fill remaining slots (up to 8 total) with AI suggestions
                const int totalSlots = 8;
                const int maxCommunitySlots = 4;

                var normalizedLanguage = (request.Language ?? "de").Trim().ToLowerInvariant();
                if (normalizedLanguage.Length > 12) normalizedLanguage = normalizedLanguage.Substring(0, 12);

                var communityToIds = new HashSet<int>();
                var communityRows = await _context.RecipeCommunityVariants
                    .AsNoTracking()
                    .Where(v => v.OriginalRecipeId == request.RecipeId)
                    .Select(v => v.SwapsJson)
                    .Take(220)
                    .ToListAsync();

                foreach (var swapsJson in communityRows)
                {
                    if (string.IsNullOrWhiteSpace(swapsJson)) continue;
                    try
                    {
                        var swaps = JsonSerializer.Deserialize<List<IngredientSwap>>(swapsJson) ?? new List<IngredientSwap>();
                        foreach (var s in swaps)
                        {
                            if (s == null) continue;
                            if (s.FromIngredientId != original.Id) continue;
                            if (s.ToIngredientId <= 0 || s.ToIngredientId == original.Id) continue;
                            communityToIds.Add(s.ToIngredientId);
                        }
                    }
                    catch
                    {
                        // ignore malformed rows
                    }
                }

                // Exclude already-selected context swap targets.
                foreach (var s in mergedContextSwaps)
                {
                    if (s != null && s.ToIngredientId > 0)
                    {
                        communityToIds.Remove(s.ToIngredientId);
                    }
                }

                var communityPick = communityToIds.ToList();
                if (communityPick.Count > maxCommunitySlots)
                {
                    // Pick 4 random to keep variety.
                    communityPick = communityPick
                        .OrderBy(_ => Random.Shared.Next())
                        .Take(maxCommunitySlots)
                        .ToList();
                }

                var qtyInGrams = actualQuantity;
                var unitLower = (actualUnit ?? "g").Trim().ToLowerInvariant();
                if (unitLower is "l" or "liter" or "litre")
                {
                    qtyInGrams = actualQuantity * 1000m;
                }

                // Community suggestion options (best-effort deltas).
                var communityOptions = new List<IngredientSwapOption>();
                if (communityPick.Count > 0)
                {
                    var communityIngredients = await _context.IngredientsAndNutrients
                        .AsNoTracking()
                        .Where(i => communityPick.Contains(i.Id))
                        .ToListAsync();

                    foreach (var ing in communityIngredients)
                    {
                        var calDelta = (ing.Calories_a_100g - original.Calories_a_100g) * (qtyInGrams / 100m);
                        var pDelta = (ing.Protein_a_100g - original.Protein_a_100g) * (qtyInGrams / 100m);
                        var cDelta = (ing.Carbohydrates_a_100g - original.Carbohydrates_a_100g) * (qtyInGrams / 100m);
                        var fDelta = (ing.Fat_a_100g - original.Fat_a_100g) * (qtyInGrams / 100m);

                        communityOptions.Add(new IngredientSwapOption
                        {
                            IngredientId = ing.Id,
                            Name = GetIngredientName(ing, normalizedLanguage),
                            Quantity = actualQuantity,
                            Unit = actualUnit,
                            Reason = "Beliebte Community-Variante.",
                            CompatibilityScore = 0.88,
                            Delta = new NutritionDelta
                            {
                                CaloriesDelta = Math.Round(calDelta, 1),
                                ProteinDelta = Math.Round(pDelta, 1),
                                CarbsDelta = Math.Round(cDelta, 1),
                                FatDelta = Math.Round(fDelta, 1)
                            },
                            PreparationChange = "Wie gewohnt zubereiten (ggf. Garzeit prüfen).",
                            TasteImpact = "Ähnliches Profil, je nach Zutat leicht verändert.",
                            Pros = new List<string> { "Community-getestet" },
                            Cons = new List<string>(),
                            CostPerKg = null
                        });
                    }

                    // preserve the random choice order (ingredients query doesn't keep it)
                    communityOptions = communityPick
                        .Select(id => communityOptions.FirstOrDefault(o => o.IngredientId == id))
                        .Where(o => o != null)
                        .Cast<IngredientSwapOption>()
                        .ToList();
                }

                var remainingSlots = Math.Max(0, totalSlots - communityOptions.Count);

                List<IngredientSwapOption> aiOptions = new();
                var provider = "community";
                var processingTimeMs = 0;

                if (remainingSlots > 0)
                {
                    var result = await _aiService.GetSwapSuggestionsAsync(
                        original,
                        recipe,
                        request.Goal,
                        normalizedLanguage,
                        mergedContextSwaps,
                        request.AiProvider,
                        request.ExistingIngredients
                    );

                    provider = result.provider;
                    processingTimeMs = result.processingTimeMs;

                    aiOptions = (result.suggestions ?? new List<IngredientSwapOption>())
                        .Where(s => s != null)
                        .Where(s => !communityOptions.Any(c => c.IngredientId == s.IngredientId))
                        .ToList();

                    if (aiOptions.Count > remainingSlots)
                    {
                        // Prefer variety: pick random subset if there are more than needed.
                        aiOptions = aiOptions
                            .OrderBy(_ => Random.Shared.Next())
                            .Take(remainingSlots)
                            .ToList();
                    }
                }

                var suggestions = communityOptions.Concat(aiOptions).ToList();
                var finalProvider = communityOptions.Count > 0 && aiOptions.Count > 0 ? $"{provider}+community" : provider;

                // 5. Build response
                var response = new SwapSuggestionResponse
                {
                    Suggestions = suggestions,
                    AiProvider = finalProvider,
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
                // Fehlerdetails nur serverseitig ins Log – dem Client nur eine
                // generische Meldung zurückgeben (kein Leak von ex.Message/Interna).
                _logger.LogError(ex, "RecipeSwap-Anfrage fehlgeschlagen.");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPost("apply")]
        public async Task<ActionResult<RecipeUserVariant>> ApplySwap(
            [FromBody] ApplySwapRequest request)
        {
            try
            {
                var userHash = ResolveUserHash(string.Empty);
                if (string.IsNullOrEmpty(userHash))
                    return Unauthorized(new { error = "User not authenticated" });

                // Get original recipe
                var originalRecipe = await _context.RecipeBaseData
                    .Include(r => r.Ingredients)
                    .FirstOrDefaultAsync(r => r.Id == request.RecipeId);

                if (originalRecipe == null)
                    return NotFound(new { error = "Recipe not found" });

                // Create or update user variant:
                // If SwapVariantId is provided -> continue that exact draft.
                // If not -> always start a new draft, so each "edit from original" is independent.
                RecipeUserVariant? variant = null;
                if (request.SwapVariantId.HasValue && request.SwapVariantId.Value > 0)
                {
                    variant = await _context.RecipeUserVariants
                        .FirstOrDefaultAsync(v =>
                            v.Id == request.SwapVariantId.Value &&
                            v.UserHash == userHash &&
                            v.OriginalRecipeId == request.RecipeId);
                }

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
                    FromName = request.FromName ?? string.Empty,
                    ToIngredientId = request.ToIngredientId,
                    ToName = request.ToName ?? string.Empty,
                    NewQuantity = request.NewQuantity,
                    Unit = request.Unit ?? "g"
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
                // Vollständige Details (inkl. StackTrace/InnerException) NUR ins Log,
                // nicht an den Client. Vorher wurden hier StackTrace + Inner-Message
                // im HTTP-Response zurückgegeben (Info-Disclosure).
                _logger.LogError(ex, "ApplySwap fehlgeschlagen.");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPost("apply-batch")]
        public async Task<ActionResult<RecipeUserVariant>> ApplySwapBatch(
            [FromBody] ApplySwapBatchRequest request)
        {
            try
            {
                var userHash = ResolveUserHash(string.Empty);
                if (string.IsNullOrEmpty(userHash))
                    return Unauthorized(new { error = "User not authenticated" });

                if (request == null || request.RecipeId <= 0 || request.Swaps == null || request.Swaps.Count == 0)
                {
                    return BadRequest(new { error = "No swaps provided" });
                }

                var originalRecipe = await _context.RecipeBaseData
                    .Include(r => r.Ingredients)
                    .FirstOrDefaultAsync(r => r.Id == request.RecipeId);

                if (originalRecipe == null)
                    return NotFound(new { error = "Recipe not found" });

                RecipeUserVariant? variant = null;
                if (request.SwapVariantId.HasValue && request.SwapVariantId.Value > 0)
                {
                    variant = await _context.RecipeUserVariants
                        .FirstOrDefaultAsync(v =>
                            v.Id == request.SwapVariantId.Value &&
                            v.UserHash == userHash &&
                            v.OriginalRecipeId == request.RecipeId);
                }

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

                var existing = JsonSerializer.Deserialize<List<IngredientSwap>>(variant.SwapsJson)
                               ?? new List<IngredientSwap>();

                // Replace swaps per FromIngredientId (latest wins)
                var incomingByFrom = request.Swaps
                    .Where(s => s.FromIngredientId > 0 && s.ToIngredientId > 0)
                    .GroupBy(s => s.FromIngredientId)
                    .ToDictionary(g => g.Key, g => g.Last());

                existing.RemoveAll(s => incomingByFrom.ContainsKey(s.FromIngredientId));
                existing.AddRange(incomingByFrom.Values.Select(s => new IngredientSwap
                {
                    FromIngredientId = s.FromIngredientId,
                    FromName = s.FromName ?? string.Empty,
                    ToIngredientId = s.ToIngredientId,
                    ToName = s.ToName ?? string.Empty,
                    NewQuantity = s.NewQuantity,
                    Unit = s.Unit ?? "g"
                }));

                variant.SwapsJson = JsonSerializer.Serialize(existing);

                await _context.SaveChangesAsync();

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
                // Fehlerdetails nur serverseitig ins Log – dem Client nur eine
                // generische Meldung zurückgeben (kein Leak von ex.Message/Interna).
                _logger.LogError(ex, "RecipeSwap-Anfrage fehlgeschlagen.");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPost("rewrite-steps")]
        public async Task<ActionResult<RewriteStepsResponse>> RewriteSteps(
            [FromBody] RewriteStepsRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var userHash = ResolveUserHash(string.Empty);
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

                // NEW SYSTEM: RecipeSteps (normalisiert, Culture + Text)
                var originalSteps = new List<string>();
                var stepsForLanguage = recipe.Steps?.FirstOrDefault(s => s.Culture == language);
                if (stepsForLanguage != null && !string.IsNullOrWhiteSpace(stepsForLanguage.Text))
                {
                    // Split by newlines to get individual steps
                    var stepLines = stepsForLanguage.Text
                        .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(line => line.Trim())
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .ToList();

                    originalSteps.AddRange(stepLines);
                }

                var swapByFromId = swaps
                    .Where(s => s.FromIngredientId > 0)
                    .GroupBy(s => s.FromIngredientId)
                    .ToDictionary(g => g.Key, g => g.Last());

                // WICHTIG: Zutatennamen in der ZIELSPRACHE (language) verwenden!
                // Die AI soll eine vollständige Übersetzung erstellen, inklusive Zutatennamen.
                var ingredientsAfterSwap = recipe.Ingredients
                    .Select(link =>
                    {
                        var ing = link.Ingredient?.IngredientsAndNutrients;
                        var ingredientId = ing?.Id ?? 0;

                        // Verwende ZIELSPRACHE für Zutatennamen (z.B. Spanisch wenn language="es")
                        var name = ing == null ? string.Empty : GetIngredientName(ing, language);
                        var qty = link.Ingredient?.Quantity?.Quantitys ?? 0;
                        var unit = link.Ingredient?.Measure?.UnitOfMeasurement ?? "g";

                        if (ingredientId > 0 && swapByFromId.TryGetValue(ingredientId, out var swap))
                        {
                            // Hole den Ingredient-Namen in der ZIELSPRACHE
                            var swapIng = _context.IngredientsAndNutrients
                                .AsNoTracking()
                                .FirstOrDefault(i => i.Id == swap.ToIngredientId);

                            if (swapIng != null)
                            {
                                name = GetIngredientName(swapIng, language);
                            }
                            else
                            {
                                // Fallback: verwende swap.ToName
                                name = swap.ToName;
                            }

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
                // Fehlerdetails nur serverseitig ins Log – dem Client nur eine
                // generische Meldung zurückgeben (kein Leak von ex.Message/Interna).
                _logger.LogError(ex, "RecipeSwap-Anfrage fehlgeschlagen.");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPost("generate-meta")]
        public async Task<ActionResult> GenerateVariantMeta(
            [FromBody] GenerateVariantMetaRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var userHash = ResolveUserHash(string.Empty);
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

                // Defensive: SwapsJson can be null/"null" during early variants or older rows.
                var swapsJson = string.IsNullOrWhiteSpace(variant.SwapsJson) || string.Equals(variant.SwapsJson.Trim(), "null", StringComparison.OrdinalIgnoreCase)
                    ? "[]"
                    : variant.SwapsJson;
                var swaps = JsonSerializer.Deserialize<List<IngredientSwap>>(swapsJson) ?? new List<IngredientSwap>();
                if (swaps.Count == 0)
                {
                    return UnprocessableEntity(new { error = "No swaps to describe (swapVariant has empty swapsJson)." });
                }

                var recipe = await _context.RecipeBaseData
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == request.RecipeId, cancellationToken);

                if (recipe == null)
                {
                    return NotFound(new { error = "Recipe not found" });
                }

                var meta = await _variantSummaryAiService.GenerateTitleAndSummaryAsync(recipe, swaps, language, cancellationToken);
                if (!meta.HasValue || string.IsNullOrWhiteSpace(meta.Value.title) || string.IsNullOrWhiteSpace(meta.Value.summary))
                {
                    return UnprocessableEntity(new
                    {
                        error = "AI metadata generation failed. Please retry.",
                        debug = new
                        {
                            hasMeta = meta.HasValue,
                            titleLen = meta.HasValue && meta.Value.title != null ? meta.Value.title.Length : 0,
                            summaryLen = meta.HasValue && meta.Value.summary != null ? meta.Value.summary.Length : 0
                        }
                    });
                }

                return Ok(new
                {
                    title = meta.Value.title,
                    summary = meta.Value.summary
                });
            }
            catch (Exception ex)
            {
                // Fehlerdetails nur serverseitig ins Log – dem Client nur eine
                // generische Meldung zurückgeben (kein Leak von ex.Message/Interna).
                _logger.LogError(ex, "RecipeSwap-Anfrage fehlgeschlagen.");
                return StatusCode(500, new { error = "Internal server error" });
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
        public int? SwapVariantId { get; set; }
        public int FromIngredientId { get; set; }
        public string FromName { get; set; }
        public int ToIngredientId { get; set; }
        public string ToName { get; set; }
        public decimal NewQuantity { get; set; }
        public string Unit { get; set; }
    }

    public sealed class ApplySwapBatchRequest
    {
        public int RecipeId { get; set; }
        public int? SwapVariantId { get; set; }
        public List<ApplySwapRequest> Swaps { get; set; } = new();
    }

    public sealed class RewriteStepsRequest
    {
        public int RecipeId { get; set; }
        public int? SwapVariantId { get; set; }
        public string? Language { get; set; }
    }

    public sealed class GenerateVariantMetaRequest
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

    public sealed class PublishCommunityVariantRequest
    {
        public int RecipeId { get; set; }
        public int SwapVariantId { get; set; }
        public int? ParentCommunityVariantId { get; set; }
        public string? Title { get; set; }
        public string? Summary { get; set; }
        public string? Language { get; set; }
    }

    public sealed class CommunityVariantsListResponse
    {
        public int Total { get; set; }
        public List<CommunityVariantListItem> Items { get; set; } = new();
    }

    public sealed class CommunityVariantListItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public string Language { get; set; } = "de";
        public int SwapCount { get; set; }
        public int LikeCount { get; set; }
        public int PreparationTime { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
