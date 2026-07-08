using DelikatessenDrehbuch.Areas.WorldMiniApp.Extensions;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services;
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
        private readonly RecipeTranslationService _recipeTranslationService;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<RecipeController> _logger;
        private const int UploadRateLimit = 5;
        private static readonly TimeSpan UploadRateWindow = TimeSpan.FromMinutes(10);
        private const int MissingIngredientSaveRateLimit = 10;
        private static readonly TimeSpan MissingIngredientSaveRateWindow = TimeSpan.FromMinutes(30);
        private const int MissingIngredientLookupRateLimit = 20;
        private static readonly TimeSpan MissingIngredientLookupRateWindow = TimeSpan.FromMinutes(10);
        private const int MaxIngredientNameLength = 80;
        private const int StepGenerationRateLimit = 20;
        private static readonly TimeSpan StepGenerationRateWindow = TimeSpan.FromMinutes(10);

        public RecipeController(
            ApplicationDbContext context,
            IBlobUploadService blobUpload,
            ISaveNewRecipeService saveNewRecipeService,
            IMissingIngredientAiService missingIngredientAiService,
            RecipeTranslationService recipeTranslationService,
            IServiceScopeFactory serviceScopeFactory,
            IMemoryCache memoryCache,
            ILogger<RecipeController> logger)
        {
            _context = context;
            _blobUpload = blobUpload;
            _saveNewRecipeService = saveNewRecipeService;
            _missingIngredientAiService = missingIngredientAiService;
            _recipeTranslationService = recipeTranslationService;
            _serviceScopeFactory = serviceScopeFactory;
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
        [ValidateAntiForgeryToken]
        [ActionName("UploadNewVideo")]
     
        public async Task<IActionResult> UploadNewVideoAsync(WorldUserPosting posting, string userHash)
        {
            userHash = ResolveUserHash(userHash);
            if (!await IsCreatorAllowedAsync(_context, userHash))
            {
                return Json(new { success = false, error = "Nicht berechtigt." });
            }

            // Validate file upload
            if (posting.Content == null || posting.Content.Length == 0)
            {
                return Json(new {
                    success = false,
                    error = "📁 Keine Datei ausgewählt!\n\nBitte wählen Sie ein Bild oder Video aus."
                });
            }

            // Set creator info and creation time
            posting.CreatorId = userHash;
            posting.CreationTime = DateTime.Now;

            // Idempotency check: prevent duplicate uploads on network retry
            var idempotencyKey = Request.Headers["X-Idempotency-Key"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var cacheKey = $"worldminiapp:idempotency:{userHash}:{idempotencyKey}";
                if (_memoryCache.TryGetValue<object>(cacheKey, out var cachedResult))
                {
                    _logger.LogInformation("Duplicate upload request detected with idempotency key {Key}.", idempotencyKey);
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

            // Check if content is a video
            var isVideo = posting.Content != null &&
                          !string.IsNullOrEmpty(posting.Content.ContentType) &&
                          posting.Content.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);

            if (isVideo)
            {
                // ASYNC PATH: Process video in background

                // 1. Load video into memory (so we can access it after request ends)
                var videoBytes = new byte[posting.Content.Length];
                using (var stream = posting.Content.OpenReadStream())
                {
                    await stream.ReadAsync(videoBytes, 0, videoBytes.Length);
                }

                // Optional: creator-pasted source captions. If the creator pasted WEBVTT text, it is
                // used as the German source caption instead of Whisper; translation into the other
                // languages stays unchanged. The WEBVTT header is added automatically if missing.
                string? manualVttContent = null;
                var pastedVtt = Request.Form["captionVttText"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(pastedVtt))
                {
                    const int maxVttChars = 200_000; // ample for a recipe video's captions
                    if (pastedVtt.Length > maxVttChars)
                    {
                        pastedVtt = pastedVtt.Substring(0, maxVttChars);
                    }

                    // Accept if it contains at least one cue marker ("-->") or already has the header.
                    // NormalizeWebVtt (on store) prepends "WEBVTT" when the header is missing.
                    if (pastedVtt.Contains("-->") ||
                        pastedVtt.TrimStart().StartsWith("WEBVTT", StringComparison.OrdinalIgnoreCase))
                    {
                        manualVttContent = pastedVtt;
                    }
                    else
                    {
                        _logger.LogWarning("Ignoring pasted caption text: no WEBVTT cues found. User={UserHash}", userHash);
                    }
                }

                // 2. Copy all needed data (so we can access it after request ends)
                // IMPORTANT: Extract simple data, not Entity objects to avoid DbContext tracking issues
                var ingredientData = posting.IngredientMeasureQuantity?.Select(imq => new
                {
                    IngredientId = imq.IngredientsAndNutrients?.Id ?? 0,
                    Quantity = imq.Quantity?.Quantitys ?? 0,
                    MeasureDe = imq.Measure?.Metrics_DE ?? ""
                }).ToList();

                var uploadData = new
                {
                    UserHash = userHash,
                    Title = posting.Title,
                    Category = posting.Recipe.Category,
                    PersonCount = posting.Recipe.PersonCount,
                    PreparationTime = posting.Recipe.PreparationTime,
                    Preferences = posting.Recipe.Preferences,
                    IngredientData = ingredientData,
                    SelectedKeywordIds = posting.SelectedKeywordIds,
                    StepsText = Request.Form["StepsText"].FirstOrDefault() ?? "",
                    IsOffline = posting.IsOffline,
                    VideoBytes = videoBytes,
                    FileName = posting.Content.FileName,
                    ContentType = posting.Content.ContentType,
                    ManualVttContent = manualVttContent
                };

                // Extract values for error logging (avoid dynamic in lambda)
                var capturedUserHash = userHash;
                var capturedTitle = posting.Title;

                // 3. Start background task (Fire-and-Forget)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessVideoUploadInBackgroundAsync(uploadData);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Background video upload failed for user {UserHash}", capturedUserHash);

                        // Send error notification
                        await SendUploadErrorNotificationAsync(capturedUserHash, capturedTitle, ex.Message);
                    }
                });

                // 4. Return success immediately
                var asyncSuccessResponse = new
                {
                    success = true,
                    isAsync = true,
                    isVideo = true,
                    message = "Upload wird verarbeitet..."
                };

                // Cache for idempotency
                if (!string.IsNullOrWhiteSpace(idempotencyKey))
                {
                    var cacheKey = $"worldminiapp:idempotency:{userHash}:{idempotencyKey}";
                    _memoryCache.Set(cacheKey, asyncSuccessResponse, TimeSpan.FromMinutes(10));
                }

                return Json(asyncSuccessResponse);
            }

            // SYNC PATH: Images remain synchronous (fast enough)
            var uploadResult = await _blobUpload.UploadContentToBlob(posting.Content);

            // Get steps text from form
            var stepsText = (Request.Form["StepsText"].FirstOrDefault() ?? "").Trim();
            var recipeSteps = new List<RecipeSteps>();

            // Translate steps if provided
            if (!string.IsNullOrWhiteSpace(stepsText))
            {
                var recipeTitle = posting.Title ?? "Rezept";
                var translation = await _recipeTranslationService.TranslateRecipeAsync(
                    recipeTitle,
                    stepsText,
                    CancellationToken.None);

                var now = DateTime.UtcNow;
                var stepTranslations = new Dictionary<string, string>
                {
                    { "de", translation.Steps.De },
                    { "en", translation.Steps.En },
                    { "esp", translation.Steps.Esp },
                    { "prt", translation.Steps.Prt },
                    { "id", translation.Steps.Id },
                    { "nl", translation.Steps.Nl },
                    { "sv", translation.Steps.Sv },
                    { "da", translation.Steps.Da },
                    { "no", translation.Steps.No },
                    { "ms", translation.Steps.Ms }
                };

                foreach (var kvp in stepTranslations)
                {
                    if (!string.IsNullOrWhiteSpace(kvp.Value))
                    {
                        recipeSteps.Add(new RecipeSteps
                        {
                            Culture = kvp.Key,
                            Text = kvp.Value,
                            CreatedAt = now
                        });
                    }
                }

            }

            SaveNewRecipeModel recipeModel = new()
            {
                Recipes = new Recipes()
                {
                    Name = posting.Title,
                    Category = posting.Recipe.Category,
                    RecipePersonCount = posting.Recipe.PersonCount,
                    PreparationTime = posting.Recipe.PreparationTime,
                    // For videos, use thumbnail; for images, use the image itself
                    ImagePath = !string.IsNullOrEmpty(uploadResult.ThumbnailUrl)
                        ? uploadResult.ThumbnailUrl
                        : uploadResult.SourceUrl
                },
                Querys = posting.Recipe.Preferences,
                IngredientMeasureQuantity = posting.IngredientMeasureQuantity,
                RecipeSteps = recipeSteps,
            };

            var recipe = await _saveNewRecipeService.SaveNewAsync(recipeModel, true);

            if (recipe == null)
            {
                _logger.LogError("SaveNewAsync returned null recipe for title {Title} by user {UserHash}.", posting.Title, userHash);
                return Json(new { success = false, error = "Rezept konnte nicht gespeichert werden." });
            }

            var user = await _context.WorldAppUser.FirstOrDefaultAsync(u => u.UserHash == userHash);
            var creatorName = user?.UserName ?? "Avocado";

            // Keine Transaction hier - SaveNewRecipeService hat schon eine Transaction gemacht
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

                // Start background translation (4 core languages: DE, EN, ESP, PRT)
                // Neuer Scope = eigener DbContext, verhindert Threading-Issues
                var recipeIdForTranslation = recipe.Id;
                // OLD SYSTEM - Background translation removed (used IRecipeStepTranslationService)
                // NEW SYSTEM: Translation happens synchronously in SaveRecipeWithTranslation endpoint

                Response.Cookies.Append("createPostingDraftReset", "1", new CookieOptions
                {
                    Path = "/",
                    HttpOnly = false,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddHours(1)
                });

                _logger.LogInformation("Recipe {RecipeId} and posting {PostingId} successfully created by user {UserHash}.", recipe.Id, posting.Id, userHash);

                // Check if uploaded content is a video (HLS or regular video file)
                var sourceLower = (uploadResult.SourceUrl ?? string.Empty).ToLowerInvariant();
                var isVideoContent = sourceLower.Contains(".m3u8")
                    || sourceLower.Contains(".mp4")
                    || sourceLower.Contains(".mov")
                    || sourceLower.Contains(".webm");

                // If video with VideoGuid (Bunny Stream), track for notification when ready
                if (isVideoContent && !string.IsNullOrEmpty(uploadResult.VideoGuid))
                {
                    var pendingVideo = new WorldUserPendingVideo
                    {
                        PostingId = posting.Id,
                        VideoGuid = uploadResult.VideoGuid,
                        UserHash = userHash,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _context.WorldUserPendingVideos.AddAsync(pendingVideo);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("📹 Pending video tracked: {VideoGuid} for posting {PostingId}", uploadResult.VideoGuid, posting.Id);
                    // ✅ Caption-Generation läuft automatisch via BunnyWebhookController → CaptionGenerationService (Whisper+DeepL)
                }

                var successResponse = new {
                    success = true,
                    isVideo = isVideoContent,
                    recipeId = recipe.Id,
                    postingId = posting.Id,
                    translationInProgress = true,
                    message = "✅ Upload successful! Translations are being processed in the background. You'll receive a notification when ready."
                };

                // Cache successful response for idempotency (10 minutes)
                if (!string.IsNullOrWhiteSpace(idempotencyKey))
                {
                    var cacheKey = $"worldminiapp:idempotency:{userHash}:{idempotencyKey}";
                    _memoryCache.Set(cacheKey, successResponse, TimeSpan.FromMinutes(10));
                }

                return Json(successResponse);
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

            // NEW SYSTEM: RecipeSteps (normalized, Culture + Text)
            // Get German steps for editing (or fallback to first available)
            var stepsForEdit = posting.Recipe.Steps?.FirstOrDefault(s => s.Culture == "de")
                ?? posting.Recipe.Steps?.FirstOrDefault();

            ViewData["EditStepsText"] = stepsForEdit?.Text ?? "";
            // SmartSteps REMOVED - using new simple translation system

            return View("~/Areas/WorldMiniApp/Views/Home/CreatePosting.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpsertStep([FromBody] UpsertStepRequest request, string userHash)
        {
            // OLD SYSTEM - RecipePreparationSteps removed
            // New system uses simple textarea with AI translation
            await Task.CompletedTask;
            return Json(new { success = false, message = "Old step system removed - use simple textarea instead" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SuggestMissingIngredient([FromBody] MissingIngredientLookupRequest request, CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.IngredientName))
            {
                return BadRequest(new { message = "Bitte gib eine Zutat ein." });
            }

            // Adding ingredients to the shared catalog is open to ANY logged-in creator (not just
            // orb-verified) — abuse is contained by the per-user rate limits below, not by an orb gate.
            var userHash = ResolveUserHash(request.UserHash);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return Json(new MissingIngredientLookupResponse
                {
                    Success = false,
                    Mode = "forbidden",
                    Message = "Bitte zuerst anmelden."
                });
            }

            var trimmedIngredientName = request.IngredientName.Trim();
            if (trimmedIngredientName.Length > MaxIngredientNameLength)
            {
                trimmedIngredientName = trimmedIngredientName.Substring(0, MaxIngredientNameLength).Trim();
            }

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

            if (!TryConsumeMissingIngredientLookupSlot(userHash, out var retryAfter))
            {
                if (retryAfter.HasValue)
                {
                    Response.Headers["Retry-After"] = Math.Ceiling(retryAfter.Value.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                return Json(new MissingIngredientLookupResponse
                {
                    Success = false,
                    Mode = "rate_limited",
                    Message = "Limit erreicht: zu viele KI-Anfragen. Bitte etwas später erneut versuchen."
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
                    Message = "Der KI-Vorschlag konnte gerade nicht geladen werden. Bitte versuche es später erneut."
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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

            // Open to any logged-in creator; abuse is contained by the save rate limit below.
            var userHash = ResolveUserHash(request.UserHash);
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return Json(new MissingIngredientSaveResponse
                {
                    Success = false,
                    Message = "Bitte zuerst anmelden."
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

            // Update steps (NEW SYSTEM: simple textarea + AI translation)
            var stepsText = (form["StepsText"].FirstOrDefault() ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(stepsText))
            {
                // Remove existing steps
                var existingSteps = await _context.RecipeSteps
                    .Where(s => s.RecipeId == postingToEdit.Recipe.Id)
                    .ToListAsync();
                if (existingSteps.Any())
                {
                    _context.RecipeSteps.RemoveRange(existingSteps);
                }

                // Translate steps to all languages using AI
                var translation = await _recipeTranslationService.TranslateRecipeAsync(
                    postingToEdit.Recipe.Title,
                    stepsText,
                    CancellationToken.None);

                // Save RecipeSteps rows (one per language)
                var now = DateTime.UtcNow;
                var stepTranslations = new Dictionary<string, string>
                {
                    { "de", translation.Steps.De },
                    { "en", translation.Steps.En },
                    { "esp", translation.Steps.Esp },
                    { "prt", translation.Steps.Prt },
                    { "id", translation.Steps.Id },
                    { "nl", translation.Steps.Nl },
                    { "sv", translation.Steps.Sv },
                    { "da", translation.Steps.Da },
                    { "no", translation.Steps.No },
                    { "ms", translation.Steps.Ms }
                };

                foreach (var kvp in stepTranslations)
                {
                    if (!string.IsNullOrWhiteSpace(kvp.Value))
                    {
                        var step = new RecipeSteps
                        {
                            RecipeId = postingToEdit.Recipe.Id,
                            Culture = kvp.Key,
                            Text = kvp.Value,
                            CreatedAt = now
                        };
                        _context.RecipeSteps.Add(step);
                    }
                }

                _logger.LogInformation("Translated and saved steps for recipe {RecipeId} in 10 languages", postingToEdit.Recipe.Id);
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

            // SmartSteps REMOVED - using new simple translation system

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

                if (posting.Recipe.Steps != null)
                    _context.RecipeSteps.RemoveRange(posting.Recipe.Steps);  // Neues System

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

        private bool TryConsumeMissingIngredientLookupSlot(string userHash, out TimeSpan? retryAfter)
        {
            retryAfter = null;
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return false;
            }

            var cacheKey = $"worldminiapp:missing-ingredient-lookup:{userHash}";
            var now = DateTimeOffset.UtcNow;
            var state = _memoryCache.Get<UploadRateState>(cacheKey);

            if (state == null)
            {
                state = new UploadRateState { Count = 1, WindowStart = now };
                _memoryCache.Set(cacheKey, state, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = MissingIngredientLookupRateWindow
                });
                return true;
            }

            if (state.Count >= MissingIngredientLookupRateLimit)
            {
                retryAfter = (state.WindowStart + MissingIngredientLookupRateWindow) - now;
                _logger.LogWarning("Missing-ingredient lookup limit exceeded for user {UserHash}.", userHash);
                return false;
            }

            state.Count += 1;
            _memoryCache.Set(cacheKey, state, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = MissingIngredientLookupRateWindow
            });
            return true;
        }

        private bool TryConsumeStepGenerationSlot(string userHash, out TimeSpan? retryAfter)
        {
            retryAfter = null;
            if (string.IsNullOrWhiteSpace(userHash))
            {
                return false;
            }

            var cacheKey = $"worldminiapp:step-generation:{userHash}";
            var now = DateTimeOffset.UtcNow;
            var state = _memoryCache.Get<UploadRateState>(cacheKey);

            if (state == null)
            {
                state = new UploadRateState { Count = 1, WindowStart = now };
                _memoryCache.Set(cacheKey, state, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = StepGenerationRateWindow
                });
                return true;
            }

            if (state.Count >= StepGenerationRateLimit)
            {
                retryAfter = (state.WindowStart + StepGenerationRateWindow) - now;
                _logger.LogWarning("Step-generation limit exceeded for user {UserHash}.", userHash);
                return false;
            }

            state.Count += 1;
            _memoryCache.Set(cacheKey, state, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = StepGenerationRateWindow
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

        // Helper method for background video processing
        private async Task ProcessVideoUploadInBackgroundAsync(dynamic uploadData)
        {
            // Extract all values from dynamic object to avoid dynamic binding issues
            string title = uploadData.Title;
            string userHash = uploadData.UserHash;
            byte[] videoBytes = uploadData.VideoBytes;
            string fileName = uploadData.FileName;
            string contentType = uploadData.ContentType;
            string stepsText = uploadData.StepsText;
            string category = uploadData.Category;
            int personCount = uploadData.PersonCount;
            int preparationTime = uploadData.PreparationTime;
            string preferences = uploadData.Preferences;
            bool isOffline = uploadData.IsOffline;
            var ingredientData = uploadData.IngredientData as IEnumerable<dynamic>;
            List<int> selectedKeywordIds = uploadData.SelectedKeywordIds;
            string? manualVttContent = uploadData.ManualVttContent;

            // Create new scope (important for DbContext!)
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var blobUpload = scope.ServiceProvider.GetRequiredService<IBlobUploadService>();
            var saveRecipeService = scope.ServiceProvider.GetRequiredService<ISaveNewRecipeService>();
            var translationService = scope.ServiceProvider.GetRequiredService<RecipeTranslationService>();

            _logger.LogInformation("Starting background upload for {Title} by {UserHash}", title, userHash);

            // 1. Upload video to Bunny (BLOCKS 3-4 minutes, but in background)
            var videoFile = CreateFormFileFromBytes(videoBytes, fileName, contentType);
            var uploadResult = await blobUpload.UploadContentToBlob(videoFile);

            _logger.LogInformation("Video uploaded to Bunny: {Url}", uploadResult.SourceUrl);

            try
            {
            // 2. AI translation (if steps provided)
            List<RecipeSteps> recipeSteps = new();
            if (!string.IsNullOrWhiteSpace(stepsText))
            {
                var translation = await translationService.TranslateRecipeAsync(
                    title,
                    stepsText,
                    CancellationToken.None);

                var now = DateTime.UtcNow;
                var stepTranslations = new Dictionary<string, string>
                {
                    { "de", translation.Steps.De },
                    { "en", translation.Steps.En },
                    { "esp", translation.Steps.Esp },
                    { "prt", translation.Steps.Prt },
                    { "id", translation.Steps.Id },
                    { "nl", translation.Steps.Nl },
                    { "sv", translation.Steps.Sv },
                    { "da", translation.Steps.Da },
                    { "no", translation.Steps.No },
                    { "ms", translation.Steps.Ms }
                };

                foreach (var kvp in stepTranslations)
                {
                    if (!string.IsNullOrWhiteSpace(kvp.Value))
                    {
                        recipeSteps.Add(new RecipeSteps
                        {
                            Culture = kvp.Key,
                            Text = kvp.Value,
                            CreatedAt = now
                        });
                    }
                }

                // Fallback: if the translation failed/returned nothing, keep at least the original
                // German steps so a recipe is never saved completely without instructions.
                if (!recipeSteps.Any(s => s.Culture == "de"))
                {
                    recipeSteps.Add(new RecipeSteps
                    {
                        Culture = "de",
                        Text = stepsText.Trim(),
                        CreatedAt = now
                    });
                    if (!translation.Success)
                    {
                        _logger.LogWarning("Recipe step translation failed for '{Title}'; saved original German steps only.", title);
                    }
                }
            }

            // 3. Reconstruct IngredientMeasureQuantity from simple data (avoid DbContext tracking issues)
            var ingredientMeasureQuantity = new List<IngredientMeasureQuantity>();
            if (ingredientData != null)
            {
                foreach (var item in ingredientData)
                {
                    int ingredientId = item.IngredientId;
                    double quantity = item.Quantity;
                    string measureDe = item.MeasureDe;

                    if (ingredientId > 0 && quantity > 0)
                    {
                        var ingredient = await context.IngredientsAndNutrients.FindAsync(ingredientId);
                        var measure = await context.Metrics.FirstOrDefaultAsync(m => m.Metrics_DE == measureDe);

                        if (ingredient != null && measure != null)
                        {
                            ingredientMeasureQuantity.Add(new IngredientMeasureQuantity
                            {
                                IngredientsAndNutrients = ingredient,
                                Measure = measure,
                                Quantity = new Quantity { Quantitys = quantity }
                            });
                        }
                    }
                }
            }

            // 4. Save recipe
            var recipeModel = new SaveNewRecipeModel
            {
                Recipes = new Recipes
                {
                    Name = title,
                    Category = category,
                    RecipePersonCount = personCount,
                    PreparationTime = preparationTime,
                    // For videos, use thumbnail URL; for images, use the image itself
                    ImagePath = !string.IsNullOrEmpty(uploadResult.ThumbnailUrl)
                        ? uploadResult.ThumbnailUrl
                        : uploadResult.SourceUrl
                },
                Querys = preferences,
                IngredientMeasureQuantity = ingredientMeasureQuantity,
                RecipeSteps = recipeSteps
            };

            var recipe = await saveRecipeService.SaveNewAsync(recipeModel, true);

            if (recipe == null)
            {
                throw new InvalidOperationException("Recipe save failed");
            }

            // 5. Create WorldUserPosting
            var user = await context.WorldAppUser.FirstOrDefaultAsync(u => u.UserHash == userHash);
            var creatorName = user?.UserName ?? "Avocado";

            var posting = new WorldUserPosting
            {
                CreationTime = DateTime.Now,
                CreatorName = creatorName,
                CreatorId = userHash,
                Title = title,
                Source = uploadResult.SourceUrl,
                ThumbnailUrl = uploadResult.ThumbnailUrl,
                IsOffline = isOffline,
                Recipe = recipe
            };

            await context.WorldUserPosting.AddAsync(posting);
            await context.SaveChangesAsync();

            // If the creator pasted a source VTT, generate captions + translations now (right after a
            // successful save) so they don't depend on the Bunny webhook firing. Runs ~10 OpenAI calls;
            // isolated in its own try so a caption hiccup never fails the (already saved) upload.
            if (!string.IsNullOrWhiteSpace(manualVttContent) && !string.IsNullOrEmpty(uploadResult.VideoGuid))
            {
                try
                {
                    var captionService = scope.ServiceProvider.GetRequiredService<CaptionGenerationService>();
                    await captionService.GenerateCaptionsFromSourceVttAsync(uploadResult.VideoGuid, manualVttContent);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Caption generation from pasted VTT failed. VideoGuid={VideoGuid}", uploadResult.VideoGuid);
                }
            }

            // 6. Link keywords
            if (selectedKeywordIds != null && selectedKeywordIds.Count > 0)
            {
                var keywordLinks = selectedKeywordIds
                    .Distinct()
                    .Select(keywordId => new RecipeBaseKeyword
                    {
                        RecipeBaseDataId = recipe.Id,
                        KeywordId = keywordId
                    })
                    .ToList();

                await context.RecipeBaseKeywords.AddRangeAsync(keywordLinks);
                await context.SaveChangesAsync();
            }

            // 7. Track pending video for transcoding notifications
            if (!string.IsNullOrEmpty(uploadResult.VideoGuid))
            {
                var pendingVideo = new WorldUserPendingVideo
                {
                    PostingId = posting.Id,
                    VideoGuid = uploadResult.VideoGuid,
                    UserHash = userHash,
                    CreatedAt = DateTime.UtcNow
                };
                await context.WorldUserPendingVideos.AddAsync(pendingVideo);
                await context.SaveChangesAsync();

                _logger.LogInformation("Pending video tracked: {VideoGuid} for posting {PostingId}",
                    uploadResult.VideoGuid, posting.Id);
            }

            // 8. Send success notification
            var notification = new WorldUserNotification
            {
                UserHash = userHash,
                Description = $"Dein Video \"{title}\" wurde hochgeladen!",
                CreatedAtUtc = DateTime.UtcNow,
                IsSeen = false,
                EventType = "upload-complete",
                Icon = "bi-check-circle-fill",
                Sender = "system",
                Href = $"/WorldMiniApp/Feed/Index?postingId={posting.Id}",
                NotificationKey = $"upload-complete:{posting.Id}",
                ContextText = title
            };

            await context.WorldUserNotifications.AddAsync(notification);
            await context.SaveChangesAsync();

            _logger.LogInformation("Upload completed successfully. Posting {PostingId} for user {UserHash}",
                posting.Id, userHash);
            }
            catch
            {
                // Something after the Bunny upload failed (recipe/ingredient save, DB, …). Remove the
                // now-orphaned Bunny video so unreferenced uploads don't pile up, then let the caller's
                // handler send the error notification.
                await blobUpload.DeleteVideoAsync(uploadResult.VideoGuid);
                throw;
            }
        }

        // Helper to create IFormFile from bytes
        private IFormFile CreateFormFileFromBytes(byte[] bytes, string fileName, string contentType)
        {
            var stream = new MemoryStream(bytes);
            return new FormFile(stream, 0, bytes.Length, "Content", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };
        }

        // Helper to send error notification
        private async Task SendUploadErrorNotificationAsync(string userHash, string title, string errorMessage)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var notification = new WorldUserNotification
            {
                UserHash = userHash,
                Description = $"Upload von \"{title}\" fehlgeschlagen. Bitte erneut versuchen.",
                CreatedAtUtc = DateTime.UtcNow,
                IsSeen = false,
                EventType = "upload-failed",
                Icon = "bi-exclamation-triangle-fill",
                Sender = "system",
                Href = "/WorldMiniApp/Home/Upload",
                NotificationKey = $"upload-failed:{Guid.NewGuid()}",
                ContextText = errorMessage.Length > 200 ? errorMessage.Substring(0, 200) : errorMessage
            };

            await context.WorldUserNotifications.AddAsync(notification);
            await context.SaveChangesAsync();
        }
    }
}
