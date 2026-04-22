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
using System.Text.RegularExpressions;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Controllers
{
    [Route("WorldMiniApp/Home/{action}")]
    public class RecipeController : WorldMiniAppBaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly IBlobUploadService _blobUpload;
        private readonly ISaveNewRecipeService _saveNewRecipeService;
        private readonly IMissingIngredientAiService _missingIngredientAiService;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<RecipeController> _logger;
        private const int UploadRateLimit = 5;
        private static readonly TimeSpan UploadRateWindow = TimeSpan.FromMinutes(10);
        private const int MissingIngredientSaveRateLimit = 10;
        private static readonly TimeSpan MissingIngredientSaveRateWindow = TimeSpan.FromMinutes(30);

        public RecipeController(
            ApplicationDbContext context,
            IBlobUploadService blobUpload,
            ISaveNewRecipeService saveNewRecipeService,
            IMissingIngredientAiService missingIngredientAiService,
            IMemoryCache memoryCache,
            ILogger<RecipeController> logger)
        {
            _context = context;
            _blobUpload = blobUpload;
            _saveNewRecipeService = saveNewRecipeService;
            _missingIngredientAiService = missingIngredientAiService;
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

            // Idempotency check: prevent duplicate uploads on network retry
            var idempotencyKey = Request.Headers["X-Idempotency-Key"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var cacheKey = $"worldminiapp:idempotency:{userHash}:{idempotencyKey}";
                if (_memoryCache.TryGetValue<object>(cacheKey, out var cachedResult))
                {
                    _logger.LogInformation("Duplicate upload request detected for user {UserHash} with idempotency key {Key}.", userHash, idempotencyKey);
                    return Json(cachedResult);
                }
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

            var recipe = await _saveNewRecipeService.SaveNewAsync(recipeModel, true);

            if (recipe == null)
            {
                _logger.LogError("SaveNewAsync returned null recipe for title {Title} by user {UserHash}.", posting.Title, userHash);
                return Json(new { success = false, error = "Rezept konnte nicht gespeichert werden." });
            }

            var user = await _context.WorldAppUser.FirstOrDefaultAsync(u => u.UserHash == userHash);
            var creatorName = user?.UserName ?? "Avocado";

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                posting.CreationTime = DateTime.Now;
                posting.CreatorName = creatorName;
                posting.CreatorId = userHash;
                posting.Source = uploadResult.SourceUrl;
                posting.ThumbnailUrl = uploadResult.ThumbnailUrl;
                posting.Recipe = recipe;

                await _context.WorldUserPosting.AddAsync(posting);
                await _context.SaveChangesAsync();

                if (posting.SelectedKeywordIds != null && posting.SelectedKeywordIds.Any())
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

                await transaction.CommitAsync();

                Response.Cookies.Append("createPostingDraftReset", "1", new CookieOptions
                {
                    Path = "/",
                    HttpOnly = false,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddHours(1)
                });

                _logger.LogInformation("Recipe {RecipeId} and posting {PostingId} successfully created by user {UserHash}.", recipe.Id, posting.Id, userHash);

                var successResponse = new { success = true };

                // Cache successful response for idempotency (10 minutes)
                if (!string.IsNullOrWhiteSpace(idempotencyKey))
                {
                    var cacheKey = $"worldminiapp:idempotency:{userHash}:{idempotencyKey}";
                    _memoryCache.Set(cacheKey, successResponse, TimeSpan.FromMinutes(10));
                }

                return Json(successResponse);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to create posting for recipe {RecipeId} by user {UserHash}.", recipe.Id, userHash);
                throw;
            }
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

        [HttpPost]
        public async Task<IActionResult> SuggestMissingIngredient([FromBody] MissingIngredientLookupRequest request, CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.IngredientName))
            {
                return BadRequest(new { message = "Bitte gib eine Zutat ein." });
            }

            var userHash = ResolveUserHash(request.UserHash);
            if (!await IsCreatorAllowedAsync(_context, userHash))
            {
                return Json(new MissingIngredientLookupResponse
                {
                    Success = false,
                    Mode = "forbidden",
                    Message = "Nicht berechtigt."
                });
            }

            var trimmedIngredientName = request.IngredientName.Trim();
            var existingMatches = await FindMatchingIngredientsAsync(trimmedIngredientName, 6, cancellationToken);
            if (existingMatches.Count > 0)
            {
                return Json(new MissingIngredientLookupResponse
                {
                    Success = true,
                    Mode = "existing_match",
                    Message = "Es wurden passende Zutaten im Katalog gefunden.",
                    Matches = existingMatches.Select(x => MapExistingMatchDto(x, trimmedIngredientName)).ToList()
                });
            }

            try
            {
                var suggestion = await _missingIngredientAiService.SuggestIngredientAsync(trimmedIngredientName, cancellationToken);
                return Json(new MissingIngredientLookupResponse
                {
                    Success = true,
                    Mode = "ai_suggestion",
                    Message = "Die Zutat wurde von der KI vorgeschlagen.",
                    Model = suggestion.Model,
                    Suggestion = suggestion.Proposal
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim KI-Vorschlag für fehlende Zutat {IngredientName}.", trimmedIngredientName);
                return Json(new MissingIngredientLookupResponse
                {
                    Success = false,
                    Mode = "error",
                    Message = ex.Message
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SaveSuggestedIngredient([FromBody] MissingIngredientSaveRequest request, CancellationToken cancellationToken)
        {
            if (request?.Suggestion == null)
            {
                return BadRequest(new { message = "Kein Zutat-Vorschlag vorhanden." });
            }

            if (request.Suggestion.IsDebugFallback)
            {
                return Json(new MissingIngredientSaveResponse
                {
                    Success = false,
                    Message = "Im Debug-Fallback werden keine neuen Zutaten gespeichert. Setze SecretKeyOpenAi für echte Übersetzungen."
                });
            }

            if (LooksSuspiciouslyUntranslated(request.Suggestion))
            {
                return Json(new MissingIngredientSaveResponse
                {
                    Success = false,
                    Message = "Die Sprachfelder sehen noch nicht sauber übersetzt aus. Bitte Vorschlag prüfen oder echten OpenAI-Key setzen."
                });
            }

            if (LooksMissingGenusData(request.Suggestion))
            {
                return Json(new MissingIngredientSaveResponse
                {
                    Success = false,
                    Message = "Die Genus-Felder sind noch nicht sauber gef?llt. Erwartet werden kurze Sprach-Codes wie DE: m/f/n/pl, NL: de/het und SE/DK/NO: en/ett oder et."
                });
            }

            var userHash = ResolveUserHash(request.UserHash);
            if (!await IsCreatorAllowedAsync(_context, userHash))
            {
                return Json(new MissingIngredientSaveResponse
                {
                    Success = false,
                    Message = "Nicht berechtigt."
                });
            }

            var duplicate = await FindExactIngredientAsync(request.Suggestion, cancellationToken);
            if (duplicate != null)
            {
                return Json(new MissingIngredientSaveResponse
                {
                    Success = true,
                    ReusedExisting = true,
                    Message = "Die Zutat existiert bereits und wurde wiederverwendet.",
                    Ingredient = MapCatalogItemDto(duplicate)
                });
            }

            if (!TryConsumeMissingIngredientSaveSlot(userHash, out var retryAfter))
            {
                if (retryAfter.HasValue)
                {
                    Response.Headers["Retry-After"] = Math.Ceiling(retryAfter.Value.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                return Json(new MissingIngredientSaveResponse
                {
                    Success = false,
                    Message = "Limit erreicht: maximal 10 neue Zutaten pro 30 Minuten."
                });
            }

            var entity = BuildIngredientEntity(request.Suggestion);
            await _context.IngredientsAndNutrients.AddAsync(entity, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            var saved = await _context.IngredientsAndNutrients
                .AsNoTracking()
                .Include(x => x.Group)
                .FirstAsync(x => x.Id == entity.Id, cancellationToken);

            return Json(new MissingIngredientSaveResponse
            {
                Success = true,
                ReusedExisting = false,
                Message = "Neue Zutat gespeichert.",
                Ingredient = MapCatalogItemDto(saved)
            });
        }

        private List<RecipeJoinPreparationSteps> ExtractCreatePostingStepsFromRequest(IFormCollection form)
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

                var preparationStepId = 0;
                var stepIdRaw = form[stepIdKey].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(stepIdRaw) && !int.TryParse(stepIdRaw, out preparationStepId))
                {
                    _logger.LogWarning("Failed to parse PreparationStepId at index {Index}: {Value}", i, stepIdRaw);
                }

                var stepIndex = 0;
                var stepIndexRaw = form[indexKey].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(stepIndexRaw) && !int.TryParse(stepIndexRaw, out stepIndex))
                {
                    _logger.LogWarning("Failed to parse StepIndex at index {Index}: {Value}", i, stepIndexRaw);
                }

                var phase = 0;
                var phaseRaw = form[phaseKey].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(phaseRaw) && !int.TryParse(phaseRaw, out phase))
                {
                    _logger.LogWarning("Failed to parse Phase at index {Index}: {Value}", i, phaseRaw);
                }

                var equipment = 0;
                var equipmentRaw = form[equipmentKey].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(equipmentRaw) && !int.TryParse(equipmentRaw, out equipment))
                {
                    _logger.LogWarning("Failed to parse Equipment at index {Index}: {Value}", i, equipmentRaw);
                }

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

        private List<SmartStepReferenceInput> ExtractCreatePostingSmartStepsFromRequest(IFormCollection form)
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

                var stepIndex = 0;
                var stepIndexRaw = form[stepIndexField].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(stepIndexRaw) && !int.TryParse(stepIndexRaw, out stepIndex))
                {
                    _logger.LogWarning("Failed to parse SmartStep StepIndex at index {Index}: {Value}", i, stepIndexRaw);
                }

                if (string.IsNullOrWhiteSpace(masterKey))
                {
                    _logger.LogWarning("Empty MasterStepKey at SmartStep index {Index}, skipping.", i);
                    continue;
                }

                // Validate MasterStepKey format (should match pattern: CATEGORY_ACTION_XX)
                if (!Regex.IsMatch(masterKey, @"^[A-Z_]+_\d{2}$"))
                {
                    _logger.LogWarning("MasterStepKey '{MasterKey}' at index {Index} does not match expected pattern (CATEGORY_ACTION_XX). Allowing but flagging for review.", masterKey, i);
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

        private List<int> ExtractKeywordIdsFromRequest(IFormCollection form)
        {
            var result = new List<int>();
            if (form.TryGetValue("SelectedKeywordIds", out var values))
            {
                foreach (var val in values)
                {
                    if (int.TryParse(val, out var id))
                    {
                        if (id > 0)
                        {
                            result.Add(id);
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(val))
                    {
                        _logger.LogWarning("Failed to parse keyword ID: {Value}", val);
                    }
                }
            }
            return result;
        }

        private (string variablesJson, int? phase, string? equipment) ParseSmartStepMetadata(string? metadataJson)
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse smart step metadata JSON. Metadata: {MetadataJson}", metadataJson);
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

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
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
                }

                // Extract ingredients from CreatePosting format: IngredientMeasureQuantity[i]
                var form = Request.Form;
                var skippedIngredients = 0;
                for (var i = 0; ; i++)
                {
                    var ingredientIdKey = $"IngredientMeasureQuantity[{i}].IngredientsAndNutrients.Id";
                    var quantityKey = $"IngredientMeasureQuantity[{i}].Quantity.Quantitys";
                    var measureDeKey = $"IngredientMeasureQuantity[{i}].Measure.Metrics_DE";

                    if (!form.ContainsKey(ingredientIdKey))
                        break;

                    if (!int.TryParse(form[ingredientIdKey].FirstOrDefault(), out var ingredientId) || ingredientId <= 0)
                    {
                        _logger.LogWarning("Invalid ingredient ID at index {Index} during recipe edit.", i);
                        skippedIngredients++;
                        continue;
                    }

                    var rawQty = (form[quantityKey].FirstOrDefault() ?? "0").Trim().Replace(",", ".");
                    if (!double.TryParse(rawQty, NumberStyles.Any, CultureInfo.InvariantCulture, out var qty) || qty <= 0)
                    {
                        _logger.LogWarning("Invalid quantity '{Quantity}' for ingredient {IngredientId} at index {Index}.", rawQty, ingredientId, i);
                        skippedIngredients++;
                        continue;
                    }

                    var measureDe = (form[measureDeKey].FirstOrDefault() ?? "").Trim();

                    var ingredient = await _context.IngredientsAndNutrients.FindAsync(ingredientId);
                    if (ingredient == null)
                    {
                        _logger.LogWarning("Ingredient ID {IngredientId} not found in database at index {Index}.", ingredientId, i);
                        skippedIngredients++;
                        continue;
                    }

                    var measure = await _context.Metrics.FirstOrDefaultAsync(m => m.Metrics_DE == measureDe);
                    if (measure == null)
                    {
                        _logger.LogWarning("Measure '{Measure}' not found in database for ingredient {IngredientId} at index {Index}.", measureDe, ingredientId, i);
                        skippedIngredients++;
                        continue;
                    }

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

                if (skippedIngredients > 0)
                {
                    _logger.LogWarning("Skipped {SkippedCount} invalid ingredients during recipe {RecipeId} edit.", skippedIngredients, postingToEdit.Recipe.Id);
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
                await transaction.CommitAsync();

                _logger.LogInformation("Recipe {RecipeId} successfully updated by user {UserHash}.", postingToEdit.Recipe.Id, userHash);

                return RedirectToAction("MyProfile", "Feed", new { area = "WorldMiniApp", userHash });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to update recipe {RecipeId} for posting {PostingId} by user {UserHash}.", recipeId, postingId, userHash);
                throw;
            }
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

        private bool TryConsumeMissingIngredientSaveSlot(string userHash, out TimeSpan? retryAfter)
        {
            retryAfter = null;
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return false;
            }

            var cacheKey = $"worldminiapp:missing-ingredient-save:{userHash}";
            var now = DateTimeOffset.UtcNow;
            var state = _memoryCache.Get<UploadRateState>(cacheKey);

            if (state == null)
            {
                state = new UploadRateState { Count = 1, WindowStart = now };
                _memoryCache.Set(cacheKey, state, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = MissingIngredientSaveRateWindow
                });
                return true;
            }

            if (state.Count >= MissingIngredientSaveRateLimit)
            {
                retryAfter = (state.WindowStart + MissingIngredientSaveRateWindow) - now;
                _logger.LogWarning("Missing-ingredient save limit exceeded for user {UserHash}.", userHash);
                return false;
            }

            state.Count += 1;
            _memoryCache.Set(cacheKey, state, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = MissingIngredientSaveRateWindow
            });
            return true;
        }

        private sealed class UploadRateState
        {
            public int Count { get; set; }
            public DateTimeOffset WindowStart { get; set; }
        }

        private async Task<List<IngredientsAndNutrients>> FindMatchingIngredientsAsync(string ingredientName, int take, CancellationToken cancellationToken)
        {
            var normalizedNeedle = NormalizeIngredientSearchValue(ingredientName);
            if (string.IsNullOrWhiteSpace(normalizedNeedle))
            {
                return new List<IngredientsAndNutrients>();
            }

            var allIngredients = await _context.IngredientsAndNutrients
                .AsNoTracking()
                .Include(x => x.Group)
                .ToListAsync(cancellationToken);

            return allIngredients
                .Select(item => new
                {
                    Item = item,
                    Score = GetIngredientMatchScore(item, normalizedNeedle)
                })
                .Where(x => x.Score >= 0)
                .OrderBy(x => x.Score)
                .ThenBy(x => x.Item.Name_DE)
                .Take(Math.Max(1, take))
                .Select(x => x.Item)
                .ToList();
        }

        private async Task<IngredientsAndNutrients?> FindExactIngredientAsync(MissingIngredientAiProposal suggestion, CancellationToken cancellationToken)
        {
            var candidates = await FindMatchingIngredientsAsync(suggestion.CanonicalName, 12, cancellationToken);
            var normalizedNames = GetIngredientNames(suggestion)
                .Select(NormalizeIngredientSearchValue)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return candidates.FirstOrDefault(item =>
                GetIngredientNames(item)
                    .Select(NormalizeIngredientSearchValue)
                    .Any(name => normalizedNames.Contains(name)));
        }

        private static MissingIngredientExistingMatchDto MapExistingMatchDto(IngredientsAndNutrients ingredient, string query)
        {
            var normalizedQuery = NormalizeIngredientSearchValue(query);
            var exactMatch = GetIngredientNames(ingredient)
                .Select(NormalizeIngredientSearchValue)
                .Any(name => string.Equals(name, normalizedQuery, StringComparison.OrdinalIgnoreCase));

            return new MissingIngredientExistingMatchDto
            {
                Id = ingredient.Id,
                NameDe = ingredient.Name_DE ?? string.Empty,
                NameEn = ingredient.Name_EN ?? string.Empty,
                Icon = ingredient.Icon ?? ingredient.Group?.Icon ?? string.Empty,
                GroupId = ingredient.GroupId,
                GroupName = ingredient.Group?.Name ?? string.Empty,
                ExactMatch = exactMatch
            };
        }

        private static IngredientCatalogItemDto MapCatalogItemDto(IngredientsAndNutrients ingredient)
        {
            return new IngredientCatalogItemDto
            {
                Id = ingredient.Id,
                Icon = ingredient.Icon ?? ingredient.Group?.Icon ?? string.Empty,
                GroupId = ingredient.GroupId,
                GroupName = ingredient.Group?.Name ?? string.Empty,
                GroupIcon = ingredient.Group?.Icon ?? string.Empty,
                Name_DE = ingredient.Name_DE ?? string.Empty,
                Name_EN = ingredient.Name_EN ?? string.Empty,
                Name_PRT = ingredient.Name_PRT ?? string.Empty,
                Name_ESP = ingredient.Name_ESP ?? string.Empty,
                Name_ID = ingredient.Name_ID ?? string.Empty,
                Name_NL = ingredient.Name_NL ?? string.Empty,
                Name_SE = ingredient.Name_SE ?? string.Empty,
                Name_DK = ingredient.Name_DK ?? string.Empty,
                Name_NO = ingredient.Name_NO ?? string.Empty,
                Name_MS = ingredient.Name_MS ?? string.Empty,
                Genus_DE = ingredient.Genus_DE ?? string.Empty,
                Genus_EN = ingredient.Genus_EN ?? string.Empty,
                Genus_ESP = ingredient.Genus_ESP ?? string.Empty,
                Genus_PRT = ingredient.Genus_PRT ?? string.Empty,
                Genus_ID = ingredient.Genus_ID ?? string.Empty,
                Genus_MS = ingredient.Genus_MS ?? string.Empty,
                Genus_NL = ingredient.Genus_NL ?? string.Empty,
                Genus_SE = ingredient.Genus_SE ?? string.Empty,
                Genus_DK = ingredient.Genus_DK ?? string.Empty,
                Genus_NO = ingredient.Genus_NO ?? string.Empty,
                is_liquid = ingredient.is_liquid,
                is_hard = ingredient.is_hard,
                is_soft = ingredient.is_soft,
                is_fat = ingredient.is_fat,
                is_peelable = ingredient.is_peelable,
                is_cuttable = ingredient.is_cuttable,
                is_grateable = ingredient.is_grateable,
                is_fryable = ingredient.is_fryable,
                is_roastable = ingredient.is_roastable,
                is_grillable = ingredient.is_grillable,
                is_steamable = ingredient.is_steamable,
                is_boilable = ingredient.is_boilable,
                is_searable = ingredient.is_searable,
                is_poachable = ingredient.is_poachable,
                is_smokable = ingredient.is_smokable,
                is_flambeable = ingredient.is_flambeable,
                is_blendable = ingredient.is_blendable,
                is_powder = ingredient.is_powder
            };
        }

        private static IngredientsAndNutrients BuildIngredientEntity(MissingIngredientAiProposal suggestion)
        {
            return new IngredientsAndNutrients
            {
                Icon = NormalizeIngredientText(suggestion.Icon, "ðŸ¥£"),
                Name_DE = NormalizeIngredientText(suggestion.Name_DE, suggestion.CanonicalName),
                Name_EN = NormalizeIngredientText(suggestion.Name_EN, suggestion.CanonicalName),
                Name_PRT = NormalizeIngredientText(suggestion.Name_PRT, suggestion.Name_EN, suggestion.Name_DE),
                Name_ESP = NormalizeIngredientText(suggestion.Name_ESP, suggestion.Name_EN, suggestion.Name_DE),
                Name_ID = NormalizeIngredientText(suggestion.Name_ID, suggestion.Name_EN, suggestion.Name_DE),
                Name_NL = NormalizeIngredientText(suggestion.Name_NL, suggestion.Name_EN, suggestion.Name_DE),
                Name_SE = NormalizeIngredientText(suggestion.Name_SE, suggestion.Name_EN, suggestion.Name_DE),
                Name_DK = NormalizeIngredientText(suggestion.Name_DK, suggestion.Name_EN, suggestion.Name_DE),
                Name_NO = NormalizeIngredientText(suggestion.Name_NO, suggestion.Name_EN, suggestion.Name_DE),
                Name_MS = NormalizeIngredientText(suggestion.Name_MS, suggestion.Name_EN, suggestion.Name_DE),
                Genus_DE = NormalizeIngredientText(suggestion.Genus_DE, "-"),
                Genus_EN = NormalizeIngredientText(suggestion.Genus_EN, "-"),
                Genus_ESP = NormalizeIngredientText(suggestion.Genus_ESP, "-"),
                Genus_PRT = NormalizeIngredientText(suggestion.Genus_PRT, "-"),
                Genus_ID = NormalizeIngredientText(suggestion.Genus_ID, "-"),
                Genus_MS = NormalizeIngredientText(suggestion.Genus_MS, "-"),
                Genus_NL = NormalizeIngredientText(suggestion.Genus_NL, "-"),
                Genus_SE = NormalizeIngredientText(suggestion.Genus_SE, "-"),
                Genus_DK = NormalizeIngredientText(suggestion.Genus_DK, "-"),
                Genus_NO = NormalizeIngredientText(suggestion.Genus_NO, "-"),
                GroupId = suggestion.GroupId,
                Calories_a_100g = Math.Max(0, suggestion.Calories_a_100g),
                Weight_per_piece = Math.Max(0, suggestion.Weight_per_piece),
                Fat_a_100g = suggestion.Fat_a_100g,
                Saturated_fat_a_100g = suggestion.Saturated_fat_a_100g,
                Carbohydrates_a_100g = suggestion.Carbohydrates_a_100g,
                Sugar_a_100g = suggestion.Sugar_a_100g,
                Salt_a_100g = suggestion.Salt_a_100g,
                Protein_a_100g = suggestion.Protein_a_100g,
                Fiber_a_100g = suggestion.Fiber_a_100g,
                is_liquid = suggestion.is_liquid,
                is_hard = suggestion.is_hard,
                is_soft = suggestion.is_soft,
                is_fat = suggestion.is_fat,
                is_peelable = suggestion.is_peelable,
                is_cuttable = suggestion.is_cuttable,
                is_grateable = suggestion.is_grateable,
                is_fryable = suggestion.is_fryable,
                is_roastable = suggestion.is_roastable,
                is_grillable = suggestion.is_grillable,
                is_steamable = suggestion.is_steamable,
                is_boilable = suggestion.is_boilable,
                is_searable = suggestion.is_searable,
                is_poachable = suggestion.is_poachable,
                is_smokable = suggestion.is_smokable,
                is_flambeable = suggestion.is_flambeable,
                is_blendable = suggestion.is_blendable,
                is_powder = suggestion.is_powder
            };
        }

        private static int GetIngredientMatchScore(IngredientsAndNutrients ingredient, string normalizedQuery)
        {
            var names = GetIngredientNames(ingredient)
                .Select(NormalizeIngredientSearchValue)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            if (names.Count == 0)
            {
                return -1;
            }

            if (names.Any(name => string.Equals(name, normalizedQuery, StringComparison.OrdinalIgnoreCase)))
            {
                return 0;
            }

            if (names.Any(name => name.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase)))
            {
                return 1;
            }

            if (names.Any(name => name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)))
            {
                return 2;
            }

            return -1;
        }

        private static IEnumerable<string> GetIngredientNames(IngredientsAndNutrients ingredient)
        {
            yield return ingredient.Name_DE ?? string.Empty;
            yield return ingredient.Name_EN ?? string.Empty;
            yield return ingredient.Name_PRT ?? string.Empty;
            yield return ingredient.Name_ESP ?? string.Empty;
            yield return ingredient.Name_ID ?? string.Empty;
            yield return ingredient.Name_NL ?? string.Empty;
            yield return ingredient.Name_SE ?? string.Empty;
            yield return ingredient.Name_DK ?? string.Empty;
            yield return ingredient.Name_NO ?? string.Empty;
            yield return ingredient.Name_MS ?? string.Empty;
        }

        private static IEnumerable<string> GetIngredientNames(MissingIngredientAiProposal suggestion)
        {
            yield return suggestion.Name_DE ?? string.Empty;
            yield return suggestion.Name_EN ?? string.Empty;
            yield return suggestion.Name_PRT ?? string.Empty;
            yield return suggestion.Name_ESP ?? string.Empty;
            yield return suggestion.Name_ID ?? string.Empty;
            yield return suggestion.Name_NL ?? string.Empty;
            yield return suggestion.Name_SE ?? string.Empty;
            yield return suggestion.Name_DK ?? string.Empty;
            yield return suggestion.Name_NO ?? string.Empty;
            yield return suggestion.Name_MS ?? string.Empty;
            yield return suggestion.CanonicalName ?? string.Empty;
        }

        private static string NormalizeIngredientSearchValue(string? value)
        {
            var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            normalized = Regex.Replace(normalized, "\\s+", " ");
            return normalized;
        }

        private static string NormalizeIngredientText(params string?[] values)
        {
            foreach (var value in values)
            {
                var trimmed = (value ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    return trimmed;
                }
            }

            return string.Empty;
        }

        private static bool LooksSuspiciouslyUntranslated(MissingIngredientAiProposal suggestion)
        {
            var names = GetIngredientNames(suggestion)
                .Select(NormalizeIngredientSearchValue)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (names.Count < 3)
            {
                return true;
            }

            var distinctCount = names.Distinct(StringComparer.OrdinalIgnoreCase).Count();
            return distinctCount <= 2;
        }

        private static bool LooksMissingGenusData(MissingIngredientAiProposal suggestion)
        {
            var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["de"] = NormalizeGenusValue(suggestion.Genus_DE),
                ["en"] = NormalizeGenusValue(suggestion.Genus_EN),
                ["esp"] = NormalizeGenusValue(suggestion.Genus_ESP),
                ["prt"] = NormalizeGenusValue(suggestion.Genus_PRT),
                ["id"] = NormalizeGenusValue(suggestion.Genus_ID),
                ["ms"] = NormalizeGenusValue(suggestion.Genus_MS),
                ["nl"] = NormalizeGenusValue(suggestion.Genus_NL),
                ["se"] = NormalizeGenusValue(suggestion.Genus_SE),
                ["dk"] = NormalizeGenusValue(suggestion.Genus_DK),
                ["no"] = NormalizeGenusValue(suggestion.Genus_NO)
            };
            if (!IsAllowedGenusValue("de", normalized["de"])) return true;
            if (!IsAllowedGenusValue("en", normalized["en"])) return true;
            if (!IsAllowedGenusValue("esp", normalized["esp"])) return true;
            if (!IsAllowedGenusValue("prt", normalized["prt"])) return true;
            if (!IsAllowedGenusValue("id", normalized["id"])) return true;
            if (!IsAllowedGenusValue("ms", normalized["ms"])) return true;
            if (!IsAllowedGenusValue("nl", normalized["nl"])) return true;
            if (!IsAllowedGenusValue("se", normalized["se"])) return true;
            if (!IsAllowedGenusValue("dk", normalized["dk"])) return true;
            if (!IsAllowedGenusValue("no", normalized["no"])) return true;
            var requiredLanguages = new[] { "de", "esp", "prt", "nl", "se", "dk", "no" };
            return requiredLanguages.Any(lang => normalized[lang] == "-");
        }
        private static string NormalizeGenusValue(string? value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }
        private static bool IsAllowedGenusValue(string lang, string value)
        {
            return lang switch
            {
                "de" => value is "M" or "F" or "N" or "PL" or "-",
                "en" => value is "-",
                "esp" => value is "M" or "F" or "PL" or "-",
                "prt" => value is "M" or "F" or "PL" or "-",
                "id" => value is "-",
                "ms" => value is "-",
                "nl" => value is "DE" or "HET" or "PL" or "-",
                "se" => value is "EN" or "ETT" or "PL" or "-",
                "dk" => value is "EN" or "ET" or "PL" or "-",
                "no" => value is "EN" or "EI" or "ET" or "PL" or "-",
                _ => false
            };
        }
    }
}
