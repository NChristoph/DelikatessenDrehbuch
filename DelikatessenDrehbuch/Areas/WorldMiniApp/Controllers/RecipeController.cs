using DelikatessenDrehbuch.Areas.WorldMiniApp.Extensions;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    [Route("WorldMiniApp/Home/{action}")]
    public class RecipeController : WorldMiniAppBaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly IBlobUploadService _blobUpload;
        private readonly ISaveNewRecipeService _saveNewRecipeService;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<RecipeController> _logger;
        private const int UploadRateLimit = 5;
        private static readonly TimeSpan UploadRateWindow = TimeSpan.FromMinutes(10);

        public RecipeController(
            ApplicationDbContext context,
            IBlobUploadService blobUpload,
            ISaveNewRecipeService saveNewRecipeService,
            IMemoryCache memoryCache,
            ILogger<RecipeController> logger)
        {
            _context = context;
            _blobUpload = blobUpload;
            _saveNewRecipeService = saveNewRecipeService;
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public async Task<IActionResult> Upload(string userHash)
        {
            userHash = ResolveUserHash(userHash);

            var model = new WorldUserPosting()
            {
                ToSelectIngredientsAndNutrients = await _context.IngredientsAndNutrients.Include(x => x.Group).ToListAsync(),
                Measure = await _context.Metrics.ToListAsync(),
                ToSelectKeywords = await _context.Keywords.OrderBy(k => k.Word_DE).ToListAsync()
            };

            return View("~/Areas/WorldMiniApp/Views/Home/CreatePosting.cshtml", model);
        }

        [HttpPost]
        public async Task<IActionResult> UploadNewVideoAsync(WorldUserPosting posting, string userHash)
        {
            userHash = ResolveUserHash(userHash);
            if (!await IsCreatorAllowedAsync(_context, userHash))
            {
                return RedirectToAction("Index", "Home", new { area = "WorldMiniApp" });
            }
            if (!TryConsumeUploadSlot(userHash, out var retryAfter))
            {
                if (retryAfter.HasValue)
                {
                    Response.Headers["Retry-After"] = Math.Ceiling(retryAfter.Value.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }
                return StatusCode(StatusCodes.Status429TooManyRequests, "Upload-Limit erreicht. Bitte später erneut versuchen.");
            }

            var uploadResult = await _blobUpload.UploadContentToBlob(posting.Content);
            SaveNewRecipeModel recipeModel = new()
            {
                Recipes = new Recipes()
                {
                    Name = posting.Title,
                    Category = posting.Recipe.Category,
                    RecipePersonCount = posting.Recipe.PersonCount,
                    ImagePath = uploadResult.SourceUrl
                },
                Querys = posting.Recipe.Preferences,
                IngredientMeasureQuantity = posting.IngredientMeasureQuantity,
                RecipeJoinPreparationSteps = ExtractCreatePostingStepsFromRequest(Request.Form),
                SmartStepReferences = ExtractCreatePostingSmartStepsFromRequest(Request.Form),
            };
            await _saveNewRecipeService.SaveNewAsync(recipeModel, true);
            var recipe = await _context.RecipeBaseData.FirstOrDefaultAsync(r => r.Title == posting.Title);
            posting.CreationTime = DateTime.Now;
            posting.CreatorName = "Avocado";
            posting.CreatorId = userHash;
            posting.Source = uploadResult.SourceUrl;
            posting.ThumbnailUrl = uploadResult.ThumbnailUrl;
            if (recipe != null)
            {
                posting.Recipe = recipe;
                posting.Recipe.PreparationTime = 0;
            }

            await _context.WorldUserPosting.AddAsync(posting);
            await _context.SaveChangesAsync();

            if (recipe != null && posting.SelectedKeywordIds != null && posting.SelectedKeywordIds.Any())
            {
                var keywordLinks = posting.SelectedKeywordIds
                    .Distinct()
                    .Select(keywordId => new RecipeBaseKeyword
                    {
                        RecipeBaseDataId = recipe.Id,
                        KeywordId = keywordId
                    })
                    .ToList();

                await _context.RecipeBaseKeywords.AddRangeAsync(keywordLinks);
                await _context.SaveChangesAsync();
            }

            Response.Cookies.Append("createPostingDraftReset", "1", new CookieOptions
            {
                Path = "/",
                HttpOnly = false,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddHours(1)
            });

            return RedirectToAction("Index", "Home", new { area = "WorldMiniApp" });
        }

        public async Task<IActionResult> EditRecipe(int postingId, string userHash)
        {
            userHash = ResolveUserHash(userHash);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return RedirectToAction("Index", "Home", new { area = "WorldMiniApp" });
            }

            var posting = await _context.WorldUserPosting
                .IncludeFullPostingRecipe()
                .FirstOrDefaultAsync(x => x.Id == postingId);

            if (posting == null || posting.Recipe == null)
            {
                return NotFound();
            }

            if (!string.Equals(posting.CreatorId, userHash, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var model = new EditPostingRecipeViewModel
            {
                PostingId = posting.Id,
                RecipeId = posting.Recipe.Id,
                Title = posting.Title,
                Category = posting.Recipe.Category,
                Preferences = posting.Recipe.Preferences,
                PersonCount = posting.Recipe.PersonCount,
                PreparationTime = posting.Recipe.PreparationTime,
                CurrentImageUrl = posting.ThumbnailUrl ?? posting.Source,
                Ingredients = posting.Recipe.Ingredients?
                    .Where(x => x.Ingredient?.IngredientsAndNutrients != null && x.Ingredient?.Measure != null)
                    .Select(x => new EditPostingIngredientRowViewModel
                    {
                        IngredientId = x.Ingredient.IngredientsAndNutrients.Id,
                        MeasureId = x.Ingredient.Measure.Id,
                        Quantity = x.Ingredient.Quantity?.Quantitys ?? 0
                    })
                    .ToList() ?? new List<EditPostingIngredientRowViewModel>(),
                Steps = posting.Recipe.Steps?
                    .OrderBy(s => s.StepIndex)
                    .Where(s => s.RecipePreparationStep != null)
                    .Select(s => new EditPostingStepRowViewModel
                    {
                        PreparationStepId = s.RecipePreparationStep.Id,
                        StepIndex = s.StepIndex
                    })
                    .ToList() ?? new List<EditPostingStepRowViewModel>(),
                ExistingSmartSteps = posting.Recipe.SmartSteps?
                    .OrderBy(ss => ss.StepIndex)
                    .Select(ss => new EditPostingSmartStepViewModel
                    {
                        MasterStepKey = ss.SmartRecipeStep.MasterStepKey,
                        VariablesJson = ss.SmartRecipeStep.VariablesJson,
                        StepIndex = ss.StepIndex
                    })
                    .ToList() ?? new List<EditPostingSmartStepViewModel>(),
                SelectedKeywordIds = posting.Recipe.RecipeKeywords?
                    .Select(k => k.KeywordId)
                    .ToList() ?? new List<int>(),
                AvailableIngredients = await _context.IngredientsAndNutrients.OrderBy(x => x.Name_DE).ToListAsync(),
                AvailableMeasures = await _context.Metrics.OrderBy(x => x.Metrics_DE).ToListAsync(),
                AvailableSteps = await _context.RecipePreparationSteps.ToListAsync(),
                AvailableKeywords = await _context.Keywords.OrderBy(k => k.Word_DE).ToListAsync(),
                IngredientStepJoins = await _context.JoinIngredientPreparationStep
                    .Include(x => x.Preparation)
                    .Include(x => x.Ingredient)
                    .ToListAsync()
            };

            return View("~/Areas/WorldMiniApp/Views/Home/EditRecipe.cshtml", model);
        }

        [HttpPost]
        public async Task<IActionResult> UpsertStep([FromBody] UpsertStepRequest request, string userHash)
        {
            userHash = ResolveUserHash(userHash);
            var isAdmin = User?.Identity?.IsAuthenticated == true && User.IsInRole("Admin");
            if (!isAdmin && !await IsCreatorAllowedAsync(_context, userHash))
            {
                return Forbid();
            }

            if (request == null || string.IsNullOrWhiteSpace(request.De) || string.IsNullOrWhiteSpace(request.En))
            {
                return BadRequest(new { message = "Ungültige Step-Daten." });
            }

            var de = request.De.Trim();
            var en = request.En.Trim();
            var esp = (request.Esp ?? string.Empty).Trim();
            var prt = (request.Prt ?? string.Empty).Trim();
            var phase = request.Phase;
            var equipment = request.Equipment;

            var existing = await _context.RecipePreparationSteps
                .FirstOrDefaultAsync(x => x.Step_DE == de
                    && x.Step_EN == en
                    && x.Step_ESP == esp
                    && x.Step_PRT == prt
                    && x.Phase == phase
                    && x.Equipment == equipment);

            if (existing != null)
            {
                return Json(new { id = existing.Id, reused = true });
            }

            var step = new RecipePreparationSteps
            {
                Step_DE = de,
                Step_EN = en,
                Step_ESP = esp,
                Step_PRT = prt,
                Phase = phase,
                Equipment = equipment
            };

            await _context.RecipePreparationSteps.AddAsync(step);
            await _context.SaveChangesAsync();

            return Json(new { id = step.Id, reused = false });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRecipe(EditPostingRecipeViewModel model, string userHash)
        {
            userHash = ResolveUserHash(userHash);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return RedirectToAction("Index", "Home", new { area = "WorldMiniApp" });
            }

            var postingToEdit = await _context.WorldUserPosting
                .Include(x => x.Recipe)
                    .ThenInclude(r => r.Ingredients)
                        .ThenInclude(link => link.Ingredient)
                            .ThenInclude(i => i.Quantity)
                .Include(x => x.Recipe)
                    .ThenInclude(r => r.Images)
                .FirstOrDefaultAsync(x => x.Id == model.PostingId);

            if (postingToEdit == null || postingToEdit.Recipe == null)
            {
                return NotFound();
            }

            if (!string.Equals(postingToEdit.CreatorId, userHash, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            postingToEdit.Title = string.IsNullOrWhiteSpace(model.Title) ? postingToEdit.Title : model.Title.Trim();
            postingToEdit.Recipe.Title = postingToEdit.Title;
            postingToEdit.Recipe.Category = model.Category ?? postingToEdit.Recipe.Category;
            postingToEdit.Recipe.Preferences = model.Preferences ?? postingToEdit.Recipe.Preferences;
            postingToEdit.Recipe.PersonCount = model.PersonCount;
            postingToEdit.Recipe.PreparationTime = model.PreparationTime;

            var existingJoinEntries = postingToEdit.Recipe.Ingredients?.ToList() ?? new List<RecipeJoinIngredientMeasureQuantity>();

            if (existingJoinEntries.Any())
            {
                _context.RecipeJoinIngredientMeasureQuantity.RemoveRange(existingJoinEntries);
                await _context.SaveChangesAsync();
            }

            var parsedRows = ExtractIngredientRowsFromRequest(model, Request.Form);
            var cleanedRows = parsedRows
                .Where(x => x.IngredientId > 0 && x.MeasureId > 0 && x.Quantity > 0)
                .ToList();

            var ingredientIds = cleanedRows.Select(x => x.IngredientId).Distinct().ToList();
            var measureIds = cleanedRows.Select(x => x.MeasureId).Distinct().ToList();

            var ingredientsById = await _context.IngredientsAndNutrients
                .Where(x => ingredientIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);

            var measuresById = await _context.Metrics
                .Where(x => measureIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);

            foreach (var row in cleanedRows)
            {
                if (!ingredientsById.TryGetValue(row.IngredientId, out var ingredient))
                {
                    continue;
                }

                if (!measuresById.TryGetValue(row.MeasureId, out var measure))
                {
                    continue;
                }

                var quantity = new Quantity { Quantitys = row.Quantity };
                var ingredientMeasureQuantity = new IngredientMeasureQuantity
                {
                    IngredientsAndNutrients = ingredient,
                    Measure = measure,
                    Quantity = quantity
                };

                var join = new RecipeJoinIngredientMeasureQuantity
                {
                    Recipe = postingToEdit.Recipe,
                    Ingredient = ingredientMeasureQuantity
                };

                await _context.Quantities.AddAsync(quantity);
                await _context.IngredientMeasureQuantity.AddAsync(ingredientMeasureQuantity);
                await _context.RecipeJoinIngredientMeasureQuantity.AddAsync(join);
            }

            if (model.NewContent != null && model.NewContent.Length > 0)
            {
                var uploadResult = await _blobUpload.UploadContentToBlob(model.NewContent);
                postingToEdit.Source = uploadResult.SourceUrl;
                postingToEdit.ThumbnailUrl = uploadResult.ThumbnailUrl;

                var recipeImage = postingToEdit.Recipe.Images?.FirstOrDefault(x => x.WorldAppImage)
                                 ?? postingToEdit.Recipe.Images?.FirstOrDefault();

                if (recipeImage == null)
                {
                    recipeImage = new RecipeBaseDataImage
                    {
                        Recipe = postingToEdit.Recipe,
                        Image = uploadResult.SourceUrl,
                        WorldAppImage = true
                    };
                    await _context.RecipeBaseDataImage.AddAsync(recipeImage);
                }
                else
                {
                    recipeImage.Image = uploadResult.SourceUrl;
                    recipeImage.WorldAppImage = true;
                }
            }

            // Update steps
            var existingSteps = await _context.RecipeJoinPreparationSteps
                .Where(s => s.Recipe.Id == postingToEdit.Recipe.Id)
                .ToListAsync();
            _context.RecipeJoinPreparationSteps.RemoveRange(existingSteps);

            var parsedSteps = ExtractStepRowsFromRequest(Request.Form);
            var stepIds = parsedSteps.Where(s => s.PreparationStepId > 0).Select(s => s.PreparationStepId).ToList();
            var stepEntities = await _context.RecipePreparationSteps
                .Where(s => stepIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id);

            foreach (var stepRow in parsedSteps.Where(s => s.PreparationStepId > 0))
            {
                if (!stepEntities.TryGetValue(stepRow.PreparationStepId, out var stepEntity)) continue;

                var stepJoin = new RecipeJoinPreparationSteps
                {
                    Recipe = postingToEdit.Recipe,
                    RecipePreparationStep = stepEntity,
                    StepIndex = stepRow.StepIndex
                };
                await _context.RecipeJoinPreparationSteps.AddAsync(stepJoin);
            }

            // Update keywords
            var existingKeywords = await _context.RecipeBaseKeywords
                .Where(k => k.RecipeBaseDataId == postingToEdit.Recipe.Id)
                .ToListAsync();
            _context.RecipeBaseKeywords.RemoveRange(existingKeywords);

            var parsedKeywordIds = ExtractKeywordIdsFromRequest(Request.Form);
            var keywordLinks = parsedKeywordIds
                .Distinct()
                .Select(keywordId => new RecipeBaseKeyword
                {
                    RecipeBaseDataId = postingToEdit.Recipe.Id,
                    KeywordId = keywordId
                })
                .ToList();
            await _context.RecipeBaseKeywords.AddRangeAsync(keywordLinks);

            // Update smart steps
            var existingSmartSteps = await _context.RecipeJoinSmartStep
                .Where(ss => ss.RecipeId == postingToEdit.Recipe.Id)
                .ToListAsync();
            _context.RecipeJoinSmartStep.RemoveRange(existingSmartSteps);

            var parsedSmartSteps = ExtractCreatePostingSmartStepsFromRequest(Request.Form);
            foreach (var stepRef in parsedSmartSteps)
            {
                var parsed = ParseSmartStepMetadata(stepRef.MetadataJson);

                var existingSmartStep = await _context.SmartRecipeStep
                    .FirstOrDefaultAsync(x =>
                        x.MasterStepKey == stepRef.MasterStepKey &&
                        x.VariablesJson == parsed.variablesJson &&
                        x.Phase == parsed.phase &&
                        x.Equipment == parsed.equipment);

                var smartStep = existingSmartStep;
                if (smartStep == null)
                {
                    smartStep = new SmartRecipeStep
                    {
                        MasterStepKey = stepRef.MasterStepKey,
                        VariablesJson = parsed.variablesJson,
                        Phase = parsed.phase,
                        Equipment = parsed.equipment
                    };
                    await _context.SmartRecipeStep.AddAsync(smartStep);
                }

                var join = new RecipeJoinSmartStep
                {
                    Recipe = postingToEdit.Recipe,
                    SmartRecipeStep = smartStep,
                    StepIndex = stepRef.StepIndex > 0 ? stepRef.StepIndex : 1
                };
                await _context.RecipeJoinSmartStep.AddAsync(join);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction("MyProfile", "Feed", new { area = "WorldMiniApp", userHash });
        }

        private static List<EditPostingIngredientRowViewModel> ExtractIngredientRowsFromRequest(EditPostingRecipeViewModel model, IFormCollection form)
        {
            var fallback = model.Ingredients ?? new List<EditPostingIngredientRowViewModel>();
            var result = new List<EditPostingIngredientRowViewModel>();

            for (var i = 0; ; i++)
            {
                var ingredientKey = $"Ingredients[{i}].IngredientId";
                var measureKey = $"Ingredients[{i}].MeasureId";
                var quantityKey = $"Ingredients[{i}].Quantity";

                if (!form.ContainsKey(ingredientKey) && !form.ContainsKey(measureKey) && !form.ContainsKey(quantityKey))
                {
                    break;
                }

                _ = int.TryParse(form[ingredientKey].FirstOrDefault(), out var ingredientId);
                _ = int.TryParse(form[measureKey].FirstOrDefault(), out var measureId);

                var rawQuantity = (form[quantityKey].FirstOrDefault() ?? string.Empty).Trim();
                var normalizedQuantity = rawQuantity.Replace(" ", string.Empty).Replace(",", ".");

                var quantityParsed = double.TryParse(normalizedQuantity, NumberStyles.Any, CultureInfo.InvariantCulture, out var quantity)
                                     || double.TryParse(rawQuantity, NumberStyles.Any, CultureInfo.CurrentCulture, out quantity);

                result.Add(new EditPostingIngredientRowViewModel
                {
                    IngredientId = ingredientId,
                    MeasureId = measureId,
                    Quantity = quantityParsed ? quantity : 0
                });
            }

            return result.Any() ? result : fallback;
        }

        private static List<RecipeJoinPreparationSteps> ExtractCreatePostingStepsFromRequest(IFormCollection form)
        {
            var result = new List<RecipeJoinPreparationSteps>();

            for (var i = 0; ; i++)
            {
                var stepIdKey = $"RecipePreperationSteps[{i}].PreperationStepId";
                var indexKey = $"RecipePreperationSteps[{i}].StepIndex";
                var stepDeKey = $"RecipePreperationSteps[{i}].RecipePreperationStep.Step_DE";
                var stepEnKey = $"RecipePreperationSteps[{i}].RecipePreperationStep.Step_EN";
                var stepEspKey = $"RecipePreperationSteps[{i}].RecipePreperationStep.Step_ESP";
                var stepPrtKey = $"RecipePreperationSteps[{i}].RecipePreperationStep.Step_PRT";
                var phaseKey = $"RecipePreperationSteps[{i}].RecipePreperationStep.Phase";
                var equipmentKey = $"RecipePreperationSteps[{i}].RecipePreperationStep.Equipment";

                if (!form.ContainsKey(stepIdKey)
                    && !form.ContainsKey(stepDeKey)
                    && !form.ContainsKey(stepEnKey)
                    && !form.ContainsKey(stepEspKey)
                    && !form.ContainsKey(stepPrtKey))
                {
                    break;
                }

                _ = int.TryParse(form[stepIdKey].FirstOrDefault(), out var preparationStepId);
                _ = int.TryParse(form[indexKey].FirstOrDefault(), out var stepIndex);
                _ = int.TryParse(form[phaseKey].FirstOrDefault(), out var phase);
                _ = int.TryParse(form[equipmentKey].FirstOrDefault(), out var equipment);

                var recipeStep = new RecipePreparationSteps
                {
                    Step_DE = (form[stepDeKey].FirstOrDefault() ?? string.Empty).Trim(),
                    Step_EN = (form[stepEnKey].FirstOrDefault() ?? string.Empty).Trim(),
                    Step_ESP = (form[stepEspKey].FirstOrDefault() ?? string.Empty).Trim(),
                    Step_PRT = (form[stepPrtKey].FirstOrDefault() ?? string.Empty).Trim(),
                    Phase = phase,
                    Equipment = equipment
                };

                var hasAnyText =
                    !string.IsNullOrWhiteSpace(recipeStep.Step_DE) ||
                    !string.IsNullOrWhiteSpace(recipeStep.Step_EN) ||
                    !string.IsNullOrWhiteSpace(recipeStep.Step_ESP) ||
                    !string.IsNullOrWhiteSpace(recipeStep.Step_PRT);

                if (!hasAnyText && preparationStepId <= 0)
                {
                    continue;
                }

                result.Add(new RecipeJoinPreparationSteps
                {
                    PreparationStepId = preparationStepId,
                    RecipePreparationStep = recipeStep,
                    StepIndex = stepIndex > 0 ? stepIndex : i + 1
                });
            }

            return result;
        }

        private static List<SmartStepReferenceInput> ExtractCreatePostingSmartStepsFromRequest(IFormCollection form)
        {
            var result = new List<SmartStepReferenceInput>();

            for (var i = 0; ; i++)
            {
                var masterKeyField = $"SmartStepReferences[{i}].MasterStepKey";
                var metadataField = $"SmartStepReferences[{i}].MetadataJson";
                var stepIndexField = $"RecipePreperationSteps[{i}].StepIndex";

                if (!form.ContainsKey(masterKeyField) && !form.ContainsKey(metadataField))
                {
                    break;
                }

                var masterKey = (form[masterKeyField].FirstOrDefault() ?? string.Empty).Trim();
                var metadataJson = (form[metadataField].FirstOrDefault() ?? string.Empty).Trim();
                _ = int.TryParse(form[stepIndexField].FirstOrDefault(), out var stepIndex);

                if (string.IsNullOrWhiteSpace(masterKey))
                {
                    continue;
                }

                result.Add(new SmartStepReferenceInput
                {
                    MasterStepKey = masterKey,
                    MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson,
                    StepIndex = stepIndex > 0 ? stepIndex : i + 1
                });
            }

            return result;
        }

        private static List<EditPostingStepRowViewModel> ExtractStepRowsFromRequest(IFormCollection form)
        {
            var result = new List<EditPostingStepRowViewModel>();
            for (var i = 0; ; i++)
            {
                var stepIdKey = $"Steps[{i}].PreparationStepId";
                var indexKey = $"Steps[{i}].StepIndex";

                if (!form.ContainsKey(stepIdKey))
                    break;

                _ = int.TryParse(form[stepIdKey].FirstOrDefault(), out var stepId);
                _ = int.TryParse(form[indexKey].FirstOrDefault(), out var stepIndex);

                if (stepId > 0)
                {
                    result.Add(new EditPostingStepRowViewModel
                    {
                        PreparationStepId = stepId,
                        StepIndex = stepIndex > 0 ? stepIndex : i + 1
                    });
                }
            }
            return result;
        }

        private static List<int> ExtractKeywordIdsFromRequest(IFormCollection form)
        {
            var result = new List<int>();
            if (form.TryGetValue("SelectedKeywordIds", out var values))
            {
                foreach (var val in values)
                {
                    if (int.TryParse(val, out var id) && id > 0)
                        result.Add(id);
                }
            }
            return result;
        }

        private static (string variablesJson, int? phase, string? equipment) ParseSmartStepMetadata(string? metadataJson)
        {
            if (string.IsNullOrWhiteSpace(metadataJson))
                return ("{}", null, null);

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
                        phase = parsedPhase;
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
            catch
            {
                return ("{}", null, null);
            }
        }

        private bool TryConsumeUploadSlot(string userHash, out TimeSpan? retryAfter)
        {
            retryAfter = null;
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return false;
            }

            var cacheKey = $"worldminiapp:upload:{userHash}";
            var now = DateTimeOffset.UtcNow;

            var state = _memoryCache.Get<UploadRateState>(cacheKey);
            if (state == null)
            {
                state = new UploadRateState { Count = 1, WindowStart = now };
                _memoryCache.Set(cacheKey, state, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = UploadRateWindow
                });
                return true;
            }

            if (state.Count >= UploadRateLimit)
            {
                retryAfter = (state.WindowStart + UploadRateWindow) - now;
                _logger.LogWarning("Upload rate limit exceeded for user {UserHash}.", userHash);
                return false;
            }

            state.Count += 1;
            _memoryCache.Set(cacheKey, state, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = UploadRateWindow
            });
            return true;
        }

        private sealed class UploadRateState
        {
            public int Count { get; set; }
            public DateTimeOffset WindowStart { get; set; }
        }
    }
}
