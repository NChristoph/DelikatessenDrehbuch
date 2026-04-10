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
                return Json(new { success = false, error = "Nicht berechtigt." });
            }
            if (!TryConsumeUploadSlot(userHash, out var retryAfter))
            {
                if (retryAfter.HasValue)
                {
                    Response.Headers["Retry-After"] = Math.Ceiling(retryAfter.Value.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }
                return Json(new { success = false, error = "Upload-Limit erreicht. Bitte später erneut versuchen." });
            }

            var uploadResult = await _blobUpload.UploadContentToBlob(posting.Content);
            SaveNewRecipeModel recipeModel = new()
            {
                Recipes = new Recipes()
                {
                    Name = posting.Title,
                    Category = posting.Recipe.Category,
                    RecipePersonCount = posting.Recipe.PersonCount,
                    PreparationTime = posting.Recipe.PreparationTime,
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

            return Json(new { success = true });
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

            // Same model as Upload() action
            var model = new WorldUserPosting()
            {
                ToSelectIngredientsAndNutrients = await _context.IngredientsAndNutrients.Include(x => x.Group).ToListAsync(),
                Measure = await _context.Metrics.ToListAsync(),
                ToSelectKeywords = await _context.Keywords.OrderBy(k => k.Word_DE).ToListAsync()
            };

            ViewData["IsEditMode"] = true;
            ViewData["PostingId"] = posting.Id;
            ViewData["RecipeId"] = posting.Recipe.Id;

            var editData = new
            {
                title = posting.Title ?? "",
                category = posting.Recipe.Category ?? "",
                preferences = posting.Recipe.Preferences ?? "",
                personCount = posting.Recipe.PersonCount,
                preparationTime = posting.Recipe.PreparationTime,
                currentImageUrl = posting.ThumbnailUrl ?? posting.Source ?? ""
            };
            ViewData["EditData"] = JsonSerializer.Serialize(editData);

            var editIngredients = (posting.Recipe.Ingredients ?? Enumerable.Empty<RecipeJoinIngredientMeasureQuantity>())
                .Where(x => x.Ingredient?.IngredientsAndNutrients != null && x.Ingredient?.Measure != null)
                .Select(x => new
                {
                    id = x.Ingredient.IngredientsAndNutrients.Id,
                    quantity = x.Ingredient.Quantity?.Quantitys ?? 0,
                    measureDe = x.Ingredient.Measure.Metrics_DE ?? ""
                })
                .ToList();
            ViewData["EditIngredients"] = JsonSerializer.Serialize(editIngredients);

            var editKeywordIds = (posting.Recipe.RecipeKeywords ?? Enumerable.Empty<RecipeBaseKeyword>())
                .Select(k => k.KeywordId)
                .ToList();
            ViewData["EditKeywordIds"] = JsonSerializer.Serialize(editKeywordIds);

            var editSteps = (posting.Recipe.Steps ?? Enumerable.Empty<RecipeJoinPreparationSteps>())
                .OrderBy(s => s.StepIndex)
                .Where(s => s.RecipePreparationStep != null)
                .Select(s => new
                {
                    preparationStepId = s.RecipePreparationStep.Id,
                    stepIndex = s.StepIndex,
                    stepDe = s.RecipePreparationStep.Step_DE ?? "",
                    stepEn = s.RecipePreparationStep.Step_EN ?? "",
                    stepEsp = s.RecipePreparationStep.Step_ESP ?? "",
                    stepPrt = s.RecipePreparationStep.Step_PRT ?? "",
                    phase = s.RecipePreparationStep.Phase,
                    equipment = s.RecipePreparationStep.Equipment
                })
                .ToList();
            ViewData["EditSteps"] = JsonSerializer.Serialize(editSteps);

            var editSmartSteps = (posting.Recipe.SmartSteps ?? Enumerable.Empty<RecipeJoinSmartStep>())
                .OrderBy(ss => ss.StepIndex)
                .Select(ss => new
                {
                    masterStepKey = ss.SmartRecipeStep.MasterStepKey ?? "",
                    variablesJson = ss.SmartRecipeStep.VariablesJson ?? "{}",
                    stepIndex = ss.StepIndex
                })
                .ToList();
            ViewData["EditSmartSteps"] = JsonSerializer.Serialize(editSmartSteps);

            return View("~/Areas/WorldMiniApp/Views/Home/CreatePosting.cshtml", model);
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRecipeFromCreateForm(WorldUserPosting posting, string userHash)
        {
            userHash = ResolveUserHash(userHash);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return RedirectToAction("Index", "Home", new { area = "WorldMiniApp" });
            }

            if (!int.TryParse(Request.Form["EditPostingId"].FirstOrDefault(), out var postingId) || postingId <= 0)
            {
                return BadRequest("Missing PostingId.");
            }
            _ = int.TryParse(Request.Form["EditRecipeId"].FirstOrDefault(), out var recipeId);

            var postingToEdit = await _context.WorldUserPosting
                .Include(x => x.Recipe)
                    .ThenInclude(r => r.Ingredients)
                        .ThenInclude(link => link.Ingredient)
                            .ThenInclude(i => i.Quantity)
                .Include(x => x.Recipe)
                    .ThenInclude(r => r.Images)
                .FirstOrDefaultAsync(x => x.Id == postingId);

            if (postingToEdit == null || postingToEdit.Recipe == null)
            {
                return NotFound();
            }

            if (!string.Equals(postingToEdit.CreatorId, userHash, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            // Update basic recipe properties
            var newTitle = (posting.Title ?? "").Trim();
            postingToEdit.Title = string.IsNullOrWhiteSpace(newTitle) ? postingToEdit.Title : newTitle;
            postingToEdit.Recipe.Title = postingToEdit.Title;
            postingToEdit.Recipe.Category = posting.Recipe?.Category ?? postingToEdit.Recipe.Category;
            postingToEdit.Recipe.Preferences = posting.Recipe?.Preferences ?? postingToEdit.Recipe.Preferences;
            postingToEdit.Recipe.PersonCount = posting.Recipe?.PersonCount ?? postingToEdit.Recipe.PersonCount;
            postingToEdit.Recipe.PreparationTime = posting.Recipe?.PreparationTime ?? postingToEdit.Recipe.PreparationTime;

            // Remove existing ingredients
            var existingJoinEntries = postingToEdit.Recipe.Ingredients?.ToList() ?? new List<RecipeJoinIngredientMeasureQuantity>();
            if (existingJoinEntries.Any())
            {
                _context.RecipeJoinIngredientMeasureQuantity.RemoveRange(existingJoinEntries);
                await _context.SaveChangesAsync();
            }

            // Extract ingredients from CreatePosting format: IngredientMeasureQuantity[i]
            var form = Request.Form;
            for (var i = 0; ; i++)
            {
                var ingredientIdKey = $"IngredientMeasureQuantity[{i}].IngredientsAndNutrients.Id";
                var quantityKey = $"IngredientMeasureQuantity[{i}].Quantity.Quantitys";
                var measureDeKey = $"IngredientMeasureQuantity[{i}].Measure.Metrics_DE";

                if (!form.ContainsKey(ingredientIdKey))
                    break;

                _ = int.TryParse(form[ingredientIdKey].FirstOrDefault(), out var ingredientId);
                if (ingredientId <= 0) continue;

                var rawQty = (form[quantityKey].FirstOrDefault() ?? "0").Trim().Replace(",", ".");
                _ = double.TryParse(rawQty, NumberStyles.Any, CultureInfo.InvariantCulture, out var qty);
                if (qty <= 0) continue;

                var measureDe = (form[measureDeKey].FirstOrDefault() ?? "").Trim();

                var ingredient = await _context.IngredientsAndNutrients.FindAsync(ingredientId);
                if (ingredient == null) continue;

                var measure = await _context.Metrics.FirstOrDefaultAsync(m => m.Metrics_DE == measureDe);
                if (measure == null) continue;

                var quantity = new Quantity { Quantitys = qty };
                var imq = new IngredientMeasureQuantity
                {
                    IngredientsAndNutrients = ingredient,
                    Measure = measure,
                    Quantity = quantity
                };
                var join = new RecipeJoinIngredientMeasureQuantity
                {
                    Recipe = postingToEdit.Recipe,
                    Ingredient = imq
                };

                await _context.Quantities.AddAsync(quantity);
                await _context.IngredientMeasureQuantity.AddAsync(imq);
                await _context.RecipeJoinIngredientMeasureQuantity.AddAsync(join);
            }

            // Handle new content (image/video upload) - optional in edit mode
            if (posting.Content != null && posting.Content.Length > 0)
            {
                var uploadResult = await _blobUpload.UploadContentToBlob(posting.Content);
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

            // Update steps (CreatePosting format: RecipePreperationSteps[i])
            var parsedSteps = ExtractCreatePostingStepsFromRequest(form);
            if (parsedSteps.Any())
            {
                var existingSteps = await _context.RecipeJoinPreparationSteps
                    .Where(s => s.Recipe.Id == postingToEdit.Recipe.Id)
                    .ToListAsync();
                _context.RecipeJoinPreparationSteps.RemoveRange(existingSteps);

                foreach (var stepJoin in parsedSteps)
                {
                    RecipePreparationSteps stepEntity;
                    if (stepJoin.PreparationStepId > 0)
                    {
                        stepEntity = await _context.RecipePreparationSteps.FindAsync(stepJoin.PreparationStepId);
                        if (stepEntity == null) continue;
                    }
                    else if (!string.IsNullOrWhiteSpace(stepJoin.RecipePreparationStep?.Step_DE)
                          || !string.IsNullOrWhiteSpace(stepJoin.RecipePreparationStep?.Step_EN))
                    {
                        stepEntity = stepJoin.RecipePreparationStep;
                        await _context.RecipePreparationSteps.AddAsync(stepEntity);
                    }
                    else
                    {
                        continue;
                    }

                    var newJoin = new RecipeJoinPreparationSteps
                    {
                        Recipe = postingToEdit.Recipe,
                        RecipePreparationStep = stepEntity,
                        StepIndex = stepJoin.StepIndex > 0 ? stepJoin.StepIndex : 1
                    };
                    await _context.RecipeJoinPreparationSteps.AddAsync(newJoin);
                }
            }

            // Update keywords
            var parsedKeywordIds = ExtractKeywordIdsFromRequest(form);
            {
                var existingKeywords = await _context.RecipeBaseKeywords
                    .Where(k => k.RecipeBaseDataId == postingToEdit.Recipe.Id)
                    .ToListAsync();
                _context.RecipeBaseKeywords.RemoveRange(existingKeywords);

                if (parsedKeywordIds.Any())
                {
                    var keywordLinks = parsedKeywordIds
                        .Distinct()
                        .Select(keywordId => new RecipeBaseKeyword
                        {
                            RecipeBaseDataId = postingToEdit.Recipe.Id,
                            KeywordId = keywordId
                        })
                        .ToList();
                    await _context.RecipeBaseKeywords.AddRangeAsync(keywordLinks);
                }
            }

            // Update smart steps (same format as CreatePosting)
            var parsedSmartSteps = ExtractCreatePostingSmartStepsFromRequest(form);
            if (parsedSmartSteps.Any() || form.Keys.Any(k => k.StartsWith("SmartStepReferences[", StringComparison.OrdinalIgnoreCase)))
            {
                var existingSmartSteps = await _context.RecipeJoinSmartStep
                    .Where(ss => ss.RecipeId == postingToEdit.Recipe.Id)
                    .ToListAsync();
                _context.RecipeJoinSmartStep.RemoveRange(existingSmartSteps);

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

                    var smartJoin = new RecipeJoinSmartStep
                    {
                        Recipe = postingToEdit.Recipe,
                        SmartRecipeStep = smartStep,
                        StepIndex = stepRef.StepIndex > 0 ? stepRef.StepIndex : 1
                    };
                    await _context.RecipeJoinSmartStep.AddAsync(smartJoin);
                }
            }

            await _context.SaveChangesAsync();

            return RedirectToAction("MyProfile", "Feed", new { area = "WorldMiniApp", userHash });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePosting([FromForm] int postingId, [FromForm] string userHash)
        {
            userHash = ResolveUserHash(userHash);
            if (userHash != SuperUserHash)
            {
                return Forbid();
            }

            var posting = await _context.WorldUserPosting
                .Include(p => p.Recipe)
                    .ThenInclude(r => r.Ingredients)
                        .ThenInclude(j => j.Ingredient)
                            .ThenInclude(i => i.Quantity)
                .Include(p => p.Recipe)
                    .ThenInclude(r => r.Steps)
                .Include(p => p.Recipe)
                    .ThenInclude(r => r.SmartSteps)
                .Include(p => p.Recipe)
                    .ThenInclude(r => r.RecipeKeywords)
                .Include(p => p.Recipe)
                    .ThenInclude(r => r.Images)
                .FirstOrDefaultAsync(p => p.Id == postingId);

            if (posting == null)
            {
                return NotFound();
            }

            if (posting.Recipe != null)
            {
                var likes = await _context.WorldUserLike
                    .Where(l => l.Recipe.Id == posting.Recipe.Id)
                    .ToListAsync();
                _context.WorldUserLike.RemoveRange(likes);

                if (posting.Recipe.RecipeKeywords != null)
                    _context.RecipeBaseKeywords.RemoveRange(posting.Recipe.RecipeKeywords);

                if (posting.Recipe.SmartSteps != null)
                    _context.RecipeJoinSmartStep.RemoveRange(posting.Recipe.SmartSteps);

                if (posting.Recipe.Steps != null)
                    _context.RecipeJoinPreparationSteps.RemoveRange(posting.Recipe.Steps);

                if (posting.Recipe.Images != null)
                    _context.RecipeBaseDataImage.RemoveRange(posting.Recipe.Images);

                if (posting.Recipe.Ingredients != null)
                {
                    foreach (var join in posting.Recipe.Ingredients)
                    {
                        if (join.Ingredient?.Quantity != null)
                            _context.Quantities.Remove(join.Ingredient.Quantity);
                        if (join.Ingredient != null)
                            _context.IngredientMeasureQuantity.Remove(join.Ingredient);
                    }
                    _context.RecipeJoinIngredientMeasureQuantity.RemoveRange(posting.Recipe.Ingredients);
                }

                _context.RecipeBaseData.Remove(posting.Recipe);
            }

            _context.WorldUserPosting.Remove(posting);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin deleted posting {PostingId} by creator {CreatorId}.", postingId, posting.CreatorId);

            return RedirectToAction("MyProfile", "Feed", new { area = "WorldMiniApp", userHash });
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
